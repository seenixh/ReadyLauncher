using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>
    /// The "TV static" open / close animation. Opening, a window's panels fill with flickering snow, like an old TV
    /// between channels, which then fades to show the window. Closing, the snow covers the window and fades away.
    /// The snow is drawn on top (in the window's adorner layer), in the shape of the window's rounded panels.
    /// </summary>
    internal class TvStatic : IOverlayAnimation
    {
        const int Tile = 256;        // noise pixels per side of one picture
        const double Grain = 2;      // size of one noise pixel, in screen units
        static BitmapSource[] pictures;
        static readonly Random rnd = new Random();

        readonly FrameworkElement el;
        readonly AdornerLayer layer;
        readonly Snow snow;
        readonly bool opening;
        readonly double ms;
        readonly Action done;
        readonly Stopwatch clock = Stopwatch.StartNew();
        bool running;
        int frame;

        /// <summary>Starts the animation on an element; null when there's no layer to draw the snow on.</summary>
        public static TvStatic Start(FrameworkElement el, bool opening, int speed, Action done)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return null;
            return new TvStatic(el, layer, opening, speed, done);
        }

        TvStatic(FrameworkElement el, AdornerLayer layer, bool opening, int speed, Action done)
        {
            this.el = el;
            this.layer = layer;
            this.opening = opening;
            this.done = done;
            ms = Duration(opening, speed);
            if (pictures == null) pictures = MakePictures(8);
            snow = new Snow(el);
            layer.Add(snow);
            running = true;
            Step();   // the very first frame on screen is already the start of the animation
            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>How long the whole animation takes at a speed, in milliseconds.</summary>
        public static int Duration(bool opening, int speed) { return ElementAnimator.Ms(opening ? 650 : 480, speed); }

        void OnRendering(object sender, EventArgs e)
        {
            if (running) Step();
        }

        void Step()
        {
            double p = Math.Min(1, clock.Elapsed.TotalMilliseconds / ms);
            double level;
            bool content;
            if (opening)
            {
                // The snow comes up out of nothing, holds while the window appears behind it, then clears.
                if (p < 0.2) { level = Smooth(p / 0.2); content = false; }
                else if (p < 0.4) { level = 1; content = true; }
                else { level = 1 - Smooth((p - 0.4) / 0.6); content = true; }
            }
            else
            {
                // The snow covers the window, holds while the window goes behind it, then fades away.
                if (p < 0.45) { level = Smooth(p / 0.45); content = true; }
                else if (p < 0.6) { level = 1; content = false; }
                else { level = 1 - Smooth((p - 0.6) / 0.4); content = false; }
            }
            el.Opacity = content ? 1 : 0;
            if (frame++ % 6 == 0) snow.Shape = PanelShape(el);
            snow.Show(level, NextPicture(), clock.Elapsed.TotalMilliseconds);
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
            layer.Remove(snow);
        }

        static double Smooth(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }

        /// <summary>One of the noise pictures, shifted to a random spot, so no two frames look alike.</summary>
        static Brush NextPicture()
        {
            double side = Tile * Grain;
            var b = new ImageBrush(pictures[rnd.Next(pictures.Length)])
            {
                TileMode = TileMode.Tile, Stretch = Stretch.Fill, ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(-rnd.Next(Tile) * Grain, -rnd.Next(Tile) * Grain, side, side)
            };
            RenderOptions.SetBitmapScalingMode(b, BitmapScalingMode.NearestNeighbor);   // crisp grains, not a blur
            b.Freeze();
            return b;
        }

        /// <summary>Grey noise, each line a little lighter or darker than the next, with now and then a bright streak.</summary>
        static BitmapSource[] MakePictures(int count)
        {
            var list = new BitmapSource[count];
            var px = new byte[Tile * Tile];
            for (int n = 0; n < count; n++)
            {
                for (int y = 0; y < Tile; y++)
                {
                    int line = rnd.Next(-26, 27);
                    if (rnd.Next(28) == 0) line += 55;
                    for (int x = 0; x < Tile; x++)
                        px[y * Tile + x] = (byte)Math.Max(0, Math.Min(255, rnd.Next(256) + line));
                }
                var bmp = BitmapSource.Create(Tile, Tile, 96, 96, PixelFormats.Gray8, null, px, Tile);
                bmp.Freeze();
                list[n] = bmp;
            }
            return list;
        }

        /// <summary>
        /// The window's visible panels: its rounded, filled borders (the launcher's panel, the strip, each sensor chip),
        /// so the snow keeps their shape instead of filling the see-through space around them.
        /// </summary>
        internal static Geometry PanelShape(FrameworkElement el)
        {
            var g = new GeometryGroup { FillRule = FillRule.Nonzero };
            Collect(el, el, g);
            if (g.Children.Count == 0) g.Children.Add(new RectangleGeometry(new Rect(0, 0, el.ActualWidth, el.ActualHeight)));
            g.Freeze();
            return g;
        }

        static void Collect(DependencyObject v, FrameworkElement el, GeometryGroup g)
        {
            var b = v as Border;
            if (b != null && b.IsVisible && Shapes.AnyRounding(b.CornerRadius) && Painted(b.Background) && b.ActualWidth > 0 && b.ActualHeight > 0)
            {
                try
                {
                    var r = b.TransformToAncestor(el).TransformBounds(new Rect(0, 0, b.ActualWidth, b.ActualHeight));
                    g.Children.Add(Shapes.Rounded(r, b.CornerRadius));   // each corner as it is (some may be square)
                    return;   // what's inside is covered already
                }
                catch (InvalidOperationException) { }
            }
            if (v is ScrollViewer) return;   // scrolled icons sit inside a panel found already
            int n = VisualTreeHelper.GetChildrenCount(v);
            for (int i = 0; i < n; i++) Collect(VisualTreeHelper.GetChild(v, i), el, g);
        }

        static bool Painted(Brush b)
        {
            if (b == null || b.Opacity <= 0) return false;
            var s = b as SolidColorBrush;
            return s == null || s.Color.A > 0;
        }

        /// <summary>Draws the snow over the element, in the shape of its panels.</summary>
        class Snow : Adorner
        {
            static readonly Brush Band = MakeBand();
            double level, time;
            Brush picture;
            public Geometry Shape;

            public Snow(UIElement el) : base(el)
            {
                IsHitTestVisible = false;
                RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            }

            public void Show(double level, Brush picture, double time)
            {
                this.level = level;
                this.picture = picture;
                this.time = time;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                if (level <= 0 || Shape == null) return;
                var size = AdornedElement.RenderSize;
                dc.PushClip(new RectangleGeometry(new Rect(size)));
                dc.PushClip(Shape);
                dc.PushOpacity(level);
                dc.DrawRectangle(picture, null, new Rect(size));
                // A soft dark band rolling down the picture, like a TV that's lost the signal.
                double h = Math.Max(30, size.Height * 0.22);
                double y = (time / 900.0 % 1) * (size.Height + h) - h;
                dc.DrawRectangle(Band, null, new Rect(0, y, size.Width, h));
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }

            static Brush MakeBand()
            {
                var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, 0, 0, 0), 0.5));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1));
                b.Freeze();
                return b;
            }
        }
    }
}
