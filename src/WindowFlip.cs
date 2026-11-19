using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ReadyLauncher
{
    /// <summary>
    /// The "flip" open / close animation. Closing, a window turns on its middle, like a card, until it's edge-on
    /// and gone; opening, it turns back to face you. While it turns, a picture of the window stands in for it,
    /// drawn in 3D on top (in the window's adorner layer).
    /// </summary>
    internal class WindowFlip : IOverlayAnimation
    {
        const double FieldOfView = 40;   // degrees: how strong the 3D depth looks
        readonly FrameworkElement el;
        readonly AdornerLayer layer;
        readonly Host host;
        readonly bool opening;
        readonly double ms, half;
        readonly Action done;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly AxisAngleRotation3D turn = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        readonly TranslateTransform3D back = new TranslateTransform3D();
        readonly AmbientLight light = new AmbientLight(Colors.White);
        bool running;

        /// <summary>How long the whole animation takes at a speed, in milliseconds.</summary>
        public static int Duration(bool opening, int speed) { return ElementAnimator.Ms(opening ? 460 : 380, speed); }

        /// <summary>Starts the animation on an element; null when it can't be shown (no layer to draw on, or no size yet).</summary>
        public static WindowFlip Start(FrameworkElement el, bool opening, int speed, Action done)
        {
            var layer = AdornerLayer.GetAdornerLayer(el);
            if (layer == null) return null;
            el.UpdateLayout();
            double w = el.ActualWidth, h = el.ActualHeight;
            if (w < 2 || h < 2) return null;
            return new WindowFlip(el, layer, Picture(el, w, h), w, h, opening, speed, done);
        }

        WindowFlip(FrameworkElement el, AdornerLayer layer, BitmapSource picture, double w, double h, bool opening, int speed, Action done)
        {
            this.el = el;
            this.layer = layer;
            this.opening = opening;
            this.done = done;
            ms = Duration(opening, speed);
            half = w / 2;

            // The picture on a flat card exactly the window's size, seen from just far enough away that, facing you,
            // it covers the window pixel for pixel.
            var card = new MeshGeometry3D
            {
                Positions = new Point3DCollection { new Point3D(-w / 2, h / 2, 0), new Point3D(w / 2, h / 2, 0), new Point3D(w / 2, -h / 2, 0), new Point3D(-w / 2, -h / 2, 0) },
                TextureCoordinates = new PointCollection { new Point(0, 0), new Point(1, 0), new Point(1, 1), new Point(0, 1) },
                TriangleIndices = new Int32Collection { 0, 3, 2, 0, 2, 1 }
            };
            var brush = new ImageBrush(picture);
            brush.Freeze();
            var model = new GeometryModel3D(card, new DiffuseMaterial(brush));
            var moves = new Transform3DGroup();
            moves.Children.Add(new RotateTransform3D(turn));
            moves.Children.Add(back);
            model.Transform = moves;
            var scene = new Model3DGroup();
            scene.Children.Add(light);
            scene.Children.Add(model);

            double distance = half / Math.Tan(FieldOfView / 2 * Math.PI / 180);
            var view = new Viewport3D
            {
                Camera = new PerspectiveCamera(new Point3D(0, 0, distance), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), FieldOfView),
                IsHitTestVisible = false
            };
            view.Children.Add(new ModelVisual3D { Content = scene });
            host = new Host(el, view);
            layer.Add(host);
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
            double p = Math.Min(1, clock.Elapsed.TotalMilliseconds / ms);
            // Closing speeds up as it turns away; opening slows down as it comes to face you.
            double angle = opening ? 90 * Math.Pow(1 - p, 3) : 90 * p * p;
            double rad = angle * Math.PI / 180;
            turn.Angle = angle;
            // Pushed back as it turns, so the edge that swings toward you never grows past the window's own edges.
            back.OffsetZ = -half * Math.Sin(rad);
            // A little darker as it turns away from the light.
            byte c = (byte)(255 - 105 * Math.Sin(rad));
            light.Color = Color.FromRgb(c, c, c);
            el.Opacity = 0;   // the picture stands in for the window while it turns
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
            layer.Remove(host);
        }

        /// <summary>A picture of the element as it looks now, at the screen's own resolution.</summary>
        internal static BitmapSource Picture(FrameworkElement el, double w, double h)
        {
            double sx = 1, sy = 1;
            var source = PresentationSource.FromVisual(el);
            if (source != null && source.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                sx = m.M11;
                sy = m.M22;
            }
            double was = el.Opacity;
            el.Opacity = 1;   // it may already be hidden, waiting to open
            try
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // The brush sees the element where it sits in its parent (the launcher's panel is inset by its
                    // shadow margin), so look at it there.
                    var at = VisualTreeHelper.GetOffset(el);
                    var look = new VisualBrush(el) { Viewbox = new Rect(at.X, at.Y, w, h), ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
                    dc.DrawRectangle(look, null, new Rect(0, 0, w, h));
                }
                var bmp = new RenderTargetBitmap((int)Math.Ceiling(w * sx), (int)Math.Ceiling(h * sy), 96 * sx, 96 * sy, PixelFormats.Pbgra32);
                bmp.Render(dv);
                bmp.Freeze();
                return bmp;
            }
            finally { el.Opacity = was; }
        }

        /// <summary>Holds the 3D view on top of the element, the element's own size.</summary>
        class Host : Adorner
        {
            readonly Viewport3D view;

            public Host(UIElement el, Viewport3D view) : base(el)
            {
                this.view = view;
                IsHitTestVisible = false;
                AddVisualChild(view);
            }

            protected override int VisualChildrenCount { get { return 1; } }
            protected override Visual GetVisualChild(int index) { return view; }

            protected override Size MeasureOverride(Size constraint)
            {
                view.Measure(AdornedElement.RenderSize);
                return AdornedElement.RenderSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                view.Arrange(new Rect(AdornedElement.RenderSize));
                return finalSize;
            }
        }
    }
}
