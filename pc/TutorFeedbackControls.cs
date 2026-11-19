using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;
namespace Frociart
{
    internal sealed class TutorTabs : TabControl
    {
        private readonly MotionValue position;
        internal TutorTabs() { position=new MotionValue(this); SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); DrawMode = TabDrawMode.OwnerDrawFixed; SizeMode = TabSizeMode.Fixed; ItemSize = new Size(150, 38); Padding = new Point(12, 6); }
        protected override void WndProc(ref Message m){if(m.Msg==0x14){using(var g=Graphics.FromHdc(m.WParam))g.Clear(Ui.Surface);m.Result=new IntPtr(1);return;}base.WndProc(ref m);}
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Ui.Surface);
            for (int i=0;i<TabCount;i++) OnDrawItem(new DrawItemEventArgs(e.Graphics,Font,GetTabRect(i),i,i==SelectedIndex?DrawItemState.Selected:DrawItemState.None));
            if(TabCount>0){float value=Math.Max(0,Math.Min(TabCount-1,position.Value));int from=(int)value,to=Math.Min(TabCount-1,from+1);Rectangle a=GetTabRect(from),b=GetTabRect(to);using(var brush=new SolidBrush(Ui.Blue))e.Graphics.FillRectangle(brush,a.Left+(b.Left-a.Left)*(value-from)+10,a.Bottom-3,a.Width-20,3);}
            using(var pen=new Pen(Ui.Line)) e.Graphics.DrawLine(pen,0,DisplayRectangle.Top-1,Width,DisplayRectangle.Top-1);
        }
        protected override void OnSelecting(TabControlCancelEventArgs e){FlowTransitions.Change(this);base.OnSelecting(e);}
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); if(position!=null)position.Set(Math.Max(0,SelectedIndex),170); Invalidate(); }
        protected override void OnDrawItem(DrawItemEventArgs e) {
            bool selected = e.Index == SelectedIndex; using (var b = new SolidBrush(Ui.Surface)) e.Graphics.FillRectangle(b, e.Bounds);
            using (var font = Ui.Font(10, selected ? FontStyle.Bold : FontStyle.Regular)) TextRenderer.DrawText(e.Graphics, TabPages[e.Index].Text, font, e.Bounds, selected ? Ui.Blue : Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            
            if(Focused&&selected)using(var path=Ui.Rounded(Rectangle.Inflate(e.Bounds,-5,-5),6))using(var pen=new Pen(Ui.Blue))e.Graphics.DrawPath(pen,path);
        }
    }
    internal sealed class TutorUnitButton : Button
    {
        internal int Number; internal string Detail = ""; internal bool Complete;
        private readonly MotionValue hover;
        internal TutorUnitButton() { hover=new MotionValue(this); SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat; TabStop = true; }
        protected override void OnMouseEnter(EventArgs e){hover.Set(1);base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover.Set(0);base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Parent == null ? Ui.Paper : Parent.BackColor); g.SmoothingMode = SmoothingMode.AntiAlias; var box = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var shape = Ui.Rounded(box, 12)) { using (var b = new SolidBrush(UiMotion.Blend(Ui.Surface,Ui.Hover,hover.Value))) g.FillPath(b, shape); using (var p = new Pen(Focused ? Ui.Blue : Ui.Line, Focused ? 2 : 1)) g.DrawPath(p, shape); }
            using (var b = new SolidBrush(Complete ? Ui.Green : Ui.Blue)) g.FillEllipse(b, 16, 27, 38, 38);
            using (var font = Ui.Font(12, FontStyle.Bold)) TextRenderer.DrawText(g, Complete ? "✓" : Number.ToString("00"), font, new Rectangle(16, 27, 38, 38), Ui.IsDark ? Color.Black : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            using (var font = Ui.Font(12, FontStyle.Bold)) TextRenderer.DrawText(g, Text, font, new Rectangle(68, 16, Width - 100, 38), Ui.Ink, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            using (var font = Ui.Font(10)) TextRenderer.DrawText(g, Detail, font, new Rectangle(68, 58, Width - 90, Height - 62), Ui.Muted, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }
    internal sealed class TutorResultBand : Control
    {
        private string heading = "", detail = ""; private bool good;
        internal TutorResultBand() { Height = 84; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); AccessibleRole = AccessibleRole.Alert; }
        internal void Bind(string title, string body, bool correct) { bool changed = heading != title || detail != body; heading = title; detail = body; good = correct; AccessibleName = title + ". " + body; Invalidate(); if (changed && IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.SystemAlert, -1); }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; Color accent=good?Ui.Green:Ui.Red;
            using (var b = new SolidBrush(Ui.Surface)) g.FillRectangle(b, ClientRectangle); using (var b = new SolidBrush(Color.FromArgb(Ui.IsDark?36:20, accent))) g.FillRectangle(b, ClientRectangle);
            using (var b = new SolidBrush(accent)) g.FillRectangle(b, 0, 0, 5, Height);
            using (var p = new Pen(accent, 3)) { g.DrawEllipse(p, 20, 23, 32, 32); if(good) g.DrawLines(p,new[]{new Point(28,39),new Point(34,45),new Point(45,33)}); else { g.DrawLine(p,36,31,36,41); g.DrawLine(p,36,46,36,48); } }
            using (var f=Ui.Font(16,FontStyle.Bold)) TextRenderer.DrawText(g,heading,f,new Rectangle(68,10,Width-86,30),accent,TextFormatFlags.NoPrefix);
            using (var f=Ui.Font(10.5f)) TextRenderer.DrawText(g,detail,f,new Rectangle(68,42,Width-86,38),Ui.Ink,TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
        }
    }
    internal static class TutorFeedbackSignals
    {
        private static readonly Dictionary<string,SoundPlayer> players = new Dictionary<string,SoundPlayer>();
        private static string Preference { get { string custom=Environment.GetEnvironmentVariable("FROCIART_DATA_DIR"); return Path.Combine(String.IsNullOrWhiteSpace(custom)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"):custom,"tutor-sound.txt"); } }
        internal static bool Enabled { get { try { return File.ReadAllText(Preference).Trim() != "off"; } catch { return true; } } }
        internal static bool SetEnabled(bool value) { try { Directory.CreateDirectory(Path.GetDirectoryName(Preference)); File.WriteAllText(Preference,value?"on":"off"); return true; } catch { return false; } }
        internal static byte[] Wave(bool good, bool complete) {
            int rate=22050; double[] notes=complete?new[]{523.25,659.25,783.99}:good?new[]{587.33,783.99}:new[]{329.63,261.63}; int perNote=rate/10, count=perNote*notes.Length;
            using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream)) { writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++){int local=i%perNote;double fade=Math.Min(1,Math.Min(local/(rate*.008),(perNote-local)/(rate*.02)));writer.Write((short)(Math.Sin(2*Math.PI*notes[i/perNote]*local/rate)*32767*.12*fade));}return stream.ToArray(); }
        }
        internal static void Play(bool good, bool complete) { if(!Enabled)return; try { string key=complete?"complete":good?"good":"retry";SoundPlayer sound;if(!players.TryGetValue(key,out sound)){sound=new SoundPlayer(new MemoryStream(Wave(good,complete)));sound.Load();players[key]=sound;}sound.Play(); } catch { /* The visible result stays available without an audio device. */ } }
    }
}
