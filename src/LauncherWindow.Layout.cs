using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Icon grid maths: free placement, snap-to-grid, auto arrange, adding and moving icons.</summary>
    internal partial class LauncherWindow
    {
        const double Pad = 14;
        int lastCols;

        double IconSize { get { return AppSettings.IconPixels(S.IconSize); } }
        double CellW { get { return IconSize + 40; } }
        double CellH { get { return IconSize + (S.ShowLabels ? 52 : 40); } }

        /// <summary>Switch icon size / labels, scaling free-placed positions so the layout keeps its shape.</summary>
        public void ChangeIconMetrics(IconSizeMode size, bool showLabels)
        {
            double oldW = CellW, oldH = CellH;
            S.IconSize = size;
            S.ShowLabels = showLabels;
            double kx = CellW / oldW, ky = CellH / oldH;
            if (Math.Abs(kx - 1) < 0.001 && Math.Abs(ky - 1) < 0.001) return;
            foreach (var it in Data.Items)
            {
                it.X = Pad + Math.Max(0, it.X - Pad) * kx;
                it.Y = Pad + Math.Max(0, it.Y - Pad) * ky;
            }
        }

        int Columns()
        {
            double w = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Math.Max(200, Width - 2 * Shadow - 60);
            return Math.Max(1, (int)((w - Pad * 2) / CellW));
        }

        Point CellPos(int col, int row)
        {
            return new Point(Pad + col * CellW, Pad + row * CellH);
        }

        void OnCanvasResized()
        {
            if (!FreeLayout && Columns() != lastCols) RenderItems();
            else UpdateCanvasExtent();
        }

        void LayoutAuto()
        {
            int cols = Columns();
            for (int i = 0; i < Data.Items.Count; i++)
            {
                var p = CellPos(i % cols, i / cols);
                Data.Items[i].X = p.X;
                Data.Items[i].Y = p.Y;
            }
        }

        /// <summary>Snap every icon to the nearest free grid cell; <paramref name="priority"/> gets first pick.</summary>
        void LayoutGrid(LauncherItem priority)
        {
            int cols = Columns();
            var taken = new HashSet<int>();
            foreach (var it in Data.Items.OrderBy(i => i == priority ? 0 : 1).ToList())
            {
                int col = Math.Max(0, Math.Min(cols - 1, (int)Math.Round((it.X - Pad) / CellW)));
                int row = Math.Max(0, (int)Math.Round((it.Y - Pad) / CellH));
                int c, r;
                NearestFree(col, row, cols, taken, out c, out r);
                taken.Add(r * 1000 + c);
                var p = CellPos(c, r);
                it.X = p.X;
                it.Y = p.Y;
            }
        }

        static void NearestFree(int col, int row, int cols, HashSet<int> taken, out int bestC, out int bestR)
        {
            bestC = 0; bestR = 0;
            for (int ring = 0; ring < 400; ring++)
            {
                int best = int.MaxValue;
                for (int dr = -ring; dr <= ring; dr++)
                    for (int dc = -ring; dc <= ring; dc++)
                    {
                        if (Math.Max(Math.Abs(dr), Math.Abs(dc)) != ring) continue;
                        int c = col + dc, r = row + dr;
                        if (c < 0 || c >= cols || r < 0 || taken.Contains(r * 1000 + c)) continue;
                        int d = dr * dr + dc * dc;
                        if (d < best) { best = d; bestC = c; bestR = r; }
                    }
                if (best != int.MaxValue) return;
            }
        }

        /// <summary>First grid cell (reading order) not overlapped by any icon.</summary>
        Point FirstFreeSpot()
        {
            int cols = Columns();
            for (int r = 0; r < 500; r++)
                for (int c = 0; c < cols; c++)
                {
                    var p = CellPos(c, r);
                    var cell = new Rect(p.X + 10, p.Y + 10, CellW - 20, CellH - 20);
                    if (!Data.Items.Any(i => new Rect(i.X, i.Y, CellW, CellH).IntersectsWith(cell))) return p;
                }
            return new Point(Pad, Pad);
        }

        int IndexFromPoint(Point canvasPoint)
        {
            int cols = Columns();
            int col = Math.Max(0, Math.Min(cols - 1, (int)((canvasPoint.X - Pad) / CellW)));
            int row = Math.Max(0, (int)((canvasPoint.Y - Pad) / CellH));
            return Math.Min(row * cols + col, Data.Items.Count);
        }

        public void RenderItems()
        {
            if (S.Arrange == ArrangeMode.Auto) LayoutAuto();
            else if (S.Arrange == ArrangeMode.Grid) LayoutGrid(null);
            lastCols = Columns();
            canvas.Children.Clear();
            tiles.Clear();
            RenderDecorations();   // lines and text sit underneath the icons
            double viewW = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
            double maxX = Math.Max(Pad, viewW - CellW);
            foreach (var it in Data.Items)
            {
                var t = new TileView(it, IconSize, S.ShowLabels || it.IsFolder, new SolidColorBrush(IconTextColor), CellW);   // icon names: chosen color, or readable
                AttachTileHandlers(t, DragOrigin.Canvas);
                Canvas.SetLeft(t, Math.Max(0, Math.Min(it.X, maxX)));
                Canvas.SetTop(t, Math.Max(0, it.Y));
                canvas.Children.Add(t);
                tiles[it.Id] = t;
            }
            emptyHint.Visibility = Data.Items.Count == 0 && Data.Decorations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateCanvasExtent();
            RefreshSelection();
        }

        void UpdateCanvasExtent()
        {
            double fill = Math.Max(0, scroll.ActualHeight - 2);
            if (!S.ScrollEnabled) { canvas.Height = fill; return; }   // no scrolling: anything lower is simply cut off
            double bottom = Data.Items.Count == 0 ? 0 : Data.Items.Max(i => i.Y) + CellH + Pad;
            bottom = Math.Max(bottom, DecorationsBottom() + Pad);
            // Fill the visible area (so drops land anywhere) without ever forcing a scrollbar.
            canvas.Height = Math.Max(bottom, fill);
        }

        /// <summary>Put new root items at a canvas point (or the next free spot) and save.</summary>
        void AddItems(IList<LauncherItem> items, Point? canvasPoint)
        {
            if (items.Count == 0) return;
            int insertAt = canvasPoint.HasValue ? IndexFromPoint(canvasPoint.Value) : Data.Items.Count;
            double offset = 0;
            Vector nudge = new Vector();   // align-to-nearby mode: the first icon lines up, the rest keep their spacing from it
            bool first = true;
            foreach (var it in items)
            {
                if (S.Arrange == ArrangeMode.Auto) { Data.Items.Insert(Math.Min(insertAt++, Data.Items.Count), it); continue; }
                Point p;
                if (canvasPoint.HasValue) p = new Point(canvasPoint.Value.X - CellW / 2 + offset, canvasPoint.Value.Y - CellH / 3);
                else p = FirstFreeSpot();
                p = new Point(Math.Max(0, p.X), Math.Max(0, p.Y));
                if (canvasPoint.HasValue && first)
                {
                    var snap = SnapToNeighbors(p.X, p.Y, items);
                    nudge = new Vector(snap.X - p.X, snap.Y - p.Y);
                }
                first = false;
                it.X = Math.Max(0, p.X + nudge.X);
                it.Y = Math.Max(0, p.Y + nudge.Y);
                Data.Items.Add(it);
                if (S.Arrange == ArrangeMode.Grid) LayoutGrid(it);
                offset += CellW;
            }
            app.Save();
            RenderItems();
        }

        /// <summary>Drop an existing root item at a new canvas point, honoring the arrange mode.</summary>
        void MoveItem(LauncherItem item, Point canvasPoint)
        {
            if (S.Arrange == ArrangeMode.Auto)
            {
                int idx = IndexFromPoint(canvasPoint);
                int old = Data.Items.IndexOf(item);
                Data.Items.RemoveAt(old);
                if (idx > old) idx--;
                Data.Items.Insert(Math.Max(0, Math.Min(idx, Data.Items.Count)), item);
            }
            else
            {
                var spot = DropSpot(canvasPoint);
                var snap = SnapToNeighbors(spot.X, spot.Y, new[] { item });   // unchanged unless "align to nearby icons" is on
                item.X = snap.X;
                item.Y = snap.Y;
                if (S.Arrange == ArrangeMode.Grid) LayoutGrid(item);
            }
            app.Save();
            RenderItems();
        }
    }
}
