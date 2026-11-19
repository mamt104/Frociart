using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace Frociart {
    // Passive, bounded device preference. No downloaded fonts or project metadata.
    internal sealed class FlowDesign {
        internal static readonly string[] Families={"Moderno","Classico","Monospazio"};
        internal static readonly string[] Roles={"Operazioni e altri blocchi","Lettura","Stampa","Decisione","Inizio","Fine"};
        private static readonly string[] defaults={"#5B8DEF","#22A5B8","#9771DC","#D68A18","#39B985","#7A719C"};
        internal static readonly FlowDesign Default=new FlowDesign(0,100,defaults);
        internal readonly int Family,Percent;
        private readonly ReadOnlyCollection<string> colors;
        internal float Scale {get{return Percent/100f;}}
        internal int FontKey {get{return Family*1000+Percent;}}
        internal string FontName {get{return Family==1?"Georgia":Family==2?"Consolas":"Segoe UI";}}
        internal string SvgFamily {get{return Family==1?"Georgia, serif":Family==2?"Consolas, monospace":"Segoe UI, Arial, sans-serif";}}
        internal FlowDesign(int family,int percent,string[] colors) {
            if(family<0||family>=Families.Length||percent<80||percent>200||percent%10!=0)throw new ArgumentException("Scegli un carattere e una dimensione tra 80% e 200%.");
            if(colors==null||colors.Length!=6)throw new ArgumentException("Servono sei colori per i titoli dei blocchi.");
            Family=family;Percent=percent;this.colors=Array.AsReadOnly(colors.Select(Normalize).ToArray());
        }
        internal string Hex(int role){return colors[role];}
        internal string[] Palette(){return colors.ToArray();}
        internal Color ColorFor(NodeKind kind,Color fallback) {
            int role=kind==NodeKind.Input?1:kind==NodeKind.Output?2:kind==NodeKind.Decision?3:kind==NodeKind.Start?4:kind==NodeKind.End?5:0;
            if(kind==NodeKind.Note||kind==NodeKind.Pause)return fallback;
            int value=Int32.Parse(colors[role].Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            return Color.FromArgb((value>>16)&255,(value>>8)&255,value&255);
        }
        internal static string Normalize(string color) {
            if(color==null||color.Length!=7||color[0]!='#'||color.Skip(1).Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F')))throw new ArgumentException("Usa un colore nel formato #RRGGBB, per esempio #5B8DEF.");
            return color.ToUpperInvariant();
        }
        internal string Encode(){return "FROCIART_DESIGN_1\n"+Family.ToString(CultureInfo.InvariantCulture)+"\n"+Percent.ToString(CultureInfo.InvariantCulture)+"\n"+String.Join("\n",colors);}
        internal static FlowDesign Decode(string text) {
            if(text==null||text.Length>256)throw new ArgumentException("Preferenza di aspetto non valida.");
            string[] parts=text.Replace("\r\n","\n").Split('\n');int family,percent;
            if(parts.Length!=9||parts[0]!="FROCIART_DESIGN_1"||!Int32.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out family)||!Int32.TryParse(parts[2],NumberStyles.None,CultureInfo.InvariantCulture,out percent))throw new ArgumentException("Preferenza di aspetto non valida.");
            return new FlowDesign(family,percent,parts.Skip(3).ToArray());
        }
        internal bool Same(FlowDesign other){return other!=null&&Encode()==other.Encode();}
    }
}
