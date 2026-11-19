using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>
    /// The "glass" open / close animation. Opening, a window comes in behind a light frosted veil that clears, a
    /// glossy sheen sweeps across it like light catching clean glass, and a little glint sparkles at the end.
    /// Closing, cracks run out from a point of impact, then the window breaks apart in a wave spreading out from it:
    /// each piece tumbles and falls away from you in perspective, darkening as it goes and flashing when it catches
    /// the light, with a burst of fine glass dust. Drawn on top, in the window's adorner layer.
    /// </summary>
    internal class Glass : IOverlayAnimation
    {
        const double Gravity = 1100;   // screen units per second per second
        const double Crack = 0.14;     // the share of closing spent on the cracks spreading, before anything falls
        const int Steps = 16;          // transparency steps, so no piece needs a transparency layer of its own
        static readonly Random rnd = new Random();
        static readonly Brush Veil = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0xF4, 0xF8, 0xFF)));
        static readonly Brush Gloss = MakeGloss();
        static readonly Brush[] Whites = Levels(Color.FromRgb(0xFF, 0xFF, 0xFF), 0.85);   // a piece catching the light
        static readonly Brush[] Shades = Levels(Color.FromRgb(0x00, 0x02, 0x08), 0.7);    // a piece falling into shadow
        static readonly Brush[] Dusts = Levels(Color.FromRgb(0xF2, 0xF8, 0xFF), 1);
        static readonly Pen CrackShade = MakePen(Color.FromArgb(0x60, 0x00, 0x00, 0x00), 2.4);
        static readonly Pen CrackLight = MakePen(Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF), 1.1);

        readonly FrameworkElement el;
        readonly AdornerLayer layer;
        readonly Pane pane;
        readonly bool opening;
        readonly double ms, width, height;
        readonly Action done;
        readonly Stopwatch clock = Stopwatch.StartNew();
        bool running;
        int frame;

        // Closing only.
        Brush[] glass;          // the picture of the window with its cracks drawn in, at each transparency
        Point impact;
        double reach, depth;    // how far the cracks run, and how far you are from the glass (for perspective)
        readonly List<Shard> shards = new List<Shard>();
        readonly List<Speck> dust = new List<Speck>();

        class Shard
        {
            public Geometry Shape, Lit;   // the piece, and the part of it that's glass (for the light and shade on it)
            public Point Center;
            public double Delay, Vx, Vy, Vz, Spin, Tumble, Phase;
            public double Z, Alpha, Shine, Dark;   // this frame
            public Matrix Place;                    // this frame
        }

        class Speck
        {
            public double Vx, Vy, Vz, Size, Life;
        }

        /// <summary>How long the whole animation takes at a speed, in milliseconds.</summary>
        public static int Duration(bool opening, int speed) { return ElementAnimator.Ms(opening ? 900 : 1100, speed); }

        /// <summary>Starts the animation on an element; null when it can't be shown (no layer to draw on, or no size yet).</summary>
        public static Glass Start(FrameworkElement el, bool opening, int speed, Action done)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return null;
            el.UpdateLayout();
            if (el.ActualWidth < 2 || el.ActualHeight < 2) return null;
            return new Glass(el, layer, opening, speed, done);
        }

        Glass(FrameworkElement el, AdornerLayer layer, bool opening, int speed, Action done)
        {
            this.el = el;
            this.layer = layer;
            this.opening = opening;
            this.done = done;
            ms = Duration(opening, speed);
            width = el.ActualWidth;
            height = el.ActualHeight;
            pane = new Pane(this);
            pane.Shape = TvStatic.PanelShape(el);
            if (!opening) Break(pane.Shape);
            layer.Add(pane);
            running = true;
            Step();   // the very first frame on screen is already the start of the animation
            CompositionTarget.Rendering += OnRendering;
        }

        // ---------- breaking ----------

        /// <summary>
        /// Breaks the window the way a pane of glass breaks: cracks running out from the point of impact, crossed by
        /// uneven rings, small pieces near the impact and bigger ones further out. The cracks are drawn into one
        /// picture of the window, made once, so each piece carries its own edges without redrawing them every frame.
        /// </summary>
        void Break(Geometry panels)
        {
            impact = PickImpact(panels);
            foreach (var corner in new[] { new Point(0, 0), new Point(width, 0), new Point(0, height), new Point(width, height) })
                reach = Math.Max(reach, (corner - impact).Length);
            depth = Math.Max(width, height) * 0.9;
            var pieces = Pieces();

            // The window's picture with the cracks in it.
            var picture = WindowFlip.Picture(el, width, height);
            var outlines = new List<Geometry>();
            foreach (var p in pieces) outlines.Add(Polygon(p));
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawImage(picture, new Rect(0, 0, width, height));
                dc.PushClip(panels);
                foreach (var g in outlines) dc.DrawGeometry(null, CrackShade, g);
                foreach (var g in outlines) dc.DrawGeometry(null, CrackLight, g);
                dc.Pop();
            }
            var cracked = new RenderTargetBitmap(picture.PixelWidth, picture.PixelHeight, picture.DpiX, picture.DpiY, PixelFormats.Pbgra32);
            cracked.Render(dv);
            cracked.Freeze();
            glass = new Brush[Steps + 1];
            for (int i = 0; i <= Steps; i++)
                glass[i] = Frozen(new ImageBrush(cracked)
                {
                    ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, width, height), Stretch = Stretch.Fill,
                    Opacity = i / (double)Steps
                });

            // How each piece will go: the ones near the impact first and fastest, outward and a little up, then falling.
            var area = panels.Bounds;
            for (int i = 0; i < pieces.Count; i++)
            {
                var shape = outlines[i];
                var box = shape.Bounds;
                if (!box.IntersectsWith(area)) continue;
                bool inside = true;
                foreach (var pt in pieces[i]) if (!panels.FillContains(pt)) { inside = false; break; }
                Geometry lit = shape;
                if (!inside)
                {
                    // Only the glass part of an edge piece takes the light and shade, not the clear space around the panels.
                    lit = Geometry.Combine(panels, shape, GeometryCombineMode.Intersect, null).GetFlattenedPathGeometry(0.3, ToleranceType.Absolute);
                    if (lit.Bounds.IsEmpty) lit = null; else lit.Freeze();
                }
                var center = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
                var away = center - impact;
                double near = 1 - Math.Min(1, away.Length / reach);
                if (away.Length < 1) away = new Vector(0, -1);
                away.Normalize();
                double speed = 50 + 230 * near + rnd.NextDouble() * 40;
                shards.Add(new Shard
                {
                    Shape = shape, Lit = lit, Center = center,
                    Delay = 0.16 * (1 - near) + rnd.NextDouble() * 0.04,
                    Vx = away.X * speed + (rnd.NextDouble() - 0.5) * 30,
                    Vy = away.Y * speed - 30 - rnd.NextDouble() * 50,
                    Vz = depth * (1.2 + rnd.NextDouble() * 1.4),
                    Spin = (rnd.NextDouble() - 0.5) * 260,
                    Tumble = 3 + rnd.NextDouble() * 7,
                    Phase = rnd.NextDouble() * Math.PI * 2
                });
            }

            // Fine glass dust bursting from the impact.
            int specks = (int)Math.Max(12, Math.Min(60, width * height / 12000));
            for (int i = 0; i < specks; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, v = 150 + rnd.NextDouble() * 500;
                dust.Add(new Speck
                {
                    Vx = Math.Cos(a) * v, Vy = Math.Sin(a) * v - 60, Vz = depth * (0.5 + rnd.NextDouble() * 1.5),
                    Size = 1 + rnd.NextDouble() * 1.8, Life = 0.3 + rnd.NextDouble() * 0.4
                });
            }
        }

        /// <summary>A point of impact somewhere in the middle, on the glass (not in a gap between panels).</summary>
        Point PickImpact(Geometry panels)
        {
            for (int i = 0; i < 16; i++)
            {
                var pt = new Point(width * (0.3 + rnd.NextDouble() * 0.4), height * (0.3 + rnd.NextDouble() * 0.4));
                if (panels.FillContains(pt)) return pt;
            }
            var b = panels.Bounds;
            return b.IsEmpty ? new Point(width / 2, height / 2) : new Point(b.X + b.Width / 2, b.Y + b.Height / 2);
        }

        /// <summary>The corners of every piece: spokes from the impact, crossed by rings that get further apart going out.</summary>
        List<Point[]> Pieces()
        {
            int rays = 14 + rnd.Next(5);
            int rings = Math.Max(3, Math.Min(7, (int)Math.Round(Math.Max(width, height) / 95)));
            double step = 2 * Math.PI / rays;
            var angle = new double[rays];
            for (int i = 0; i < rays; i++) angle[i] = i * step + (rnd.NextDouble() - 0.5) * step * 0.55;
            var at = new Point[rays, rings + 1];
            for (int i = 0; i < rays; i++)
                for (int k = 0; k <= rings; k++)
                {
                    if (k == 0) { at[i, k] = impact; continue; }
                    double r = k == rings ? reach * 1.3 : reach * Math.Pow(k / (double)rings, 1.6) * (0.8 + rnd.NextDouble() * 0.4);
                    double a = angle[i] + (k == rings ? 0 : (rnd.NextDouble() - 0.5) * step * 0.3);
                    at[i, k] = new Point(impact.X + r * Math.Cos(a), impact.Y + r * Math.Sin(a));
                }
            var list = new List<Point[]>();
            for (int i = 0; i < rays; i++)
            {
                int j = (i + 1) % rays;
                for (int k = 0; k < rings; k++)
                {
                    if (k == 0) { list.Add(new[] { at[i, 1], at[j, 1], impact }); continue; }
                    Point a = at[i, k], b = at[j, k], c = at[j, k + 1], d = at[i, k + 1];
                    double roll = rnd.NextDouble();
                    if (roll < 0.35) list.Add(new[] { a, b, c, d });
                    else if (roll < 0.7) { list.Add(new[] { a, b, c }); list.Add(new[] { a, c, d }); }
                    else { list.Add(new[] { a, b, d }); list.Add(new[] { b, c, d }); }
                }
            }
            return list;
        }

        static Geometry Polygon(Point[] corners)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(corners[0], true, true);
                for (int i = 1; i < corners.Length; i++) c.LineTo(corners[i], true, false);
            }
            g.Freeze();
            return g;
        }

        // ---------- every frame ----------

        void OnRendering(object sender, EventArgs e)
        {
            if (running) Step();
        }

        void Step()
        {
            double p = Math.Min(1, clock.Elapsed.TotalMilliseconds / ms);
            if (opening)
            {
                el.Opacity = Ramp(p, 0, 0.3);   // the window comes in behind the veil
                if (frame % 6 == 0) pane.Shape = TvStatic.PanelShape(el);
            }
            else
            {
                el.Opacity = p < Crack ? 1 : 0;   // the cracked picture stands in once it starts to fall apart
                Move(p);
            }
            frame++;
            pane.Show(p);
            if (p >= 1) Finish();
        }

        /// <summary>Where each piece is now: thrown out, falling, turning over, and going away from you.</summary>
        void Move(double p)
        {
            double flight = Math.Max(0, (p - Crack) * ms / 1000);
            double end = 1 - Ramp(p, 0.86, 1);
            foreach (var s in shards)
            {
                double t = Math.Max(0, flight - s.Delay);
                double x = s.Vx * t, y = s.Vy * t + 0.5 * Gravity * t * t, z = s.Vz * t;
                double f = depth / (depth + z);
                var m = Matrix.Identity;
                m.ScaleAt(Math.Max(0.06, Math.Abs(Math.Cos(s.Tumble * t))), 1, s.Center.X, s.Center.Y);   // turning over
                m.RotateAt(s.Spin * t, s.Center.X, s.Center.Y);
                m.Translate(x, y);
                m.ScaleAt(f, f, width / 2, height / 2);                                                  // further away
                s.Place = m;
                s.Z = z;
                s.Alpha = Math.Max(0, Math.Min(1, (f - 0.32) / 0.38)) * end;
                s.Shine = t > 0 ? 0.9 * Math.Pow(Math.Max(0, Math.Sin(s.Tumble * t + s.Phase)), 16) : 0;
                s.Dark = 0.9 * (1 - f);
            }
            shards.Sort((a, b) => b.Z.CompareTo(a.Z));   // the furthest drawn first
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
            layer.Remove(pane);
        }

        // ---------- helpers ----------

        /// <summary>0 before "from", 1 after "to", smoothly in between.</summary>
        static double Ramp(double p, double from, double to)
        {
            double t = Math.Max(0, Math.Min(1, (p - from) / (to - from)));
            return t * t * (3 - 2 * t);
        }

        static int Level(double a) { return (int)Math.Round(Math.Max(0, Math.Min(1, a)) * Steps); }

        static Brush Frozen(Brush b)
        {
            b.Freeze();
            return b;
        }

        static Brush[] Levels(Color c, double most)
        {
            var list = new Brush[Steps + 1];
            for (int i = 0; i <= Steps; i++) list[i] = Frozen(new SolidColorBrush(Color.FromArgb((byte)(255 * most * i / Steps), c.R, c.G, c.B)));
            return list;
        }

        static Pen MakePen(Color c, double thickness)
        {
            var p = new Pen(new SolidColorBrush(c), thickness) { LineJoin = PenLineJoin.Round };
            p.Freeze();
            return p;
        }

        /// <summary>A soft reflection across the top of the glass.</summary>
        static Brush MakeGloss()
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF), 0.45));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.5));
            return Frozen(b);
        }

        /// <summary>The bright band of the sheen, centered at "at" along the diagonal (0 = top left, 1 = bottom right).</summary>
        static Brush Sheen(double at)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            Action<double, byte> stop = (o, a) => b.GradientStops.Add(new GradientStop(Color.FromArgb(a, 0xFF, 0xFF, 0xFF), o));
            stop(at - 0.34, 0); stop(at - 0.29, 0x50); stop(at - 0.27, 0);   // a thin line just behind it
            stop(at - 0.16, 0); stop(at, 0xA8); stop(at + 0.16, 0);          // the main band
            return Frozen(b);
        }

        /// <summary>A small four-pointed glint of light.</summary>
        static void Glint(DrawingContext dc, Point c, double size, double strength)
        {
            if (size < 1 || strength <= 0) return;
            dc.PushOpacity(strength);
            var halo = new RadialGradientBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0, 0xFF, 0xFF, 0xFF));
            dc.DrawEllipse(halo, null, c, size * 0.35, size * 0.35);
            var across = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            across.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 0));
            across.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0.5));
            across.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 1));
            var down = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1), GradientStops = across.GradientStops.Clone() };
            dc.DrawRectangle(across, null, new Rect(c.X - size, c.Y - 0.9, size * 2, 1.8));
            dc.DrawRectangle(down, null, new Rect(c.X - 0.9, c.Y - size * 0.7, 1.8, size * 1.4));
            dc.Pop();
        }

        /// <summary>Draws the shimmer, or the cracks, the falling pieces and the dust, over the element.</summary>
        class Pane : Adorner
        {
            readonly Glass owner;
            double p;
            public Geometry Shape;

            public Pane(Glass owner) : base(owner.el)
            {
                this.owner = owner;
                IsHitTestVisible = false;
            }

            public void Show(double p)
            {
                this.p = p;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                if (owner.opening) Shimmer(dc); else Shatter(dc);
            }

            void Shimmer(DrawingContext dc)
            {
                if (Shape == null) return;
                var all = new Rect(AdornedElement.RenderSize);
                dc.PushClip(new RectangleGeometry(all));
                dc.PushClip(Shape);
                double veil = 1 - Ramp(p, 0.05, 0.5);                        // the frost clearing
                if (veil > 0) { dc.PushOpacity(veil); dc.DrawRectangle(Veil, null, all); dc.Pop(); }
                double gloss = Ramp(p, 0.15, 0.45) * (1 - Ramp(p, 0.7, 1));  // the reflection, coming and going
                if (gloss > 0) { dc.PushOpacity(gloss); dc.DrawRectangle(Gloss, null, all); dc.Pop(); }
                if (p > 0.15 && p < 0.9)                                      // the sheen sweeping across
                    dc.DrawRectangle(Sheen(-0.35 + (p - 0.15) / 0.75 * 1.7), null, all);
                double g = p < 0.72 ? 0 : Math.Sin(Math.PI * Math.Min(1, (p - 0.72) / 0.28));
                Glint(dc, new Point(all.Width - Math.Min(40, all.Width * 0.2), Math.Min(26, all.Height * 0.3)), 18 * g, g);
                dc.Pop();
                dc.Pop();
            }

            void Shatter(DrawingContext dc)
            {
                var o = owner;
                var all = new Rect(0, 0, o.width, o.height);
                if (p < Crack)
                {
                    // The cracks running out from the impact, over the window itself.
                    double q = p / Crack, r = o.reach * 1.05 * (1 - (1 - q) * (1 - q));
                    dc.PushClip(new EllipseGeometry(o.impact, r, r));
                    dc.DrawRectangle(o.glass[Steps], null, all);
                    dc.Pop();
                }
                else
                {
                    foreach (var s in o.shards)
                    {
                        int level = Level(s.Alpha);
                        if (level == 0) continue;
                        dc.PushTransform(new MatrixTransform(s.Place));
                        dc.DrawGeometry(o.glass[level], null, s.Shape);
                        if (s.Lit != null)
                        {
                            if (s.Shine > s.Dark) { int l = Level(s.Shine * s.Alpha); if (l > 0) dc.DrawGeometry(Whites[l], null, s.Lit); }
                            else { int l = Level(s.Dark * s.Alpha); if (l > 0) dc.DrawGeometry(Shades[l], null, s.Lit); }
                        }
                        dc.Pop();
                    }
                    // The dust, in front.
                    double flight = (p - Crack) * o.ms / 1000;
                    foreach (var d in o.dust)
                    {
                        if (flight >= d.Life) continue;
                        double x = o.impact.X + d.Vx * flight, y = o.impact.Y + d.Vy * flight + 0.5 * Gravity * flight * flight;
                        double f = o.depth / (o.depth + d.Vz * flight);
                        double sx = o.width / 2 + (x - o.width / 2) * f, sy = o.height / 2 + (y - o.height / 2) * f, size = d.Size * f;
                        int l = Level(1 - flight / d.Life);
                        if (l > 0) dc.DrawRectangle(Dusts[l], null, new Rect(sx - size / 2, sy - size / 2, size, size));
                    }
                }
                double flash = Ramp(p, 0, 0.03) * (1 - Ramp(p, 0.05, 0.3));
                if (flash > 0) Glint(dc, o.impact, 46, flash);
            }
        }
    }
}
