using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Windows.Forms;

namespace Frociart.LightSetup {
    internal sealed class DownloadProgress:Control {
        readonly Timer timer=new Timer{Interval=16};readonly Stopwatch clock=Stopwatch.StartNew();
        double shown,target,start,changed;bool unknown=true;
        internal DownloadProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=false;AccessibleRole=AccessibleRole.ProgressBar;timer.Tick+=(s,e)=>{Invalidate();if(!unknown&&clock.Elapsed.TotalMilliseconds-changed>=160)timer.Stop();};}
        internal void SetProgress(long received,long total){
            unknown=total<=0;start=Current();target=unknown?0:Math.Max(0,Math.Min(1,(double)received/total));changed=clock.Elapsed.TotalMilliseconds;
            AccessibilityNotifyClients(AccessibleEvents.ValueChange,-1);RefreshTimer();Invalidate();
        }
        double Current(){if(!SystemInformation.IsMenuAnimationEnabled)return target;double t=Math.Max(0,Math.Min(1,(clock.Elapsed.TotalMilliseconds-changed)/160));shown=start+(target-start)*(1-Math.Pow(1-t,3));return shown;}
        void RefreshTimer(){if(Visible&&SystemInformation.IsMenuAnimationEnabled&&(unknown||Math.Abs(start-target)>0.0001))timer.Start();else timer.Stop();}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);RefreshTimer();}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            var track=new RectangleF(0,Math.Max(1,(Height-8)/2f),Math.Max(0,Width-1),8);
            using(var brush=new SolidBrush(Color.FromArgb(48,52,62)))Fill(e.Graphics,brush,track);
            if(track.Width<=0)return;
            float width=(float)(unknown?track.Width*0.24:track.Width*Current());
            float x=unknown?(float)((Math.Sin(clock.Elapsed.TotalMilliseconds/500.0)+1)*0.5*(track.Width-width)):0;
            if(width>0)using(var brush=new SolidBrush(Color.FromArgb(117,172,255)))Fill(e.Graphics,brush,new RectangleF(x,track.Y,width,track.Height));
        }
        static void Fill(Graphics graphics,Brush brush,RectangleF bounds){
            float diameter=Math.Min(8,bounds.Width);if(diameter<=0)return;
            using(var path=new GraphicsPath()){
                path.AddArc(bounds.X,bounds.Y,diameter,bounds.Height,90,180);
                path.AddArc(bounds.Right-diameter,bounds.Y,diameter,bounds.Height,270,180);path.CloseFigure();graphics.FillPath(brush,path);
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance(){return new ProgressAccessibility(this);}
        sealed class ProgressAccessibility:ControlAccessibleObject {
            readonly DownloadProgress owner;internal ProgressAccessibility(DownloadProgress owner):base(owner){this.owner=owner;}
            public override string Value {get{return owner.unknown?"Download in corso":Math.Round(owner.target*100).ToString()+"%";}set{}}
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();clock.Stop();}base.Dispose(disposing);}
    }
}
