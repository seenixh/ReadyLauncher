using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>
    /// The "digital rain" open / close animation: columns of glowing green characters pour down a window's panels,
    /// straight over them, while the window stays in full view. Opening, the window is there at once and the rain
    /// falls over it and fades away; closing, the rain falls over it, and at the very end the window and the rain fade
    /// out together. Drawn on top, in the window's adorner layer, in the panels' shape.
    /// </summary>
    internal class DigitalRain : IOverlayAnimation
    {
        const double CellW = 12, CellH = 15;   // one character's place, in screen units
        const double FontSize = 13.5;
        const double Glow = 4;                 // room around each character for its glow
        const double RowsPerSecond = 32;       // how fast the rain falls at normal speed (about a third of what it was)
        static readonly string Chars = MakeChars();
        static readonly Color[] Shades =
        {
            Color.FromRgb(0xE6, 0xFF, 0xEA),          // the leading character: nearly white
            Color.FromArgb(0xFF, 0x3C, 0xFF, 0x70),   // just behind it: bright green
            Color.FromArgb(0xE0, 0x22, 0xD0, 0x55),
            Color.FromArgb(0xB0, 0x14, 0x9C, 0x40),
            Color.FromArgb(0x78, 0x0C, 0x6E, 0x2E)    // the tail: dim
        };
        static readonly Dictionary<double, ImageSource[][]> glyphCache = new Dictionary<double, ImageSource[][]>();
        static readonly Random rnd = new Random();

        readonly FrameworkElement el;
        readonly AdornerLayer layer;
        readonly Rain rain;
        readonly bool opening;
        readonly double ms;
        readonly Action done;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ImageSource[][] glyphs;
        readonly int cols, rows;
        readonly double[] head, fall;      // per column: where the leading character is (in rows), and rows per second
        readonly int[] length;             // per column: how long the trail is
        readonly int[] cell;               // which character shows in each place
        readonly double pace;              // the animation speed: 1 = normal
        double lastTime;
        bool running, spawning = true;
        int frame;

        /// <summary>How long the whole animation takes at a speed, in milliseconds.</summary>
        public static int Duration(bool opening, int speed) { return ElementAnimator.Ms(opening ? 1300 : 1000, speed); }

        /// <summary>Starts the animation on an element; null when it can't be shown (no layer to draw on, or no size yet).</summary>
        public static DigitalRain Start(FrameworkElement el, bool opening, int speed, Action done)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return null;
            el.UpdateLayout();
            if (el.ActualWidth < 2 || el.ActualHeight < 2) return null;
            return new DigitalRain(el, layer, opening, speed, done);
        }

        DigitalRain(FrameworkElement el, AdornerLayer layer, bool opening, int speed, Action done)
        {
            this.el = el;
            this.layer = layer;
            this.opening = opening;
            this.done = done;
            ms = Duration(opening, speed);
            pace = Math.Max(25, Math.Min(400, speed)) / 100.0;
            double scale = 1;
            var source = PresentationSource.FromVisual(el);
            if (source != null && source.CompositionTarget != null) scale = source.CompositionTarget.TransformToDevice.M11;
            glyphs = GlyphsFor(scale);

            cols = Math.Max(1, (int)Math.Ceiling(el.ActualWidth / CellW));
            rows = Math.Max(1, (int)Math.Ceiling(el.ActualHeight / CellH));
            head = new double[cols];
            fall = new double[cols];
            length = new int[cols];
            cell = new int[cols * rows];
            for (int i = 0; i < cell.Length; i++) cell[i] = rnd.Next(Chars.Length);
            for (int c = 0; c < cols; c++) Spawn(c, true);

            rain = new Rain(this);
            layer.Add(rain);
            running = true;
            Step();   // the very first frame on screen is already the start of the animation
            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>A new stream of characters at the top of a column (at the start: some way down already, so it covers the window).</summary>
        void Spawn(int c, bool first)
        {
            // An unhurried fall, the same in every window (faster or slower with the animation speed), some streams
            // quicker than others.
            fall[c] = RowsPerSecond * pace * (0.6 + rnd.NextDouble() * 0.8);
            length[c] = rnd.Next(6, Math.Max(8, (int)(rows * 0.7)) + 1);
            if (first && opening) head[c] = rnd.NextDouble() * rows - rows * 0.4;          // coming in from the top, some partway down
            else if (first) head[c] = rnd.NextDouble() * (rows + length[c]) - rows * 0.2;  // already all over the window
            else head[c] = -rnd.NextDouble() * rows * 0.3;
        }

        void OnRendering(object sender, EventArgs e)
        {
            if (running) Step();
        }

        void Step()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = Math.Min(0.1, (now - lastTime) / 1000);
            lastTime = now;
            double p = Math.Min(1, now / ms);
            // The rain falls straight over the window, which stays in full view (nothing darkens it or hides it).
            double level, content;
            if (opening)
            {
                // The window is there at once; the rain comes in over it, keeps falling a moment, then fades away.
                level = Smooth(p / 0.2) * (1 - Smooth((p - 0.6) / 0.4));
                content = 1;
            }
            else
            {
                // The rain comes in over the window and keeps falling; only at the very end do the window and the
                // rain fade out together, so the window doesn't just vanish.
                level = Smooth(p / 0.25) * (1 - Smooth((p - 0.8) / 0.2));
                content = 1 - Smooth((p - 0.8) / 0.2);
                if (p >= 0.8) spawning = false;
            }
            el.Opacity = content;

            for (int c = 0; c < cols; c++)
            {
                head[c] += fall[c] * dt;
                if (head[c] - length[c] > rows && spawning) Spawn(c, false);
            }
            // Characters keep changing as they fall.
            int changes = Math.Max(1, cols * rows / 30);
            for (int i = 0; i < changes; i++) cell[rnd.Next(cell.Length)] = rnd.Next(Chars.Length);
            if (frame++ % 6 == 0) rain.Shape = TvStatic.PanelShape(el);
            rain.Show(level);
            if (p >= 1) Finish();
        }

        void Finish()
        {
            Stop();
            if (opening) el.Opacity = 1;
            if (done != null) done();
        }

        /// <summary>Ends it where it is, without calling "done" (the caller puts the window back as it was).</summary>
        public void Stop()
        {
            if (!running) return;
            running = false;
            CompositionTarget.Rendering -= OnRendering;
            layer.Remove(rain);
        }

        static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Half-width katakana and digits.</summary>
        static string MakeChars()
        {
            var sb = new System.Text.StringBuilder();
            for (char ch = 'ｦ'; ch <= 'ﾝ'; ch++) sb.Append(ch);
            sb.Append("0123456789");
            return sb.ToString();
        }

        /// <summary>
        /// Every character in every shade, as small pictures at the screen's resolution, made once: drawing pictures
        /// is far quicker than laying out text a thousand times a frame.
        /// </summary>
        static ImageSource[][] GlyphsFor(double scale)
        {
            ImageSource[][] all;
            if (glyphCache.TryGetValue(scale, out all)) return all;
            int cw = (int)Math.Round((CellW + 2 * Glow) * scale), ch = (int)Math.Round((CellH + 2 * Glow) * scale);
            var face = new Typeface(new FontFamily("MS Gothic, Yu Gothic UI, Meiryo UI, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            all = new ImageSource[Shades.Length][];
            for (int s = 0; s < Shades.Length; s++)
            {
                var brush = new SolidColorBrush(Shades[s]);
                brush.Freeze();
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    for (int i = 0; i < Chars.Length; i++)
                    {
                        var ft = new FormattedText(Chars[i].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, FontSize * scale, brush, 1.0);
                        dc.DrawText(ft, new Point(i * cw + (cw - ft.Width) / 2, (ch - ft.Height) / 2));
                    }
                }
                if (s < 3) dv.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 7 * scale, Color = Color.FromRgb(0x3C, 0xFF, 0x70), Opacity = s == 0 ? 1 : 0.7 };
                var atlas = new RenderTargetBitmap(cw * Chars.Length, ch, 96, 96, PixelFormats.Pbgra32);
                atlas.Render(dv);
                atlas.Freeze();
                all[s] = new ImageSource[Chars.Length];
                for (int i = 0; i < Chars.Length; i++)
                {
                    var one = new CroppedBitmap(atlas, new Int32Rect(i * cw, 0, cw, ch));
                    one.Freeze();
                    all[s][i] = one;
                }
            }
            glyphCache[scale] = all;
            return all;
        }

        /// <summary>Draws the rain over the element, in the shape of its panels.</summary>
        class Rain : Adorner
        {
            readonly DigitalRain owner;
            double level;
            public Geometry Shape;

            public Rain(DigitalRain owner) : base(owner.el)
            {
                this.owner = owner;
                IsHitTestVisible = false;
            }

            public void Show(double level)
            {
                this.level = level;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                if (level <= 0 || Shape == null) return;
                var o = owner;
                var size = AdornedElement.RenderSize;
                dc.PushClip(new RectangleGeometry(new Rect(size)));
                dc.PushClip(Shape);
                dc.PushOpacity(level);
                for (int c = 0; c < o.cols; c++)
                {
                    int top = (int)Math.Floor(o.head[c]), len = o.length[c];
                    for (int k = 0; k <= len; k++)
                    {
                        int r = top - k;
                        if (r < 0) break;
                        if (r >= o.rows) continue;
                        int shade = k == 0 ? 0 : k <= len * 0.3 ? 1 : k <= len * 0.6 ? 2 : k <= len * 0.85 ? 3 : 4;
                        dc.DrawImage(o.glyphs[shade][o.cell[c * o.rows + r]],
                            new Rect(c * CellW - Glow, r * CellH - Glow, CellW + 2 * Glow, CellH + 2 * Glow));
                    }
                }
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }
    }
}
