using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using Media = System.Windows.Media;
using Wpf = System.Windows;

namespace Frociart
{
    // Keep ordinary Latin rendering unchanged. The system formatter supplies
    // shaping and installed-font fallback for the rest of Unicode, including
    // supplementary characters which GDI+ misses in the three base families.
    internal static class FlowText
    {
        private const long BitmapBudget = 8 * 1024 * 1024;
        private const int TextBudget = 256 * 1024;
        private const int TilePixels = 768;
        private const int BleedPixels = 2;
        private static readonly object sync = new object();
        private static readonly Dictionary<string, SizeF> measurements = new Dictionary<string, SizeF>();
        private static readonly Queue<string> measuredOrder = new Queue<string>();
        private static readonly Dictionary<string, Bitmap> rasters = new Dictionary<string, Bitmap>();
        private static readonly Queue<string> rasterOrder = new Queue<string>();
        private static long rasterBytes;
        private static int measuredCharacters, rasterCharacters;

        internal static bool UnicodeLayout(string text)
        {
            if (text != null) foreach (char c in text) if (c > 255 && c != '\u2026') return true;
            return false;
        }

        private static string SafeText(string value)
        {
            var result = new StringBuilder((value ?? "").Length);
            for (int i = 0; i < (value ?? "").Length; i++)
            {
                char c = value[i];
                if (Char.IsHighSurrogate(c) && i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1])) result.Append(c).Append(value[++i]);
                else result.Append(Char.IsSurrogate(c) ? '\uFFFD' : c);
            }
            return result.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
        }

        private static double FontPixels(Font font) { return font.Unit == GraphicsUnit.Pixel ? font.Size : font.SizeInPoints * 96.0 / 72.0; }
        private static string F(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static string FontKey(Font font) { return font.Name + "|" + F(FontPixels(font)) + "|" + (int)font.Style; }

        private static Media.FormattedText Layout(string text, Font font, double width, Color color)
        {
            var brush = new Media.SolidColorBrush(Media.Color.FromArgb(color.A, color.R, color.G, color.B)); brush.Freeze();
            var face = new Media.Typeface(new Media.FontFamily(font.Name), font.Italic ? Wpf.FontStyles.Italic : Wpf.FontStyles.Normal,
                font.Bold ? Wpf.FontWeights.Bold : Wpf.FontWeights.Normal, Wpf.FontStretches.Normal);
            string value = SafeText(text);
            var result = new Media.FormattedText(value, CultureInfo.InvariantCulture, Wpf.FlowDirection.LeftToRight, face, FontPixels(font), brush, 1.0);
            // Composite fallback may retain a Latin digit's base family while
            // losing the combining keycap. Select the system emoji family for
            // an entire emoji sequence, never just one UTF-16 code unit.
            int[] elements = Elements(value);
            for (int i = 0; i < elements.Length; i++)
            {
                int start = elements[i], end = i + 1 < elements.Length ? elements[i + 1] : value.Length;
                bool emoji = false;
                for (int j = start; j < end; j++)
                {
                    int scalar = Char.ConvertToUtf32(value, j);
                    if (scalar >= 0x1F000 && scalar <= 0x1FAFF || scalar == 0xFE0F || scalar == 0x20E3) emoji = true;
                    if (Char.IsHighSurrogate(value[j])) j++;
                }
                if (emoji) result.SetFontFamily("Segoe UI Emoji", start, end - start);
            }
            result.MaxTextWidth = Math.Max(.01, Math.Min(3579139, width));
            result.Trimming = Wpf.TextTrimming.None;
            if (font.Underline) result.SetTextDecorations(Wpf.TextDecorations.Underline);
            if (font.Strikeout) result.SetTextDecorations(Wpf.TextDecorations.Strikethrough);
            return result;
        }

        internal static SizeF Measure(Graphics graphics, string text, Font font, SizeF available, StringFormat format)
        {
            if (!UnicodeLayout(text)) return graphics.MeasureString(text, font, available, format);
            return MeasureUnicode(text, font, available, format);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SizeF MeasureUnicode(string text, Font font, SizeF available, StringFormat format)
        {
            bool noWrap = (format.FormatFlags & StringFormatFlags.NoWrap) != 0;
            double width = noWrap ? 3579139 : available.Width;
            bool trailing = (format.FormatFlags & StringFormatFlags.MeasureTrailingSpaces) != 0;
            string key = FontKey(font) + "|" + F(width) + "|" + trailing + "|" + text;
            lock (sync)
            {
                SizeF size;
                if (measurements.TryGetValue(key, out size)) return size;
                var layout = Layout(text, font, width, Color.Black);
                size = new SizeF((float)(trailing ? layout.WidthIncludingTrailingWhitespace : layout.Width), (float)layout.Height);
                while (measuredOrder.Count > 0 && (measurements.Count >= 256 || measuredCharacters + key.Length > TextBudget))
                { string old = measuredOrder.Dequeue(); measuredCharacters -= old.Length; measurements.Remove(old); }
                if (key.Length <= TextBudget) { measurements.Add(key, size); measuredOrder.Enqueue(key); measuredCharacters += key.Length; }
                return size;
            }
        }

        internal static SizeF MeasureLine(Graphics graphics, string text, Font font, StringFormat format, int maxWidth = Int32.MaxValue)
        {
            return UnicodeLayout(text) ? Measure(graphics, text, font, new SizeF(3579139, 100000), format) : graphics.MeasureString(text, font, maxWidth, format);
        }

        internal static SizeF MeasureNatural(Graphics graphics, string text, Font font)
        {
            if (!UnicodeLayout(text)) return graphics.MeasureString(text, font);
            using (var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap }) return Measure(graphics, text, font, new SizeF(3579139, 100000), format);
        }

        // .NET Framework's StringInfo predates emoji ZWJ/modifier/flag sequences.
        // Keep these sequences intact when deriving SVG lines or an ellipsis.
        internal static int[] Elements(string text)
        {
            var starts = StringInfo.ParseCombiningCharacters(text);
            var result = new List<int>(); int regional = 0;
            foreach (int index in starts)
            {
                int scalar = Char.ConvertToUtf32(text, index);
                int previous = index == 0 ? -1 : Char.ConvertToUtf32(text, index - (index > 1 && Char.IsLowSurrogate(text[index - 1]) ? 2 : 1));
                bool flag = scalar >= 0x1F1E6 && scalar <= 0x1F1FF;
                bool joined = index > 0 && (scalar == 0x200D || previous == 0x200D || scalar == 0xFE0F || scalar == 0xFE0E ||
                    scalar >= 0x1F3FB && scalar <= 0x1F3FF || scalar >= 0xE0020 && scalar <= 0xE007F || flag && regional % 2 == 1);
                if (!joined) result.Add(index);
                regional = flag ? regional + 1 : 0;
            }
            return result.ToArray();
        }

        internal static bool DrawUnicode(Graphics graphics, string text, Font font, Color color, RectangleF bounds, StringFormat format)
        {
            if (!UnicodeLayout(text)) return false;
            DrawUnicodeCore(graphics, text, font, color, bounds, format);
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DrawUnicodeCore(Graphics graphics, string text, Font font, Color color, RectangleF bounds, StringFormat format)
        {
            RectangleF visible = RectangleF.Intersect(bounds, graphics.VisibleClipBounds);
            if (visible.IsEmpty) return;
            float resolution;
            using (var transform = graphics.Transform)
            {
                var m = transform.Elements;
                double zoom = Math.Max(Math.Sqrt(m[0] * m[0] + m[1] * m[1]), Math.Sqrt(m[2] * m[2] + m[3] * m[3]));
                resolution = .125f;
                while (resolution < Math.Min(4, zoom)) resolution *= 2;
            }
            float step = TilePixels / resolution;
            int left = Math.Max(0, (int)Math.Floor((visible.Left - bounds.Left) / step));
            int top = Math.Max(0, (int)Math.Floor((visible.Top - bounds.Top) / step));
            int right = (int)Math.Ceiling((visible.Right - bounds.Left) / step);
            int bottom = (int)Math.Ceiling((visible.Bottom - bounds.Top) / step);
            string prefix = FontKey(font) + "|" + F(bounds.Width) + "|" + F(bounds.Height) + "|" + (int)format.Alignment + "|" +
                (int)format.LineAlignment + "|" + (int)format.Trimming + "|" + (int)format.FormatFlags + "|" + color.ToArgb() + "|" + F(resolution) + "|";
            lock (sync)
            {
                Media.FormattedText layout = null;
                for (int row = top; row < bottom; row++) for (int column = left; column < right; column++)
                {
                    float x = column * step, y = row * step;
                    float width = Math.Min(step, bounds.Width - x), height = Math.Min(step, bounds.Height - y);
                    if (width <= 0 || height <= 0) continue;
                    string key = prefix + column + "," + row + "|" + text;
                    Bitmap bitmap;
                    if (!rasters.TryGetValue(key, out bitmap))
                    {
                        if (layout == null)
                        {
                            layout = Layout(text, font, bounds.Width, color);
                            layout.TextAlignment = format.Alignment == StringAlignment.Center ? Wpf.TextAlignment.Center :
                                format.Alignment == StringAlignment.Far ? Wpf.TextAlignment.Right : Wpf.TextAlignment.Left;
                            layout.MaxTextHeight = Math.Max(.01, bounds.Height);
                            bool oneLine = (format.FormatFlags & StringFormatFlags.NoWrap) != 0;
                            if (oneLine) layout.MaxLineCount = 1;
                            // WPF ellipsis disables emergency breaks inside a
                            // long word. Nodes must retain the wrapping used by
                            // their size calculation; one-line page headings
                            // may abbreviate. MaxTextHeight still limits lines.
                            layout.Trimming = oneLine && format.Trimming != StringTrimming.None ? Wpf.TextTrimming.CharacterEllipsis : Wpf.TextTrimming.None;
                        }
                        float bleed = BleedPixels / resolution;
                        bitmap = Raster(layout, width + 2 * bleed, height + 2 * bleed, x - bleed, y - bleed, bounds.Height, resolution, format.LineAlignment);
                        long bytes = (long)bitmap.Width * bitmap.Height * 4;
                        while (rasterOrder.Count > 0 && (rasterBytes + bytes > BitmapBudget || rasterCharacters + key.Length > TextBudget || rasters.Count >= 128))
                        { string old = rasterOrder.Dequeue(); Bitmap b = rasters[old]; rasterBytes -= (long)b.Width * b.Height * 4; rasterCharacters -= old.Length; rasters.Remove(old); b.Dispose(); }
                        if (bytes <= BitmapBudget && key.Length <= TextBudget)
                        { rasters.Add(key, bitmap); rasterOrder.Enqueue(key); rasterBytes += bytes; rasterCharacters += key.Length; }
                        else
                        { using (bitmap) PaintTile(graphics, bitmap, new RectangleF(bounds.Left + x, bounds.Top + y, width, height), resolution); continue; }
                    }
                    PaintTile(graphics, bitmap, new RectangleF(bounds.Left + x, bounds.Top + y, width, height), resolution);
                }
            }
        }

        private static void PaintTile(Graphics graphics, Bitmap bitmap, RectangleF destination, float resolution)
        {
            graphics.DrawImage(bitmap, destination, new RectangleF(BleedPixels, BleedPixels, destination.Width * resolution, destination.Height * resolution), GraphicsUnit.Pixel);
        }

        private static Bitmap Raster(Media.FormattedText layout, float width, float height, float x, float y, float boxHeight, float resolution, StringAlignment vertical)
        {
            int w = Math.Max(1, (int)Math.Ceiling(width * resolution)), h = Math.Max(1, (int)Math.Ceiling(height * resolution));
            double offset = vertical == StringAlignment.Center ? Math.Max(0, (boxHeight - layout.Height) / 2) :
                vertical == StringAlignment.Far ? Math.Max(0, boxHeight - layout.Height) : 0;
            var visual = new Media.DrawingVisual();
            // Transparent RenderTargetBitmap uses alpha antialiasing; no opaque
            // GDI background or ClearType fringes are copied into the canvas.
            using (var context = visual.RenderOpen())
            {
                context.PushTransform(new Media.ScaleTransform(resolution, resolution));
                context.PushTransform(new Media.TranslateTransform(-x, -y));
                context.DrawText(layout, new Wpf.Point(0, offset)); context.Pop(); context.Pop();
            }
            var rendered = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, Media.PixelFormats.Pbgra32);
            rendered.Render(visual);
            var pixels = new byte[w * h * 4]; rendered.CopyPixels(pixels, w * 4, 0);
            var bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            try
            {
                var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try { for (int row = 0; row < h; row++) Marshal.Copy(pixels, row * w * 4, IntPtr.Add(data.Scan0, row * data.Stride), w * 4); }
                finally { bitmap.UnlockBits(data); }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        internal static long CachedBitmapBytes { get { lock (sync) return rasterBytes; } }
        internal static int CachedCharacters { get { lock (sync) return measuredCharacters + rasterCharacters; } }
        internal static void Clear()
        {
            lock (sync)
            {
                foreach (var b in rasters.Values) b.Dispose(); rasters.Clear(); rasterOrder.Clear(); measurements.Clear(); measuredOrder.Clear();
                rasterBytes = 0; rasterCharacters = measuredCharacters = 0;
            }
        }
    }
}
