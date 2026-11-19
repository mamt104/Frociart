<?php
// Supporto dei programmi PHP esportati da Frociart. Nessun eval o interprete di grafi.
// Le stringhe conservano le unita UTF-16 del progetto, anche durante copie e sottocalcoli.
final class FcError extends RuntimeException {}
final class FcText {
    public readonly string $data;
    private function __construct(string $data) { $this->data=$data; }
    public static function units(array $units): self { return new self(pack('v*',...$units)); }
    public static function raw(string $data): self {
        if(strlen($data)%2)throw new FcError('Testo UTF-16 incompleto.');
        return new self($data);
    }
    public static function fromUtf8(string $s): self {
        $data='';$length=strlen($s);
        for($i=0;$i<$length;) {
            $b=ord($s[$i++]);$remaining=0;$minimum=0;
            if($b<128)$code=$b;
            elseif($b>=194&&$b<=223){$code=$b&31;$remaining=1;$minimum=128;}
            elseif($b>=224&&$b<=239){$code=$b&15;$remaining=2;$minimum=2048;}
            elseif($b>=240&&$b<=244){$code=$b&7;$remaining=3;$minimum=65536;}
            else throw new FcError('Il testo non e UTF-8 valido.');
            for($j=0;$j<$remaining;$j++) {
                if($i>=$length)throw new FcError('Il testo non e UTF-8 valido.');
                $next=ord($s[$i++]);if(($next&192)!==128)throw new FcError('Il testo non e UTF-8 valido.');
                $code=($code<<6)|($next&63);
            }
            if($code<$minimum||$code>1114111||($code>=55296&&$code<=57343))throw new FcError('Il testo non e UTF-8 valido.');
            if($code<65536)$data.=pack('v',$code);else{$code-=65536;$data.=pack('v2',55296+($code>>10),56320+($code&1023));}
        }
        return new self($data);
    }
    private function points(bool $replace=false): Generator {
        $length=strlen($this->data);
        for($i=0;$i<$length;$i+=2) {
            $u=ord($this->data[$i])|(ord($this->data[$i+1])<<8);$next=$i+3<$length?ord($this->data[$i+2])|(ord($this->data[$i+3])<<8):0;
            if($u>=55296&&$u<=56319&&$next>=56320&&$next<=57343){$u=65536+(($u-55296)<<10)+$next-56320;$i+=2;}
            elseif($u>=55296&&$u<=57343){if(!$replace)throw new FcError('Il testo contiene un carattere Unicode incompleto.');$u=65533;}
            yield $u;
        }
    }
    public function codepoints(bool $replace=false): array {return iterator_to_array($this->points($replace),false);}
    public function validate(): void {foreach($this->points() as $_){}}
    public static function point(int $u): self {
        if($u<0||$u>1114111||($u>=55296&&$u<=57343))throw new FcError('Codice Unicode non valido.');
        if($u<65536)return self::units([$u]);$u-=65536;return self::units([55296+($u>>10),56320+($u&1023)]);
    }
    public function utf8(bool $replace=false): string {
        $result='';foreach($this->points($replace) as $u) {
            if($u<128)$result.=chr($u);
            elseif($u<2048)$result.=chr(192|($u>>6)).chr(128|($u&63));
            elseif($u<65536)$result.=chr(224|($u>>12)).chr(128|(($u>>6)&63)).chr(128|($u&63));
            else $result.=chr(240|($u>>18)).chr(128|(($u>>12)&63)).chr(128|(($u>>6)&63)).chr(128|($u&63));
        }
        return $result;
    }
    public function length(): int { return intdiv(strlen($this->data),2); }
}
final class FcCell { public function __construct(public array $value,public int $kind=0){} }
final class FcArgument {public readonly array $snapshot;public function __construct(public FcCell $cell){$this->snapshot=$cell->value;}}
final class FcFrame {
    public array $variables=[],$bindings=[],$overlays=[],$newArrays=[],$wholeWrites=[],$retained=[];
    public ?array $transaction=null;
    public mixed $assignment;
    public function __construct(public ?string $scope,object $unset){$this->assignment=$unset;}
}
final class FcRuntime {
    public readonly object $unset;
    public int $steps=0,$maxSteps=10000;
    public array $frames=[],$lastVariables=[],$output=[],$outputNewlines=[],$segments=[];
    public ?array $errorVariables=null;
    public ?string $recoveryPath=null;
    public float $x=0.0,$y=0.0,$heading=90.0;
    public string $color='#000000';
    private mixed $reader,$writer,$fileReader,$fileSaver;
    private bool $references=false,$turtleUsed=false;
    private int $outputSize=0,$readPosition=0,$writeSize=0,$writeBytes=0;
    private ?FcText $readText=null,$writeName=null;
    private array $writeParts=[];
    public function __construct(?callable $reader=null,?callable $writer=null,?callable $fileReader=null,?callable $fileSaver=null) {
        $this->unset=new stdClass();
        $this->reader=$reader??function(string $prompt): string {fwrite(STDOUT,$prompt);$line=fgets(STDIN);if($line===false)throw new FcError('Input terminato prima di ricevere un valore valido.');return preg_replace('/\r?\n\z/','',$line);};
        $this->writer=$writer;$this->fileReader=$fileReader??[$this,'readPath'];$this->fileSaver=$fileSaver??[$this,'savePath'];
    }
    public function text(mixed $value): FcText {
        if(!$value instanceof FcText)throw new FcError('Serve un testo.');
        if($value->length()>32768)throw new FcError('Il testo supera 32768 caratteri.');return $value;
    }
    public function number(mixed $value): float {
        if(!is_float($value)&&!is_int($value))throw new FcError('Serve un numero.');
        $number=(float)$value;if(!is_finite($number))throw new FcError('Il numero non e finito.');return $number;
    }
    public function real(string $bits): float {if(!preg_match('/\A[0-9a-f]{16}\z/D',$bits))throw new FcError('Numero binario non valido.');return $this->number(unpack('E',hex2bin($bits))[1]);}
    public function truth(mixed $value): bool {return is_bool($value)?$value:$this->number($value)!=0;}
    public function value(mixed $value,string $name): mixed {
        if($value!==$this->unset)return $value;
        if(strtolower($name)==='pi'||$name==='π')return M_PI;
        throw new FcError('La variabile «'.$name.'» non ha ancora un valore.');
    }
    private static function multiply(string $digits,int $factor): string {
        $carry=0;$out='';for($i=strlen($digits)-1;$i>=0;$i--){$carry+=(ord($digits[$i])-48)*$factor;$out.=chr(48+$carry%10);$carry=intdiv($carry,10);}while($carry){$out.=chr(48+$carry%10);$carry=intdiv($carry,10);}return strrev($out);
    }
    private static function increment(string $digits): string {
        for($i=strlen($digits)-1;$i>=0;$i--){if($digits[$i]!=='9'){$digits[$i]=chr(ord($digits[$i])+1);return $digits;}$digits[$i]='0';}return '1'.$digits;
    }
    private static function decimal(float $value): array {
        if($value==0)return ['0',0];$bits=unpack('Nhigh/Nlow',pack('E',abs($value)));$exponent=(($bits['high']>>20)&2047);$mantissa=($bits['high']&1048575)*4294967296+$bits['low'];
        if($exponent!==0)$mantissa+=4503599627370496;$exponent=($exponent===0?-1022:$exponent-1023)-52;
        $digits=(string)$mantissa;$scale=max(0,-$exponent);$factor=$exponent<0?5:2;$power=abs($exponent);
        while($power>0){$count=min($power,13);$digits=self::multiply($digits,(int)($factor**$count));$power-=$count;}
        return [$digits,$scale];
    }
    public function formatNumber(float $value): string {
        $value=$this->number($value);if($value==0)return '0';
        [$digits,$scale]=self::decimal($value);$exponent=strlen($digits)-$scale-1;
        if(strlen($digits)>15){$round=$digits[15]>='5';$digits=substr($digits,0,15);if($round)$digits=self::increment($digits);if(strlen($digits)>15){$exponent++;$digits=substr($digits,0,15);}}
        $digits=rtrim($digits,'0');
        if($exponent>=15||$exponent< -4)$body=$digits[0].(strlen($digits)>1?'.'.substr($digits,1):'').'E'.($exponent>=0?'+':'-').str_pad((string)abs($exponent),2,'0',STR_PAD_LEFT);
        else {$at=$exponent+1;$body=$at<=0?'0.'.str_repeat('0',-$at).$digits:($at>=strlen($digits)?$digits.str_repeat('0',$at-strlen($digits)):substr($digits,0,$at).'.'.substr($digits,$at));}
        return ($value<0?'-':'').$body;
    }
    public function format(mixed $value): FcText {
        if($value instanceof FcText)return $this->text($value);
        if($value===null)return FcText::fromUtf8('');
        if(is_bool($value))return FcText::fromUtf8($value?'vero':'falso');
        if(is_array($value)) {
            $parts=[];foreach($value as $item){if($item instanceof FcText){$escaped=[34];foreach(unpack('v*',$this->text($item)->data) as $u)foreach(match($u){92=>[92,92],34=>[92,34],10=>[92,110],13=>[92,114],9=>[92,116],default=>[$u]} as $unit)$escaped[]=$unit;$escaped[]=34;$parts[]=FcText::units($escaped)->data;}else $parts[]=$this->format($item)->data;}
            return $this->text(FcText::raw(FcText::fromUtf8('[')->data.implode(FcText::fromUtf8(', ')->data,$parts).FcText::fromUtf8(']')->data));
        }
        return FcText::fromUtf8($this->formatNumber($this->number($value)));
    }
    public function scalarKind(mixed $value): int {if(is_bool($value))return 1;if($value instanceof FcText){$this->text($value);return 2;}$this->number($value);return 0;}
    public function vector(array $values): array {
        if(count($values)>1024)throw new FcError('Un vettore puo contenere al massimo 1024 elementi.');$kind=null;
        foreach($values as $v){$next=$this->scalarKind($v);if($kind!==null&&$next!==$kind)throw new FcError('Gli elementi del vettore devono avere lo stesso tipo.');$kind=$next;}
        $values=array_values($values);if($this->format($values)->length()+(count($values)?2:0)>32768)throw new FcError('Il contenuto del vettore supera 32768 caratteri.');return $values;
    }
    public function requireVector(mixed $value): array {if(!is_array($value))throw new FcError('Serve un vettore.');return $value;}
    public function integer(mixed $value,int $minimum,int $maximum,string $label='Il valore'): int {$n=$this->number($value);if($n!=floor($n)||$n<$minimum||$n>$maximum)throw new FcError($label.' deve essere un intero da '.$minimum.' a '.$maximum.'.');return (int)$n;}
    public function index(mixed $vector,mixed $value): int {$vector=$this->requireVector($vector);$i=$this->integer($value,0,2147483647,'L’indice');if($i>=count($vector))throw new FcError('Indice fuori dal vettore.');return $i;}
    public function at(mixed $vector,callable $expression): mixed {$this->requireVector($vector);return $vector[$this->index($vector,$expression())];}
    public function atName(string $name,callable $expression): mixed {$this->requireVector($this->lookup($name));$i=$expression();$value=$this->lookup($name);return $value[$this->index($value,$i)];}
    public function setAfter(mixed $value,mixed $vector,callable $expression): array {
        $vector=$this->requireVector($vector);$frame=$this->frame();$frame->assignment=$value;
        try{$i=$this->index($vector,$expression());}finally{$frame->assignment=$this->unset;}
        if($this->scalarKind($vector[$i])!==$this->scalarKind($value))throw new FcError('Il nuovo valore deve avere lo stesso tipo del vettore.');$vector[$i]=$value;return $this->vector($vector);
    }
    public function setAfterName(mixed $value,string $name,callable $expression): array {
        $this->requireVector($this->lookup($name));$frame=$this->frame();$frame->assignment=$value;
        try{$i=$expression();return $this->setAfter($value,$this->lookup($name),fn()=>$i);}finally{$frame->assignment=$this->unset;}
    }
    public function concat(mixed $a,mixed $b): FcText {return $this->text(FcText::raw($this->format($a)->data.$this->format($b)->data));}
    public function add(mixed $a,mixed $b): mixed {return $a instanceof FcText||$b instanceof FcText?$this->concat($a,$b):$this->number($this->number($a)+$this->number($b));}
    public function sub(mixed $a,mixed $b): float {return $this->number($this->number($a)-$this->number($b));}
    public function mul(mixed $a,mixed $b): float {return $this->number($this->number($a)*$this->number($b));}
    public function div(mixed $a,mixed $b): float {$a=$this->number($a);$b=$this->number($b);if($b==0)throw new FcError('Divisione per zero.');return $this->number($a/$b);}
    public function mod(mixed $a,mixed $b): float {$a=$this->number($a);$b=$this->number($b);if($b==0)throw new FcError('Resto con divisore zero.');return $this->number(fmod($a,$b));}
    public function power(mixed $a,mixed $b): float {return $this->number($this->number($a)**$this->number($b));}
    public function equal(mixed $a,mixed $b): bool {
        if(is_bool($a)||is_bool($b))return is_bool($a)&&is_bool($b)&&$a===$b;
        if(is_array($a)||is_array($b)){if(!is_array($a)||!is_array($b)||count($a)!==count($b))return false;foreach($a as $i=>$v)if(!$this->equal($v,$b[$i]))return false;return true;}
        if($a instanceof FcText||$b instanceof FcText)return $a instanceof FcText&&$b instanceof FcText&&$this->text($a)->data===$this->text($b)->data;
        return $this->number($a)==$this->number($b);
    }
    public function compare(mixed $a,mixed $b,string $op): bool {
        if($a instanceof FcText&&$b instanceof FcText){$left=array_values(unpack('v*',$this->text($a)->data));$right=array_values(unpack('v*',$this->text($b)->data));$order=0;for($i=0;$i<min(count($left),count($right));$i++)if($left[$i]!==$right[$i]){$order=$left[$i]<=>$right[$i];break;}if($order===0)$order=count($left)<=>count($right);}
        else $order=$this->number($a)<=>$this->number($b);
        return match($op){'<'=>$order<0,'<='=>$order<=0,'>'=>$order>0,default=>$order>=0};
    }
    public function checkType(mixed $value,int $kind,bool $array=false): mixed {
        if($array){foreach($this->requireVector($value) as $v)$this->checkType($v,$kind);return $value;}
        $numeric=is_float($value)||is_int($value);
        $valid=match($kind){1=>$numeric&&$this->number($value)==($value<0?ceil($value):floor($value)),2=>$numeric,3=>$value instanceof FcText,4=>is_bool($value),5=>false,default=>!is_array($value)&&$value!==null&&$value!==$this->unset};
        if(!$valid)throw new FcError('Il valore non corrisponde al tipo dichiarato.');$this->scalarKind($value);return $value;
    }
    public function convert(mixed $value,?array $declaration=null,?array $parameter=null): mixed {
        if($declaration!==null){[$kind,$array,$length]=$declaration;$scalar=fn($v)=>$kind===1&&(is_float($v)||is_int($v))?($this->number($v)<0?ceil($v):floor($v)):$this->checkType($v,$kind);
            if($array){if(count($this->requireVector($value))!==$length)throw new FcError('La dimensione del vettore dichiarato non puo cambiare.');$value=$this->vector(array_map($scalar,$value));}else $value=$scalar($value);}
        return $parameter!==null?$this->checkType($value,$parameter[0],$parameter[1]):$value;
    }
    public function initial(int $kind,bool $array=false,int $length=0): mixed {$value=$kind===3?FcText::fromUtf8(''):($kind===4?false:0.0);return $array?$this->vector(array_fill(0,$this->integer($length,0,1024,'La dimensione'),$value)):$value;}
    public function trim(FcText $text): string {
        return preg_replace('/\A[\x{0009}-\x{000d}\x{0020}\x{0085}\x{00a0}\x{1680}\x{2000}-\x{200a}\x{2028}\x{2029}\x{202f}\x{205f}\x{3000}]+|[\x{0009}-\x{000d}\x{0020}\x{0085}\x{00a0}\x{1680}\x{2000}-\x{200a}\x{2028}\x{2029}\x{202f}\x{205f}\x{3000}]+\z/u','',$text->utf8());
    }
    private const NUMBER='/\A[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?\z/D';
    public function parseInput(FcText $raw,bool $isText=false): mixed {
        $raw=$this->text($raw);if($isText)return $raw;$trimmed=$this->trim($raw);$candidate=preg_replace('/[\x09-\x0d\x20]+\z/','',rtrim($trimmed,"\0"));
        if(preg_match(self::NUMBER,$candidate)){$value=(float)str_replace(',','.',$candidate);if(!is_finite($value)&&$candidate!==$trimmed)return $raw;return $this->number($value);}
        $lower=strtolower($trimmed);if(in_array($lower,['true','vero'],true))return true;if(in_array($lower,['false','falso'],true))return false;
        if(in_array($lower,['nan','infinity','+infinity','-infinity','infinito'],true))throw new FcError('Inserisci un numero finito.');return $raw;
    }
    public function roundNumber(mixed $value,mixed $digits=0.0): float {
        $value=$this->number($value);$digits=$this->integer($digits,0,15,'Le cifre decimali');if(abs($value)>=1e16)return $value;
        $factor=10.0**$digits;$scaled=$value*$factor;$absolute=abs($scaled);$whole=floor($absolute);if($absolute-$whole>=.5)$whole+=1;return ($scaled<0?-1.0:1.0)*$whole/$factor;
    }
    private function fixed(float $value,int $precision): FcText {
        $value=$this->roundNumber($value,(float)$precision);[$digits,$scale]=self::decimal($value);$shift=$scale-$precision;
        if($shift<=0)$digits.=str_repeat('0',-$shift);
        else {$keep=strlen($digits)-$shift;$round=$keep>=0&&$digits[$keep]>='5';$digits=$keep>0?substr($digits,0,$keep):'0';if($round)$digits=self::increment($digits);}
        $digits=str_pad($digits,$precision+1,'0',STR_PAD_LEFT);$body=$precision===0?$digits:substr($digits,0,-$precision).'.'.substr($digits,-$precision);
        return FcText::fromUtf8(($value<0&&strspn($digits,'0')<strlen($digits)?'-':'').$body);
    }
    public function intrinsic(string $name,mixed ...$args): mixed {
        if($name==='eof')return $this->eof();$a=$args[0]??null;
        if($name==='array'||$name==='vettore'){$length=$this->integer($a,0,1024,'La dimensione');$first=count($args)===2?$args[1]:0.0;$this->scalarKind($first);return $this->vector(array_fill(0,$length,$first));}
        if($name==='len'||$name==='size'){if(is_array($a))return (float)count($a);if($name==='len')return (float)count($this->text($a)->codepoints());throw new FcError('size richiede un vettore.');}
        if($name==='char'||$name==='tocode'){$chars=$this->text($a)->codepoints();if($name==='tocode'){if(count($chars)!==1)throw new FcError('tocode richiede un solo carattere.');return (float)$chars[0];}$i=$this->integer($args[1],0,2147483647,'L’indice');if($i>=count($chars))throw new FcError('Indice fuori dal testo.');return FcText::point($chars[$i]);}
        if($name==='tointeger'||$name==='toreal'){$str=$this->trim($this->text($a));if(!preg_match($name==='tointeger'?'/\A[+-]?[0-9]+\z/D':self::NUMBER,$str))throw new FcError('Il testo non contiene un numero valido.');$v=$this->number((float)str_replace(',','.',$str));return $name==='tointeger'?(float)$this->integer($v,-2147483648,2147483647):$v;}
        if($name==='int'&&count($args)===2){if(!$args[1] instanceof FcText||strtolower($args[1]->utf8())!=='passo')throw new FcError('Il secondo argomento di int deve essere "passo".');$v=$this->integer($a,-2147483648,2147483647,'Il passo');if($v===0)throw new FcError('Il passo del ciclo deve essere diverso da zero.');return (float)$v;}
        if(in_array($name,['sum','somma'],true)||(in_array($name,['min','max'],true)&&count($args)===1)){$values=$this->requireVector($a);if(!$values&&in_array($name,['min','max'],true))throw new FcError('Il vettore e vuoto.');$result=0.0;foreach($values as $i=>$v){$n=$this->number($v);$result=$name==='min'?($i===0?$n:min($result,$n)):($name==='max'?($i===0?$n:max($result,$n)):$this->add($result,$n));}return $result;}
        $n=$this->number($a);
        if($name==='round')return $this->roundNumber($n,$args[1]??0.0);
        if($name==='min'||$name==='max'){$values=array_map([$this,'number'],$args);return $name==='min'?min($values):max($values);}
        if($name==='random')return (float)random_int(0,$this->integer($n,1,2147483647,'Il limite')-1);
        if($name==='tochar')return FcText::point($this->integer($n,0,1114111,'Il codice Unicode'));
        if($name==='tostring')return $this->format($n);
        if($name==='tofixed')return $this->fixed($n,$this->integer($args[1],0,15,'Le cifre decimali'));
        $result=match($name){'abs'=>abs($n),'floor'=>floor($n),'int'=>$n<0?ceil($n):floor($n),'sgn'=>(float)($n<=>0),'sqrt'=>sqrt($n),'sin'=>sin($n),'cos'=>cos($n),'tan'=>tan($n),'arcsin'=>asin($n),'arccos'=>acos($n),'arctan'=>atan($n),'ln','log'=>log($n),'log10'=>log10($n),default=>throw new FcError('Funzione non riconosciuta: '.$name)};
        return $this->number($result);
    }
    private function frame(): FcFrame {if(!$this->frames)throw new FcError('Nessuna chiamata attiva.');return $this->frames[count($this->frames)-1];}
    public function guardCall(): void {if(count($this->frames)>=128)throw new FcError('Troppe chiamate annidate: massimo 128 livelli.');}
    public function guard(): void {if($this->steps>=$this->maxSteps)throw new FcError('Limite di esecuzione raggiunto. Controlla la condizione del ciclo.');}
    public function step(): void {$this->guard();$this->steps++;}
    public function test(callable $expression): bool {$this->step();return $this->truth($expression());}
    public function enter(?string $scope,array $parameters=[],bool $references=false): void {
        $this->guardCall();$this->references=$this->references||$references;$frame=new FcFrame($scope,$this->unset);
        foreach($parameters as $name=>$v){if($v instanceof FcArgument){$frame->bindings[$name]=$v->cell;$v=$v->cell->value;}$frame->variables[$name]=$v;}$this->frames[]=$frame;
    }
    public function leave(?Throwable $error=null): void {
        $frame=$this->frame();if($this->errorVariables===null&&$error!==null)$this->errorVariables=$frame->variables;
        if($frame->scope===null)$this->lastVariables=$frame->variables;array_pop($this->frames);
    }
    public function lookup(string $name): mixed {$frame=$this->frame();$values=$frame->transaction??$frame->variables;return $this->value($values[$name]??$this->unset,$name);}
    private function overlay(FcFrame $frame,FcCell $cell,array $value): FcCell {
        $id=spl_object_id($cell);if(!isset($frame->overlays[$id]))$frame->overlays[$id]=[$cell,new FcCell($value,$cell->kind)];return $frame->overlays[$id][1];
    }
    public function reference(string $name,?array $declaration=null,?array $parameter=null): FcArgument {
        $frame=$this->frame();$value=$this->requireVector($this->lookup($name));$spec=$declaration??$parameter;$kind=$spec[0]??0;
        if($frame->transaction!==null&&(isset($frame->wholeWrites[$name])||!array_key_exists($name,$frame->variables))){$frame->newArrays[$name]??=new FcCell($value,$kind);return new FcArgument($frame->newArrays[$name]);}
        $cell=$frame->bindings[$name]??=new FcCell($this->requireVector($frame->variables[$name]),$kind);
        if($frame->transaction!==null){$id=spl_object_id($cell);if(isset($frame->overlays[$id]))return new FcArgument($frame->overlays[$id][1]);if($value!==$cell->value)return new FcArgument($this->overlay($frame,$cell,$value));}
        return new FcArgument($cell);
    }
    public function checkReference(mixed $argument,int $kind): FcArgument {
        if(!$argument instanceof FcArgument)throw new FcError('Parametro per riferimento non valido.');$this->checkType($argument->snapshot,$kind,true);$cell=$argument->cell;
        if($cell->kind!==0&&$kind!==0&&$cell->kind!==$kind)throw new FcError('Il tipo del vettore per riferimento deve coincidere con quello originale.');
        $this->checkType($cell->value,$kind,true);if($cell->kind===0&&$kind!==0)$cell->kind=$kind;return $argument;
    }
    private function checkCell(FcCell $cell,array $value): void {if(count($value)!==count($cell->value))throw new FcError('Un vettore per riferimento conserva la dimensione originale.');$this->checkType($value,$cell->kind,true);}
    private function propagate(FcCell $cell): void {
        foreach($this->frames as $frame){
            foreach($frame->bindings as $name=>$bound)if($bound===$cell){$frame->variables[$name]=$cell->value;if($frame->transaction!==null&&!isset($frame->wholeWrites[$name]))$frame->transaction[$name]=($frame->overlays[spl_object_id($cell)][1]??$cell)->value;}
            if($frame->transaction===null)continue;
            foreach($frame->newArrays as $name=>$bound)if($bound===$cell)$frame->transaction[$name]=$cell->value;
            foreach($frame->overlays as [$parent,$overlay])if($overlay===$cell)foreach($frame->bindings as $name=>$bound)if($bound===$parent&&!isset($frame->wholeWrites[$name]))$frame->transaction[$name]=$cell->value;
        }
    }
    private function valueSize(mixed $value): int {
        if($value instanceof FcArgument)return $this->valueSize($value->snapshot);
        if($value instanceof FcText)return $value->length();if(is_array($value)){$sum=count($value)*8;foreach($value as $v)if($v instanceof FcText)$sum+=$v->length();return $sum;}return 8;
    }
    public function checkMemory(): void {
        $size=$this->outputSize;$slots=0;foreach($this->frames as $frame){foreach([$frame->variables,$frame->transaction] as $values)if($values!==null){$slots+=count($values);foreach($values as $v)$size+=$this->valueSize($v);}foreach($frame->retained as $values)foreach($values as $v)$size+=$this->valueSize($v);if($frame->assignment!==$this->unset)$size+=$this->valueSize($frame->assignment);if($size>2097152||$slots>16384)throw new FcError('La memoria dei valori supera il limite di esecuzione. Riduci i dati conservati o le chiamate ricorsive.');}
    }
    public function beginValues(): void {$frame=$this->frame();$frame->transaction=$frame->variables;$frame->overlays=[];$frame->newArrays=[];$frame->wholeWrites=[];}
    public function stage(string $name,mixed $value,bool $check=true,bool $indexed=false): mixed {
        $frame=$this->frame();if($frame->transaction===null)throw new FcError('Assegnazione fuori dalla transazione.');
        if(!$this->references)$frame->transaction[$name]=$value;
        elseif(!$indexed){$frame->transaction[$name]=$value;$frame->wholeWrites[$name]=true;unset($frame->newArrays[$name]);}
        elseif(isset($frame->wholeWrites[$name])&&isset($frame->newArrays[$name])){$cell=$frame->newArrays[$name];$this->checkCell($cell,$value);$cell->value=$value;$this->propagate($cell);}
        elseif(!isset($frame->wholeWrites[$name])&&isset($frame->bindings[$name])){$cell=$this->overlay($frame,$frame->bindings[$name],$frame->transaction[$name]);$this->checkCell($cell,$value);$cell->value=$value;$this->propagate($cell);}
        else $frame->transaction[$name]=$value;
        if($check)$this->checkMemory();return $value;
    }
    public function commit(bool $check=false): void {
        if($check)$this->checkMemory();$frame=$this->frame();if($frame->transaction===null)throw new FcError('Assegnazione fuori dalla transazione.');
        if($this->references){foreach($frame->overlays as [$parent,$overlay])$this->checkCell($parent,$overlay->value);foreach($frame->newArrays as $cell)$this->checkCell($cell,$cell->value);foreach($frame->wholeWrites as $name=>$_)unset($frame->bindings[$name]);foreach($frame->overlays as [$parent,$overlay]){$parent->value=$overlay->value;$this->propagate($parent);}foreach($frame->newArrays as $name=>$cell)$frame->bindings[$name]=$cell;}
        $frame->variables=$frame->transaction;$frame->transaction=null;$frame->overlays=[];$frame->newArrays=[];$frame->wholeWrites=[];
    }
    public function acceptInput(string $name,mixed $value,bool $indexed=false): void {$this->beginValues();try{$this->stage($name,$value,true,$indexed);$this->commit();}finally{$this->frame()->transaction=null;}}
    public function hold(int $key,mixed $value): mixed {$this->frame()->retained[$key][]=$value;return $value;}
    public function release(int $key,mixed $value): mixed {unset($this->frame()->retained[$key]);return $value;}
    public function call(int $key,callable $function,mixed ...$arguments): mixed {$value=$function(...$arguments);$this->release($key,$value);$this->hold(-1,$value);try{$this->checkMemory();}finally{$this->release(-1,$value);}return $value;}
    public function emit(mixed $value,bool $newline=true): void {
        $value=$this->format($value);$size=$value->length();if($this->outputSize+$size>262144)throw new FcError('Il risultato supera 256 KiB.');$this->output[]=$value;$this->outputNewlines[]=$newline;$this->outputSize+=$size;
        if($this->writer!==null)($this->writer)($value);else{fwrite(STDOUT,$value->utf8(true).($newline?"\n":''));fflush(STDOUT);}
    }
    private function wantsText(?array $declaration,?array $parameter,bool $indexed): bool {$spec=$declaration??$parameter;return $spec!==null&&$spec[0]===3&&($indexed||!$spec[1]);}
    public function read(string $name,string $prompt='',?array $declaration=null,?array $parameter=null,mixed $vector=null,?callable $expression=null): mixed {
        $this->guard();$variableName=$name;$i=null;
        if($expression!==null){$this->requireVector($vector);$position=$expression();$i=$this->index($this->references?$this->lookup($variableName):$vector,$position);$name.='['.$i.']';}
        $this->guard();if($this->trim(FcText::fromUtf8($prompt))===''||$prompt==='Inserisci un valore per x')$prompt='Inserisci un valore per '.$name;
        while(true){$raw=($this->reader)($prompt.': ');if(is_string($raw))$raw=FcText::fromUtf8($raw);
            try{$value=$this->parseInput($raw,$this->wantsText($declaration,$parameter,$i!==null));if($i!==null)$value=$this->setAfter($value,$this->references?$this->lookup($variableName):$vector,fn()=>(float)$i);$value=$this->convert($value,$declaration,$parameter);$this->acceptInput($variableName,$value,$i!==null);return $value;}catch(FcError $error){fwrite(STDERR,$error->getMessage()."\n");}
        }
    }
    public function requireClosed(bool $reading): void {if($reading?$this->readText!==null:$this->writeName!==null)throw new FcError('Il canale e gia aperto. Chiudilo prima di aprirne un altro.');}
    public function requireWrite(): void {if($this->writeName===null)throw new FcError('Apri prima un file in scrittura.');}
    private function validateFileText(FcText $value): string {if($value->length()>2097152)throw new FcError('Il file supera 2 milioni di caratteri.');$data=$value->utf8();if(strlen($data)>4194304)throw new FcError('Il file supera 4 MiB.');return $data;}
    private function fileName(mixed $value): FcText {
        if(!$value instanceof FcText||$value->length()>256||$this->trim($value)==='')throw new FcError('Il nome del file deve contenere da 1 a 256 caratteri senza controlli.');
        foreach(unpack('v*',$value->data) as $u)if($u<32||($u>=127&&$u<=159))throw new FcError('Il nome del file deve contenere da 1 a 256 caratteri senza controlli.');$this->validateFileText($value);return $value;
    }
    public function openFile(bool $reading,mixed $name): void {
        $this->requireClosed($reading);$name=$this->fileName($name);
        if($reading){$value=($this->fileReader)($name);if(is_string($value))$value=FcText::fromUtf8($value);if(!$value instanceof FcText)throw new FcError('Il file non contiene un testo.');$this->validateFileText($value);$this->readText=$value;$this->readPosition=0;}
        else{$this->writeName=$name;$this->writeParts=[];$this->writeSize=$this->writeBytes=0;}
    }
    public function eof(): bool {if($this->readText===null)throw new FcError('Apri prima un file in lettura.');return $this->readPosition>=strlen($this->readText->data);}
    public function readFile(string $name,?array $declaration=null,?array $parameter=null,mixed $vector=null,?callable $expression=null): mixed {
        $i=null;if($expression!==null){$this->requireVector($vector);$position=$expression();$i=$this->index($this->references?$this->lookup($name):$vector,$position);}
        if($this->eof())throw new FcError('Il file e terminato. Controlla EOF() prima di leggere.');
        $data=$this->readText->data;$finish=$this->readPosition;$length=strlen($data);
        while($finish<$length){$u=unpack('v',substr($data,$finish,2))[1];if($u===10||$u===13)break;$finish+=2;}
        $line=FcText::raw(substr($data,$this->readPosition,$finish-$this->readPosition));if($line->length()>32768)throw new FcError('Una riga del file supera 32768 caratteri.');
        $value=$this->parseInput($line,$this->wantsText($declaration,$parameter,$i!==null));if($i!==null)$value=$this->setAfter($value,$this->references?$this->lookup($name):$vector,fn()=>(float)$i);$value=$this->convert($value,$declaration,$parameter);$this->acceptInput($name,$value,$i!==null);
        if($finish<$length&&unpack('v',substr($data,$finish,2))[1]===13){$finish+=2;if($finish<$length&&unpack('v',substr($data,$finish,2))[1]===10)$finish+=2;}
        elseif($finish<$length&&unpack('v',substr($data,$finish,2))[1]===10)$finish+=2;
        $this->readPosition=$finish;return $value;
    }
    public function writeFile(mixed $value,bool $newline=true): void {
        $this->requireWrite();$text=$this->format($value);if($newline)$text=FcText::raw($text->data.pack('v',10));$data=$this->validateFileText($text);
        if($this->writeSize+$text->length()>2097152||$this->writeBytes+strlen($data)>4194304)throw new FcError('Il file generato supera il limite di dimensione.');
        $this->writeParts[]=$text->data;$this->writeSize+=$text->length();$this->writeBytes+=strlen($data);
    }
    public function closeFile(bool $reading): void {
        if($reading){$this->eof();$this->readText=null;$this->readPosition=0;}
        else{$this->requireWrite();($this->fileSaver)($this->writeName,FcText::raw(implode('',$this->writeParts)));$this->writeName=null;$this->writeParts=[];$this->writeSize=$this->writeBytes=0;}
    }
    public function finishFiles(): void {if($this->writeName!==null)$this->closeFile(false);if($this->readText!==null)$this->closeFile(true);}
    private function localPath(FcText $name): string {
        $name=$this->fileName($name)->utf8();
        // PHP supports URL wrappers; an exported local file block must never open one.
        if(str_contains($name,'://')||preg_match('/\A[A-Za-z][A-Za-z0-9+.-]*:/D',$name)&&!preg_match('/\A[A-Za-z]:[\\\\\/]/D',$name))throw new FcError('Usa un percorso di file locale, senza URL o canali speciali.');
        return $name;
    }
    public function readPath(FcText $name): FcText {
        $path=$this->localPath($name);$file=@fopen($path,'rb');if($file===false)throw new FcError('Impossibile aprire il file in lettura.');
        try{$data=stream_get_contents($file,4194305);if($data===false)throw new FcError('Lettura del file non riuscita.');}finally{fclose($file);}
        if(strlen($data)>4194304)throw new FcError('Il file supera 4 MiB.');
        if(str_starts_with($data,"\xff\xfe")){$value=FcText::raw(substr($data,2));$value->validate();}
        elseif(str_starts_with($data,"\xfe\xff")){$body=substr($data,2);if(strlen($body)%2)throw new FcError('Il file non e un testo UTF-8 o UTF-16 valido.');$swapped='';for($i=0;$i<strlen($body);$i+=2)$swapped.=$body[$i+1].$body[$i];$value=FcText::raw($swapped);$value->validate();}
        else{$value=FcText::fromUtf8(str_starts_with($data,"\xef\xbb\xbf")?substr($data,3):$data);}
        $this->validateFileText($value);return $value;
    }
    private function writeAll(mixed $file,string $data): void {$at=0;while($at<strlen($data)){$written=fwrite($file,substr($data,$at));if($written===false||$written===0)throw new FcError('Scrittura della bozza non riuscita.');$at+=$written;}if(!fflush($file)||!fsync($file))throw new FcError('Impossibile completare la bozza del file.');}
    public function savePath(FcText $name,FcText $value): void {
        $destination=$this->localPath($name);$data=$this->validateFileText($value);$directory=dirname($destination);$parent=realpath($directory);$overwrite=file_exists($destination)||is_link($destination);
        $file=false;$draft=null;
        // Exclusive creation prevents clobbering an existing path. The fallback retains data
        // if the requested destination directory is unavailable; it cannot report a save.
        foreach(array_unique([$parent===false?$directory:$parent,sys_get_temp_dir()]) as $where){for($attempt=0;$attempt<12;$attempt++){$candidate=$where.DIRECTORY_SEPARATOR.'frociart-'.bin2hex(random_bytes(12)).'.txt';$file=@fopen($candidate,'x+b');if($file!==false){$draft=$candidate;break;}}if($file!==false)break;}
        if($file===false||$draft===null)throw new FcError('Impossibile creare una bozza recuperabile.');$this->recoveryPath=$draft;
        try{$this->writeAll($file,$data);}finally{fclose($file);}
        if($parent===false||realpath(dirname($draft))!==$parent)throw new FcError('Destinazione non disponibile. Il contenuto resta nella bozza recuperabile.');
        $destination=$parent.DIRECTORY_SEPARATOR.basename($destination);
        if(is_link($destination)||is_dir($destination))throw new FcError('La destinazione non e un file ordinario. La bozza resta recuperabile.');
        if($overwrite){
            try{$answer=($this->reader)('Il file esiste. Scrivi SOVRASCRIVI per sostituirlo: ');if($answer instanceof FcText)$answer=$answer->utf8();$accepted=trim($answer)==='SOVRASCRIVI';}catch(FcError $error){$accepted=false;}
            if(!$accepted)throw new FcError('Salvataggio annullato. Il file originale e intatto; il testo generato e nella bozza recuperabile.');
            if(is_link($destination)||is_dir($destination)||!@rename($draft,$destination))throw new FcError('Impossibile sostituire il file. La bozza resta recuperabile.');
        }else{
            // link() is create-if-absent on the same filesystem: a file which appeared
            // after the initial check is never silently overwritten.
            if(!@link($draft,$destination))throw new FcError('Impossibile creare il file senza sostituirne uno esistente. La bozza resta recuperabile.');
            if(!@unlink($draft))throw new FcError('File creato; impossibile rimuovere la bozza recuperabile.');
        }
        $this->recoveryPath=null;
    }
    public function turtleForward(mixed $distance,bool $draw=true): void {
        $distance=$this->number($distance);$radians=$this->heading*M_PI/180;
        $cosine=match($this->heading){90.0,270.0=>0.0,0.0=>1.0,180.0=>-1.0,default=>cos($radians)};
        $sine=match($this->heading){0.0,180.0=>0.0,90.0=>1.0,270.0=>-1.0,default=>sin($radians)};
        $x=$this->x+$distance*$cosine;$y=$this->y+$distance*$sine;if(!is_finite($x)||!is_finite($y)||abs($x)>1000000||abs($y)>1000000)throw new FcError('Il disegno supera le coordinate da -1000000 a 1000000.');
        if($draw&&($x!=$this->x||$y!=$this->y)){if(count($this->segments)>=10000)throw new FcError('Il disegno supera 10000 tratti.');$this->segments[]=[$this->x,$this->y,$x,$y,$this->color];}$this->x=$x;$this->y=$y;$this->turtleUsed=true;
    }
    public function turtleTurn(mixed $angle,bool $left=false): void {$angle=fmod($this->number($angle),360);$this->heading=fmod(fmod($this->heading+($left?$angle:-$angle),360)+360,360);$this->turtleUsed=true;}
    public function turtleHome(): void {$this->x=$this->y=0.0;$this->heading=90.0;$this->turtleUsed=true;}
    public function turtleClear(): void {$this->segments=[];$this->turtleHome();}
    public function turtlePen(mixed $value): void {
        $value=strtolower($this->trim($this->text($value)));$names=[['nero','black','#000000'],['bianco','white','#FFFFFF'],['grigio','gray','#808080'],['rosso','red','#FF0000'],['arancione','orange','#FF8C00'],['giallo','yellow','#FFFF00'],['verde','green','#008000'],['lime','lime','#00FF00'],['turchese','teal','#008080'],['ciano','cyan','#00FFFF'],['blu','blue','#0000FF'],['azzurro','skyblue','#00BFFF'],['viola','purple','#800080'],['magenta','magenta','#FF00FF'],['rosa','pink','#FF69B4'],['marrone','brown','#A52A2A'],['grey','grey','#808080']];
        foreach($names as [$italian,$english,$code])if($value===$italian||$value===$english){$value=$code;break;}
        if(preg_match('/\A#[0-9a-f]{3}\z/D',$value))$value='#'.$value[1].$value[1].$value[2].$value[2].$value[3].$value[3];
        if(!preg_match('/\A#[0-9a-fA-F]{6}\z/D',$value))throw new FcError('Colore non riconosciuto. Usa un nome o #RRGGBB.');$this->color=strtoupper($value);$this->turtleUsed=true;
    }
    public function drawingSvg(): string {
        $xmin=min(0.0,$this->x);$xmax=max(0.0,$this->x);$ymin=min(0.0,$this->y);$ymax=max(0.0,$this->y);
        foreach($this->segments as [$x1,$y1,$x2,$y2,$_]){$xmin=min($xmin,$x1,$x2);$xmax=max($xmax,$x1,$x2);$ymin=min($ymin,$y1,$y2);$ymax=max($ymax,$y1,$y2);}
        $width=max(100.0,$xmax-$xmin)*1.2;$height=max(100.0,$ymax-$ymin)*1.2;$cx=($xmin+$xmax)/2;$cy=($ymin+$ymax)/2;$lines=[];
        foreach($this->segments as [$x1,$y1,$x2,$y2,$color])$lines[]='<line x1="'.$this->formatNumber($x1).'" y1="'.$this->formatNumber(-$y1).'" x2="'.$this->formatNumber($x2).'" y2="'.$this->formatNumber(-$y2).'" stroke="'.$color.'" stroke-width="2" vector-effect="non-scaling-stroke"/>';
        return '<svg xmlns="http://www.w3.org/2000/svg" width="900" height="700" viewBox="'.implode(' ',array_map([$this,'formatNumber'],[$cx-$width/2,-$cy-$height/2,$width,$height])).'"><title>Frociart — disegno</title>'.implode("\n",$lines).'</svg>';
    }
    public function showDrawing(): void {if($this->turtleUsed)($this->fileSaver)(FcText::fromUtf8('Frociart_disegno.svg'),FcText::fromUtf8($this->drawingSvg()));}
    public function run(callable $main,bool $drawing=true): bool {
        try{$main();if($drawing)$this->showDrawing();return true;}catch(Throwable $error){fwrite(STDERR,'Frociart: '.$error->getMessage()."\n");if($this->recoveryPath!==null)fwrite(STDERR,'Bozza recuperabile: '.$this->recoveryPath."\n");return false;}
    }
}
