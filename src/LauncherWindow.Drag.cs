using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ReadyLauncher
{
    internal enum DragOrigin { Canvas, Folder, Dock }

    internal class DragState
    {
        public TileView Tile;
        public LauncherItem Item;
        public DragOrigin Origin;
        public LauncherItem Folder;   // set when dragging out of an open folder
        public Point Start;
        public bool Active;
        public FrameworkElement Ghost;
        public bool Blocked;   // a drag while the layout is locked: nothing moves, and it doesn't count as a click
        public int Clicks;     // 2 for the second click of a double-click
    }

    /// <summary>Moving icons around, merging them into folders, and accepting drops from Explorer/browsers.</summary>
    internal partial class LauncherWindow
    {
        DragState drag;
        TileView mergeTarget;

        void AttachTileHandlers(TileView t, DragOrigin origin)
        {
            t.MouseLeftButtonDown += (s, e) =>
            {
                if (origin == DragOrigin.Canvas && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    ToggleSelected(t.Item);   // Ctrl+click adds or removes one icon
                    e.Handled = true;
                    return;
                }
                if (origin == DragOrigin.Canvas && InMultiSelection(t.Item) && !S.LayoutLocked)
                {
                    StartGroupMove(t, e.GetPosition(canvas));   // drag all the selected items together
                    t.CaptureMouse();
                    e.Handled = true;
                    return;
                }
                ClearSelection();
                drag = new DragState
                {
                    Tile = t, Item = t.Item, Origin = origin, Start = e.GetPosition(this),
                    Folder = origin == DragOrigin.Folder ? openFolder : null, Clicks = e.ClickCount
                };
                t.CaptureMouse();
                e.Handled = true;
            };
            t.MouseMove += (s, e) =>
            {
                if (groupMove != null && groupMove.Source == t) { UpdateGroupMove(e.GetPosition(canvas)); return; }
                if (drag == null || drag.Tile != t) return;
                var p = e.GetPosition(this);
                if (!drag.Active)
                {
                    if (Math.Abs(p.X - drag.Start.X) < SystemParameters.MinimumHorizontalDragDistance * 2
                        && Math.Abs(p.Y - drag.Start.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
                    if (S.LayoutLocked) { drag.Blocked = true; return; }   // locked: nothing moves
                    BeginDrag();
                }
                UpdateDrag(p);
            };
            t.MouseLeftButtonUp += (s, e) =>
            {
                if (groupMove != null && groupMove.Source == t)
                {
                    var g = groupMove;
                    groupMove = null;
                    t.ReleaseMouseCapture();
                    e.Handled = true;
                    if (g.Active) EndGroupMove(g, e.GetPosition(canvas));
                    else ClearSelection();   // a plain click on a selected icon just ends the selection
                    return;
                }
                if (drag == null || drag.Tile != t) return;
                var d = drag;
                drag = null;
                t.ReleaseMouseCapture();
                e.Handled = true;
                if (d.Active) EndDrag(d, e.GetPosition(this));
                else if (!d.Blocked && (!S.OpenWithDoubleClick || d.Clicks >= 2)) LaunchItem(d.Item, false);   // a click (or double-click) opens it
            };
            t.LostMouseCapture += (s, e) =>
            {
                if (groupMove != null && groupMove.Source == t) { var g = groupMove; groupMove = null; CancelGroupMove(g); return; }
                if (drag == null || drag.Tile != t) return;
                var d = drag;
                drag = null;
                if (d.Active) CancelDrag(d);
            };
            t.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (origin == DragOrigin.Canvas && InMultiSelection(t.Item)) { ShowMenu(null, SelectionMenuItems()); return; }
                ClearSelection();
                ShowItemMenu(t, origin);
            };
        }

        void BeginDrag()
        {
            drag.Active = true;
            drag.Ghost = drag.Tile.CreateGhost();
            dragLayer.Children.Add(drag.Ghost);
            drag.Tile.Opacity = 0.35;
            ToolTipService.SetIsEnabled(drag.Tile, false);
        }

        void UpdateDrag(Point p)
        {
            double ghostX = p.X - drag.Tile.IconSize / 2, ghostY = p.Y - drag.Tile.IconSize / 2;

            // Dragging an icon out of an open folder fades the folder away so it can land on the main canvas.
            // Only fade it: the dragged icon lives inside the folder, so collapsing the folder, or making it
            // click-through (IsHitTestVisible = false), would take the mouse away from the icon and cancel the drag.
            // While the icon holds the mouse, clicks can't reach the faded folder anyway.
            if (drag.Origin == DragOrigin.Folder && FolderOverlayShown && !IsOver(folderPanel, p))
                overlay.Opacity = 0;

            TileView target = FolderOverlayShown ? null : HitTile(p, drag.Item);
            if (target != null && drag.Folder != null && target.Item == drag.Folder) target = null;
            if (target != null && drag.Item.IsFolder && !target.Item.IsFolder) target = null;
            SetMergeTarget(target);

            // Align-to-nearby mode: the icon jumps to where it will line up, with guide lines to the icons it lines up with.
            ClearGuides();
            if (S.Arrange == ArrangeMode.FreeAlign && target == null && !FolderOverlayShown && IsOver(scroll, p))
            {
                var spot = DropSpot(TranslatePoint(p, canvas));
                var snap = SnapToNeighbors(spot.X, spot.Y, new[] { drag.Item });
                ghostX += snap.X - spot.X;
                ghostY += snap.Y - spot.Y;
                ShowGuides(snap);
            }
            Canvas.SetLeft(drag.Ghost, ghostX);
            Canvas.SetTop(drag.Ghost, ghostY);
        }

        void EndDrag(DragState d, Point p)
        {
            ClearGuides();
            dragLayer.Children.Clear();
            d.Tile.Opacity = 1;
            ToolTipService.SetIsEnabled(d.Tile, true);
            var target = mergeTarget == null ? null : mergeTarget.Item;
            SetMergeTarget(null);
            Point cp = TranslatePoint(p, canvas);
            bool overCanvas = IsOver(scroll, p);

            switch (d.Origin)
            {
                case DragOrigin.Canvas:
                    if (target != null) MergeIntoFolder(d.Item, target, true);
                    else if (overCanvas) MoveItem(d.Item, cp);
                    else RenderItems();
                    break;

                case DragOrigin.Dock:
                    if (target != null) MergeIntoFolder(d.Item.CloneShortcut(), target, true);
                    else if (overCanvas) AddItems(new[] { d.Item.CloneShortcut() }, cp);
                    break;

                case DragOrigin.Folder:
                    var folder = d.Folder;
                    if (FolderOverlayShown) { ReorderInFolder(d.Item, p); break; }
                    CloseFolder();
                    if (target != null) { RemoveFromFolder(folder, d.Item, null, false); MergeIntoFolder(d.Item, target, true); }
                    else if (overCanvas) RemoveFromFolder(folder, d.Item, cp, true);
                    else OpenFolder(folder, false);
                    break;
            }
        }

        void CancelDrag(DragState d)
        {
            ClearGuides();
            dragLayer.Children.Clear();
            d.Tile.Opacity = 1;
            ToolTipService.SetIsEnabled(d.Tile, true);
            SetMergeTarget(null);
            if (d.Origin == DragOrigin.Folder && openFolder != null)
            {
                overlay.Opacity = 1;
                overlay.IsHitTestVisible = true;
            }
        }

        TileView HitTile(Point windowPoint, LauncherItem exclude)
        {
            if (!IsOver(scroll, windowPoint)) return null;
            foreach (var t in tiles.Values)
            {
                if (t.Item == exclude) continue;
                var c = t.IconCenter(this);
                double r = t.IconSize * 0.55;
                if (Math.Abs(c.X - windowPoint.X) < r && Math.Abs(c.Y - windowPoint.Y) < r) return t;
            }
            return null;
        }

        bool IsOver(FrameworkElement el, Point windowPoint)
        {
            if (!el.IsVisible) return false;
            var q = TranslatePoint(windowPoint, el);
            return q.X >= 0 && q.Y >= 0 && q.X <= el.ActualWidth && q.Y <= el.ActualHeight;
        }

        void SetMergeTarget(TileView t)
        {
            if (mergeTarget == t) return;
            if (mergeTarget != null) mergeTarget.SetMergeTarget(false);
            mergeTarget = t;
            if (t != null) t.SetMergeTarget(true);
        }

        // ---------- drops from Explorer, the desktop, Start, or a browser ----------

        static DragDropEffects LinkEffect(DragEventArgs e)
        {
            if (!ShellHelper.CanAccept(e.Data)) return DragDropEffects.None;
            return (e.AllowedEffects & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.Copy;
        }

        void OnExternalDragOver(object sender, DragEventArgs e)
        {
            e.Effects = LinkEffect(e);
            SetMergeTarget(e.Effects == DragDropEffects.None ? null : HitTile(e.GetPosition(this), null));
            ClearGuides();
            if (S.Arrange == ArrangeMode.FreeAlign && e.Effects != DragDropEffects.None && mergeTarget == null)
            {
                // Show where something dragged in from Explorer or a browser will line up.
                var cp = e.GetPosition(canvas);
                var spot = new Point(Math.Max(0, cp.X - CellW / 2), Math.Max(0, cp.Y - CellH / 3));
                ShowGuides(SnapToNeighbors(spot.X, spot.Y, new LauncherItem[0]));
            }
            e.Handled = true;
        }

        void OnExternalDrop(object sender, DragEventArgs e)
        {
            ClearGuides();
            var target = mergeTarget == null ? null : mergeTarget.Item;
            SetMergeTarget(null);
            var items = ShellHelper.ItemsFromData(e.Data);
            if (items.Count == 0) return;
            if (target != null)
            {
                var into = target;
                foreach (var it in items) into = MergeIntoFolder(it, into, false);
                if (into != target) OpenFolder(into, true);
            }
            else AddItems(items, e.GetPosition(canvas));
            Activate();
        }
    }
}
