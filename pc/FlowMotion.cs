using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Frociart
{
    // Animation is local, event-driven and bounded. No idle repaint loop.
    internal static class UiMotion
    {
        private static bool preferred = ReadPreference();
        private static bool systemAnimations = ReadSystemPreference();
        internal static event EventHandler Changed;
        internal static bool PreferredEnabled { get { return preferred; } }
        internal static bool Enabled { get { return preferred && systemAnimations; } }
        internal static int ActiveTweens;
        private static string PreferencePath {
            get { string custom = Environment.GetEnvironmentVariable("FROCIART_DATA_DIR"); return Path.Combine(String.IsNullOrWhiteSpace(custom) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frociart") : custom, "motion.txt"); }
        }
        private static bool ReadPreference() { try { return File.ReadAllText(PreferencePath).Trim() != "off"; } catch { return true; } }
        private static bool ReadSystemPreference() {
            try { bool enabled = true; return !SystemInformation.HighContrast && (!SystemParametersInfo(0x1042, 0, ref enabled, 0) || enabled); }
            catch (DllNotFoundException) { return false; } catch (EntryPointNotFoundException) { return false; }
        }
        internal static bool SetEnabled(bool value) {
            try { Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)); File.WriteAllText(PreferencePath, value ? "on" : "off"); }
            catch { return false; }
            preferred = value; RefreshSystemPreference(); return true;
        }
        internal static void RefreshSystemPreference() {
            systemAnimations = ReadSystemPreference(); var handler = Changed; if (handler != null) handler(null, EventArgs.Empty);
        }
        internal static float Ease(float t) { t = Math.Max(0, Math.Min(1, t)); return 1 - (1 - t) * (1 - t) * (1 - t); }
        // A damped unit step: small overshoot, zero initial velocity and an exact
        // resting endpoint. Use for geometry; alpha and colour remain bounded.
        internal static float Spring(float t) { if(t<=0)return 0;if(t>=1)return 1;double x=t*11,d=.72,w=Math.Sqrt(1-d*d);return(float)(1-Math.Exp(-d*x)*(Math.Cos(w*x)+d/w*Math.Sin(w*x))); }
        internal static Color Blend(Color from, Color to, float amount) {
            float t = Math.Max(0, Math.Min(1, amount)); return Color.FromArgb((int)(from.A + (to.A - from.A) * t), (int)(from.R + (to.R - from.R) * t), (int)(from.G + (to.G - from.G) * t), (int)(from.B + (to.B - from.B) * t));
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] ref bool value, uint flags);
    }

    internal sealed class MotionValue : IDisposable
    {
        private readonly Control owner;
        private readonly Timer timer = new Timer { Interval = 16 };
        private readonly Stopwatch watch = new Stopwatch();
        private float from, target, value, velocity, initialVelocity;
        private int duration;
        private bool active, disposed;
        internal float Value { get { return value; } }
        internal float Target { get { return target; } }
        internal bool Running { get { return active; } }
        internal event EventHandler Changed;
        internal MotionValue(Control owner, float initial = 0) {
            this.owner = owner; from = target = value = initial;
            timer.Tick += Tick; owner.Disposed += OwnerDisposed; owner.VisibleChanged += VisibilityChanged; UiMotion.Changed += PolicyChanged;
        }
        internal void Set(float next, int milliseconds = 260) {
            if (disposed || Math.Abs(next - target) < .0001f) return;
            if(active)Sample();
            from = value; initialVelocity=velocity; target = next; duration = Math.Max(1, milliseconds);
            if (!UiMotion.Enabled || !owner.IsHandleCreated || !owner.Visible) { Finish(); return; }
            watch.Restart(); if (!active) { active = true; UiMotion.ActiveTweens++; } timer.Start();
        }
        internal void Jump(float next) { target = next; Finish(); }
        private void Tick(object sender, EventArgs e) {
            if (disposed) return;
            if (!owner.Visible || !UiMotion.Enabled) { Finish(); return; }
            Sample();NotifyChanged();owner.Invalidate(); if (watch.ElapsedMilliseconds >= duration) Finish();
        }
        private void Sample() {
            double seconds=watch.Elapsed.TotalSeconds,omega=11000.0/Math.Max(1,duration),d=.72,w=omega*Math.Sqrt(1-d*d);
            double a=from-target,b=(initialVelocity+d*omega*a)/w,c=Math.Cos(w*seconds),s=Math.Sin(w*seconds),decay=Math.Exp(-d*omega*seconds),y=a*c+b*s;
            value=(float)(target+decay*y);velocity=(float)(decay*(-d*omega*y+w*(-a*s+b*c)));
        }
        private void Stop() { timer.Stop(); watch.Stop(); if (active) { active = false; UiMotion.ActiveTweens--; } }
        private void NotifyChanged(){var handler=Changed;if(handler!=null)handler(this,EventArgs.Empty);}
        private void Finish() { Stop(); velocity=initialVelocity=0;value = from = target; if (!owner.IsDisposed){NotifyChanged();owner.Invalidate();} }
        private void PolicyChanged(object sender, EventArgs e) { if (!UiMotion.Enabled) Finish(); }
        private void VisibilityChanged(object sender, EventArgs e) { if (!owner.Visible) Finish(); }
        private void OwnerDisposed(object sender, EventArgs e) { Dispose(); }
        public void Dispose() { if (disposed) return; disposed = true; Stop(); timer.Dispose(); UiMotion.Changed -= PolicyChanged; owner.Disposed -= OwnerDisposed; owner.VisibleChanged -= VisibilityChanged; }
    }
}
