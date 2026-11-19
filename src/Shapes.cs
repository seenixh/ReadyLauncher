using System;
using System.Windows;
using System.Windows.Media;

namespace ReadyLauncher
{
    internal static class Shapes
    {
        /// <summary>A rectangle with its own radius at each corner (any of them can be square).</summary>
        public static Geometry Rounded(Rect r, CornerRadius c)
        {
            double x = r.X, y = r.Y, w = r.Width, h = r.Height, most = Math.Max(0, Math.Min(w, h) / 2);
            double tl = Math.Min(most, Math.Max(0, c.TopLeft)), tr = Math.Min(most, Math.Max(0, c.TopRight));
            double br = Math.Min(most, Math.Max(0, c.BottomRight)), bl = Math.Min(most, Math.Max(0, c.BottomLeft));
            var g = new StreamGeometry();
            using (var s = g.Open())
            {
                s.BeginFigure(new Point(x + tl, y), true, true);
                s.LineTo(new Point(x + w - tr, y), false, false);
                if (tr > 0) s.ArcTo(new Point(x + w, y + tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise, false, false);
                s.LineTo(new Point(x + w, y + h - br), false, false);
                if (br > 0) s.ArcTo(new Point(x + w - br, y + h), new Size(br, br), 0, false, SweepDirection.Clockwise, false, false);
                s.LineTo(new Point(x + bl, y + h), false, false);
                if (bl > 0) s.ArcTo(new Point(x, y + h - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise, false, false);
                s.LineTo(new Point(x, y + tl), false, false);
                if (tl > 0) s.ArcTo(new Point(x + tl, y), new Size(tl, tl), 0, false, SweepDirection.Clockwise, false, false);
            }
            g.Freeze();
            return g;
        }

        public static bool AnyRounding(CornerRadius c)
        {
            return c.TopLeft > 0 || c.TopRight > 0 || c.BottomRight > 0 || c.BottomLeft > 0;
        }
    }
}
