using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Frociart {
 internal sealed partial class MainForm {
  private DocumentDropCue dropCue;private Timer dropLeaveTimer;private int documentPresentationTicket;private bool documentPresentationDisposed;
  private void InitializeDocumentPresentation(){
   dropLeaveTimer=new Timer{Interval=40};dropLeaveTimer.Tick+=delegate{dropLeaveTimer.Stop();HideDocumentDropCue();};
   ClientSizeChanged+=delegate{LayoutDocumentDropCue();};
  }
  private void ShowDocumentDropCue(string path){
   if(documentPresentationDisposed||IsDisposed||Disposing)return;if(dropLeaveTimer!=null)dropLeaveTimer.Stop();
   if(dropCue==null){dropCue=new DocumentDropCue();Controls.Add(dropCue);}dropCue.FileName=Path.GetFileName(path);LayoutDocumentDropCue();dropCue.Visible=true;dropCue.BringToFront();dropCue.Invalidate();
  }
  private void LayoutDocumentDropCue(){if(dropCue==null||dropCue.IsDisposed)return;int width=Math.Min((int)(460*DeviceDpi/96f),Math.Max(240,ClientSize.Width-40));int height=(int)(100*DeviceDpi/96f);dropCue.SetBounds((ClientSize.Width-width)/2,Math.Max(12,(ClientSize.Height-height)/2),width,height);}
  private void HideDocumentDropCue(){if(dropLeaveTimer!=null)dropLeaveTimer.Stop();if(dropCue!=null&&!dropCue.IsDisposed)dropCue.Visible=false;}
  private void DocumentDragLeave(object sender,EventArgs e){if(dropLeaveTimer!=null&&!documentPresentationDisposed){dropLeaveTimer.Stop();dropLeaveTimer.Start();}}
  private void FinishDocumentPresentation(){documentPresentationTicket++;HideDocumentDropCue();if(Canvas!=null&&!Canvas.IsDisposed)Canvas.FinishDocumentEntrance();}
  private void QueueDroppedDocumentEntrance(){
   if(documentPresentationDisposed||!IsHandleCreated||!UiMotion.Enabled)return;var expected=Project;var diagram=Canvas.Diagram;int ticket=++documentPresentationTicket;
   BeginInvoke((Action)delegate{if(documentPresentationDisposed||IsDisposed||Disposing||ticket!=documentPresentationTicket||!ReferenceEquals(Project,expected)||!ReferenceEquals(Canvas.Diagram,diagram)||!UiMotion.Enabled)return;Canvas.BeginDocumentEntrance();});
  }
  private void DisposeDocumentPresentation(){if(documentPresentationDisposed)return;documentPresentationDisposed=true;FinishDocumentPresentation();if(dropLeaveTimer!=null){dropLeaveTimer.Dispose();dropLeaveTimer=null;}if(dropCue!=null){dropCue.Dispose();dropCue=null;}}
 }
 internal sealed class DocumentDropCue:Control {
  private string fileName="";private readonly Font titleFont=Ui.Font(13,FontStyle.Bold),fileFont=Ui.Font(10);
  [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
  internal string FileName {get{return fileName;}set{fileName=value??"";AccessibleDescription=fileName;Invalidate();}}
  internal DocumentDropCue(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;AccessibleRole=AccessibleRole.StaticText;AccessibleName="Rilascia per aprire il diagramma";Visible=false;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Ui.Paper);}
  protected override void OnPaint(PaintEventArgs e){float scale=DeviceDpi/96f;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Ui.Rounded(new RectangleF(2,2,Width-5,Height-5),14*scale))using(var b=new SolidBrush(Ui.Tint))using(var pen=new Pen(Ui.Blue,2*scale)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}TextRenderer.DrawText(e.Graphics,"Rilascia per aprire il diagramma",titleFont,new Rectangle((int)(18*scale),(int)(18*scale),Width-(int)(36*scale),(int)(30*scale)),Ui.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);TextRenderer.DrawText(e.Graphics,fileName,fileFont,new Rectangle((int)(18*scale),(int)(53*scale),Width-(int)(36*scale),(int)(25*scale)),Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
  protected override void Dispose(bool disposing){if(disposing){titleFont.Dispose();fileFont.Dispose();}base.Dispose(disposing);}
 }
}
