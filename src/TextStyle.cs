using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ReadyLauncher
{
    /// <summary>Draws a text label with its font, color, underline, drop shadow and outline.</summary>
    internal static class TextStyle
    {
        public static readonly string[] ShadowNames = { "None", "Small", "Medium", "Large" };
        public static readonly string[] OutlineNames = { "None", "Thin", "Thick" };

        public static Color ParseColor(string s, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            try { return (Color)ColorConverter.ConvertFromString(s.Trim()); }
            catch (Exception) { return fallback; }
        }

        /// <param name="autoColor">The color used when the label's color is "Auto".</param>
        public static FrameworkElement Build(Decoration d, Color autoColor, double pixelsPerDip)
        {
            var family = new FontFamily(string.IsNullOrWhiteSpace(d.Font) ? "Segoe UI" : d.Font);
            double size = Math.Max(6, Math.Min(200, d.FontSize));
            var weight = d.Bold ? FontWeights.Bold : FontWeights.Normal;
            var style = d.Italic ? FontStyles.Italic : FontStyles.Normal;
            var fill = new SolidColorBrush(ParseColor(d.Color, autoColor));
            string text = string.IsNullOrEmpty(d.Text) ? " " : d.Text;

            FrameworkElement body;
            if (d.Outline > 0)
            {
                var outline = new SolidColorBrush(ParseColor(d.OutlineColor, Colors.Black));
                body = new OutlinedText(text, new Typeface(family, style, weight, FontStretches.Normal), size, fill, outline,
                    d.Outline == 1 ? 1.5 : 3, d.Underline, pixelsPerDip);
            }
            else
            {
                var tb = new TextBlock { Text = text, FontFamily = family, FontSize = size, FontWeight = weight, FontStyle = style, Foreground = fill };
                if (d.Underline) tb.TextDecorations = TextDecorations.Underline;
                body = tb;
            }
            if (d.Shadow > 0) body.Effect = Shadow(d.Shadow, ParseColor(d.ShadowColor, Colors.Black));
            return body;
        }

        public static Effect Shadow(int level, Color color)
        {
            double depth = level == 1 ? 1.5 : level == 2 ? 3 : 5;
            double blur = level == 1 ? 3 : level == 2 ? 7 : 14;
            return new DropShadowEffect { Color = color, ShadowDepth = depth, BlurRadius = blur, Direction = 315, Opacity = 0.85 };
        }
    }

    /// <summary>Text drawn as a shape so it can have an outline (the outline sits outside the letters).</summary>
    internal class OutlinedText : FrameworkElement
    {
        readonly Geometry shape;
        readonly Brush fill;
        readonly Pen pen;
        readonly Size size;

        public OutlinedText(string text, Typeface typeface, double emSize, Brush fill, Brush outline, double width, bool underline, double pixelsPerDip)
        {
            var ft = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, emSize, fill, pixelsPerDip);
            if (underline) ft.SetTextDecorations(TextDecorations.Underline);
            shape = ft.BuildGeometry(new Point(width, width));
            shape.Freeze();
            size = new Size(Math.Ceiling(ft.WidthIncludingTrailingWhitespace + 2 * width), Math.Ceiling(ft.Height + 2 * width));
            this.fill = fill;
            pen = new Pen(outline, width * 2) { LineJoin = PenLineJoin.Round };
            pen.Freeze();
            SnapsToDevicePixels = true;
        }

        protected override Size MeasureOverride(Size available) { return size; }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawGeometry(null, pen, shape);    // outline first, then the letters on top of it
            dc.DrawGeometry(fill, null, shape);
        }
    }
}
