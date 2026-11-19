# Supporto dei programmi Ruby esportati da Frociart.
# Il controllo del programma vive nelle funzioni Ruby: nessun eval o interprete di grafi.
require 'bigdecimal'
require 'tempfile'
require 'set'

class FcError < StandardError; end
class FcArrayCell
  attr_accessor :value, :kind
  def initialize(value, kind=0); @value=value; @kind=kind; end
end
class FcArrayArgument
  attr_reader :cell, :snapshot
  def initialize(cell); @cell=cell; @snapshot=cell.value; end
end

class FcRuntime
  UNSET=Object.new.freeze
  attr_reader :frames, :last_variables, :error_variables, :output, :output_newlines,
              :segments, :x, :y, :heading, :color, :recovery_path
  attr_accessor :steps, :max_steps
  def initialize(reader: nil, writer: nil, file_reader: nil, file_saver: nil)
    @reader=reader || ->(prompt) { $stdout.write(prompt); $stdout.flush; line=$stdin.gets; raise EOFError unless line; line.sub(/\r?\n\z/,'') }
    @writer=writer
    @file_reader=file_reader || method(:read_path)
    @file_saver=file_saver || method(:save_path)
    @steps=0; @max_steps=10000; @frames=[]; @references=false
    @last_variables={}; @error_variables=nil; @output=[]; @output_newlines=[]; @output_size=0
    @read_name=@read_text=nil; @read_position=0; @write_name=nil; @write_parts=[]
    @write_size=@write_bytes=0; @recovery_path=nil
    @x=@y=0.0; @heading=90.0; @color='#000000'; @segments=[]; @turtle_used=false
  end
  def unset; UNSET; end
  def self.utf16_units(units); units.pack('v*').force_encoding(Encoding::UTF_16LE).freeze; end
  def text(value)
    raise FcError,'Serve un testo.' unless value.is_a?(String)
    value=value.encode(Encoding::UTF_16LE) unless value.encoding==Encoding::UTF_16LE
    raise FcError,'Il testo supera 32768 caratteri.' if units(value)>32768
    value.freeze
  end
  def units(value); value.encoding==Encoding::UTF_16LE ? value.bytesize/2 : value.encode(Encoding::UTF_16LE).bytesize/2; end
  def utf8(value); value.encode(Encoding::UTF_8); end
  def real(bits); [bits].pack('Q>').unpack1('G'); end
  def number(value)
    raise FcError,'Serve un numero.' unless value.instance_of?(Integer) || value.instance_of?(Float)
    n=value.to_f; raise FcError,'Il numero non è finito.' unless n.finite?; n
  end
  def truth(value); value==true || value==false ? value : number(value)!=0; end
  def value(value,name)
    return value unless value.equal?(UNSET)
    return Math::PI if name.downcase=='pi' || name=='π'
    raise FcError,'La variabile «'+name+'» non ha ancora un valore.'
  end
  def format_number(value)
    return '0' if value==0
    return value.to_i.to_s if value.abs<1e15 && value==value.truncate
    rational=value.abs.to_r; scale=rational.denominator.bit_length-1
    digits=(rational.numerator*5**scale).to_s; exponent=digits.length-scale-1
    if digits.length>15
      digits=(digits[0,15].to_i+(digits[15]>='5' ? 1 : 0)).to_s
      if digits.length>15; exponent+=1; digits=digits[0,15]; end
    end
    digits=digits.sub(/0+\z/,'')
    if exponent>=15 || exponent< -4
      body=digits[0]+(digits.length>1 ? '.'+digits[1..] : '')+'E'+(exponent>=0 ? '+' : '-')+exponent.abs.to_s.rjust(2,'0')
    else
      at=exponent+1
      body=at<=0 ? '0.'+'0'*(-at)+digits : at>=digits.length ? digits+'0'*(at-digits.length) : digits[0,at]+'.'+digits[at..]
    end
    (value<0 ? '-' : '')+body
  end
  def format(value)
    result=case value
      when NilClass then ''
      when TrueClass then 'vero'
      when FalseClass then 'falso'
      when String then return value.encoding==Encoding::UTF_16LE ? value : text(value)
      when Array
        values=value.map do |v|
          if v.is_a?(String)
            # Operate on code units, preserving even an incomplete source character.
            escaped=text(v).unpack('v*').flat_map { |u| {92=>[92,92],34=>[92,34],10=>[92,110],13=>[92,114],9=>[92,116]}.fetch(u,[u]) }
            FcRuntime.utf16_units([34]+escaped+[34])
          else format(v) end
        end
        return text('[')+values.join(text(', '))+text(']')
      else format_number(number(value))
    end
    text(result)
  end
  def scalar_kind(value)
    return 1 if value==true || value==false
    if value.is_a?(String); text(value); return 2; end
    number(value); 0
  end
  def vector(values)
    raise FcError,'Un vettore può contenere al massimo 1024 elementi.' if values.length>1024
    values=values.map { |v| v.is_a?(String) ? text(v) : v }.freeze
    raise FcError,'Gli elementi del vettore devono avere lo stesso tipo.' if values.map { |v| scalar_kind(v) }.uniq.length>1
    raise FcError,'Il contenuto del vettore supera 32768 caratteri.' if units(format(values))+(values.empty? ? 0 : 2)>32768
    values
  end
  def require_vector(value); raise FcError,'Serve un vettore.' unless value.is_a?(Array); value; end
  def integer(value,minimum,maximum,label='Il valore')
    n=number(value)
    raise FcError,"#{label} deve essere un intero da #{minimum} a #{maximum}." unless n==n.truncate && n>=minimum && n<=maximum
    n.to_i
  end
  def index(vector,value)
    require_vector(vector); i=integer(value,0,2147483647,'L’indice')
    raise FcError,'Indice fuori dal vettore.' if i>=vector.length; i
  end
  def at(vector,index_expression); require_vector(vector); vector[index(vector,index_expression.call)]; end
  def at_name(name,index_expression)
    require_vector(lookup(name)); i=index_expression.call; v=lookup(name); v[index(v,i)]
  end
  def set_after_name(value,name,index_expression)
    require_vector(lookup(name)); frame=@frames[-1]; frame[:assignment]=value
    begin i=index_expression.call; set_after(value,lookup(name),-> { i })
    ensure frame[:assignment]=UNSET end
  end
  def set_after(value,vector,index_expression)
    require_vector(vector); frame=@frames[-1]; frame[:assignment]=value if frame
    begin i=index(vector,index_expression.call)
    ensure frame[:assignment]=UNSET if frame end
    raise FcError,'Il nuovo valore deve avere lo stesso tipo del vettore.' unless scalar_kind(vector[i])==scalar_kind(value)
    copy=vector.dup; copy[i]=value; self.vector(copy)
  end
  def concat(a,b); text(format(a)+format(b)); end
  def add(a,b); a.is_a?(String)||b.is_a?(String) ? concat(a,b) : number(number(a)+number(b)); end
  def sub(a,b); number(number(a)-number(b)); end
  def mul(a,b); number(number(a)*number(b)); end
  def div(a,b); a=number(a); b=number(b); raise FcError,'Divisione per zero.' if b==0; number(a/b); end
  def mod(a,b); a=number(a); b=number(b); raise FcError,'Resto con divisore zero.' if b==0; number(a.remainder(b)); end
  def power(a,b)
    a=number(a); b=number(b); result=a**b
    raise FcError,'Potenza non definita o risultato troppo grande.' if result.is_a?(Complex)
    number(result)
  rescue Math::DomainError, FloatDomainError, RangeError
    raise FcError,'Potenza non definita o risultato troppo grande.'
  end
  def equal(a,b)
    return (a==true||a==false)&&(b==true||b==false)&&a==b if a==true||a==false||b==true||b==false
    return a.is_a?(Array)&&b.is_a?(Array)&&a.length==b.length&&a.zip(b).all? { |x,y| equal(x,y) } if a.is_a?(Array)||b.is_a?(Array)
    return a.is_a?(String)&&b.is_a?(String)&&text(a)==text(b) if a.is_a?(String)||b.is_a?(String)
    number(a)==number(b)
  end
  def compare(a,b,op)
    order=a.is_a?(String)&&b.is_a?(String) ? text(a).unpack('v*')<=>text(b).unpack('v*') : number(a)<=>number(b)
    case op; when '<' then order<0; when '<=' then order<=0; when '>' then order>0; else order>=0 end
  end
  def check_type(value,kind,array=false)
    if array; require_vector(value).each { |item| check_type(item,kind) }; return value; end
    numeric=value.instance_of?(Integer)||value.instance_of?(Float)
    valid=case kind
      when 1 then numeric && number(value)==value.truncate
      when 2 then numeric
      when 3 then value.is_a?(String)
      when 4 then value==true||value==false
      when 5 then false
      else !value.is_a?(Array)&&!value.nil?&&!value.equal?(UNSET)
    end
    raise FcError,'Il valore non corrisponde al tipo dichiarato.' unless valid
    scalar_kind(value); value
  end
  def convert(value,declaration=nil,parameter=nil)
    if declaration
      kind,array,length=declaration
      scalar=->(v) { kind==1&&(v.instance_of?(Integer)||v.instance_of?(Float)) ? number(v).truncate.to_f : check_type(v,kind) }
      if array
        raise FcError,'La dimensione del vettore dichiarato non può cambiare.' unless require_vector(value).length==length
        value=vector(value.map { |v| scalar.call(v) })
      else value=scalar.call(value) end
    end
    value=check_type(value,parameter[0],parameter[1]) if parameter; value
  end
  def initial(kind,array=false,length=0)
    value=kind==3 ? text('') : kind==4 ? false : 0.0
    array ? vector(Array.new(integer(length,0,1024,'La dimensione'),value)) : value
  end
  NUMBER=/\A[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?\z/
  SPACE_EDGE=/\A[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+\z/
  def trim(value); utf8(value).gsub(SPACE_EDGE,''); end
  def parse_number(value); Float(value.tr(',','.').sub(/\.(?=[eE]|\z)/,'.0')); end
  # Match the native numeric reader, including its accepted trailing NUL terminators.
  def numeric_text(value); value.sub(/\u0000+\z/,'').sub(/[\u0009-\u000d\u0020]+\z/,''); end
  def parse_input(raw,is_text=false)
    raw=text(raw); return raw if is_text
    trimmed=trim(raw); candidate=numeric_text(trimmed)
    if NUMBER.match?(candidate)
      value=parse_number(candidate)
      return raw if !value.finite? && candidate!=trimmed
      return number(value)
    end
    return true if ['true','vero'].include?(trimmed.downcase)
    return false if ['false','falso'].include?(trimmed.downcase)
    raise FcError,'Inserisci un numero finito.' if ['nan','infinity','+infinity','-infinity','infinito'].include?(trimmed.downcase)
    raw
  end
  def wants_text(declaration,parameter,indexed); spec=declaration||parameter; spec&&spec[0]==3&&(indexed||!spec[1]); end
  def read(name,prompt='',declaration=nil,parameter=nil,vector=nil,index_expression=nil)
    guard; variable_name=name; i=nil
    if index_expression; require_vector(vector); position=index_expression.call; i=index(@references ? lookup(variable_name) : vector,position); name+='['+i.to_s+']'; end
    guard; prompt='Inserisci un valore per '+name if trim(prompt).empty?||prompt=='Inserisci un valore per x'
    loop do
      begin raw=@reader.call(prompt+': ')
      rescue EOFError; raise FcError,'Input terminato prima di ricevere un valore valido.' end
      begin
        value=parse_input(raw,wants_text(declaration,parameter,!i.nil?))
        value=set_after(value,@references ? lookup(variable_name) : vector,-> { i.to_f }) unless i.nil?
        value=convert(value,declaration,parameter); accept_input(variable_name,value,!i.nil?); return value
      rescue FcError => error; $stderr.puts(error.message) end
    end
  end
  def guard; raise FcError,'Limite di esecuzione raggiunto. Controlla la condizione del ciclo.' if @steps>=@max_steps; end
  def step; guard; @steps+=1; end
  def test(expression); step; truth(expression.call); end
  def enter(scope,parameters={},references=false)
    guard_call; @references ||= references; values={}; bindings={}
    parameters.each { |name,v| if v.is_a?(FcArrayArgument); bindings[name]=v.cell; v=v.cell.value; end; values[name]=v }
    @frames << {scope: scope,variables: values,bindings: bindings,overlays: {},new_arrays: {},whole_writes: Set.new,transaction: nil,retained: {},assignment: UNSET}
  end
  def lookup(name); frame=@frames[-1]; values=frame[:transaction]||frame[:variables]; value(values.fetch(name,UNSET),name); end
  def reference(name,declaration=nil,parameter=nil)
    frame=@frames[-1]; value=require_vector(lookup(name)); spec=declaration||parameter; kind=spec ? spec[0] : 0
    if frame[:transaction]&&(frame[:whole_writes].include?(name)||!frame[:variables].key?(name))
      frame[:new_arrays][name] ||= FcArrayCell.new(value,kind); return FcArrayArgument.new(frame[:new_arrays][name])
    end
    frame[:bindings][name] ||= FcArrayCell.new(require_vector(frame[:variables][name]),kind); cell=frame[:bindings][name]
    if frame[:transaction]
      return FcArrayArgument.new(frame[:overlays][cell]) if frame[:overlays].key?(cell)
      return FcArrayArgument.new(overlay(frame,cell,value)) unless value.equal?(cell.value)
    end
    FcArrayArgument.new(cell)
  end
  def check_reference(argument,kind)
    raise FcError,'Parametro per riferimento non valido.' unless argument.is_a?(FcArrayArgument)
    check_type(argument.snapshot,kind,true); cell=argument.cell
    raise FcError,'Il tipo del vettore per riferimento deve coincidere con quello originale.' if cell.kind!=0&&kind!=0&&cell.kind!=kind
    check_type(cell.value,kind,true); cell.kind=kind if cell.kind==0&&kind!=0; argument
  end
  def overlay(frame,cell,value); frame[:overlays][cell] ||= FcArrayCell.new(value,cell.kind); end
  def propagate(cell)
    @frames.each do |frame|
      transaction=frame[:transaction]
      frame[:bindings].each do |name,bound|
        next unless bound.equal?(cell)
        frame[:variables][name]=cell.value
        transaction[name]=frame[:overlays].fetch(cell,cell).value if transaction&&!frame[:whole_writes].include?(name)
      end
      next unless transaction
      frame[:new_arrays].each { |name,bound| transaction[name]=cell.value if bound.equal?(cell) }
      frame[:overlays].each do |parent,overlay|
        next unless overlay.equal?(cell)
        frame[:bindings].each { |name,bound| transaction[name]=cell.value if bound.equal?(parent)&&!frame[:whole_writes].include?(name) }
      end
    end
  end
  def check_cell(cell,value)
    raise FcError,'Un vettore per riferimento conserva la dimensione originale.' unless require_vector(value).length==cell.value.length
    check_type(value,cell.kind,true)
  end
  def guard_call; raise FcError,'Troppe chiamate annidate: massimo 128 livelli.' if @frames.length>=128; end
  def leave
    values=@frames[-1][:variables].dup; @error_variables=values if @error_variables.nil?&&!$!.nil?
    @last_variables=values if @frames[-1][:scope].nil?; @frames.pop
  end
  def value_size(value)
    return value_size(value.snapshot) if value.is_a?(FcArrayArgument)
    return units(value) if value.is_a?(String)
    return value.length*8+value.select { |v| v.is_a?(String) }.sum { |v| units(v) } if value.is_a?(Array)
    8
  end
  def check_memory
    size=@output_size; slots=0
    @frames.each do |frame|
      [frame[:variables],frame[:transaction]].compact.each { |values| slots+=values.length; size+=values.values.sum { |v| value_size(v) } }
      frame[:retained].each_value { |values| size+=values.sum { |v| value_size(v) } }
      size+=value_size(frame[:assignment]) unless frame[:assignment].equal?(UNSET)
      raise FcError,'La memoria dei valori supera il limite di esecuzione. Riduci i dati conservati o le chiamate ricorsive.' if size>2097152||slots>16384
    end
  end
  def begin_values
    frame=@frames[-1]; frame[:transaction]=frame[:variables].dup
    if @references; frame[:overlays].clear; frame[:new_arrays].clear; frame[:whole_writes].clear; end
  end
  def stage(name,value,check=true,indexed=false)
    frame=@frames[-1]
    if !@references; frame[:transaction][name]=value
    elsif !indexed; frame[:transaction][name]=value; frame[:whole_writes].add(name); frame[:new_arrays].delete(name)
    elsif frame[:whole_writes].include?(name)&&frame[:new_arrays].key?(name)
      cell=frame[:new_arrays][name]; check_cell(cell,value); cell.value=value; propagate(cell)
    elsif !frame[:whole_writes].include?(name)&&frame[:bindings].key?(name)
      cell=overlay(frame,frame[:bindings][name],frame[:transaction][name]); check_cell(cell,value); cell.value=value; propagate(cell)
    else frame[:transaction][name]=value end
    check_memory if check; value
  end
  def commit(check=false)
    check_memory if check; frame=@frames[-1]
    if @references
      frame[:overlays].each { |parent,overlay| check_cell(parent,overlay.value) }
      frame[:new_arrays].each_value { |cell| check_cell(cell,cell.value) }
      frame[:whole_writes].each { |name| frame[:bindings].delete(name) }
      frame[:overlays].each { |parent,overlay| parent.value=overlay.value; propagate(parent) }
      frame[:bindings].update(frame[:new_arrays])
    end
    frame[:variables]=frame[:transaction]; frame[:transaction]=nil; frame[:overlays].clear; frame[:new_arrays].clear; frame[:whole_writes].clear
  end
  def accept_input(name,value,indexed=false)
    begin_values
    begin stage(name,value,true,indexed); commit
    ensure @frames[-1][:transaction]=nil end
  end
  def hold(key,value); (@frames[-1][:retained][key] ||= []) << value; value; end
  def release(key,value); @frames[-1][:retained].delete(key); value; end
  def call(key,function,*arguments)
    value=function.call(*arguments); release(key,value); hold(-1,value)
    begin check_memory
    ensure release(-1,value) end
    value
  end
  def emit(value,newline=true)
    value=format(value); size=units(value)
    raise FcError,'Il risultato supera 256 KiB.' if @output_size+size>262144
    @output << value; @output_newlines << newline; @output_size+=size
    if @writer; @writer.call(value)
    else $stdout.write(value.encode(Encoding::UTF_8,invalid: :replace,undef: :replace)+(newline ? "\n" : '')); $stdout.flush end
  end
  def round(value,digits=0.0)
    value=number(value); digits=integer(digits,0,15,'Le cifre decimali'); return value if value.abs>=1e16
    factor=10.0**digits; scaled=value*factor; absolute=scaled.abs; whole=absolute.floor; whole+=1 if absolute-whole>=0.5
    sign=scaled<0||(scaled==0&&1.0/scaled<0) ? -1.0 : 1.0; sign*whole/factor
  end
  def characters(value)
    utf8(text(value))
  rescue EncodingError
    raise FcError,'Il testo contiene un carattere Unicode incompleto.'
  end
  def intrinsic(name,*args)
    return eof if name=='eof'; a=args[0]
    if ['array','vettore'].include?(name); length=integer(a,0,1024,'La dimensione'); first=args.length==2 ? args[1] : 0.0; scalar_kind(first); return vector(Array.new(length,first)); end
    if ['len','size'].include?(name)
      return a.length.to_f if a.is_a?(Array)
      return characters(a).length.to_f if name=='len'
      raise FcError,'size richiede un vettore.'
    end
    if ['char','tocode'].include?(name)
      chars=characters(a).each_char.to_a
      if name=='tocode'; raise FcError,'tocode richiede un solo carattere.' unless chars.length==1; return chars[0].ord.to_f; end
      i=integer(args[1],0,2147483647,'L’indice'); raise FcError,'Indice fuori dal testo.' if i>=chars.length; return text(chars[i])
    end
    if ['tointeger','toreal'].include?(name)
      str=trim(text(a)); pattern=name=='tointeger' ? /\A[+-]?[0-9]+\z/ : NUMBER
      raise FcError,'Il testo non contiene un numero valido.' unless pattern.match?(str)
      v=number(parse_number(str)); return name=='tointeger' ? integer(v,-2147483648,2147483647).to_f : v
    end
    if name=='int'&&args.length==2
      raise FcError,'Il secondo argomento di int deve essere "passo".' unless args[1].is_a?(String)&&utf8(text(args[1])).downcase=='passo'
      v=integer(a,-2147483648,2147483647,'Il passo'); raise FcError,'Il passo del ciclo deve essere diverso da zero.' if v==0; return v.to_f
    end
    if ['sum','somma'].include?(name)||(['min','max'].include?(name)&&args.length==1)
      values=require_vector(a); raise FcError,'Il vettore è vuoto.' if values.empty?&&['min','max'].include?(name)
      result=0.0; values.each_with_index { |item,i| n=number(item); result=name=='min' ? (i==0 ? n : [result,n].min) : name=='max' ? (i==0 ? n : [result,n].max) : add(result,n) }; return result
    end
    n=number(a)
    case name
    when 'abs' then n.abs
    when 'floor' then n.floor.to_f
    when 'int' then n.truncate.to_f
    when 'sgn' then (n<=>0).to_f
    when 'round' then round(n,args.length==2 ? args[1] : 0.0)
    when 'min' then args.map { |v| number(v) }.min
    when 'max' then args.map { |v| number(v) }.max
    when 'random' then Random.rand(integer(n,1,2147483647,'Il limite')).to_f
    when 'tochar'
      code=integer(n,0,0x10ffff,'Il codice Unicode'); raise FcError,'Il codice Unicode è incompleto.' if code>=0xd800&&code<=0xdfff; text([code].pack('U'))
    when 'tostring' then format(n)
    when 'tofixed'
      digits=integer(args[1],0,15,'Le cifre decimali'); rounded=round(n,digits)
      decimal=BigDecimal(rounded.to_r,400).round(digits,BigDecimal::ROUND_HALF_UP); decimal=decimal.abs if decimal.zero?
      whole,fraction=decimal.to_s('F').split('.',2); text(whole+(digits==0 ? '' : '.'+(fraction||'').ljust(digits,'0')[0,digits]))
    else
      functions={'sqrt'=>:sqrt,'sin'=>:sin,'cos'=>:cos,'tan'=>:tan,'arcsin'=>:asin,'arccos'=>:acos,'arctan'=>:atan,'ln'=>:log,'log'=>:log,'log10'=>:log10}
      raise FcError,'Valore fuori dal dominio di '+name+'.' unless functions.key?(name)
      begin number(Math.public_send(functions[name],n))
      rescue Math::DomainError, FloatDomainError, RangeError; raise FcError,'Valore fuori dal dominio di '+name+'.' end
    end
  end
  def require_closed(reading); raise FcError,'Il canale è già aperto. Chiudilo prima di aprirne un altro.' if reading ? !@read_text.nil? : !@write_name.nil?; end
  def require_write; raise FcError,'Apri prima un file in scrittura.' if @write_name.nil?; end
  def file_name(name)
    raise FcError,'Il nome del file deve contenere da 1 a 256 caratteri senza controlli.' unless name.is_a?(String)
    name=text(name); chars=name.unpack('v*')
    raise FcError,'Il nome del file deve contenere da 1 a 256 caratteri senza controlli.' if units(name)>256||trim(name).empty?||chars.any? { |c| c<32||(c>=127&&c<=159) }
    validate_file_text(name); name
  end
  def validate_file_text(value)
    raise FcError,'Il file supera 2 milioni di caratteri.' unless value.is_a?(String)&&units(value)<=2097152
    data=utf8(value); raise FcError,'Il file supera 4 MiB.' if data.bytesize>4194304; data
  rescue EncodingError
    raise FcError,'Il file contiene caratteri Unicode non validi.'
  end
  def open_file(reading,name)
    require_closed(reading); name=file_name(name)
    if reading
      value=@file_reader.call(name); validate_file_text(value); value=value.encode(Encoding::UTF_16LE) unless value.encoding==Encoding::UTF_16LE
      @read_name=name; @read_text=value; @read_position=0
    else @write_name=name; @write_parts=[]; @write_size=@write_bytes=0 end
  end
  def eof; raise FcError,'Apri prima un file in lettura.' if @read_text.nil?; @read_position>=@read_text.bytesize; end
  def read_file(name,declaration=nil,parameter=nil,vector=nil,index_expression=nil)
    i=nil
    if index_expression; require_vector(vector); position=index_expression.call; i=index(@references ? lookup(name) : vector,position); end
    raise FcError,'Il file è terminato. Controlla EOF() prima di leggere.' if eof
    finish=@read_position; finish+=2 while finish<@read_text.bytesize&&![10,13].include?(@read_text.byteslice(finish,2).unpack1('v'))
    line=@read_text.byteslice(@read_position,finish-@read_position)
    raise FcError,'Una riga del file supera 32768 caratteri.' if units(line)>32768
    value=parse_input(line,wants_text(declaration,parameter,!i.nil?))
    value=set_after(value,@references ? lookup(name) : vector,-> { i.to_f }) unless i.nil?
    value=convert(value,declaration,parameter); accept_input(name,value,!i.nil?)
    if finish<@read_text.bytesize&&@read_text.byteslice(finish,2).unpack1('v')==13
      finish+=2; finish+=2 if finish<@read_text.bytesize&&@read_text.byteslice(finish,2).unpack1('v')==10
    elsif finish<@read_text.bytesize&&@read_text.byteslice(finish,2).unpack1('v')==10; finish+=2 end
    @read_position=finish; value
  end
  def write_file(value,newline=true)
    require_write; value=format(value)+(newline ? text("\n") : text('')); data=validate_file_text(value)
    raise FcError,'Il file generato supera il limite di dimensione.' if @write_size+units(value)>2097152||@write_bytes+data.bytesize>4194304
    @write_parts << value; @write_size+=units(value); @write_bytes+=data.bytesize
  end
  def close_file(reading)
    if reading; eof; @read_name=@read_text=nil; @read_position=0
    else require_write; @file_saver.call(@write_name,@write_parts.join(text(''))); @write_name=nil; @write_parts=[]; @write_size=@write_bytes=0 end
  end
  def finish_files; close_file(false) unless @write_name.nil?; close_file(true) unless @read_text.nil?; end
  def read_path(name)
    data=File.open(utf8(name),'rb') { |file| file.read(4194305) }
    raise FcError,'Il file supera 4 MiB.' if data.bytesize>4194304
    if data.start_with?("\xff\xfe".b); data=data.byteslice(2..).force_encoding(Encoding::UTF_16LE)
    elsif data.start_with?("\xfe\xff".b); data=data.byteslice(2..).force_encoding(Encoding::UTF_16BE)
    else data=data.byteslice(3..) if data.start_with?("\xef\xbb\xbf".b); data.force_encoding(Encoding::UTF_8) end
    raise FcError,'Il file non è un testo UTF-8 o UTF-16 valido.' unless data.valid_encoding?
    data.encode(Encoding::UTF_16LE)
  rescue EncodingError
    raise FcError,'Il file non è un testo UTF-8 o UTF-16 valido.'
  end
  def save_path(name,value)
    destination=File.expand_path(utf8(name)); overwrite=File.exist?(destination); data=validate_file_text(value)
    begin file=Tempfile.create(['frociart-','.txt'],File.dirname(destination))
    rescue SystemCallError; file=Tempfile.create(['frociart-','.txt']) end
    @recovery_path=file.path
    begin file.binmode; file.write(data); file.flush; file.fsync
    ensure file.close end
    if overwrite
      begin accepted=@reader.call('Il file esiste. Scrivi SOVRASCRIVI per sostituirlo: ').strip=='SOVRASCRIVI'
      rescue EOFError; accepted=false end
      raise FcError,'Salvataggio annullato. Il file originale è intatto; il testo generato è nella bozza recuperabile.' unless accepted
      commit_path(@recovery_path,destination,true)
    else
      commit_path(@recovery_path,destination,false)
    end
    @recovery_path=nil
  end
  def commit_path(source,destination,overwrite)
    if /mswin|mingw/.match?(RUBY_PLATFORM)
      begin require 'fiddle'
      rescue LoadError; raise FcError,'Questa installazione Ruby non include Fiddle. La bozza del file resta recuperabile.' end
      @move_library ||= Fiddle.dlopen('kernel32.dll')
      @move_file ||= Fiddle::Function.new(@move_library['MoveFileExW'],[Fiddle::TYPE_VOIDP,Fiddle::TYPE_VOIDP,Fiddle::TYPE_INT],Fiddle::TYPE_INT,Fiddle::Function.const_defined?(:STDCALL) ? Fiddle::Function::STDCALL : Fiddle::Function::DEFAULT)
      from=(source+"\0").encode(Encoding::UTF_16LE); to=(destination+"\0").encode(Encoding::UTF_16LE)
      if @move_file.call(from,to,overwrite ? 1 : 0)==0
        error=Fiddle.win32_last_error
        raise Errno::EEXIST,destination if [80,183].include?(error)
        raise Errno::EACCES,destination if error==5
        raise IOError,'Impossibile completare il salvataggio (Windows '+error.to_s+').' 
      end
    elsif overwrite; File.rename(source,destination)
    else File.link(source,destination); File.unlink(source) end
  end
  def turtle_forward(distance,draw=true)
    distance=number(distance); radians=@heading*Math::PI/180
    cosine=[90,270].include?(@heading) ? 0.0 : @heading==0 ? 1.0 : @heading==180 ? -1.0 : Math.cos(radians)
    sine=[0,180].include?(@heading) ? 0.0 : @heading==90 ? 1.0 : @heading==270 ? -1.0 : Math.sin(radians)
    x=@x+distance*cosine; y=@y+distance*sine
    raise FcError,'Il disegno supera le coordinate da -1000000 a 1000000.' unless x.finite?&&y.finite?&&x.abs<=1000000&&y.abs<=1000000
    if draw&&(x!=@x||y!=@y); raise FcError,'Il disegno supera 10000 tratti.' if @segments.length>=10000; @segments << [@x,@y,x,y,@color]; end
    @x=x; @y=y; @turtle_used=true
  end
  def turtle_turn(angle,left=false); angle=number(angle).remainder(360); @heading=((@heading+(left ? angle : -angle))%360+360)%360; @turtle_used=true; end
  def turtle_home; @x=@y=0.0; @heading=90.0; @turtle_used=true; end
  def turtle_clear; @segments.clear; turtle_home; end
  def turtle_pen(value)
    value=trim(text(value)).downcase
    names=[['nero','black','#000000'],['bianco','white','#FFFFFF'],['grigio','gray','#808080'],['rosso','red','#FF0000'],['arancione','orange','#FF8C00'],['giallo','yellow','#FFFF00'],['verde','green','#008000'],['lime','lime','#00FF00'],['turchese','teal','#008080'],['ciano','cyan','#00FFFF'],['blu','blue','#0000FF'],['azzurro','skyblue','#00BFFF'],['viola','purple','#800080'],['magenta','magenta','#FF00FF'],['rosa','pink','#FF69B4'],['marrone','brown','#A52A2A'],['grey','grey','#808080']]
    names.each { |italian,english,code| if value==italian||value==english; value=code; break; end }
    value='#'+value[1..].each_char.map { |c| c*2 }.join if /\A#[0-9a-fA-F]{3}\z/.match?(value)
    raise FcError,'Colore non riconosciuto. Usa un nome o #RRGGBB.' unless /\A#[0-9a-fA-F]{6}\z/.match?(value)
    @color=value.upcase; @turtle_used=true
  end
  def drawing_svg
    points=[[0.0,0.0],[@x,@y]]+@segments.flat_map { |x1,y1,x2,y2,_| [[x1,y1],[x2,y2]] }
    xmin,xmax=points.map(&:first).minmax; ymin,ymax=points.map(&:last).minmax
    width=[100.0,xmax-xmin].max; height=[100.0,ymax-ymin].max
    cx=(xmin+xmax)/2; cy=(ymin+ymax)/2; width*=1.2; height*=1.2
    fmt=->(v) { Kernel.format('%.15g',v) }
    lines=@segments.map { |x1,y1,x2,y2,c| '<line x1="'+fmt.call(x1)+'" y1="'+fmt.call(-y1)+'" x2="'+fmt.call(x2)+'" y2="'+fmt.call(-y2)+'" stroke="'+c+'" stroke-width="2" vector-effect="non-scaling-stroke"/>' }.join("\n")
    '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="700" viewBox="'+[cx-width/2,-cy-height/2,width,height].map { |v| fmt.call(v) }.join(' ')+'"><title>Frociart — disegno</title>'+lines+'</svg>'
  end
  def show_drawing; @file_saver.call(text('Frociart_disegno.svg'),drawing_svg.encode(Encoding::UTF_16LE).freeze) if @turtle_used; end
  def run(main,show_drawing=true)
    main.call; self.show_drawing if show_drawing; true
  rescue FcError, SystemCallError, IOError, EncodingError, SystemStackError => error
    $stderr.puts('Frociart: '+error.message); $stderr.puts('Bozza recuperabile: '+@recovery_path) if @recovery_path; false
  end
end
