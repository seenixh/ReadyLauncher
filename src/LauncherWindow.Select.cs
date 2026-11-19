using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ReadyLauncher
{
    /// <summary>Several icons, lines and text dragged or removed together.</summary>
    internal class GroupMove
    {
        public UIElement Source;
        public Point Start;
        public bool Active;
        public readonly List<Tuple<FrameworkElement, double, double>> Views = new List<Tuple<FrameworkElement, double, double>>();
        public double MinLeft, MinTop;   // keep the whole group inside the icon area
        public IconSnap Snap;            // align-to-nearby mode: how the grabbed icon lines up right now
    }

    /// <summary>Drag a box on an empty spot to select icons, lines and text; then move, remove or group them.</summary>
    internal partial class LauncherWindow
    {
        public static readonly Color SelectColor = Color.FromRgb(0x4A, 0x9E, 0xFF);
        readonly HashSet<LauncherItem> selItems = new HashSet<LauncherItem>();
        readonly HashSet<Decoration> selDecor = new HashSet<Decoration>();
        HashSet<LauncherItem> boxStartItems;
        HashSet<Decoration> boxStartDecor;
        Rectangle selectBox;
        Point boxStart;
        bool boxing;
        GroupMove groupMove;

        int SelectionCount { get { return selItems.Count + selDecor.Count; } }

        void InitSelection()
        {
            canvas.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource != canvas) return;
                bool add = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (!add) ClearSelection();
                boxStartItems = new HashSet<LauncherItem>(selItems);
                boxStartDecor = new HashSet<Decoration>(selDecor);
                boxStart = e.GetPosition(canvas);
                boxing = true;
                canvas.CaptureMouse();
                Focus();
                e.Handled = true;
            };
            canvas.MouseMove += (s, e) =>
            {
                if (!boxing) return;
                var p = e.GetPosition(canvas);
                if (selectBox == null)
                {
                    if (Math.Abs(p.X - boxStart.X) < 4 && Math.Abs(p.Y - boxStart.Y) < 4) return;
                    selectBox = new Rectangle
                    {
                        Stroke = new SolidColorBrush(SelectColor), StrokeThickness = 1,
                        Fill = new SolidColorBrush(Color.FromArgb(0x33, SelectColor.R, SelectColor.G, SelectColor.B)),
                        IsHitTestVisible = false
                    };
                    Panel.SetZIndex(selectBox, 10000);
                    canvas.Children.Add(selectBox);
                }
                var r = new Rect(boxStart, p);
                Canvas.SetLeft(selectBox, r.X);
                Canvas.SetTop(selectBox, r.Y);
                selectBox.Width = r.Width;
                selectBox.Height = r.Height;
                SelectInBox(r);
            };
            canvas.MouseLeftButtonUp += (s, e) => { if (boxing) { canvas.ReleaseMouseCapture(); e.Handled = true; } };
            canvas.LostMouseCapture += delegate { EndBox(); };
        }

        void EndBox()
        {
            if (!boxing) return;
            boxing = false;
            if (selectBox != null) { canvas.Children.Remove(selectBox); selectBox = null; }
        }

        /// <summary>Everything the box touches is selected (plus what was selected before, with Ctrl).</summary>
        void SelectInBox(Rect box)
        {
            selItems.Clear();
            selItems.UnionWith(boxStartItems);
            selDecor.Clear();
            selDecor.UnionWith(boxStartDecor);
            foreach (var t in tiles.Values)
                if (BoundsOf(t).IntersectsWith(box)) selItems.Add(t.Item);
            foreach (var v in decorViews.Values)
                if (BoundsOf(v).IntersectsWith(box)) selDecor.Add(v.D);
            ShowSelection();
        }

        static Rect BoundsOf(FrameworkElement el)
        {
            double w = el.ActualWidth > 0 ? el.ActualWidth : el.DesiredSize.Width, h = el.ActualHeight > 0 ? el.ActualHeight : el.DesiredSize.Height;
            return new Rect(Canvas.GetLeft(el), Canvas.GetTop(el), w, h);
        }

        void ShowSelection()
        {
            foreach (var t in tiles.Values) t.SetSelected(selItems.Contains(t.Item));
            foreach (var v in decorViews.Values) ShowDecorSelected(v);
        }

        void ShowDecorSelected(DecorView v)
        {
            if (v.Outline == null) return;
            bool on = selDecor.Contains(v.D);
            if (on)
            {
                v.Outline.Stroke = new SolidColorBrush(SelectColor);
                v.Outline.StrokeDashArray = null;
                v.Outline.StrokeThickness = 1.5;
                v.Outline.Fill = new SolidColorBrush(Color.FromArgb(0x26, SelectColor.R, SelectColor.G, SelectColor.B));
                v.Outline.Visibility = Visibility.Visible;
            }
            else
            {
                v.Outline.Stroke = new SolidColorBrush(Color.FromArgb(0x99, fgColor.R, fgColor.G, fgColor.B));
                v.Outline.StrokeDashArray = new DoubleCollection { 3, 3 };
                v.Outline.StrokeThickness = 1;
                v.Outline.Fill = null;
                if (!v.IsMouseOver && !v.IsMouseCaptured) v.Outline.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>After a redraw: forget anything that's gone and show the rest as selected again.</summary>
        void RefreshSelection()
        {
            selItems.IntersectWith(Data.Items);
            selDecor.IntersectWith(Data.Decorations);
            ShowSelection();
        }

        public void ClearSelection()
        {
            if (SelectionCount == 0) return;
            selItems.Clear();
            selDecor.Clear();
            ShowSelection();
        }

        void SelectAll()
        {
            selItems.Clear(); selItems.UnionWith(Data.Items);
            selDecor.Clear(); selDecor.UnionWith(Data.Decorations);
            ShowSelection();
        }

        void ToggleSelected(LauncherItem it)
        {
            if (!selItems.Remove(it)) selItems.Add(it);
            ShowSelection();
        }

        void ToggleSelected(Decoration d)
        {
            if (!selDecor.Remove(d)) selDecor.Add(d);
            ShowSelection();
        }

        bool InMultiSelection(LauncherItem it) { return SelectionCount > 1 && selItems.Contains(it); }
        bool InMultiSelection(Decoration d) { return SelectionCount > 1 && selDecor.Contains(d); }

        // ---------- moving the selection together ----------

        void StartGroupMove(UIElement source, Point canvasPoint)
        {
            var g = new GroupMove { Source = source, Start = canvasPoint, MinLeft = double.MaxValue, MinTop = double.MaxValue };
            foreach (var t in tiles.Values.Where(t => selItems.Contains(t.Item))) g.Views.Add(Tuple.Create((FrameworkElement)t, Canvas.GetLeft(t), Canvas.GetTop(t)));
            foreach (var v in decorViews.Values.Where(v => selDecor.Contains(v.D))) g.Views.Add(Tuple.Create((FrameworkElement)v, Canvas.GetLeft(v), Canvas.GetTop(v)));
            foreach (var x in g.Views) { g.MinLeft = Math.Min(g.MinLeft, x.Item2); g.MinTop = Math.Min(g.MinTop, x.Item3); }
            groupMove = g;
        }

        void Offset(GroupMove g, Point p, out double dx, out double dy)
        {
            dx = Math.Max(p.X - g.Start.X, -g.MinLeft);   // never past the left or top edge
            dy = Math.Max(p.Y - g.Start.Y, -g.MinTop);

            // Align-to-nearby mode: the grabbed icon lines up with the icons near it, and the rest move with it.
            g.Snap = null;
            var src = g.Source as TileView;
            if (S.Arrange != ArrangeMode.FreeAlign || src == null) return;
            var from = g.Views.FirstOrDefault(v => v.Item1 == src);
            if (from == null) return;
            double x = from.Item2 + dx, y = from.Item3 + dy;
            var snap = SnapToNeighbors(x, y, selItems);
            double sdx = dx + snap.X - x, sdy = dy + snap.Y - y;
            if (sdx < -g.MinLeft || sdy < -g.MinTop) return;   // lining up would push the group past the edge
            dx = sdx;
            dy = sdy;
            g.Snap = snap;
        }

        void UpdateGroupMove(Point p)
        {
            var g = groupMove;
            if (!g.Active)
            {
                if (Math.Abs(p.X - g.Start.X) < 4 && Math.Abs(p.Y - g.Start.Y) < 4) return;
                g.Active = true;
            }
            double dx, dy;
            Offset(g, p, out dx, out dy);
            foreach (var x in g.Views) { Canvas.SetLeft(x.Item1, x.Item2 + dx); Canvas.SetTop(x.Item1, x.Item3 + dy); }
            ShowGuides(g.Snap);
        }

        void EndGroupMove(GroupMove g, Point p)
        {
            ClearGuides();
            double dx, dy;
            Offset(g, p, out dx, out dy);
            if (g.Snap == null) { dx = Math.Round(dx); dy = Math.Round(dy); }   // lined-up moves stay exact
            foreach (var d in selDecor) { d.X += dx; d.Y += dy; }
            if (S.Arrange == ArrangeMode.Auto)
            {
                // Auto arrange keeps icons in reading order: move the selected icons, in order, to where they were dropped.
                var src = g.Source as TileView;
                var moved = Data.Items.Where(selItems.Contains).ToList();
                if (src != null && moved.Count > 0)
                {
                    var drop = new Point(Canvas.GetLeft(src) + CellW / 2, Canvas.GetTop(src) + CellH / 3);
                    int idx = IndexFromPoint(drop);
                    int before = Data.Items.Take(idx).Count(selItems.Contains);
                    Data.Items.RemoveAll(selItems.Contains);
                    Data.Items.InsertRange(Math.Max(0, Math.Min(idx - before, Data.Items.Count)), moved);
                }
            }
            else
            {
                foreach (var it in selItems) { it.X = Math.Max(0, it.X + dx); it.Y = Math.Max(0, it.Y + dy); }
                if (S.Arrange == ArrangeMode.Grid) LayoutGrid(null);
            }
            app.Save();
            RenderItems();
        }

        void CancelGroupMove(GroupMove g)
        {
            ClearGuides();
            foreach (var x in g.Views) { Canvas.SetLeft(x.Item1, x.Item2); Canvas.SetTop(x.Item1, x.Item3); }
        }

        // ---------- removing / grouping ----------

        void RemoveSelected()
        {
            int n = SelectionCount;
            if (n == 0) return;
            bool folders = selItems.Any(i => i.IsFolder && i.Children.Count > 0);
            if (n > 1 || folders)
            {
                app.ModalOpen = true;
                MessageBoxResult answer;
                try
                {
                    answer = MessageBox.Show(this,
                        "Remove the " + (n == 1 ? "selected item" : n + " selected items") + " from the launcher?"
                        + (folders ? "\n\nThis includes folders and the shortcuts inside them." : "")
                        + "\n\n(The apps and files themselves are not touched.)",
                        Program.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                }
                finally { app.ModalOpen = false; }
                if (answer != MessageBoxResult.OK) return;
            }
            Data.Items.RemoveAll(selItems.Contains);
            Data.Decorations.RemoveAll(selDecor.Contains);
            selItems.Clear();
            selDecor.Clear();
            app.Save();
            RenderItems();
        }

        /// <summary>Put the selected icons (and the contents of selected folders) into one new folder.</summary>
        void FolderFromSelection()
        {
            var icons = Data.Items.Where(selItems.Contains).ToList();
            if (icons.Count == 0) return;
            var first = icons[0];
            var folder = new LauncherItem { Kind = ItemKind.Folder, Name = "New folder", X = first.X, Y = first.Y };
            int idx = Data.Items.IndexOf(first);
            foreach (var i in icons)
            {
                Data.Items.Remove(i);
                if (i.IsFolder) folder.Children.AddRange(i.Children); else folder.Children.Add(i);
            }
            Data.Items.Insert(Math.Max(0, Math.Min(idx, Data.Items.Count)), folder);
            selItems.Clear();
            app.Save();
            RenderItems();
            OpenFolder(folder, true);
        }

        List<object> SelectionMenuItems()
        {
            int n = SelectionCount;
            var items = new List<object>();
            items.Add(Item("Remove " + n + " selected item" + (n == 1 ? "" : "s"), RemoveSelected, false, ""));
            var folder = Item("Put the selected icons in a new folder", FolderFromSelection, false, "");
            folder.IsEnabled = selItems.Count > 0;
            items.Add(folder);
            items.Add(new Separator());
            items.Add(Item("Clear selection", ClearSelection, false, ""));
            return items;
        }
    }
}
