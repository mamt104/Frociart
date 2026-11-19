using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Frociart {
    // Immutable presentation choice per canvas/export; preferences never enter the project file.
    internal sealed class FlowAppearance {
        internal readonly int Mode;
        internal readonly FlowDesign CustomDesign;
        internal FlowDesign Design {get{return Mode==3?CustomDesign:FlowDesign.Default;}}
        private readonly Dictionary<long,Color> colors=new Dictionary<long,Color>();
        internal static readonly string[] Names={"Standard","Contorni marcati","Bianco e nero","Personalizzato"};
        internal static readonly string[] Descriptions={"Stile originale, con i colori dei blocchi e dei rami.","Linee più evidenti e colori più leggibili. Le caselle restano neutre.","Diagramma monocromatico con linee evidenti. I rami si distinguono dal nome.","Il tuo carattere, la dimensione del testo e i colori dei titoli. Le caselle restano neutre."};
        internal static int Current {get;private set;}
        internal static FlowDesign CurrentDesign {get;private set;}
        internal static FlowDesign ActiveDesign {get{return Current==3?CurrentDesign:FlowDesign.Default;}}
        static FlowAppearance(){CurrentDesign=FlowDesign.Default;try{if(File.Exists(PreferencePath)){int mode;FlowDesign design;DecodeSettings(ReadBounded(PreferencePath),out mode,out design);Current=mode;CurrentDesign=design;}else{int value;string legacy=Path.Combine(Path.GetDirectoryName(PreferencePath),"diagram-style.txt");if(File.Exists(legacy)&&Int32.TryParse(ReadBounded(legacy).Trim(),out value))Current=Valid(value);}}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}}
        internal FlowAppearance(int mode):this(mode,CurrentDesign){}
        internal FlowAppearance(int mode,FlowDesign design){Mode=Valid(mode);CustomDesign=design??FlowDesign.Default;}
        internal static int Valid(int mode){return mode>=0&&mode<=3?mode:0;}
        internal static string PreferencePath {get{return Path.Combine(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"),"diagram-design.txt");}}
        private static string ReadBounded(string path){using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){var bytes=new byte[1025];int used=0,n;while(used<bytes.Length&&(n=f.Read(bytes,used,bytes.Length-used))>0)used+=n;if(used>1024)throw new ArgumentException("Preferenza troppo lunga.");return new System.Text.UTF8Encoding(false,true).GetString(bytes,0,used);}}
        internal static string EncodeSettings(int mode,FlowDesign design){return "FROCIART_STYLE_1\n"+Valid(mode).ToString(CultureInfo.InvariantCulture)+"\n"+design.Encode();}
        internal static void DecodeSettings(string text,out int mode,out FlowDesign design){mode=0;design=FlowDesign.Default;if(text==null||text.Length>1024)throw new ArgumentException("Preferenza non valida.");string normalized=text.Replace("\r\n","\n");int first=normalized.IndexOf('\n'),second=first<0?-1:normalized.IndexOf('\n',first+1);if(first<0||second<0||normalized.Substring(0,first)!="FROCIART_STYLE_1"||!Int32.TryParse(normalized.Substring(first+1,second-first-1),NumberStyles.None,CultureInfo.InvariantCulture,out mode)||mode<0||mode>3)throw new ArgumentException("Preferenza non valida.");design=FlowDesign.Decode(normalized.Substring(second+1));}
        internal static bool SetPreference(int mode,bool persist=true) {
            return SetSettings(mode,CurrentDesign,persist);
        }
        internal static bool SetSettings(int mode,FlowDesign design,bool persist=true) {
            Current=Valid(mode);CurrentDesign=design??FlowDesign.Default;if(!persist)return true;string temp=null;
            try {Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath));temp=PreferencePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,EncodeSettings(Current,CurrentDesign));if(File.Exists(PreferencePath))File.Replace(temp,PreferencePath,null);else File.Move(temp,PreferencePath);return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
            finally{if(temp!=null&&File.Exists(temp))try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        internal Color Kind(NodeKind kind,Color original,bool dark){return Ink(Mode==3?Design.ColorFor(kind,original):original,dark);}
        internal float Width(float value){return Mode==0?value:value*1.5f;}
        internal Color Neutral(Color color){if(Mode!=2)return color;int gray=(77*color.R+150*color.G+29*color.B+128)>>8;return Color.FromArgb(color.A,gray,gray,gray);}
        internal Color Border(bool dark,Color original){return Mode==0?original:Neutral(dark?Color.FromArgb(167,175,188):Color.FromArgb(98,107,121));}
        internal Color Ink(Color color,bool dark) {
            if(Mode==0)return color;
            long key=((long)(uint)color.ToArgb()<<1)|(dark?1L:0L);Color cached;if(colors.TryGetValue(key,out cached))return cached;
            Color value=Neutral(color),background=dark?Color.FromArgb(32,34,40):Color.White;
            if(Contrast(value,background)<4.5) {
                Color target=dark?Color.White:Color.Black;double low=0,high=1;
                for(int i=0;i<10;i++){double middle=(low+high)/2;Color candidate=Blend(value,target,middle);if(Contrast(candidate,background)>=4.5)high=middle;else low=middle;}
                value=Blend(value,target,high);
            }
            if(colors.Count>=4096)colors.Clear();colors[key]=value;return value;
        }
        private static Color Blend(Color a,Color b,double t){return Color.FromArgb(a.A,(int)Math.Floor(a.R+(b.R-a.R)*t+.5),(int)Math.Floor(a.G+(b.G-a.G)*t+.5),(int)Math.Floor(a.B+(b.B-a.B)*t+.5));}
        private static double Channel(int c){double x=c/255d;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        private static double Light(Color c){return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
        internal static double Contrast(Color a,Color b){double x=Light(a),y=Light(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        internal void StyleSvg(string path) {
            if(Mode==0)return;
            var xml=new XmlDocument{XmlResolver=null,PreserveWhitespace=true};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))xml.Load(reader);
            foreach(XmlElement element in xml.SelectNodes("//*")) {
                foreach(string attr in new[]{"fill","stroke","stop-color"}) {
                    string text=element.GetAttribute(attr);int rgb;
                    if(text.Length!=7||text[0]!='#'||!Int32.TryParse(text.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb))continue;
                    Color c=Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255);
                    if(attr=="fill"&&rgb==0xffffff)continue;
                    c=Ink(c,false);element.SetAttribute(attr,String.Format(CultureInfo.InvariantCulture,"#{0:X2}{1:X2}{2:X2}",c.R,c.G,c.B));
                }
                double width;if(element.HasAttribute("stroke")&&element.GetAttribute("stroke")!="none"&&Double.TryParse(element.GetAttribute("stroke-width"),NumberStyles.Float,CultureInfo.InvariantCulture,out width))element.SetAttribute("stroke-width",(width*1.5).ToString("0.###",CultureInfo.InvariantCulture));
            }
            xml.Save(path);
        }
    }
}
