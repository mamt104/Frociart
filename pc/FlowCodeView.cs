using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart
{
    internal sealed class FlowCodeView : RichTextBox
    {
        internal FlowSourceText Source {get;private set;}
        internal FlowSourceText CompleteSource {get;private set;}
        internal string HighlightedFunction,HighlightedNode;
        internal int HighlightUpdates;
        internal event Action<string,string> BlockRequested;
        private readonly List<FlowSourceText.Line> highlighted=new List<FlowSourceText.Line>();
        private FlowSourceText.Position savedStart,savedEnd,savedTop;
        private Point savedScroll;private int savedTopInset;
        [DllImport("user32.dll",CharSet=CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr parameter,ref Point point);
        internal FlowCodeView(){ReadOnly=true;WordWrap=false;DetectUrls=false;HideSelection=false;ScrollBars=RichTextBoxScrollBars.Both;BorderStyle=BorderStyle.None;Font=Ui.Mono(9.5f);Dock=DockStyle.Fill;AccessibleName="Codice del flowchart";BackColor=Ui.InputBackground;ForeColor=Ui.Ink;
            AccessibleDescription="Doppio clic su una riga, oppure Ctrl+Invio, per selezionare il blocco corrispondente nel diagramma. Il testo è in sola lettura.";
            DoubleClick+=delegate{RequestBlock();};KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.Enter){e.Handled=true;e.SuppressKeyPress=true;RequestBlock();}};
            Ui.ThemeChanged+=ThemeChanged;
        }
        internal void Bind(FlowSourceText source,bool showSupport=false){
            if(CompleteSource!=null&&Source!=null&&TextLength>0){
                savedStart=CompleteSource.Capture(Source,SelectionStart,TextLength);savedEnd=CompleteSource.Capture(Source,SelectionStart+SelectionLength,TextLength);
                int top=GetCharIndexFromPosition(Point.Empty);savedTop=CompleteSource.Capture(Source,top,TextLength);savedTopInset=GetPositionFromCharIndex(top).Y;if(IsHandleCreated)SendMessage(Handle,0x04DD,IntPtr.Zero,ref savedScroll);
            }
            CompleteSource=source;Source=source==null?null:showSupport?source:source.ProgramView();AccessibleName=source==null?"Codice del flowchart":"Codice "+FlowSourceTarget.NameOf(source.Language)+" del flowchart";highlighted.Clear();HighlightedFunction=HighlightedNode=null;Text=Source==null?"":showSupport?Source.Text:SourceCodeDialog.ProgramText(Source);BackColor=Ui.InputBackground;ForeColor=Ui.Ink;SelectAll();SelectionBackColor=BackColor;SelectionColor=ForeColor;
            if(source==null){Select(0,0);return;}
            int? start=source.Restore(Source,savedStart,TextLength);int begin=start??(Source.OpeningLine(null,null)==null?0:Source.OpeningLine(null,null).Start);int end=start.HasValue?(source.Restore(Source,savedEnd,TextLength)??begin):begin;Select(Math.Min(begin,TextLength),Math.Max(0,Math.Min(end,TextLength)-begin));
            int? topPosition=start.HasValue?source.Restore(Source,savedTop,TextLength):null;
            if(topPosition.HasValue&&IsHandleCreated){Point now=Point.Empty;SendMessage(Handle,0x04DD,IntPtr.Zero,ref now);Point target=new Point(savedScroll.X,Math.Max(0,now.Y+GetPositionFromCharIndex(Math.Min(topPosition.Value,TextLength)).Y-savedTopInset));SendMessage(Handle,0x04DE,IntPtr.Zero,ref target);}else ScrollToCaret();
            savedStart=savedEnd=savedTop=null;
        }
        internal FlowSourceText.Origin SelectedOrigin(){if(Source==null)return null;var line=Source.AtOffset(SelectionStart);return line==null?null:line.Origins.FirstOrDefault();}
        internal void RequestBlock(){var origin=SelectedOrigin();var handler=BlockRequested;if(origin!=null&&handler!=null)handler(origin.FunctionId,origin.NodeId);}
        internal void RevealHighlighted(){
            if(Focused||highlighted.Count==0)return;int caret=SelectionStart,length=SelectionLength;Point scroll=Point.Empty;
            Select(highlighted[0].Start,0);ScrollToCaret();if(IsHandleCreated)SendMessage(Handle,0x04DD,IntPtr.Zero,ref scroll);
            Select(Math.Min(caret,TextLength),Math.Min(length,Math.Max(0,TextLength-caret)));if(IsHandleCreated)SendMessage(Handle,0x04DE,IntPtr.Zero,ref scroll);
        }
        internal IList<FlowSourceText.Line> Highlight(string function,string node,bool follow){
            if(HighlightedFunction==function&&HighlightedNode==node)return highlighted.AsReadOnly();
            int caret=SelectionStart,length=SelectionLength;Point scroll=Point.Empty;
            if(IsHandleCreated)SendMessage(Handle,0x04DD,IntPtr.Zero,ref scroll); // EM_GETSCROLLPOS
            foreach(var line in highlighted)PaintLine(line,BackColor,ForeColor);
            highlighted.Clear();HighlightedFunction=function;HighlightedNode=node;HighlightUpdates++;
            if(Source!=null&&node!=null)foreach(var line in Source.ForBlock(function,node))if(line.Start<TextLength){highlighted.Add(line);PaintLine(line,Ui.Tint,Ui.Ink);}
            if(follow&&highlighted.Count>0&&!Focused){Select(highlighted[0].Start,0);ScrollToCaret();if(IsHandleCreated)SendMessage(Handle,0x04DD,IntPtr.Zero,ref scroll);}
            Select(Math.Min(caret,TextLength),Math.Min(length,Math.Max(0,TextLength-caret)));
            // Restoring the user's caret otherwise scrolls back to its old line.
            if(IsHandleCreated)SendMessage(Handle,0x04DE,IntPtr.Zero,ref scroll); // EM_SETSCROLLPOS
            return highlighted.AsReadOnly();
        }
        private void PaintLine(FlowSourceText.Line line,Color background,Color foreground){if(line.Start>=TextLength)return;Select(line.Start,Math.Min(line.Length,TextLength-line.Start));SelectionBackColor=background;SelectionColor=foreground;}
        private void ThemeChanged(object sender,EventArgs args){int caret=SelectionStart,length=SelectionLength;BackColor=Ui.InputBackground;ForeColor=Ui.Ink;SelectAll();SelectionBackColor=BackColor;SelectionColor=ForeColor;Select(caret,length);string function=HighlightedFunction,node=HighlightedNode;highlighted.Clear();HighlightedFunction=HighlightedNode=null;Highlight(function,node,false);}
        protected override void Dispose(bool disposing){if(disposing)Ui.ThemeChanged-=ThemeChanged;base.Dispose(disposing);}
    }
}
