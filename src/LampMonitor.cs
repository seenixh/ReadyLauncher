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
    /// a dark window with lamps blinking behind it (spaced well apart, fewer lit than on the strip), and WOPR lettered
    /// below. It's drawn rather than a picture, so it's sharp at any screen scale, seen straight on like the strip.
    /// </summary>
    internal class LampMonitor : Grid
    {
        public const double CaseWidth = 130, CaseHeight = 140;
        const double RollDepth = 21;     // the rolled top, down to where it turns smoothly into the front
        const double TopCorner = 13;
        static readonly Rect Glass = new Rect(17, 29, 96, 46);   // the window on the front

        public LampPanel Lamps { get; private set; }

        /// <summary>The case's outline: its rolled top's rounded corners, square at the bottom (for the open and close effects).</summary>
        public static CornerRadius CaseCorners { get { return new CornerRadius(TopCorner, TopCorner, 0, 0); } }

        /// <param name="baseHeight">The base's height, so it's level with the strip's sill.</param>
        public LampMonitor(LampActivity mode, double baseHeight)
        {
            Width = CaseWidth;
            Height = CaseHeight;
            UseLayoutRounding = true;
            Children.Add(new CaseFront { BaseHeight = baseHeight });
            Lamps = new LampPanel(mode, true, true)
            {
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(Glass.X, Glass.Y, 0, 0), Width = Glass.Width, Height = Glass.Height,
                Corners = new CornerRadius(1.5)
            };
            Children.Add(Lamps);
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
}
