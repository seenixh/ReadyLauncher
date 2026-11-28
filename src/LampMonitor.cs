using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// A computer cabinet standing at one end of the lamp strip and rising above it: a charcoal case with a rolled top,
    /// a dark window with lamps blinking behind it (spaced well apart, fewer lit than on the strip) and a small board of
    /// tiny red LEDs flashing in the middle of it, and WOPR lettered below. It's drawn rather than a picture, so it's sharp at any screen scale, seen straight on like the strip.
    /// </summary>
    internal class LampMonitor : Grid
    {
        public const double CaseWidth = 130, CaseHeight = 140;
        const double RollDepth = 21;     // the rolled top, down to where it turns smoothly into the front
        const double TopCorner = 13;
        static readonly Rect Glass = new Rect(17, 29, 96, 46);   // the window on the front

        public LampPanel Lamps { get; private set; }

        /// <summary>The small board of red LEDs in the middle of its window.</summary>
        public LedBoard Board { get; private set; }

        /// <summary>The Magic 8 Ball in its window, or null.</summary>
        public EightBall Ball { get; private set; }

        /// <summary>Puts the Magic 8 Ball in its window, in front of the lamps, behind the glass.</summary>
        public EightBall AddBall()
        {
            if (Ball != null) return Ball;
            Ball = new EightBall { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Glass.X, Glass.Y, 0, 0), Width = Glass.Width, Height = Glass.Height };
            Children.Insert(Children.Count - 1, Ball);
            Board.Visibility = Visibility.Collapsed;   // the ball takes the middle of the window
            // No lamps behind the ball, or so near it they'd show only in part (it shakes a little, too).
            var middle = new Point(Glass.Width / 2, Glass.Height / 2);
            Lamps.Covered = p => (p - middle).Length < EightBall.BallRadius + 6;
            return Ball;
        }

        /// <summary>The case's outline: its rolled top's rounded corners, square at the bottom (for the open and close effects).</summary>
        public static CornerRadius CaseCorners { get { return new CornerRadius(TopCorner, TopCorner, 0, 0); } }

        /// <param name="baseHeight">The base's height, so it's level with the strip's sill.</param>
        public LampMonitor(LampActivity mode, double baseHeight)
        {
            Width = CaseWidth;
            Height = CaseHeight;
            UseLayoutRounding = true;
            Children.Add(new CaseFront { BaseHeight = baseHeight });
            Lamps = new LampPanel(mode, LampFont.MinRows, true, true)
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(Glass.X, Glass.Y, 0, 0), Width = Glass.Width, Height = Glass.Height,
                Corners = new CornerRadius(1.5)
            };
            Children.Add(Lamps);
            // The LED board, a little below the middle of the window, the big lamps either side of it and above it.
            Board = new LedBoard(() => Lamps.Current)
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
                Margin = new Thickness(Glass.X + (Glass.Width - LedBoard.BoardWidth) / 2, Glass.Y + LedBoard.Bottom - LedBoard.BoardHeight, 0, 0)
            };
            Children.Add(Board);
            // No lamps behind the board, or so near it they'd show only in part.
            var board = new Rect(Board.Margin.Left - Glass.X, Board.Margin.Top - Glass.Y, LedBoard.BoardWidth, LedBoard.BoardHeight);
            board.Inflate(4, 4);
            Lamps.Covered = p => board.Contains(p);
            Children.Add(new GlassSheen { IsHitTestVisible = false });
        }

        /// <summary>
        /// The case: drawn once per size and screen scale by WPF, as shapes and text. Its edges run straight down to
        /// the base on both sides, the strip's plain panel butting up against it.
        /// </summary>
        class CaseFront : FrameworkElement
        {
            public double BaseHeight = 7;

            protected override void OnRender(DrawingContext dc)
            {
                double w = CaseWidth, h = CaseHeight;
                var outline = Shapes.Rounded(new Rect(0, 0, w, h), new CornerRadius(TopCorner, TopCorner, 0, 0));
                dc.DrawGeometry(Vertical("#2e3133", "#1b1d1f"), null, outline);
                dc.PushClip(outline);

                // The rolled top: catching the light where it curves toward you, darker as it turns down into the front.
                var roll = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                roll.GradientStops.Add(new GradientStop(Hex("#26292b"), 0));
                roll.GradientStops.Add(new GradientStop(Hex("#4c5053"), 0.2));
                roll.GradientStops.Add(new GradientStop(Hex("#5f6366"), 0.34));
                roll.GradientStops.Add(new GradientStop(Hex("#3c3f42"), 0.66));
                roll.GradientStops.Add(new GradientStop(Hex("#2b2e30"), 1));   // the front's own color there: no seam
                roll.Freeze();
                dc.DrawRectangle(roll, null, new Rect(0, 0, w, RollDepth));
                dc.DrawRectangle(Solid(Color.FromArgb(60, 255, 255, 255)), null, new Rect(TopCorner, 5.5, w - 2 * TopCorner, 0.7));

                // The sides turning away, a little darker, all the way down to the base.
                var sides = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
                sides.GradientStops.Add(new GradientStop(Color.FromArgb(110, 0, 0, 0), 0));
                sides.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.05));
                sides.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.95));
                sides.GradientStops.Add(new GradientStop(Color.FromArgb(130, 0, 0, 0), 1));
                sides.Freeze();
                dc.DrawRectangle(sides, null, new Rect(0, 0, w, h - BaseHeight));
                dc.DrawRectangle(Solid(Color.FromArgb(22, 255, 255, 255)), null, new Rect(5.5, RollDepth + 2, 0.7, h - BaseHeight - RollDepth - 2));

                // The window's recess: in shadow along its top and left, catching the light along its bottom and right.
                var recess = Rect.Inflate(Glass, 3.5, 3.5);
                var lip = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                lip.GradientStops.Add(new GradientStop(Hex("#08090a"), 0));
                lip.GradientStops.Add(new GradientStop(Hex("#141617"), 0.55));
                lip.GradientStops.Add(new GradientStop(Hex("#45494c"), 1));
                lip.Freeze();
                dc.DrawRoundedRectangle(lip, null, recess, 2.5, 2.5);
                dc.DrawRectangle(Solid(Hex("#030303")), null, Glass);

                // The lettering.
                var ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);   // a round O
                var letters = Vertical("#a7abae", "#7d8184");
                var shade = Solid(Color.FromArgb(210, 8, 9, 10));
                double cx = w / 2;
                double titleWidth = Spaced(dc, face, ppd, "WOPR".Select(c => Tuple.Create(c.ToString(), 22.0)).ToList(), 6, cx, 104, letters, shade, 1);
                // The name in small capitals, as wide as WOPR above it.
                var words = "WAR OPERATION PLAN RESPONSE".Split(' ');
                var parts = new List<Tuple<string, double>>();
                for (int k = 0; k < words.Length; k++)
                {
                    if (k > 0) parts.Add(Tuple.Create(" ", 4.1));
                    parts.Add(Tuple.Create(words[k].Substring(0, 1), 5.3));
                    foreach (char c in words[k].Substring(1)) parts.Add(Tuple.Create(c.ToString(), 4.1));
                }
                double natural = Spaced(null, face, ppd, parts, 0.3, cx, 0, null, null, 1);
                Spaced(dc, face, ppd, parts, 0.3, cx, 113, Solid(Hex("#8c9093")), Solid(Color.FromArgb(170, 8, 9, 10)), titleWidth / natural);

                // The base: the strip's sill running on into it, the same height and finish.
                dc.DrawRectangle(Vertical("#111416", "#2e3438"), null, new Rect(0, h - BaseHeight, w, BaseHeight));
                dc.DrawRectangle(Solid(Color.FromArgb(34, 255, 255, 255)), null, new Rect(0, h - BaseHeight, w, 0.8));
                dc.Pop();

                // A fine dark edge, so the case stands clear of whatever is behind it, down to the base (which runs on
                // from the strip's sill).
                var edge = new Pen(Solid(Color.FromArgb(200, 6, 7, 8)), 1);
                edge.Freeze();
                dc.PushClip(new RectangleGeometry(new Rect(-2, -2, w + 4, h - BaseHeight + 2)));
                dc.DrawGeometry(null, edge, Shapes.Rounded(new Rect(0.5, 0.5, w - 1, h - 0.5), new CornerRadius(TopCorner - 0.5, TopCorner - 0.5, 0, 0)));
                dc.Pop();
            }
        }

        /// <summary>A faint reflection across the window's glass, over the lamps.</summary>
        class GlassSheen : FrameworkElement
        {
            protected override void OnRender(DrawingContext dc)
            {
                var sheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(16, 255, 255, 255), 0.3));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(5, 255, 255, 255), 0.42));
                sheen.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
                sheen.Freeze();
                dc.DrawRectangle(sheen, null, Glass);
                dc.DrawRectangle(Solid(Color.FromArgb(20, 255, 255, 255)), null, new Rect(Glass.X, Glass.Bottom - 0.8, Glass.Width, 0.8));
            }
        }

        /// <summary>
        /// Draws letters spaced out, centered on <paramref name="cx"/> with their baselines on <paramref name="baseline"/>,
        /// with a dark copy just below each for depth; returns the width. With no drawing context it only measures.
        /// </summary>
        static double Spaced(DrawingContext dc, Typeface face, double ppd, IList<Tuple<string, double>> parts, double tracking,
                             double cx, double baseline, Brush fill, Brush shade, double scale)
        {
            var texts = parts.Select(p => new FormattedText(p.Item1, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                face, p.Item2 * scale, fill ?? Brushes.Black, ppd)).ToList();
            double width = texts.Sum(t => t.WidthIncludingTrailingWhitespace) + tracking * scale * (texts.Count - 1);
            if (dc == null) return width;
            double x = cx - width / 2;
            for (int i = 0; i < texts.Count; i++)
            {
                var t = texts[i];
                if (shade != null)
                {
                    var s = new FormattedText(parts[i].Item1, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, parts[i].Item2 * scale, shade, ppd);
                    dc.DrawText(s, new Point(x, baseline - t.Baseline + 0.8));
                }
                dc.DrawText(t, new Point(x, baseline - t.Baseline));
                x += t.WidthIncludingTrailingWhitespace + tracking * scale;
            }
            return width;
        }

        static Color Hex(string h) { return (Color)ColorConverter.ConvertFromString(h); }
        static Brush Solid(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        static Brush Vertical(string a, string b)
        {
            var br = new LinearGradientBrush(Hex(a), Hex(b), 90);
            br.Freeze();
            return br;
        }
    }

    /// <summary>
    /// A small board of tiny red LEDs set into the middle of the monitor's window, flashing like a busy computer: each LED
    /// on its own, on and off at random moments, only a few lit at a time; quicker and a few more of them with the strip's
    /// activity. LEDs switch at once (they aren't bulbs), with just a trace of light left as they go out. It only runs
    /// while it's on screen.
    /// </summary>
    internal class LedBoard : FrameworkElement
    {
        const int Cols = 13, Rows = 6;
        const double Pitch = 2.6, Dot = 0.72, Edge = 2.4;
        public const double BoardWidth = Cols * Pitch + 2 * Edge - (Pitch - 2 * Dot), BoardHeight = Rows * Pitch + 2 * Edge - (Pitch - 2 * Dot);
        /// <summary>
        /// Its bottom edge in the monitor's window, from the window's top: low enough to leave two full rows of the big lamps
        /// above it, clear of it.
        /// </summary>
        public const double Bottom = 40.0;

        readonly Func<LampActivity> activity;
        readonly System.Windows.Threading.DispatcherTimer timer;
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        readonly Random rnd = new Random();
        readonly bool[,] on = new bool[Rows, Cols];
        readonly double[,] glow = new double[Rows, Cols];
        readonly double[,] next = new double[Rows, Cols];   // seconds until each LED goes on or off
        readonly byte[,] tint = new byte[Rows, Cols];       // which of the colors each LED has (one of them, picked as it lights)
        LedLook[] looks = { LedLook.Red };
        double lastOn, lastOff;   // the pace those were drawn for
        double last;

        public LedBoard(Func<LampActivity> activity)
        {
            this.activity = activity;
            Width = BoardWidth;
            Height = BoardHeight;
            // Already blinking as it would be after a while: about the usual share lit, each with its own time to go.
            double onFor, offFor;
            Rates(activity(), out onFor, out offFor);
            lastOn = onFor;
            lastOff = offFor;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    on[r, c] = rnd.NextDouble() < onFor / (onFor + offFor);
                    next[r, c] = -Math.Log(1 - rnd.NextDouble()) * (on[r, c] ? onFor : offFor);
                }
            timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += delegate { Step(); };
            IsVisibleChanged += delegate { Running(); };
            Loaded += delegate { Running(); };
            Unloaded += delegate { timer.Stop(); };
        }

        /// <summary>
        /// How long an LED stays on, and off, on average: on for a while (longer than the big lamps around it stay as they
        /// are) and off for much longer, so only a few are lit at once; fewer and slower when idle (slower still on standby),
        /// more and quicker on alert.
        /// </summary>
        static void Rates(LampActivity a, out double onFor, out double offFor)
        {
            double share;   // lit at once
            if (a == LampActivity.Custom)
            {
                var c = LampPanel.CustomLevels() ?? new LampActivityLevels();
                onFor = 1.7 * LampActivityLevels.Slowness(c.LedSpeed);
                share = c.LedLamps / 100.0;
            }
            else
            {
                onFor = a == LampActivity.Standby ? 5.6 : a == LampActivity.Idle ? 2.8 : a == LampActivity.Alert ? 1.0 : 1.7;   // seconds
                share = a == LampActivity.Standby || a == LampActivity.Idle ? 0.04 : a == LampActivity.Alert ? 0.09 : 0.06;
            }
            const double Ever = 1e9;   // (seconds: never, in effect)
            if (share <= 0) offFor = Ever;   // none: once off, they stay off
            else if (share >= 1) { onFor = Ever; offFor = 0; }   // all of them
            else offFor = onFor * (1 - share) / share;
        }

        void Running()
        {
            if (IsVisible && IsLoaded) { if (!timer.IsEnabled) { clock.Restart(); last = 0; timer.Start(); } }
            else timer.Stop();
        }

        /// <summary>
        /// The LEDs' colors, as they look lit (null or none: the usual red). With several, each LED takes one of them as it
        /// lights, picked at random.
        /// </summary>
        public Color[] Tints
        {
            set
            {
                looks = value == null || value.Length == 0 ? new[] { LedLook.Red } : value.Select(LedLook.For).ToArray();
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++) tint[r, c] = (byte)rnd.Next(looks.Length);
                InvalidateVisual();
            }
        }

        /// <summary>Test copies: how many colors its LEDs have.</summary>
        public int TintCount { get { return looks.Length; } }

        /// <summary>An LED's look in one color: dark, dim, lit, and the light around it as it fades.</summary>
        sealed class LedLook
        {
            public Brush Off, Dim, Lit;
            public Brush[] Glows;

            public static readonly LedLook Red = new LedLook { Off = OffBrush, Dim = DimBrush, Lit = LitBrush, Glows = GlowBrushes };
            static readonly System.Collections.Generic.Dictionary<Color, LedLook> made = new System.Collections.Generic.Dictionary<Color, LedLook>();

            public static LedLook For(Color c)
            {
                LedLook look;
                if (made.TryGetValue(c, out look)) return look;
                Func<double, Color> scaled = k => Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
                Color white = Color.FromRgb(255, 255, 255), hot = Color.FromRgb((byte)((c.R + 255) / 2), (byte)((c.G + 255) / 2), (byte)((c.B + 255) / 2));
                look = new LedLook
                {
                    Off = Frozen(new SolidColorBrush(scaled(0.18))),
                    Dim = Frozen(new SolidColorBrush(scaled(0.6))),
                    Lit = Frozen(new RadialGradientBrush(hot, c) { GradientOrigin = new Point(0.4, 0.35) }),
                    Glows = new Brush[GlowBrushes.Length]
                };
                for (int k = 0; k < look.Glows.Length; k++)
                {
                    double b = (k + 1) / (double)look.Glows.Length;
                    var g = new RadialGradientBrush();
                    g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(150 * b), c.R, c.G, c.B), 0));
                    g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(50 * b), c.R, c.G, c.B), 0.5));
                    g.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                    look.Glows[k] = Frozen(g);
                }
                return made[c] = look;
            }
        }

        /// <summary>Test copies: how many times its LEDs have gone on or off.</summary>
        public long Changes { get; private set; }

        /// <summary>Test copies: how many of its LEDs are lit.</summary>
        public int LitCount { get { int n = 0; foreach (bool b in on) if (b) n++; return n; } }

        void Step()
        {
            double now = clock.Elapsed.TotalSeconds, dt = Math.Min(0.2, now - last);
            last = now;
            double onFor, offFor;
            Rates(activity(), out onFor, out offFor);
            if (onFor != lastOn || offFor != lastOff)
            {
                // A new pace: each LED's time to go is drawn again, as if it had been at this pace all along.
                lastOn = onFor;
                lastOff = offFor;
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++) next[r, c] = -Math.Log(1 - rnd.NextDouble()) * (on[r, c] ? onFor : offFor);
            }
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    next[r, c] -= dt;
                    if (next[r, c] > 0) continue;
                    on[r, c] = !on[r, c];
                    if (on[r, c] && looks.Length > 1) tint[r, c] = (byte)rnd.Next(looks.Length);   // a color for this time it's lit
                    Changes++;
                    // how long until it changes again: random, mostly short, now and then much longer
                    next[r, c] = -Math.Log(1 - rnd.NextDouble()) * (on[r, c] ? onFor : offFor);
                }
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    glow[r, c] = on[r, c] ? 1 : glow[r, c] * Math.Exp(-dt / 0.05);   // on at once; a trace of light going out
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = BoardWidth, h = BoardHeight;
            // Set into the window: a black face, with shade along its top inside edge.
            dc.DrawRectangle(Face, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(Shade, null, new Rect(0, 0, w, 2));
            double x0 = Edge + Dot, y0 = Edge + Dot;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var p = new Point(x0 + c * Pitch, y0 + r * Pitch);
                    double g = glow[r, c];
                    var glows = looks[tint[r, c] % looks.Length].Glows;
                    if (g > 0.05) dc.DrawEllipse(glows[Math.Min(glows.Length - 1, (int)(g * (glows.Length - 1) + 0.5))], null, p, Dot * 2.4, Dot * 2.4);
                }
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    double g = glow[r, c];
                    var look = looks[tint[r, c] % looks.Length];
                    dc.DrawEllipse(g > 0.5 ? look.Lit : g > 0.15 ? look.Dim : look.Off, null, new Point(x0 + c * Pitch, y0 + r * Pitch), Dot, Dot);
                }
        }

        static readonly Brush Face = Frozen(new SolidColorBrush(Color.FromRgb(6, 5, 5)));
        static readonly Brush Shade = Frozen(new LinearGradientBrush(Color.FromArgb(200, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90));
        static readonly Brush OffBrush = Frozen(new SolidColorBrush(Color.FromRgb(46, 10, 9)));
        static readonly Brush DimBrush = Frozen(new SolidColorBrush(Color.FromRgb(150, 22, 16)));
        static readonly Brush LitBrush = Frozen(new RadialGradientBrush(Color.FromRgb(255, 150, 120), Color.FromRgb(255, 34, 22)) { GradientOrigin = new Point(0.4, 0.35) });
        static readonly Brush[] GlowBrushes = MakeGlows();

        static Brush[] MakeGlows()
        {
            var list = new Brush[6];
            for (int k = 0; k < list.Length; k++)
            {
                double b = (k + 1) / (double)list.Length;
                var g = new RadialGradientBrush();
                g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(150 * b), 255, 40, 25), 0));
                g.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(50 * b), 255, 30, 20), 0.5));
                g.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 30, 20), 1));
                list[k] = Frozen(g);
            }
            return list;
        }

        static Brush Frozen(Brush b) { b.Freeze(); return b; }
        static Pen FrozenPen(Pen p) { p.Freeze(); return p; }
    }
}
