using System;using System.IO;using System.Text;using System.Linq;using System.Globalization;using System.Collections.Generic;using System.Collections.ObjectModel;
namespace Frociart {
 // The .fpgt format is data. Parsing and substitution have no host callbacks or code evaluation.
 internal sealed class FlowFpgtSyntax {
  internal const int MaxCharacters=262144,MaxBytes=1048576,MaxLines=16384,MaxSections=256,MaxRows=8192,MaxExpandedCharacters=2097152;
  internal sealed class SyntaxError:ArgumentException {
   internal readonly int Line;internal readonly string Section,Key;
   internal SyntaxError(int line,string section,string key,string detail):base("Modello .fpgt · riga "+line+(section.Length==0?"":" · ["+section+"]")+(key.Length==0?"":" · "+key)+": "+detail){Line=line;Section=section;Key=key;}
  }
  internal sealed class Row {
   internal readonly int Line,Indent;internal readonly string Section,Key,Text;internal readonly ReadOnlyCollection<string> Conditions;
   internal Row(int line,string section,string key,string text,int indent,IEnumerable<string> conditions){Line=line;Section=section;Key=key;Text=text;Indent=indent;Conditions=new List<string>(conditions).AsReadOnly();}
   internal bool Matches(ISet<string> flags,ISet<string> known=null){bool matches=true;foreach(string condition in Conditions){string flag=condition[0]=='~'?condition.Substring(1):condition;if(known!=null&&!known.Contains(flag))throw Error("Flag non riconosciuto: «"+flag+"».");bool present=flags!=null&&flags.Contains(flag);if(present==(condition[0]=='~'))matches=false;}return matches;}
   internal SyntaxError Error(string message){return new SyntaxError(Line,Section,Key,message);}
   internal string Expand(IDictionary<string,string> fields){
    var result=new StringBuilder();for(int i=0;i<Text.Length;){char c=Text[i++];if(c!='{'){Append(result,c.ToString());continue;}
     if(i<Text.Length&&Text[i]=='{'){i++;Append(result,"{");continue;}int start=i-1,end=Text.IndexOf('}',i);if(end<0)throw Error("Chiudi il campo con } oppure usa {{ per una parentesi letterale.");
     string name=Normalize(Text.Substring(i,end-i));i=end+1;string value;
     if(name=="space")value=" ";else if(name=="pipe")value="|";
     // Shell parameter expansions and Pascal comments are literal target syntax, not .fpgt field names.
     else if(!NameToken(name))value=Text.Substring(start,end-start+1);
     else if(fields==null||!fields.TryGetValue(name,out value))throw Error("Campo non disponibile: {"+name+"}.");
     Append(result,value??"");
    }return result.ToString();
   }
   void Append(StringBuilder target,string value){if(value.Length>MaxExpandedCharacters-target.Length)throw Error("Il testo generato supera 2 MiB di caratteri.");target.Append(value);}
  }
  internal sealed class Section {
   internal readonly string Name;internal readonly int Line;readonly Dictionary<string,List<Row>> keys=new Dictionary<string,List<Row>>(StringComparer.Ordinal);
   internal Section(string name,int line){Name=name;Line=line;}
   internal IEnumerable<string> Keys {get{return keys.Keys;}}
   internal ReadOnlyCollection<Row> Rows(string key){List<Row> rows;return(keys.TryGetValue(Normalize(key),out rows)?rows:new List<Row>()).AsReadOnly();}
   internal bool Has(string key){return keys.ContainsKey(Normalize(key));}
   internal string Value(string key,ISet<string> flags=null,IDictionary<string,string> fields=null,string fallback=null,ISet<string> known=null){foreach(var row in Rows(key))if(row.Matches(flags,known))return fields==null?row.Text:row.Expand(fields);return fallback;}
   internal int Number(string key,ISet<string> flags,int fallback,ISet<string> known=null){foreach(var row in Rows(key))if(row.Matches(flags,known)){int value;if(!Int32.TryParse(row.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value))throw row.Error("Serve un valore intero valido.");return value;}return fallback;}
   internal void Add(string key,Row row,bool continuation){List<Row> rows;if(!keys.TryGetValue(key,out rows)){if(continuation)throw row.Error("La continuazione non ha una chiave precedente.");keys.Add(key,rows=new List<Row>());}else if(!continuation)throw row.Error("Chiave duplicata. Per un altro valore usa una riga che inizia con =.");rows.Add(row);}
  }
  internal readonly string Text,Name,Extension,Direction;internal readonly bool CaseSensitive;readonly Dictionary<string,Section> sections;
  internal IEnumerable<string> SectionNames {get{return sections.Keys;}}
  FlowFpgtSyntax(string text,Dictionary<string,Section> sections){Text=text;this.sections=sections;var language=Require("language");Name=language.Value("name");Extension=language.Value("extension");Direction=language.Value("direction",fallback:"left-to-right");if(String.IsNullOrWhiteSpace(Name)||Name.Length>80||Name.Any(Char.IsControl))throw new SyntaxError(language.Line,"language","name","Indica un nome di massimo 80 caratteri.");if(Extension!=null&&Extension.StartsWith(".",StringComparison.Ordinal))Extension=Extension.Substring(1);if(Extension==null||!System.Text.RegularExpressions.Regex.IsMatch(Extension,"\\A[A-Za-z][A-Za-z0-9]{0,15}\\z"))throw new SyntaxError(language.Line,"language","extension","Usa un’estensione di sole lettere e numeri, senza percorso.");if(Direction!="left-to-right"&&Direction!="right-to-left")throw new SyntaxError(language.Line,"language","direction","Usa left-to-right oppure right-to-left.");string sensitive=language.Value("case sensitive",fallback:"true");if(!Boolean.TryParse(sensitive,out CaseSensitive))throw new SyntaxError(language.Line,"language","case sensitive","Usa true oppure false.");}
  internal static string Normalize(string text){return text.Trim().ToLowerInvariant();}
  static bool NameToken(string name){return name.Length>0&&name.Length<=80&&name.All(c=>c>='a'&&c<='z'||c>='0'&&c<='9'||c==' '||c=='-'||c=='_');}
  internal Section Find(string name){Section section;return sections.TryGetValue(Normalize(name),out section)?section:null;}
  internal Section Require(string name){var section=Find(name);if(section==null)throw new SyntaxError(1,name,"","Manca questa sezione del modello.");return section;}
  internal static string Decode(byte[] bytes){if(bytes==null||bytes.Length>MaxBytes)throw new ArgumentException("Il modello supera 1 MiB.");try{if(bytes.Length>=2&&bytes[0]==0xff&&bytes[1]==0xfe)return new UnicodeEncoding(false,true,true).GetString(bytes,2,bytes.Length-2);if(bytes.Length>=2&&bytes[0]==0xfe&&bytes[1]==0xff)return new UnicodeEncoding(true,true,true).GetString(bytes,2,bytes.Length-2);int skip=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf?3:0;return new UTF8Encoding(false,true).GetString(bytes,skip,bytes.Length-skip);}catch(DecoderFallbackException){throw new ArgumentException("Usa UTF-8 oppure UTF-16 con il contrassegno BOM. Il file non è stato modificato.");}}
  internal static FlowFpgtSyntax Parse(string raw){
   if(raw==null||raw.Length>MaxCharacters)throw new ArgumentException("Il modello supera 262144 caratteri.");int lineNo=1;for(int i=0;i<raw.Length;i++){char c=raw[i];if(c=='\n')lineNo++;if(Char.IsHighSurrogate(c)){if(++i>=raw.Length||!Char.IsLowSurrogate(raw[i]))throw new SyntaxError(lineNo,"","","Carattere Unicode incompleto.");}else if(Char.IsLowSurrogate(c)||Char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t')throw new SyntaxError(lineNo,"","","Carattere non valido nel modello.");}
   string text=raw.TrimStart('\ufeff').Replace("\r\n","\n").Replace('\r','\n');string[] lines=text.Split('\n');if(lines.Length>MaxLines)throw new ArgumentException("Il modello supera 16384 righe.");var sections=new Dictionary<string,Section>(StringComparer.Ordinal);Section current=null;string previous=null;int count=0;
   for(int i=0;i<lines.Length;i++){string s=lines[i].Trim();if(s.Length==0||s[0]==';')continue;if(s[0]=='['){if(s[s.Length-1]!=']')throw new SyntaxError(i+1,"","","Chiudi il nome della sezione con ].");string name=Normalize(s.Substring(1,s.Length-2));if(!NameToken(name))throw new SyntaxError(i+1,name,"","Nome della sezione non valido.");if(sections.Count>=MaxSections)throw new SyntaxError(i+1,name,"","Troppe sezioni (massimo 256).");if(sections.ContainsKey(name))throw new SyntaxError(i+1,name,"","Sezione duplicata.");current=new Section(name,i+1);sections.Add(name,current);previous=null;continue;}
    if(current==null)throw new SyntaxError(i+1,"","","Inserisci una sezione prima delle chiavi.");int equal=s.IndexOf('=');if(equal<0)throw new SyntaxError(i+1,current.Name,previous??"","Manca =. Per continuare un valore inizia la riga con =.");bool continuation=equal==0;string key=continuation?previous:Normalize(s.Substring(0,equal));if(key==null||!NameToken(key))throw new SyntaxError(i+1,current.Name,key??"","Chiave non valida o continuazione senza chiave.");previous=key;string[] parts=s.Substring(equal+1).Split('|');if(parts.Length>3)throw new SyntaxError(i+1,current.Name,key,"Per un pipe letterale usa {pipe}; i separatori ammessi sono due.");string value=parts[0].Trim();var conditions=new List<string>();if(parts.Length>1&&parts[1].Trim().Length>0)foreach(string part in parts[1].Split(',')){string flag=Normalize(part);if(flag.Length==0)continue;string name=flag.StartsWith("~",StringComparison.Ordinal)?flag.Substring(1):flag;if(!NameToken(name)||name.Contains(" ")||conditions.Count>=64)throw new SyntaxError(i+1,current.Name,key,"Flag non valido o troppi flag sulla riga.");conditions.Add(flag);}int indent=0;if(parts.Length>2&&parts[2].Trim().Length>0&&(!Int32.TryParse(parts[2].Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out indent)||indent< -32||indent>32))throw new SyntaxError(i+1,current.Name,key,"Il rientro deve essere un intero da -32 a 32.");if(++count>MaxRows)throw new SyntaxError(i+1,current.Name,key,"Il modello supera 8192 valori.");var row=new Row(i+1,current.Name,key,value,indent,conditions);ValidateFields(row);current.Add(key,row,continuation);
   }
   return new FlowFpgtSyntax(text,sections);
  }
  static void ValidateFields(Row row){if(row.Key!="text"&&row.Key!="normal"&&row.Key!="conflict"&&row.Key!="separator")return;for(int i=0;i<row.Text.Length;i++){if(row.Text[i]!='{')continue;if(i+1<row.Text.Length&&row.Text[i+1]=='{'){i++;continue;}int end=row.Text.IndexOf('}',i+1);if(end<0)throw row.Error("Chiudi il campo con } oppure usa {{ per una parentesi letterale.");string name=Normalize(row.Text.Substring(i+1,end-i-1));i=end;}}
 }
}
