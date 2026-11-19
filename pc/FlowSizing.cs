using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace Frociart {
 // Derived geometry only. Files/history retain the original text and coordinates.
 internal static class FlowSizing {
  private sealed class Entry {internal readonly string Text;internal readonly NodeKind Kind;internal readonly SizeF Size;internal readonly int FontKey;internal Entry(string text,NodeKind kind,int key,SizeF size){Text=text;Kind=kind;FontKey=key;Size=size;}}
  private sealed class Cache {internal volatile Entry Last;public Cache(){}}
  private static readonly ConditionalWeakTable<FlowNode,Cache> cache=new ConditionalWeakTable<FlowNode,Cache>();
  private static readonly Bitmap bitmap=new Bitmap(1,1);
  private static readonly Graphics measure=Graphics.FromImage(bitmap);
  private sealed class Fonts {internal readonly Font Body,Decision,Terminal;internal Fonts(FlowDesign d){Body=new Font(d.FontName,15.333f*d.Scale,FontStyle.Regular,GraphicsUnit.Pixel);Decision=new Font(d.FontName,14*d.Scale,FontStyle.Regular,GraphicsUnit.Pixel);Terminal=new Font(d.FontName,15.333f*d.Scale,FontStyle.Bold,GraphicsUnit.Pixel);}}
  // At most 39 validated family/size combinations; one shared measurement lock.
  private static readonly Dictionary<int,Fonts> fontCache=new Dictionary<int,Fonts>();
  internal static SizeF Default(NodeKind k){return k==NodeKind.Start||k==NodeKind.End?new SizeF(164,58):k==NodeKind.Decision?new SizeF(232,136):k==NodeKind.Input||k==NodeKind.Output?new SizeF(226,92):new SizeF(212,88);}
  internal static string Text(FlowNode n){string body=IoInstruction.Display(n),note=n.PreviewComment??n.Comment;return String.IsNullOrEmpty(note)?body:body+"\n\nNota: "+note;}
  internal static SizeF Size(FlowNode n){return Size(n,FlowAppearance.ActiveDesign);}
  internal static SizeF Size(FlowNode n,FlowDesign design){
   string text=Text(n);var box=cache.GetOrCreateValue(n);var e=box.Last;if(e!=null&&e.Text==text&&e.Kind==n.Kind&&e.FontKey==design.FontKey)return e.Size;
   lock(measure){Fonts fonts;if(!fontCache.TryGetValue(design.FontKey,out fonts)){fonts=new Fonts(design);fontCache.Add(design.FontKey,fonts);}float scale=design.Scale;var size=Default(n.Kind);size=new SizeF(size.Width*scale,size.Height*scale);bool diamond=n.Kind==NodeKind.Decision,term=n.Kind==NodeKind.Start||n.Kind==NodeKind.End;
    Font font=diamond?fonts.Decision:term?fonts.Terminal:fonts.Body;float pad=(term?64:n.Kind==NodeKind.Input||n.Kind==NodeKind.Output?60:36)*scale;
    if(!String.IsNullOrWhiteSpace(text))using(var f=new StringFormat()){
     f.FormatFlags=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces;float longest=0;
     foreach(string line in text.Replace("\r\n","\n").Replace('\r','\n').Replace("\t","    ").Split('\n'))longest=Math.Max(longest,FlowText.MeasureLine(measure,line,font,f,100000).Width);
     size.Width=Math.Max(size.Width,Math.Min((diamond?620:520)*scale,diamond?(longest+6*scale)/.56f:longest+pad+6*scale));
     f.FormatFlags=StringFormatFlags.LineLimit;float available=diamond?size.Width*.56f:size.Width-pad;
     var h=FlowText.Measure(measure,text.Replace("\t","    "),font,new SizeF(available,100000),f).Height;
     size.Height=Math.Max(size.Height,diamond?(h+8*scale)/.34f:h+(term?20:47)*scale);
     if(diamond){size.Width=Math.Max(size.Width,Math.Min(620*scale,size.Height*1.55f));size.Height=Math.Max(136*scale,size.Width/1.71f);h=FlowText.Measure(measure,text.Replace("\t","    "),font,new SizeF(size.Width*.56f,100000),f).Height;size.Height=Math.Max(size.Height,(h+8*scale)/.34f);}
    }
    size.Width=(float)Math.Ceiling(size.Width);size.Height=(float)Math.Ceiling(size.Height);box.Last=new Entry(text,n.Kind,design.FontKey,size);return size;
   }
  }
  internal static RectangleF DecisionBody(RectangleF b){return new RectangleF(b.X+b.Width*.22f,b.Y+b.Height*.34f,b.Width*.56f,b.Height*.34f);}
  internal static RectangleF DecisionTitle(RectangleF b){return DecisionTitle(b,FlowAppearance.ActiveDesign);}
  internal static RectangleF DecisionTitle(RectangleF b,FlowDesign d){return new RectangleF(b.X+b.Width*.25f,b.Y+b.Height*.22f,b.Width*.5f,16*d.Scale);}
 }
 public partial class FlowCanvas {
  private sealed class SizeMotion {internal SizeF From,To;internal long At;}
  private readonly Dictionary<string,SizeMotion> sizeMotion=new Dictionary<string,SizeMotion>();
  internal void ChangeDesignAnimated(FlowAppearance style,Action move){FlowTransitions.CanvasChange(this);var before=ModelPositions();var sizes=_diagram.Nodes.ToDictionary(n=>n.Id,n=>DisplaySize(n));SetAppearance(style);move();long now=manipulationClock.ElapsedMilliseconds;foreach(var n in _diagram.Nodes){SizeF old;if(sizes.TryGetValue(n.Id,out old))sizeMotion[n.Id]=new SizeMotion{From=old,To=FlowSizing.Size(n,style.Design),At=now};}AnimatePlacement(before);RequestFrameRepaint();}
  private SizeF DisplaySize(FlowNode n){var target=FlowSizing.Size(n,appearance.Design);if(_exportRendering||n.Id==null)return target;SizeMotion m;
   if(!sizeMotion.TryGetValue(n.Id,out m)){sizeMotion[n.Id]=new SizeMotion{From=target,To=target,At=manipulationClock.ElapsedMilliseconds-400};return target;}
   long now=manipulationClock.ElapsedMilliseconds;float t=UiMotion.Enabled?UiMotion.Spring(Math.Min(1,(now-m.At)/320f)):1;
   var current=new SizeF(m.From.Width+(m.To.Width-m.From.Width)*t,m.From.Height+(m.To.Height-m.From.Height)*t);
   if(m.To!=target){m.From=current;m.To=target;m.At=now;manipulationTimer.Start();return UiMotion.Enabled?current:target;}
   return current;
  }
  private bool TickSizeMotion(long now){foreach(var m in sizeMotion.Values)if(UiMotion.Enabled&&m.From!=m.To&&now-m.At<320)return true;return false;}
  internal void RefreshIoPresentation(){if(_blockPicker!=null){_blockPicker.Dispose();_blockPicker=null;}if(_menu!=null){_menu.Dispose();_menu=null;}if(_diagram!=null)foreach(var n in _diagram.Nodes)DisplaySize(n);RequestFrameRepaint();FlowTransitions.Enter(this);}
  internal void PreviewText(string id,string text){if(_diagram==null)return;foreach(var n in _diagram.Nodes)if(n.Id!=id){n.PreviewText=null;n.PreviewComment=null;}var node=FindNode(id);if(node!=null){node.PreviewText=text;DisplaySize(node);}RequestFrameRepaint();}
  internal void PreviewComment(string id,string comment){var n=FindNode(id);if(n==null)return;n.PreviewComment=comment;DisplaySize(n);RequestFrameRepaint();}
  internal void ApplyTextEdit(FlowNode node,Action apply){var oldText=node.PreviewText;var oldComment=node.PreviewComment;node.PreviewText=null;node.PreviewComment=null;var old=FlowSizing.Size(node);node.PreviewText=oldText;node.PreviewComment=oldComment;var before=ModelPositions();apply();node.PreviewText=null;node.PreviewComment=null;if(old!=FlowSizing.Size(node)){FlowPlacement.Grow(_diagram,node.Id);AnimatePlacement(before);}RequestFrameRepaint();}
 }
}
