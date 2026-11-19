using System.Drawing;
using System.Drawing.Drawing2D;
namespace Frociart {
    internal static class BrandArtwork {
        internal static void Draw(Graphics graphics, RectangleF bounds, Color color) {
            var state=graphics.Save();
            try {
                graphics.SmoothingMode=SmoothingMode.AntiAlias;
                float side=System.Math.Min(bounds.Width,bounds.Height);
                graphics.TranslateTransform(bounds.X+(bounds.Width-side)/2,bounds.Y+(bounds.Height-side)/2);
                graphics.ScaleTransform(side/64f,side/64f);
                using(var pen=new Pen(color,4){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}) {
                    graphics.DrawLine(pen,32,18,32,23);
                    graphics.DrawLines(pen,new[]{new PointF(22,32),new PointF(14,32),new PointF(14,44)});
                    graphics.DrawLines(pen,new[]{new PointF(42,32),new PointF(50,32),new PointF(50,44)});
                }
                using(var brush=new SolidBrush(color)) {
                    Fill(graphics,brush,23,8,18,10,5);
                    graphics.FillPolygon(brush,new[]{new PointF(32,22),new PointF(44,32),new PointF(32,42),new PointF(20,32)});
                    Fill(graphics,brush,6,44,16,12,4);Fill(graphics,brush,42,44,16,12,4);
                }
            } finally {graphics.Restore(state);}
        }
        private static void Fill(Graphics g,Brush b,float x,float y,float w,float h,float r) {
            using(var path=new GraphicsPath()) {float d=r*2;path.AddArc(x,y,d,d,180,90);path.AddArc(x+w-d,y,d,d,270,90);path.AddArc(x+w-d,y+h-d,d,d,0,90);path.AddArc(x,y+h-d,d,d,90,90);path.CloseFigure();g.FillPath(b,path);}
        }
    }
}
