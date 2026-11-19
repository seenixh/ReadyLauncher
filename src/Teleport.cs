using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The "beam" open / close animation, like a science-fiction transporter. Closing, thin threads of gold light run
    /// down through a window, the window washes out into a glowing column of light, and the light fades away.
    /// Opening, the threads appear first, light gathers into a glowing column, the window forms inside it, and the
    /// glow clears with the threads lingering a moment. Drawn on top, in the window's adorner layer, in its panels' shape.
    /// </summary>
    internal class Teleport : IOverlayAnimation
    {
        const double Spacing = 9;   // about one thread of light every this many screen units
        const int Levels = 6;       // brightness steps, so no thread needs its own transparency
        static readonly Random rnd = new Random();
        static readonly Brush[] Glows = MakeGlows();
        static readonly Brush[][] Cores = MakeCores();
        static readonly Brush Column = MakeColumn();
        static readonly Brush Middle = MakeMiddle();
        static readonly Brush Bright = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xEA)));   // warm white

        readonly FrameworkElement el;
        readonly AdornerLayer layer;
        readonly Light light;
        readonly bool opening;
        readonly double ms;
        readonly Action done;
        readonly Stopwatch clock = Stopwatch.StartNew();
        // Each thread of light: where it runs, how bright it is at most, how it flickers, how thick it is.
        readonly double[] x, brightness, phase, rate, thickness;
        readonly int[] shape;
        bool running;
        int frame;

        /// <summary>How long the whole animation takes at a speed, in milliseconds.</summary>
        public static int Duration(bool opening, int speed) { return ElementAnimator.Ms(opening ? 1300 : 1100, speed); }

        /// <summary>Starts the animation on an element; null when it can't be shown (no layer to draw on, or no size yet).</summary>
        public static Teleport Start(FrameworkElement el, bool opening, int speed, Action done)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return null;
            el.UpdateLayout();
            if (el.ActualWidth < 2 || el.ActualHeight < 2) return null;
            return new Teleport(el, layer, opening, speed, done);
        }

        Teleport(FrameworkElement el, AdornerLayer layer, bool opening, int speed, Action done)
        {
            this.el = el;
            this.layer = layer;
            this.opening = opening;
            this.done = done;
            ms = Duration(opening, speed);

            int n = Math.Max(2, (int)(el.ActualWidth / Spacing));
            x = new double[n]; brightness = new double[n]; phase = new double[n]; rate = new double[n]; thickness = new double[n];
            shape = new int[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = (i + 0.5) * el.ActualWidth / n + (rnd.NextDouble() - 0.5) * Spacing * 0.7;
                brightness[i] = 0.35 + rnd.NextDouble() * 0.65;
                phase[i] = rnd.NextDouble() * Math.PI * 2;
                rate[i] = 4 + rnd.NextDouble() * 9;            // flickers a few times a second
                thickness[i] = 1 + Math.Pow(rnd.NextDouble(), 2) * 2;
                shape[i] = rnd.Next(Cores[0].Length);
            }

            light = new Light(this);
            layer.Add(light);
            running = true;
            Step();   // the very first frame on screen is already the start of the animation
            CompositionTarget.Rendering += OnRendering;
        }

        void OnRendering(object sender, EventArgs e)
        {
            if (running) Step();
        }

        void Step()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double p = Math.Min(1, now / ms);
            double threads, glow, content;
            if (opening)
            {
                threads = Ramp(p, 0, 0.2) * (1 - Ramp(p, 0.75, 1));      // first in, last out
                glow = Ramp(p, 0.1, 0.4) * (1 - Ramp(p, 0.6, 0.95));     // the column of light
                content = Ramp(p, 0.38, 0.6);                             // the window forming inside it
            }
            else
            {
                threads = Ramp(p, 0, 0.2) * (1 - Ramp(p, 0.7, 1));
                glow = Ramp(p, 0.15, 0.5) * (1 - Ramp(p, 0.65, 0.95));
                content = 1 - Ramp(p, 0.45, 0.62);                        // the window washing out inside the light
            }
            el.Opacity = content;
            if (frame++ % 6 == 0) light.Shape = TvStatic.PanelShape(el);
            light.Show(threads, glow, now / 1000);
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
            layer.Remove(light);
        }

        /// <summary>0 before "from", 1 after "to", smoothly in between.</summary>
        static double Ramp(double p, double from, double to)
        {
            double t = Math.Max(0, Math.Min(1, (p - from) / (to - from)));
            return t * t * (3 - 2 * t);
        }

        static Brush Frozen(Brush b)
        {
            b.Freeze();
            return b;
        }

        /// <summary>The soft gold haze around a thread, strongest at its middle, in each brightness.</summary>
        static Brush[] MakeGlows()
        {
            var list = new Brush[Levels];
            for (int l = 0; l < Levels; l++)
            {
                byte a = (byte)(110 * (l + 1) / Levels);
                var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xC8, 0x60), 0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(a, 0xFF, 0xC8, 0x60), 0.5));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xC8, 0x60), 1));
                list[l] = Frozen(b);
            }
            return list;
        }

        /// <summary>The bright line of a thread, fading out toward its ends (three ways), in each brightness.</summary>
        static Brush[][] MakeCores()
        {
            var ends = new[] { new[] { 0.04, 0.96 }, new[] { 0.15, 0.85 }, new[] { 0.0, 0.7 } };
            var list = new Brush[Levels][];
            for (int l = 0; l < Levels; l++)
            {
                byte a = (byte)(255 * (l + 1) / Levels);
                list[l] = new Brush[ends.Length];
                for (int k = 0; k < ends.Length; k++)
                {
                    var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xF4, 0xD6), 0));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(a, 0xFF, 0xF4, 0xD6), ends[k][0] + 0.1));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(a, 0xFF, 0xF8, 0xE8), (ends[k][0] + ends[k][1]) / 2));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(a, 0xFF, 0xF4, 0xD6), ends[k][1] - 0.05));
                    b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xF4, 0xD6), Math.Min(1, ends[k][1] + 0.05)));
                    list[l][k] = Frozen(b);
                }
            }
            return list;
        }

        /// <summary>The column of light: deep gold at the sides, almost white down the middle.</summary>
        static Brush MakeColumn()
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xC8, 0x92, 0x2E), 0));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xF2, 0xCC, 0x78), 0.3));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xF2, 0xD4), 0.5));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xF2, 0xCC, 0x78), 0.7));
            b.GradientStops.Add(new GradientStop(Color.FromRgb(0xC8, 0x92, 0x2E), 1));
            return Frozen(b);
        }

        /// <summary>Extra brightness halfway down, where the light is strongest.</summary>
        static Brush MakeMiddle()
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFB, 0xF0), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x90, 0xFF, 0xFB, 0xF0), 0.5));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFB, 0xF0), 1));
            return Frozen(b);
        }

        /// <summary>Soft upright streaks in the light that drift sideways, so the column shimmers.</summary>
        static Brush Streaks(double time)
        {
            double at = time * 45 % 34;
            var b = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute, SpreadMethod = GradientSpreadMethod.Reflect,
                StartPoint = new Point(at, 0), EndPoint = new Point(at + 17, 0)
            };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.6));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x60, 0xFF, 0xF8, 0xE6), 1));
            return Frozen(b);
        }

        /// <summary>Draws the column of light and the threads over the element, in the shape of its panels.</summary>
        class Light : Adorner
        {
            readonly Teleport owner;
            double threads, glow, time;
            public Geometry Shape;

            public Light(Teleport owner) : base(owner.el)
            {
                this.owner = owner;
                IsHitTestVisible = false;
            }

            public void Show(double threads, double glow, double time)
            {
                this.threads = threads;
                this.glow = glow;
                this.time = time;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                if ((threads <= 0 && glow <= 0) || Shape == null) return;
                var o = owner;
                var size = AdornedElement.RenderSize;
                var all = new Rect(size);
                dc.PushClip(new RectangleGeometry(all));
                dc.PushClip(Shape);
                if (glow > 0)
                {
                    // The gold goes first as the light fades and warm white lasts longer, so a window showing
                    // through it looks lit up rather than tinted.
                    dc.PushOpacity(glow * glow);
                    dc.DrawRectangle(Column, null, all);
                    dc.Pop();
                    double white = glow * (1 - glow * glow);
                    if (white > 0.01)
                    {
                        dc.PushOpacity(white);
                        dc.DrawRectangle(Bright, null, all);
                        dc.Pop();
                    }
                    dc.PushOpacity(glow);
                    dc.DrawRectangle(Middle, null, all);
                    dc.DrawRectangle(Streaks(time), null, all);
                    dc.Pop();
                }
                if (threads > 0)
                {
                    for (int i = 0; i < o.x.Length; i++)
                    {
                        // Each thread flickers, and they all grow brighter and thicker as the light builds.
                        double b = threads * o.brightness[i] * (0.55 + 0.45 * Math.Sin(o.phase[i] + time * o.rate[i]));
                        if (b < 0.06) continue;
                        int level = Math.Min(Levels - 1, (int)(b * Levels));
                        double core = o.thickness[i] * (1 + glow), haze = core * 6;
                        dc.DrawRectangle(Glows[level], null, new Rect(o.x[i] - haze / 2, 0, haze, size.Height));
                        dc.DrawRectangle(Cores[level][o.shape[i]], null, new Rect(o.x[i] - core / 2, 0, core, size.Height));
                    }
                }
                dc.Pop();
                dc.Pop();
            }
        }
    }
}
