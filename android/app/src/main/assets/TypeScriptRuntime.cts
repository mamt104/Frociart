// Standalone Node.js support. Emitted instructions execute the program; no graph/parser is loaded.
interface FCRequire { (name: string): unknown; main: unknown; }
interface FCProcess { stdout: {write(text: string): boolean}; stderr: {write(text: string): boolean}; exitCode: number; }
interface FCFileSystem {
    openSync(path: string,flags: string): number; closeSync(fd: number): void;
    readSync(fd: number,buffer: Uint8Array,offset: number,length: number,position: number|null): number;
    writeSync(fd: number,buffer: Uint8Array,offset?: number,length?: number): number;
    fsyncSync(fd: number): void; existsSync(path: string): boolean;
    statSync(path: string): {size: number;isDirectory(): boolean}; readFileSync(path: string): Uint8Array;
    renameSync(from: string,to: string): void; unlinkSync(path: string): void;
    copyFileSync(from: string,to: string,flags: number): void;
    constants: {COPYFILE_EXCL: number};
}
interface FCPath {resolve(...parts: string[]): string;dirname(path: string): string;join(...parts: string[]): string;}
declare const require: FCRequire;
declare const module: unknown;
declare const process: FCProcess;
const nodeFs=require("node:fs") as FCFileSystem;
const nodePath=require("node:path") as FCPath;
const nodeCrypto=require("node:crypto") as {randomUUID(): string};
const utf8=new TextEncoder();
class Problem extends Error {}
function problem(text: string): Problem {return new Problem(text);}
function finite(n: number): number {if(!Number.isFinite(n))throw problem("Il risultato non è un numero finito: controlla dominio e grandezza dei valori.");return n;}
function shortText(text: string): string {return text.length<=60?text:text.slice(0,57)+"...";}
function numberFromBits(hex: string): number {const bytes=new DataView(new ArrayBuffer(8));bytes.setBigUint64(0,BigInt("0x"+hex));return bytes.getFloat64(0);}
function formatNumber(n: number): string {
    if(n===0)return "0";
    const parts=n.toExponential(14).split("e");let mantissa=parts[0].replace(/(\.[0-9]*?)0+$/,"$1").replace(/\.$/,"");const exponent=Number(parts[1]);
    if(exponent>=15||exponent< -4)return mantissa+"E"+(exponent>=0?"+":"-")+Math.abs(exponent).toString().padStart(2,"0");
    const negative=mantissa.startsWith("-");if(negative)mantissa=mantissa.slice(1);const digits=mantissa.replace(".","");const at=exponent+1;
    const body=at<=0?"0."+"0".repeat(-at)+digits:at>=digits.length?digits+"0".repeat(at-digits.length):digits.slice(0,at)+"."+digits.slice(at);
    return (negative?"-":"")+body;
}
function roundAway(n: number,digits: number): number {if(Math.abs(n)>=1e16)return n;const factor=10**digits,scaled=n*factor,abs=Math.abs(scaled);let whole=Math.floor(abs);if(abs-whole>=0.5)whole++;return (scaled<0?-whole:whole)/factor;}
function fixed(n: number,digits: number): string {
    const rounded=roundAway(n,digits),data=new DataView(new ArrayBuffer(8));data.setFloat64(0,Math.abs(rounded));const bits=data.getBigUint64(0);const exponent=Number((bits>>52n)&0x7ffn);let mantissa=bits&0xfffffffffffffn;if(exponent!==0)mantissa|=1n<<52n;
    const shift=(exponent===0?-1022:exponent-1023)-52;let scaled=mantissa*10n**BigInt(digits);
    if(shift>=0)scaled<<=BigInt(shift);else{const divisor=1n<<BigInt(-shift),remainder=scaled%divisor;scaled/=divisor;if(remainder*2n>=divisor)scaled++;}
    let text=scaled.toString();if(digits>0){text=text.padStart(digits+1,"0");text=text.slice(0,-digits)+"."+text.slice(-digits);}return (rounded<0&&scaled!==0n?"-":"")+text;
}
type RawValue=number|string|boolean|VectorValue|ArrayArgument;
class Value {
    constructor(readonly raw: RawValue){}
    static number(n: number): Value {return new Value(finite(n));}
    static text(text: string): Value {if(text.length>32768)throw problem("Il testo supera il limite di 32768 caratteri.");return new Value(text);}
    static logical(value: boolean): Value {return new Value(value);}
    static vector(values: readonly Value[]): Value {return new Value(new VectorValue(values));}
    isVector(): boolean {return this.raw instanceof VectorValue;}isNumber(): boolean {return typeof this.raw==="number";}
    isText(): boolean {return typeof this.raw==="string";}isBoolean(): boolean {return typeof this.raw==="boolean";}
    asNumber(): number {if(typeof this.raw!=="number")throw problem("Serve un numero, ma il valore è «"+shortText(this.format())+"».");return finite(this.raw);}
    asBoolean(): boolean {if(typeof this.raw==="boolean")return this.raw;if(typeof this.raw==="number")return this.asNumber()!==0;throw problem("La condizione deve restituire vero/falso o un numero, non un testo.");}
    vector(): VectorValue {if(!(this.raw instanceof VectorValue))throw problem("Serve un vettore, per esempio valori = [10, 20, 30].");return this.raw;}
    vectorSize(): number {return this.vector().items.length;}element(i: number): Value {return this.vector().at(i);}
    format(): string {if(typeof this.raw==="number")return formatNumber(this.raw);if(typeof this.raw==="boolean")return this.raw?"vero":"falso";if(typeof this.raw==="string")return this.raw;if(this.raw instanceof VectorValue)return this.raw.format();throw problem("Il riferimento a un vettore può essere usato solo come parametro.");}
}
class VectorValue {
    readonly items: readonly Value[];
    constructor(values: readonly Value[]){if(values.length>1024)throw problem("Un vettore può contenere al massimo 1024 elementi.");let kind=-1,total=2;for(const value of values){const next=VectorValue.scalar(value);if(kind<0)kind=next;else if(kind!==next)throw problem("Tutti gli elementi del vettore devono avere lo stesso tipo.");total+=VectorValue.elementText(value).length+2;if(total>32768)throw problem("Il contenuto del vettore supera 32768 caratteri.");}this.items=[...values];}
    static scalar(value: Value): number {if(value.isNumber()){value.asNumber();return 0;}if(value.isBoolean())return 1;if(value.isText())return 2;throw problem("I vettori contengono numeri, testi o valori logici; i vettori annidati non sono supportati.");}
    static elementText(value: Value): string {return value.isText()?"\""+value.format().replace(/\\/g,"\\\\").replace(/"/g,'\\"').replace(/\n/g,"\\n").replace(/\r/g,"\\r").replace(/\t/g,"\\t")+"\"":value.format();}
    format(): string {return "["+this.items.map(VectorValue.elementText).join(", ")+"]";}
    index(value: Value): number {const i=integer(value,0,2147483647,"L’indice");if(i>=this.items.length)throw problem(this.items.length===0?"Il vettore è vuoto: non contiene elementi.":"Indice "+i+" fuori dal vettore. Usa un indice da 0 a "+(this.items.length-1)+".");return i;}
    at(index: number): Value {return this.items[this.index(Value.number(index))];}
    set(index: number,value: Value): Value {if(VectorValue.scalar(this.at(index))!==VectorValue.scalar(value))throw problem("Il nuovo valore deve avere lo stesso tipo degli elementi del vettore.");const next=[...this.items];next[index]=value;return Value.vector(next);}
    equals(other: VectorValue): boolean {return this.items.length===other.items.length&&this.items.every((v,i)=>v.raw===other.items[i].raw);}
}
function integer(value: Value,minimum: number,maximum: number,label: string): number {const n=value.asNumber();if(n<minimum||n>maximum||n!==Math.trunc(n))throw problem(label+" deve essere un intero da "+minimum+" a "+maximum+".");return n;}
function textValue(value: Value): string {if(typeof value.raw!=="string")throw problem("Questa funzione richiede un testo tra virgolette o una variabile di testo.");return value.raw;}
function characterCount(text: string): number {let count=0;for(let i=0;i<text.length;i++,count++){const ch=text.charCodeAt(i);if(ch>=0xd800&&ch<=0xdbff){const next=text.charCodeAt(++i);if(!(next>=0xdc00&&next<=0xdfff))throw problem("Il testo contiene un carattere Unicode incompleto.");}else if(ch>=0xdc00&&ch<=0xdfff)throw problem("Il testo contiene un carattere Unicode incompleto.");}return count;}
function characters(text: string): string[] {characterCount(text);return Array.from(text);}
function trimInput(value: string): string {return value.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/g,"");}
function numericInputText(value: string): string {return value.replace(/\u0000+$/,"").replace(/[\u0009-\u000d\u0020]+$/,"");}
function parseInput(raw: string): Value {
    if(raw.length>32768)throw problem("Il valore inserito è troppo lungo (massimo 32768 caratteri).");const source=trimInput(raw),numeric=numericInputText(source);
    if(/^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$/.test(numeric)){const value=Number(numeric.replace(",","."));if(!Number.isFinite(value)&&numeric!==source)return Value.text(raw);return Value.number(value);}
    const lower=source.toLowerCase();if(lower==="true"||lower==="vero")return Value.logical(true);if(lower==="false"||lower==="falso")return Value.logical(false);if(["nan","infinity","+infinity","-infinity","infinito"].includes(lower))throw problem("Inserisci un numero finito.");return Value.text(raw);
}
class Variables extends Map<string,Value> {
    private readonly keysByLower=new Map<string,string>();
    constructor(source?: Variables){super();if(source)for(const [key,value] of source)this.set(key,value);}
    override set(key: string,value: Value): this {const lower=key.toLowerCase();let original=this.keysByLower.get(lower);if(original===undefined){original=key;this.keysByLower.set(lower,key);}super.set(original,value);return this;}
    override get(key: string): Value|undefined {const original=this.keysByLower.get(key.toLowerCase());return original===undefined?undefined:super.get(original);}
    override has(key: string): boolean {return this.keysByLower.has(key.toLowerCase());}
    override delete(key: string): boolean {const lower=key.toLowerCase(),original=this.keysByLower.get(lower);if(original===undefined)return false;this.keysByLower.delete(lower);return super.delete(original);}
    override clear(): void {super.clear();this.keysByLower.clear();}
    find(key: string): Value|null {return this.get(key)??null;}
}
class TypeSpec {constructor(readonly kind: number,readonly array: boolean,readonly length: number){}}
class ArrayCell {constructor(public value: Value,public kind: number){}}
class ArrayArgument {readonly snapshot: Value;constructor(readonly cell: ArrayCell){this.snapshot=cell.value;}}
interface FCJournal {readonly id: string;append(text: string): void;close(): void;saved(): boolean;}
class TextFiles {
    input: string|null=null;position=0;output: string|null=null;outputBytes=0;readName: string|null=null;writeName: string|null=null;journal: FCJournal|null=null;journalFactory: ((name: string)=>FCJournal)|null=null;
    hasRead(): boolean {return this.input!==null;}hasWrite(): boolean {return this.output!==null;}
    closeJournal(): void {if(this.journal){this.journal.close();this.journal=null;}}
    static validate(text: string): void {if(text.length>2097152)throw problem("Il file supera 2 milioni di caratteri.");characterCount(text);if(utf8.encode(text).length>4194304)throw problem("Il file supera 4 MiB.");}
    static fileName(value: Value): string {const text=textValue(value);if(text.trim().length===0||text.length>256||/[\x00-\x1f\x7f-\x9f]/.test(text))throw problem("Il nome del file deve essere un testo da 1 a 256 caratteri, senza caratteri di controllo.");characterCount(text);return text;}
    requireRead(): string {if(this.input===null)throw problem("Apri prima un file in lettura. EOF() controlla quel file.");return this.input;}
    eof(): boolean {return this.position>=this.requireRead().length;}
    openRead(name: string,text: string): void {if(this.hasRead())throw problem("Un file è già aperto in lettura. Chiudilo prima di aprirne un altro.");TextFiles.validate(text);this.input=text;this.position=0;this.readName=name;}
    openWrite(name: string): void {if(this.hasWrite())throw problem("Un file è già aperto in scrittura. Chiudilo e salvalo prima di aprirne un altro.");try{this.journal=this.journalFactory?this.journalFactory(name):null;}catch{throw problem("Non riesco a preparare la bozza locale nella cartella di destinazione.");}this.output="";this.outputBytes=0;this.writeName=name;}
    peekLine(): string {const input=this.requireRead();if(this.eof())throw problem("Il file è terminato. Usa non EOF() prima di leggere.");let end=this.position;while(end<input.length&&input[end]!=="\r"&&input[end]!=="\n"){end++;if(end-this.position>32768)throw problem("La riga supera 32768 caratteri.");}return input.slice(this.position,end);}
    commitRead(): void {const line=this.peekLine(),input=this.requireRead();this.position+=line.length;if(input[this.position]==="\r"){this.position++;if(input[this.position]==="\n")this.position++;}else if(input[this.position]==="\n")this.position++;}
    write(text: string,newline: boolean): void {if(this.output===null)throw problem("Apri prima un file in scrittura.");text+=newline?"\n":"";characterCount(text);const bytes=utf8.encode(text).length;if(this.output.length+text.length>2097152||this.outputBytes+bytes>4194304)throw problem("Il file generato supera 4 MiB o 2 milioni di caratteri.");try{if(this.journal)this.journal.append(text);}catch{throw problem("Scrittura interrotta: la bozza di recupero conserva il testo precedente.");}this.output+=text;this.outputBytes+=bytes;}
    closeRead(): void {this.requireRead();this.input=this.readName=null;this.position=0;}
    saved(): void {if(this.journal){this.journal.saved();this.journal=null;}this.output=this.writeName=null;this.outputBytes=0;}
    static decode(bytes: Uint8Array): string {if(bytes.length>4194304)throw problem("Il file supera 4 MiB.");let offset=0,encoding="utf-8";if(bytes[0]===239&&bytes[1]===187&&bytes[2]===191)offset=3;else if(bytes[0]===255&&bytes[1]===254){offset=2;encoding="utf-16le";}else if(bytes[0]===254&&bytes[1]===255){offset=2;encoding="utf-16be";}let text: string;try{text=new TextDecoder(encoding,{fatal:true,ignoreBOM:true}).decode(bytes.subarray(offset));}catch{throw problem("Il file non è un testo UTF-8 o UTF-16 valido.");}TextFiles.validate(text);return text;}
}
interface TurtleSegment {x1: number;y1: number;x2: number;y2: number;color: string;}
class Turtle {
    x=0;y=0;heading=90;color="#000000";used=false;readonly segments: TurtleSegment[]=[];
    apply(kind: number,value: Value|null,flag: boolean): void {
        if(kind===12||kind===13){if(!value)throw problem("La tartaruga richiede un numero.");const n=value.asNumber();if(kind===12){const rad=this.heading*Math.PI/180,cos=this.heading===90||this.heading===270?0:this.heading===0?1:this.heading===180?-1:Math.cos(rad),sin=this.heading===0||this.heading===180?0:this.heading===90?1:this.heading===270?-1:Math.sin(rad);const nx=this.x+n*cos,ny=this.y+n*sin;if(!Number.isFinite(nx)||!Number.isFinite(ny)||Math.abs(nx)>1000000||Math.abs(ny)>1000000)throw problem("Il disegno supera le coordinate tra -1000000 e 1000000.");const line=flag&&(nx!==this.x||ny!==this.y);if(line&&this.segments.length>=10000)throw problem("Il disegno supera 10000 tratti.");if(line)this.segments.push({x1:this.x,y1:this.y,x2:nx,y2:ny,color:this.color});this.x=nx;this.y=ny;}else this.heading=((this.heading+(flag?n%360:-(n%360)))%360+360)%360;}
        else if(kind===14||kind===15){this.x=this.y=0;this.heading=90;if(kind===15)this.segments.length=0;}
        else if(kind===16){if(!value)throw problem("Il colore richiede un testo.");this.color=Turtle.parseColor(textValue(value));}else throw problem("Comando tartaruga non riconosciuto.");this.used=true;
    }
    static parseColor(raw: string): string {const text=raw.trim().toLowerCase();const colors: readonly (readonly string[])[]=[["nero","black","#000000"],["bianco","white","#FFFFFF"],["grigio","gray","#808080"],["rosso","red","#FF0000"],["arancione","orange","#FF8C00"],["giallo","yellow","#FFFF00"],["verde","green","#008000"],["lime","lime","#00FF00"],["turchese","teal","#008080"],["ciano","cyan","#00FFFF"],["blu","blue","#0000FF"],["azzurro","skyblue","#00BFFF"],["viola","purple","#800080"],["magenta","magenta","#FF00FF"],["rosa","pink","#FF69B4"],["marrone","brown","#A52A2A"]];for(const [it,en,hex] of colors)if(text===it||text===en)return hex;if(text==="grey")return "#808080";if(/^#[0-9a-f]{6}$/.test(text))return text.toUpperCase();if(/^#[0-9a-f]{3}$/.test(text))return ("#"+text[1]+text[1]+text[2]+text[2]+text[3]+text[3]).toUpperCase();throw problem("Colore non riconosciuto. Usa un nome o #RRGGBB.");}
    svg(): string {let minX=Math.min(0,this.x),maxX=Math.max(0,this.x),minY=Math.min(0,this.y),maxY=Math.max(0,this.y);for(const s of this.segments){minX=Math.min(minX,s.x1,s.x2);maxX=Math.max(maxX,s.x1,s.x2);minY=Math.min(minY,s.y1,s.y2);maxY=Math.max(maxY,s.y1,s.y2);}const margin=Math.max(10,Math.max(maxX-minX,maxY-minY)*.05),width=Math.max(1,maxX-minX)+2*margin,height=Math.max(1,maxY-minY)+2*margin;const box=[minX-margin,-maxY-margin,width,height].join(" ");return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="'+box+'"><title>Disegno tartaruga — Frociart</title><rect x="'+(minX-margin)+'" y="'+(-maxY-margin)+'" width="'+width+'" height="'+height+'" fill="white"/>'+this.segments.map(s=>'<line x1="'+s.x1+'" y1="'+(-s.y1)+'" x2="'+s.x2+'" y2="'+(-s.y2)+'" stroke="'+s.color+'" stroke-width="2" stroke-linecap="round" vector-effect="non-scaling-stroke"/>').join("")+"</svg>";}
}
class Frame {
    variables=new Variables();transaction: Variables|null=null;readonly retained=new Map<number,(Value|null)[]>();assignment: Value|null=null;
    readonly bindings=new Map<string,ArrayCell>();readonly newArrays=new Map<string,ArrayCell>();readonly overlays=new Map<ArrayCell,ArrayCell>();readonly wholeWrites=new Set<string>();
    constructor(readonly scope: string|null){}
}
interface RuntimeOptions {reader?: (prompt: string)=>string|null;writer?: (text: string)=>void;fileReader?: (name: string)=>string;fileSaver?: (name: string,text: string)=>boolean;}
class Runtime {
    readonly reader: (prompt: string)=>string|null;readonly writer: ((text: string)=>void)|null;readonly fileReader: (name: string)=>string;readonly fileSaver: (name: string,text: string)=>boolean;
    readonly files=new TextFiles();readonly turtleState=new Turtle();readonly output: string[]=[];readonly outputNewlines: boolean[]=[];readonly frames: Frame[]=[];
    lastVariables=new Variables();errorVariables: Variables|null=null;steps=0;outputSize=0;maxSteps=10000;references=false;recoveryPath: string|null=null;
    private consoleBuffer="";private consoleSkipLF=false;private consoleEnded=false;private readonly consoleDecoder=new TextDecoder("utf-8",{fatal:true});
    constructor(options: RuntimeOptions={}){this.reader=options.reader??(prompt=>this.consoleRead(prompt));this.writer=options.writer??null;this.fileReader=options.fileReader??(name=>this.readPath(name));this.fileSaver=options.fileSaver??((name,text)=>this.savePath(name,text));if(!options.fileSaver)this.files.journalFactory=name=>this.journal(name);}
    private consoleRead(prompt: string): string|null {process.stdout.write(prompt);const bytes=new Uint8Array(8192);while(true){
        if(this.consoleSkipLF&&this.consoleBuffer.length>0){if(this.consoleBuffer[0]==="\n")this.consoleBuffer=this.consoleBuffer.slice(1);this.consoleSkipLF=false;}
        const at=this.consoleBuffer.search(/[\r\n]/);if(at>=0){const line=this.consoleBuffer.slice(0,at);let end=at+1;if(this.consoleBuffer[at]==="\r"){if(this.consoleBuffer[end]==="\n")end++;else if(end===this.consoleBuffer.length)this.consoleSkipLF=true;}this.consoleBuffer=this.consoleBuffer.slice(end);return line;}
        if(this.consoleEnded){if(this.consoleBuffer.length===0)return null;const line=this.consoleBuffer;this.consoleBuffer="";return line;}
        const count=nodeFs.readSync(0,bytes,0,bytes.length,null);if(count===0){this.consoleBuffer+=this.consoleDecoder.decode();this.consoleEnded=true;}else this.consoleBuffer+=this.consoleDecoder.decode(bytes.subarray(0,count),{stream:true});if(this.consoleBuffer.length>2097152)throw problem("L’input supera lo spazio disponibile.");
    }}
    guard(): void {if(this.steps>=this.maxSteps)throw problem("Limite di esecuzione raggiunto. Controlla la condizione del ciclo.");}
    step(): void {this.guard();this.steps++;}test(expression: ()=>Value): boolean {this.step();return expression().asBoolean();}
    guardCall(): void {if(this.frames.length>=128)throw problem("Troppe chiamate annidate: massimo 128 livelli.");}
    enter(scope: string|null,names: string[],values: Value[],references=false): void {this.guardCall();this.references||=references;const f=new Frame(scope);for(let i=0;i<names.length;i++){let v=values[i];if(v.raw instanceof ArrayArgument){f.bindings.set(names[i].toLowerCase(),v.raw.cell);v=v.raw.cell.value;}f.variables.set(names[i],v);}this.frames.push(f);}
    private frame(): Frame {return this.frames[this.frames.length-1];}
    captureError(): void {if(this.errorVariables===null&&this.frames.length>0)this.errorVariables=new Variables(this.frame().variables);}
    leave(): void {const f=this.frame();if(f.scope===null)this.lastVariables=new Variables(f.variables);this.frames.pop();}
    requireValue(value: Value|null): Value {if(value===null)throw problem("La funzione non ha restituito un valore.");return value;}
    value(value: Value|null,name: string): Value {if(value!==null)return value;if(name.toLowerCase()==="pi"||name==="π")return Value.number(Math.PI);throw problem("La variabile «"+name+"» non ha ancora un valore.");}
    lookup(name: string): Value {const f=this.frame();return this.value((f.transaction??f.variables).find(name),name);}
    reference(name: string,declaration: TypeSpec|null,parameter: TypeSpec|null): Value {name=name.toLowerCase();const f=this.frame(),v=this.lookup(name);v.vector();const kind=(declaration??parameter)?.kind??0;let cell: ArrayCell|undefined;
        if(f.transaction&&(f.wholeWrites.has(name)||!f.variables.has(name))){cell=f.newArrays.get(name);if(!cell){cell=new ArrayCell(v,kind);f.newArrays.set(name,cell);}return new Value(new ArrayArgument(cell));}
        cell=f.bindings.get(name);if(!cell){cell=new ArrayCell(this.requireValue(f.variables.find(name)),kind);cell.value.vector();f.bindings.set(name,cell);}if(f.transaction){const staged=f.overlays.get(cell);if(staged)return new Value(new ArrayArgument(staged));if(v!==cell.value)return new Value(new ArrayArgument(this.overlay(f,cell,v)));}return new Value(new ArrayArgument(cell));
    }
    checkReference(argument: Value,kind: number): Value {if(!(argument.raw instanceof ArrayArgument))throw problem("Parametro per riferimento non valido.");const a=argument.raw,cell=a.cell;this.checkType(a.snapshot,kind,true);if(cell.kind!==0&&kind!==0&&cell.kind!==kind)throw problem("Il tipo del vettore per riferimento deve coincidere con quello originale.");this.checkType(cell.value,kind,true);if(cell.kind===0&&kind!==0)cell.kind=kind;return cell.value;}
    private overlay(f: Frame,cell: ArrayCell,value: Value): ArrayCell {let staged=f.overlays.get(cell);if(!staged){staged=new ArrayCell(value,cell.kind);f.overlays.set(cell,staged);}return staged;}
    private propagate(cell: ArrayCell): void {for(const f of this.frames){for(const [name,bound] of f.bindings)if(bound===cell){f.variables.set(name,cell.value);if(f.transaction&&!f.wholeWrites.has(name))f.transaction.set(name,f.overlays.get(cell)?.value??cell.value);}if(f.transaction){for(const [name,bound] of f.newArrays)if(bound===cell)f.transaction.set(name,cell.value);for(const [original,staged] of f.overlays)if(staged===cell)for(const [name,bound] of f.bindings)if(bound===original&&!f.wholeWrites.has(name))f.transaction.set(name,cell.value);}}}
    private checkCell(cell: ArrayCell,value: Value): void {if(value.vectorSize()!==cell.value.vectorSize())throw problem("Un vettore per riferimento conserva la dimensione originale.");this.checkType(value,cell.kind,true);}
    private valueSize(v: Value|null): number {if(v===null)return 8;if(v.raw instanceof ArrayArgument)return this.valueSize(v.raw.snapshot);if(v.isText())return v.format().length;if(v.isVector())return v.vectorSize()*8+v.vector().items.reduce((sum,x)=>sum+(x.isText()?x.format().length:0),0);return 8;}
    checkMemory(): void {let size=this.outputSize,slots=0;for(const f of this.frames){for(const values of [f.variables,f.transaction])if(values){slots+=values.size;for(const v of values.values())size+=this.valueSize(v);}for(const retained of f.retained.values())for(const v of retained)size+=this.valueSize(v);if(f.assignment)size+=this.valueSize(f.assignment);if(size>2097152||slots>16384)throw problem("La memoria dei valori supera il limite di esecuzione.");}}
    begin(): void {const f=this.frame();f.transaction=new Variables(f.variables);if(this.references){f.overlays.clear();f.newArrays.clear();f.wholeWrites.clear();}}
    private transaction(): Variables {const t=this.frame().transaction;if(!t)throw problem("Assegnazione non inizializzata.");return t;}
    stage(name: string,value: Value,check=true,indexed=false): Value {const original=name;name=name.toLowerCase();const f=this.frame(),t=this.transaction();if(!this.references)t.set(original,value);else if(!indexed){t.set(original,value);f.wholeWrites.add(name);f.newArrays.delete(name);}else if(f.wholeWrites.has(name)&&f.newArrays.has(name)){const cell=f.newArrays.get(name)!;this.checkCell(cell,value);cell.value=value;this.propagate(cell);}else if(!f.wholeWrites.has(name)&&f.bindings.has(name)){const cell=this.overlay(f,f.bindings.get(name)!,this.requireValue(t.find(name)));this.checkCell(cell,value);cell.value=value;this.propagate(cell);}else t.set(original,value);if(check)this.checkMemory();return value;}
    staged(name: string): Value|null {return this.transaction().find(name);}variable(name: string): Value|null {return this.frame().variables.find(name);}
    commit(check=false): void {if(check)this.checkMemory();const f=this.frame();if(this.references){for(const [cell,staged] of f.overlays)this.checkCell(cell,staged.value);for(const cell of f.newArrays.values())this.checkCell(cell,cell.value);for(const name of f.wholeWrites)f.bindings.delete(name);for(const [cell,staged] of f.overlays){cell.value=staged.value;this.propagate(cell);}for(const [name,cell] of f.newArrays)f.bindings.set(name,cell);}f.variables=this.transaction();f.transaction=null;f.overlays.clear();f.newArrays.clear();f.wholeWrites.clear();}
    private acceptInput(name: string,value: Value,indexed: boolean): void {this.begin();try{this.stage(name,value,true,indexed);this.commit();}finally{this.frame().transaction=null;}}
    hold<T extends Value|null>(key: number,value: T): T {let list=this.frame().retained.get(key);if(!list){list=[];this.frame().retained.set(key,list);}list.push(value);return value;}
    release<T extends Value|null>(key: number,value: T): T {this.frame().retained.delete(key);return value;}
    call<T extends Value|null>(key: number,invocation: ()=>T): T {const value=invocation();this.release(key,value);this.hold(-1,value);try{this.checkMemory();}finally{this.release(-1,value);}return value;}
    unary(op: string,value: Value): Value {return op==="not"?Value.logical(!value.asBoolean()):Value.number(op==="-"?-value.asNumber():value.asNumber());}
    binary(op: string,a: Value,b: Value): Value {
        if(op==="&"||op==="+"&&(a.isText()||b.isText()))return Value.text(a.format()+b.format());
        if(["=","==","!=","<>"].includes(op)){const same=a.raw instanceof VectorValue&&b.raw instanceof VectorValue?a.raw.equals(b.raw):a.raw===b.raw;return Value.logical(op==="!="||op==="<>"?!same:same);}
        if(["<","<=",">",">="].includes(op)){const x=a.isText()&&b.isText()?a.format():a.asNumber(),y=a.isText()&&b.isText()?b.format():b.asNumber();return Value.logical(op==="<"?x<y:op==="<="?x<=y:op===">"?x>y:x>=y);}
        const x=a.asNumber(),y=b.asNumber();switch(op){case "+":return Value.number(x+y);case "-":return Value.number(x-y);case "*":return Value.number(x*y);case "/":if(y===0)throw problem("Divisione per zero.");return Value.number(x/y);case "%":if(y===0)throw problem("Resto con divisore zero.");return Value.number(x%y);case "^":return Value.number(x**y);default:throw problem("Operatore non riconosciuto.");}
    }
    at(target: Value,index: ()=>Value): Value {const v=target.vector();return v.at(v.index(index()));}
    atName(name: string,index: ()=>Value): Value {this.lookup(name).vector();const i=index(),v=this.lookup(name).vector();return v.at(v.index(i));}
    setAfter(value: Value,target: Value,index: ()=>Value): Value {const f=this.frame();f.assignment=value;try{const v=target.vector();return v.set(v.index(index()),value);}finally{f.assignment=null;}}
    setAfterName(value: Value,name: string,index: ()=>Value): Value {this.lookup(name).vector();const f=this.frame();f.assignment=value;try{const i=index(),v=this.lookup(name).vector();return v.set(v.index(i),value);}finally{f.assignment=null;}}
    checkType(v: Value|null,kind: number,array: boolean): Value {if(array){if(!v||!v.isVector())throw problem("Serve un vettore.");for(const item of v.vector().items)this.checkType(item,kind,false);return v;}let valid=v!==null&&!v.isVector();if(kind===1)valid=v!==null&&v.isNumber()&&v.asNumber()===Math.trunc(v.asNumber());else if(kind===2)valid=v!==null&&v.isNumber();else if(kind===3)valid=v!==null&&v.isText();else if(kind===4)valid=v!==null&&v.isBoolean();else if(kind===5)valid=false;if(!valid||v===null)throw problem("Il valore non corrisponde al tipo dichiarato.");return v;}
    private convertScalar(v: Value,kind: number): Value {return kind===1&&v.isNumber()?Value.number(Math.trunc(v.asNumber())):this.checkType(v,kind,false);}
    convert(value: Value,declaration: TypeSpec|null,parameter: TypeSpec|null): Value {if(declaration){if(declaration.array){if(!value.isVector()||value.vectorSize()!==declaration.length)throw problem("La dimensione del vettore dichiarato non può cambiare.");value=Value.vector(value.vector().items.map(v=>this.convertScalar(v,declaration.kind)));}else value=this.convertScalar(value,declaration.kind);}if(parameter)value=this.checkType(value,parameter.kind,parameter.array);return value;}
    initial(kind: number,array: boolean,length: number): Value {const v=kind===3?Value.text(""):kind===4?Value.logical(false):Value.number(0);return array?Value.vector(Array.from({length},()=>v)):v;}
    private wantsText(d: TypeSpec|null,p: TypeSpec|null,indexed: boolean): boolean {const s=d??p;return s!==null&&s.kind===3&&(indexed||!s.array);}
    read(name: string,prompt: string,declaration: TypeSpec|null,parameter: TypeSpec|null,vector: Value|null,indexExpression: (()=>Value)|null): Value {this.guard();let display=name,index: number|null=null;if(indexExpression){let v=this.requireValue(vector).vector();const i=indexExpression();if(this.references)v=this.lookup(name).vector();index=v.index(i);display+="["+index+"]";}this.guard();if(!prompt.trim()||prompt==="Inserisci un valore per x")prompt="Inserisci un valore per "+display;while(true){const raw=this.reader(prompt+": ");if(raw===null)throw problem("Input terminato prima di ricevere un valore valido.");try{let v=this.wantsText(declaration,parameter,index!==null)?Value.text(raw):parseInput(raw);if(index!==null)v=this.requireValue(this.references?this.lookup(name):vector).vector().set(index,v);v=this.convert(v,declaration,parameter);this.acceptInput(name,v,index!==null);return v;}catch(error){if(!(error instanceof Problem))throw error;process.stderr.write(error.message+"\n");}}}
    emit(value: Value|null,newline=true): void {const text=value?.format()??"";if(this.outputSize+text.length>262144)throw problem("Il risultato supera 256 KiB.");this.output.push(text);this.outputNewlines.push(newline);this.outputSize+=text.length;if(this.writer)this.writer(text);else process.stdout.write(text+(newline?"\n":""));}
    intrinsic(name: string,...args: Value[]): Value {
        if(name==="eof")return Value.logical(this.files.eof());const first=args[0];
        if(name==="array"||name==="vettore"){const length=integer(first,0,1024,"La dimensione"),initial=args[1]??Value.number(0);VectorValue.scalar(initial);return Value.vector(Array.from({length},()=>initial));}
        if(name==="len"){if(first.isVector())return Value.number(first.vectorSize());if(first.isText())return Value.number(characterCount(first.format()));throw problem("len richiede un testo o un vettore.");}
        if(name==="sum"||name==="somma"||((name==="min"||name==="max")&&args.length===1)){const items=first.vector().items;if(items.length===0&&name!=="sum"&&name!=="somma")throw problem("Il vettore è vuoto.");let result=0;for(let i=0;i<items.length;i++){const n=items[i].asNumber();result=name==="min"?(i===0?n:Math.min(result,n)):name==="max"?(i===0?n:Math.max(result,n)):finite(result+n);}return Value.number(result);}
        if(name==="size")return Value.number(first.vectorSize());
        if(name==="char"){const chars=characters(textValue(first)),i=integer(args[1],0,2147483647,"L’indice");if(i>=chars.length)throw problem("Indice fuori dal testo.");return Value.text(chars[i]);}
        if(name==="tocode"){const chars=characters(textValue(first));if(chars.length!==1)throw problem("tocode richiede esattamente un carattere.");return Value.number(chars[0].codePointAt(0)!);}
        if(name==="int"&&args.length===2){if(textValue(args[1]).toLowerCase()!=="passo")throw problem('Il secondo argomento di int deve essere "passo".');const step=integer(first,-2147483648,2147483647,"Il passo");if(step===0)throw problem("Il passo deve essere diverso da zero.");return Value.number(step);}
        if(name==="tointeger"||name==="toreal"){const source=trimInput(textValue(first)),whole=name==="tointeger";if(!(whole?/^[+-]?[0-9]+$/:/^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$/).test(source))throw problem("Scrivi un testo che contenga un numero valido.");const v=finite(Number(source.replace(",",".")));if(whole&&(v< -2147483648||v>2147483647))throw problem("tointeger accetta valori da -2147483648 a 2147483647.");return Value.number(v);}
        const n=first.asNumber();switch(name){case "abs":return Value.number(Math.abs(n));case "sqrt":if(n<0)throw problem("La radice richiede un numero non negativo.");return Value.number(Math.sqrt(n));case "floor":return Value.number(Math.floor(n));case "round":return Value.number(roundAway(n,args.length===1?0:integer(args[1],0,15,"Le cifre decimali")));case "min":case "max":return Value.number(args.slice(1).reduce((v,item)=>name==="min"?Math.min(v,item.asNumber()):Math.max(v,item.asNumber()),n));case "sin":return Value.number(Math.sin(n));case "cos":return Value.number(Math.cos(n));case "tan":return Value.number(Math.tan(n));case "arcsin":case "arccos":if(n< -1||n>1)throw problem("Il dominio richiede un numero tra -1 e 1.");return Value.number(name==="arcsin"?Math.asin(n):Math.acos(n));case "arctan":return Value.number(Math.atan(n));case "int":return Value.number(Math.trunc(n));case "sgn":return Value.number(n<0?-1:n>0?1:0);case "ln":case "log":case "log10":if(n<=0)throw problem("Il logaritmo richiede un numero maggiore di 0.");return Value.number(name==="log10"?Math.log10(n):Math.log(n));case "random":return Value.number(Math.floor(Math.random()*integer(first,1,2147483647,"Il limite di random")));case "tochar":{const code=integer(first,0,0x10ffff,"Il codice Unicode");if(code>=0xd800&&code<=0xdfff)throw problem("Il codice non rappresenta un carattere Unicode completo.");return Value.text(String.fromCodePoint(code));}case "tostring":return Value.text(first.format());case "tofixed":return Value.text(fixed(n,integer(args[1],0,15,"Le cifre decimali")));default:throw problem("Funzione sconosciuta.");}
    }
    requireClosed(reading: boolean): void {if(reading?this.files.hasRead():this.files.hasWrite())throw problem("Chiudi il file precedente prima di aprirne un altro.");}
    requireWrite(): void {if(!this.files.hasWrite())throw problem("Apri prima un file in scrittura.");}
    openFile(reading: boolean,value: Value): void {this.requireClosed(reading);const name=TextFiles.fileName(value);if(reading)this.files.openRead(name,this.fileReader(name));else this.files.openWrite(name);}
    readFile(name: string,declaration: TypeSpec|null,parameter: TypeSpec|null,vector: Value|null,indexExpression: (()=>Value)|null): Value {let index: number|null=null;if(indexExpression){let v=this.requireValue(vector).vector();const i=indexExpression();if(this.references)v=this.lookup(name).vector();index=v.index(i);}const raw=this.files.peekLine();let v=this.wantsText(declaration,parameter,index!==null)?Value.text(raw):parseInput(raw);if(index!==null)v=this.requireValue(this.references?this.lookup(name):vector).vector().set(index,v);v=this.convert(v,declaration,parameter);this.acceptInput(name,v,index!==null);this.files.commitRead();return v;}
    writeFile(value: Value,newline: boolean): void {this.files.write(value.format(),newline);}
    closeFile(reading: boolean): void {if(reading){this.files.closeRead();return;}this.requireWrite();if(!this.fileSaver(this.files.writeName!,this.files.output!))throw problem("Salvataggio annullato: la bozza resta disponibile.");this.files.saved();}
    finishFiles(): void {if(this.files.hasWrite())this.closeFile(false);if(this.files.hasRead())this.files.closeRead();}
    private readPath(name: string): string {try{if(nodeFs.statSync(name).size>4194304)throw problem("Il file supera 4 MiB.");const fd=nodeFs.openSync(name,"r");try{const chunks: Uint8Array[]=[];let size=0;while(true){const buffer=new Uint8Array(65536),count=nodeFs.readSync(fd,buffer,0,buffer.length,null);if(count===0)break;size+=count;if(size>4194304)throw problem("Il file supera 4 MiB.");chunks.push(buffer.slice(0,count));}const bytes=new Uint8Array(size);let at=0;for(const chunk of chunks){bytes.set(chunk,at);at+=chunk.length;}return TextFiles.decode(bytes);}finally{nodeFs.closeSync(fd);}}catch(error){if(error instanceof Problem)throw error;throw problem("Non riesco a leggere il file «"+name+"».");}}
    private journal(name: string): FCJournal {const parent=nodePath.dirname(nodePath.resolve(name));if(!nodeFs.statSync(parent).isDirectory())throw problem("Cartella non disponibile.");const path=nodePath.join(parent,".frociart-recovery-"+nodeCrypto.randomUUID()+".txt");const fd=nodeFs.openSync(path,"wx");this.recoveryPath=path;let closed=false;return {id:path,append(text){const bytes=utf8.encode(text);let at=0;while(at<bytes.length)at+=nodeFs.writeSync(fd,bytes,at,bytes.length-at);nodeFs.fsyncSync(fd);},close(){if(!closed){nodeFs.closeSync(fd);closed=true;}},saved(){this.close();try{if(nodeFs.existsSync(path))nodeFs.unlinkSync(path);return true;}catch{return false;}}};}
    private savePath(name: string,_text: string): boolean {try{this.files.closeJournal();const target=nodePath.resolve(name);if(this.recoveryPath===null)throw problem("La bozza non è disponibile.");if(nodeFs.existsSync(target)){const answer=this.reader("Il file «"+name+"» esiste. Sostituirlo? [s/N] ");if(answer===null||answer.trim().toLowerCase()!=="s")return false;nodeFs.renameSync(this.recoveryPath,target);}else{nodeFs.copyFileSync(this.recoveryPath,target,nodeFs.constants.COPYFILE_EXCL);const fd=nodeFs.openSync(target,"r+");try{nodeFs.fsyncSync(fd);}finally{nodeFs.closeSync(fd);}nodeFs.unlinkSync(this.recoveryPath);}this.recoveryPath=null;return true;}catch(error){if(error instanceof Problem)throw error;throw problem("File non salvato. La bozza è conservata in "+this.recoveryPath+".");}}
    turtle(kind: number,value: Value|null,flag: boolean): void {this.turtleState.apply(kind,value,flag);}
    private showDrawing(): void {if(!this.turtleState.used)return;const name=nodePath.resolve("Frociart_disegno_"+nodeCrypto.randomUUID()+".svg"),fd=nodeFs.openSync(name,"wx");try{const data=utf8.encode(this.turtleState.svg());let at=0;while(at<data.length)at+=nodeFs.writeSync(fd,data,at,data.length-at);nodeFs.fsyncSync(fd);}finally{nodeFs.closeSync(fd);}process.stderr.write("Disegno SVG salvato: "+name+"\n");}
    run(main: ()=>Value|null,drawing: boolean): boolean {try{main();if(drawing)this.showDrawing();return true;}catch(error){process.stderr.write((error instanceof Error?error.message:"Esecuzione interrotta.")+"\n");return false;}finally{try{this.files.closeJournal();}catch{process.stderr.write("Non riesco a chiudere la bozza.\n");}if(this.recoveryPath!==null)process.stderr.write("Bozza recuperabile: "+this.recoveryPath+"\n");}}
}
