using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The Magic 8 Ball in the monitor's window: a black pool ball with its white 8. Clicked, it shakes and spins for a few
    /// seconds and comes to rest with its window turned to you (a dark blue disc with the answer's triangle in it), and asks
    /// for an answer; when the answer is over it turns back to its 8. It's drawn as a sphere, so the 8 slides round it
    /// and gets narrower toward the edge as the ball spins.
    /// </summary>
    internal class EightBall : FrameworkElement
    {
        const double Radius = 17.5;
        public const double BallRadius = Radius;
        const double SpinSeconds = 2.8, TurnSeconds = 0.9;
        const double RestPitch = 0.16;   // the 8 tipped a little toward the top, the way a ball sits
        enum Phase { Still, Spinning, Answering, TurningBack }

        Phase phase = Phase.Still;
        readonly Stopwatch clock = new Stopwatch();
        double yaw, pitch = RestPitch, roll, shakeX, shakeY, startYaw, endYaw;
        bool hooked;

        /// <summary>The spin is over and the ball's window is turned to you: time for an answer.</summary>
        public event Action Asked;

        /// <summary>It's been clicked and starts to spin.</summary>
        public event Action Shaken;

        public EightBall()
        {
            Cursor = Cursors.Hand;
            ToolTip = "Magic 8 Ball: ask it a yes-or-no question, then click it";
            ClipToBounds = true;   // shaking, it stays inside the monitor's window
            MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            MouseLeftButtonUp += (s, e) => { e.Handled = true; Shake(); };
            Unloaded += delegate { Stop(); };
        }

        /// <summary>Test copies: what it's doing.</summary>
        public string State { get { return phase.ToString().ToLowerInvariant(); } }

        /// <summary>Shakes and spins it (not while it's spinning already); asked again while answering, it spins again.</summary>
        public void Shake()
        {
            if (phase == Phase.Spinning) return;
            startYaw = yaw;
            // Three whole turns and then some, to come to rest with the window (the far side from the 8) facing you.
            double toWindow = ((Math.PI - startYaw) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            endYaw = startYaw + 6 * Math.PI + toWindow;
            phase = Phase.Spinning;
            Run();
            var h = Shaken;
            if (h != null) h();
        }

        /// <summary>Turned to its window at once, still, as though it had just answered (its answer is still on the strip).</summary>
        public void FaceAnswer()
        {
            if (phase != Phase.Still) return;
            yaw = Math.PI;
            phase = Phase.Answering;
            InvalidateVisual();
        }

        /// <summary>Turns it back to its 8 once the answer is over.</summary>
        public void TurnBack()
        {
            if (phase != Phase.Answering) return;
            startYaw = yaw;
            endYaw = yaw + Math.PI;
            phase = Phase.TurningBack;
            Run();
        }

        void Run()
        {
            clock.Restart();
            if (!hooked) { CompositionTarget.Rendering += OnFrame; hooked = true; }
        }

        void Stop()
        {
            if (hooked) { CompositionTarget.Rendering -= OnFrame; hooked = false; }
        }

        void OnFrame(object sender, EventArgs e)
        {
            double t = clock.Elapsed.TotalSeconds;
            if (phase == Phase.Spinning)
            {
                double u = Math.Min(1, t / SpinSeconds), calm = 1 - u, ease = 1 - calm * calm * calm;
                yaw = startYaw + (endYaw - startYaw) * ease;
                pitch = RestPitch + 0.55 * Math.Sin(t * 8.5) * calm;
                roll = 0.45 * Math.Sin(t * 6.3) * calm;
                shakeX = 3.6 * Math.Sin(t * 41) * calm * calm;
                shakeY = 1.8 * Math.Sin(t * 33 + 1) * calm;
                if (u >= 1)
                {
                    yaw = endYaw; pitch = RestPitch; roll = shakeX = shakeY = 0;
                    phase = Phase.Answering;
                    Stop();
                    var h = Asked;
                    if (h != null) h();
                }
            }
            else if (phase == Phase.TurningBack)
            {
                double u = Math.Min(1, t / TurnSeconds), ease = u * u * (3 - 2 * u);
                yaw = startYaw + (endYaw - startYaw) * ease;
                if (u >= 1) { yaw = 0; phase = Phase.Still; Stop(); }
            }
            else Stop();
            InvalidateVisual();
        }

        /// <summary>Only the ball itself can be clicked, not the window around it.</summary>
        protected override HitTestResult HitTestCore(PointHitTestParameters p)
        {
            var c = new Point(ActualWidth / 2, ActualHeight / 2);
            return (p.HitPoint - c).Length <= Radius + 1.5 ? new PointHitTestResult(this, p.HitPoint) : null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var c = new Point(ActualWidth / 2 + shakeX, ActualHeight / 2 + shakeY);
            var sphere = new EllipseGeometry(c, Radius, Radius);
            dc.DrawGeometry(Body, null, sphere);
            dc.PushClip(sphere);
            double cp = Math.Cos(pitch), dx = Math.Sin(yaw) * cp, dy = Math.Sin(pitch), dz = Math.Cos(yaw) * cp;
            Disc(dc, c, dx, dy, dz, true);       // the 8
            Disc(dc, c, -dx, -dy, -dz, false);   // the window, on the far side
            dc.Pop();
            // The gloss: a soft sheen and a sharp highlight, from a light above and to the left.
            dc.DrawEllipse(Sheen, null, new Point(c.X - Radius * 0.3, c.Y - Radius * 0.44), Radius * 0.5, Radius * 0.3);
            dc.DrawEllipse(Spark, null, new Point(c.X - Radius * 0.38, c.Y - Radius * 0.5), Radius * 0.14, Radius * 0.08);
            dc.DrawGeometry(null, Edge, sphere);
        }

        /// <summary>
        /// A disc on the ball's surface facing the way (dx, dy, dz) points (z toward you): seen at an angle it's an ellipse,
        /// narrower the further round it is, and darker toward the edge.
        /// </summary>
        void Disc(DrawingContext dc, Point c, double dx, double dy, double dz, bool eight)
        {
            if (dz <= 0.02) return;
            const double share = 0.47;   // its radius, as a share of the ball's
            double rf = Radius * share, inset = Math.Sqrt(1 - share * share);
            var m = Matrix.Identity;
            double toward = Math.Atan2(-dy, dx) * 180 / Math.PI;
            m.Rotate(roll * 180 / Math.PI);
            m.Rotate(-toward);
            m.Scale(dz, 1);
            m.Rotate(toward);
            m.Translate(c.X + Radius * inset * dx, c.Y - Radius * inset * dy);
            dc.PushTransform(new MatrixTransform(m));
            if (eight)
            {
                dc.DrawEllipse(Face, null, new Point(0, 0), rf, rf);
                var digit = new FormattedText("8", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Digit, rf * 1.45, Ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(digit, new Point(-digit.Width / 2, -digit.Height / 2 - rf * 0.04));
            }
            else
            {
                dc.DrawEllipse(WindowFill, WindowRim, new Point(0, 0), rf, rf);
                var tri = new StreamGeometry();
                using (var g = tri.Open())
                {
                    g.BeginFigure(new Point(0, -rf * 0.55), true, true);
                    g.LineTo(new Point(rf * 0.5, rf * 0.33), true, false);
                    g.LineTo(new Point(-rf * 0.5, rf * 0.33), true, false);
                }
                tri.Freeze();
                dc.DrawGeometry(Triangle, null, tri);
            }
            if (dz < 0.98) dc.DrawEllipse(Solid(Color.FromArgb((byte)(150 * (1 - dz)), 0, 0, 0)), null, new Point(0, 0), rf + 0.3, rf + 0.3);
            dc.Pop();
        }

        static readonly Typeface Digit = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        static readonly Brush Body = Radial(new Point(0.36, 0.3), "#4a4e55", 0, "#16171a", 0.45, "#050506", 1);
        static readonly Brush Face = Radial(new Point(0.4, 0.35), "#ffffff", 0, "#f2f1ea", 0.6, "#cfcdc4", 1);
        static readonly Brush WindowFill = Radial(new Point(0.5, 0.45), "#0d2257", 0, "#081536", 0.7, "#040a1c", 1);
        static readonly Brush Ink = Solid(Color.FromRgb(14, 14, 16));
        static readonly Brush Triangle = Solid(Color.FromArgb(210, 36, 74, 170));
        static readonly Pen WindowRim = Frozen(new Pen(Solid(Color.FromRgb(24, 30, 44)), 0.8));
        static readonly Pen Edge = Frozen(new Pen(Solid(Color.FromArgb(90, 0, 0, 0)), 0.8));
        static readonly Brush Sheen = Radial(new Point(0.5, 0.5), "#55ffffff", 0, "#00ffffff", 1);
        static readonly Brush Spark = Radial(new Point(0.5, 0.5), "#eeffffff", 0, "#00ffffff", 1);

        static Brush Radial(Point origin, params object[] stops)
        {
            var b = new RadialGradientBrush { GradientOrigin = origin };
            for (int i = 0; i < stops.Length; i += 2)
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString((string)stops[i]), Convert.ToDouble(stops[i + 1], CultureInfo.InvariantCulture)));
            b.Freeze();
            return b;
        }

        static Brush Solid(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        static Pen Frozen(Pen p) { p.Freeze(); return p; }
    }
}
