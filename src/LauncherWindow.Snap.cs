using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Where a dragged icon lands in "align to nearby icons" mode, and which icons it lines up with.</summary>
    internal class IconSnap
    {
        public double X, Y;                // the icon's top-left on the canvas
        public bool InColumn, InRow;       // lined up above/below another icon (same X) / beside one (same Y)
        public Point ColumnWith, RowWith;  // top-left of the icon it lines up with
    }

    /// <summary>Free placement that lines icons up with the icons near them, with guide lines while dragging.</summary>
    internal partial class LauncherWindow
    {
        readonly List<UIElement> guides = new List<UIElement>();

        /// <summary>Either free placement mode (icons keep the position they're dropped at).</summary>
        bool FreeLayout { get { return S.Arrange == ArrangeMode.Free || S.Arrange == ArrangeMode.FreeAlign; } }

        /// <summary>The top-left an icon gets when it's dropped with the mouse at a canvas point.</summary>
        Point DropSpot(Point canvasPoint)
        {
            return new Point(Math.Max(0, Math.Min(canvasPoint.X - CellW / 2, MaxIconLeft())), Math.Max(0, canvasPoint.Y - CellH / 3));
        }

        double MaxIconLeft()
        {
            double viewW = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
            return Math.Max(0, viewW - CellW);
        }

        /// <summary>
        /// "Align to nearby icons" mode: nudges an icon about to land at (x, y) so it lines up with the nearest icon in its
        /// column and/or row. Beside an icon in a row (or above/below one in a column) it also sits exactly one icon's
        /// spacing away, like the grid. Other modes: (x, y) unchanged.
        /// </summary>
        IconSnap SnapToNeighbors(double x, double y, ICollection<LauncherItem> moving)
        {
            var r = new IconSnap { X = x, Y = y };
            if (S.Arrange != ArrangeMode.FreeAlign) return r;
            double reachX = CellW * 0.25, reachY = CellH * 0.25;   // how close counts as "meant to line up"
            double maxX = MaxIconLeft();

            // Where the other icons are shown.
            var others = new List<Point>();
            foreach (var t in tiles.Values)
                if (!moving.Contains(t.Item)) others.Add(new Point(Canvas.GetLeft(t), Canvas.GetTop(t)));
            if (others.Count == 0) return r;
            Func<Point, double> distance = o => (o.X - x) * (o.X - x) + (o.Y - y) * (o.Y - y);

            // The nearest icon whose row (or column) it's close to wins, so it lines up with the icon it's placed near.
            var row = others.Where(o => Math.Abs(o.Y - y) <= reachY).OrderBy(distance).ToList();
            var col = others.Where(o => Math.Abs(o.X - x) <= reachX).OrderBy(distance).ToList();
            if (row.Count > 0) { r.Y = row[0].Y; r.InRow = true; r.RowWith = row[0]; }
            if (col.Count > 0) { r.X = col[0].X; r.InColumn = true; r.ColumnWith = col[0]; }

            // Even spacing, when nothing above or below decides the other direction.
            if (r.InRow && !r.InColumn)
            {
                var beside = others.Where(o => Math.Abs(o.Y - r.Y) < 0.5)
                                   .SelectMany(o => new[] { o.X - CellW, o.X + CellW })
                                   .Where(v => v >= 0 && v <= maxX + 0.5 && Math.Abs(v - x) <= reachX)
                                   .OrderBy(v => Math.Abs(v - x)).ToList();
                if (beside.Count > 0) r.X = beside[0];
            }
            else if (r.InColumn && !r.InRow)
            {
                var stacked = others.Where(o => Math.Abs(o.X - r.X) < 0.5)
                                    .SelectMany(o => new[] { o.Y - CellH, o.Y + CellH })
                                    .Where(v => v >= 0 && Math.Abs(v - y) <= reachY)
                                    .OrderBy(v => Math.Abs(v - y)).ToList();
                if (stacked.Count > 0) r.Y = stacked[0];
            }

            // Never land exactly on top of another icon (it would hide it): stay where it was dropped instead.
            if (others.Any(o => Math.Abs(o.X - r.X) < 1 && Math.Abs(o.Y - r.Y) < 1)) return new IconSnap { X = x, Y = y };
            return r;
        }

        // ---------- guide lines while dragging ----------

        void ShowGuides(IconSnap s)
        {
            ClearGuides();
            if (s == null || (!s.InColumn && !s.InRow)) return;
            var sample = tiles.Values.FirstOrDefault();
            Point icon = sample != null ? sample.IconCenter(sample) : new Point(CellW / 2, CellH / 3);   // icon center inside its tile
            var view = new Rect(scroll.TranslatePoint(new Point(0, 0), dragLayer), new Size(scroll.ViewportWidth, scroll.ViewportHeight));
            double past = IconSize / 2 + 10;   // the guide runs a little past both icons
            var me = new Point(s.X + icon.X, s.Y + icon.Y);
            if (s.InColumn)
            {
                double otherY = s.ColumnWith.Y + icon.Y;
                AddGuide(new Point(me.X, Math.Min(me.Y, otherY) - past), new Point(me.X, Math.Max(me.Y, otherY) + past), view);
            }
            if (s.InRow)
            {
                double otherX = s.RowWith.X + icon.X;
                AddGuide(new Point(Math.Min(me.X, otherX) - past, me.Y), new Point(Math.Max(me.X, otherX) + past, me.Y), view);
            }
        }

        /// <summary>One dashed guide line (canvas coordinates), kept inside the icon area.</summary>
        void AddGuide(Point a, Point b, Rect view)
        {
            a = canvas.TranslatePoint(a, dragLayer);
            b = canvas.TranslatePoint(b, dragLayer);
            bool vertical = Math.Abs(a.X - b.X) < 0.5;
            if (vertical ? (a.X < view.Left || a.X > view.Right) : (a.Y < view.Top || a.Y > view.Bottom)) return;
            double x1 = Math.Max(view.Left, Math.Min(a.X, b.X)), x2 = Math.Min(view.Right, Math.Max(a.X, b.X));
            double y1 = Math.Max(view.Top, Math.Min(a.Y, b.Y)), y2 = Math.Min(view.Bottom, Math.Max(a.Y, b.Y));
            if (x2 < x1 || y2 < y1) return;
            var line = new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(SelectColor), StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }, SnapsToDevicePixels = true, IsHitTestVisible = false
            };
            dragLayer.Children.Insert(0, line);   // under the icon being dragged
            guides.Add(line);
        }

        void ClearGuides()
        {
            foreach (var g in guides) dragLayer.Children.Remove(g);
            guides.Clear();
        }

        // ---------- test copies ----------

        /// <summary>
        /// Test copies only: "snapdrag:i:x,y" shows root icon i being dragged with the mouse at canvas point (x, y), guide
        /// lines and all, and leaves it there for a screenshot; "snapdrop:i:x,y" drops it there. Both wait 3 seconds first.
        /// </summary>
        bool RunSnapTestAction(string action)
        {
            if (action.StartsWith("folderout:")) { TestDragOutOfFolder(action.Substring(10)); return true; }
            if (RunNotesTestAction(action)) return true;
            if (action == "dropapps") { TestStartMenuDrops(); return true; }
            if (action.StartsWith("dropfile:"))
            {
                // Test copies only: a file-list drop with this name in it (as the Start menu can send), and what it became.
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, new[] { action.Substring(9) });
                var got = ShellHelper.ItemsFromData(data).FirstOrDefault();
                File.WriteAllText(Path.Combine(LauncherData.DataFolder, "dropfile.txt"), got == null ? "nothing added"
                    : "name: " + got.Name + "\r\npath: " + got.Path + "\r\nicon: " + (IconCache.Get(got.Path, 48) != null ? "yes" : "NO"));
                return true;
            }
            if (action == "animcheck")
            {
                // Test copies only: close, then open again, noting how see-through each window is along the way.
                var lines = new List<string>();
                Func<string> look = () =>
                {
                    Func<Window, string> op = w => w == null ? "-" : !w.IsVisible ? "hidden" : ((FrameworkElement)w.Content).Opacity.ToString("0.00");
                    return "launcher " + (IsVisible ? shell.Opacity.ToString("0.00") : "hidden") + ", notes " + op(notesWindow) + ", sensors " + op(sensorWindow);
                };
                Action<int, string> at = null;
                at = (ms, label) =>
                {
                    var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                    t.Tick += delegate { t.Stop(); lines.Add(label + ": " + look()); File.WriteAllLines(Path.Combine(LauncherData.DataFolder, "anim.txt"), lines); };
                    t.Start();
                };
                var start = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                start.Tick += delegate
                {
                    start.Stop();
                    lines.Add("open: " + look());
                    HideLauncher();
                    at(80, "closing, 0.08 s");
                    at(1500, "closed");
                    var again = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    again.Tick += delegate
                    {
                        again.Stop();
                        ShowLauncher();
                        at(60, "opening, 0.06 s");
                        at(300, "opening, 0.3 s");
                        at(2000, "open again");
                    };
                    again.Start();
                };
                start.Start();
                return true;
            }
            if (action == "lockcheck")
            {
                // Test copies only: lock, and note what the arrange menu offers then.
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                t.Tick += delegate
                {
                    t.Stop();
                    ToggleLock();
                    var report = new List<string> { "locked: " + S.LayoutLocked };
                    foreach (var o in ArrangeMenuItems())
                    {
                        var mi = o as MenuItem;
                        if (mi != null) report.Add((mi.IsEnabled ? "  usable: " : "  greyed: ") + mi.Header);
                    }
                    File.WriteAllLines(Path.Combine(LauncherData.DataFolder, "lock.txt"), report);
                };
                t.Start();
                return true;
            }
            if (action == "sendback" || action == "closereopen")
            {
                // Test copies only: send to background after 4 s; or (later) close the launcher and open it again.
                var step = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(action == "sendback" ? 4 : 10) };
                step.Tick += delegate
                {
                    step.Stop();
                    if (action == "sendback") { SendToBackground(); return; }
                    HideLauncher();
                    var again = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    again.Tick += delegate { again.Stop(); ShowLauncher(); };
                    again.Start();
                };
                step.Start();
                return true;
            }
            if (action == "togglesensors")
            {
                // Test copies only: press the sensors button in the header, a few seconds after starting.
                var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                later.Tick += delegate { later.Stop(); ToggleSensors(); };
                later.Start();
                return true;
            }
            bool show = action.StartsWith("snapdrag:"), drop = action.StartsWith("snapdrop:");
            if (!show && !drop) return false;
            var parts = action.Substring(9).Split(':');
            int i = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var xy = parts[1].Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            var cp = new Point(xy[0], xy[1]);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (i < 0 || i >= Data.Items.Count) return;
                var item = Data.Items[i];
                if (drop) { MoveItem(item, cp); return; }
                TileView t;
                if (!tiles.TryGetValue(item.Id, out t)) return;
                drag = new DragState { Tile = t, Item = item, Origin = DragOrigin.Canvas, Start = new Point() };
                BeginDrag();
                UpdateDrag(canvas.TranslatePoint(cp, this));
            };
            timer.Start();
            return true;
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SHParseDisplayName(string name, IntPtr bindCtx, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);
        [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern uint ILGetSize(IntPtr pidl);

        /// <summary>
        /// Test copies only: drops items from Windows' "Apps" folder (what the Start menu lists) the way a Start menu drag
        /// does (a "Shell IDList Array"), and writes to drops.txt which path each one got and whether it has an icon,
        /// next to what older versions saved.
        /// </summary>
        void TestStartMenuDrops()
        {
            var apps = AppCatalog.AppsFolderItems();
            // A mix: desktop apps (ids with a folder in them), desktop apps with their own app ID ("AcrobatReader"),
            // and Store apps (ids with a "!").
            var pick = apps.Where(a => a.Key.Contains("\\")).Take(6)
                .Concat(apps.Where(a => a.Key.IndexOfAny(new[] { '\\', '!' }) < 0).Take(6))
                .Concat(apps.Where(a => a.Key.Contains("!")).Take(4)).ToList();
            var lines = new List<string>();
            foreach (var a in pick)
            {
                IntPtr pidl;
                uint attrs;
                if (SHParseDisplayName(@"shell:AppsFolder\" + a.Key, IntPtr.Zero, out pidl, 0, out attrs) != 0 || pidl == IntPtr.Zero) continue;
                try
                {
                    // CIDA: count, offsets (parent, then each item), the parent (the desktop: an empty ID list), the items.
                    int size = (int)ILGetSize(pidl);
                    var ms = new MemoryStream();
                    var w = new BinaryWriter(ms);
                    w.Write(1);
                    w.Write(12);
                    w.Write(14);
                    w.Write((short)0);
                    var bytes = new byte[size];
                    System.Runtime.InteropServices.Marshal.Copy(pidl, bytes, 0, size);
                    w.Write(bytes);
                    var data = new DataObject();
                    data.SetData("Shell IDList Array", new MemoryStream(ms.ToArray()));

                    // What older versions kept: the item's full shell name.
                    string old = null;
                    IShellItem item = null;
                    try
                    {
                        Native.SHCreateItemFromIDList(pidl, typeof(IShellItem).GUID, out item);
                        item.GetDisplayName(Native.SIGDN_DESKTOPABSOLUTEPARSING, out old);
                    }
                    catch (Exception) { }
                    finally { Native.Release(item); }

                    var got = ShellHelper.ItemsFromData(data).FirstOrDefault();
                    if (got == null) { lines.Add(a.Value + " | nothing added"); continue; }
                    lines.Add(a.Value + " | before: " + (IconCache.Get(old, 48) != null ? "icon" : "NO ICON") + " (" + old + ")"
                              + " | now: " + (IconCache.Get(got.Path, 48) != null ? "icon" : "NO ICON") + " (" + got.Path + ") named " + got.Name);
                }
                finally { Native.ILFree(pidl); }
            }
            File.WriteAllLines(Path.Combine(LauncherData.DataFolder, "drops.txt"), lines);
        }

        /// <summary>
        /// Test copies only: "folderout:x,y" opens the first folder, grabs its first icon the way a mouse press does
        /// (mouse capture included), drags it to canvas point (x, y) outside the folder, waits a second, then lets go.
        /// What happened is written to folderout.txt in the test data folder.
        /// </summary>
        void TestDragOutOfFolder(string at)
        {
            var xy = at.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            var cp = new Point(xy[0], xy[1]);
            string report = Path.Combine(Environment.GetEnvironmentVariable("READYLAUNCHER_DATA") ?? ".", "folderout.txt");
            var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            wait.Tick += delegate
            {
                wait.Stop();
                var folder = Data.Items.FirstOrDefault(i => i.IsFolder);
                if (folder == null) { File.WriteAllText(report, "no folder"); return; }
                Activate();
                OpenFolder(folder, false);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var tile = folderItems.Children.OfType<TileView>().FirstOrDefault();
                    if (tile == null) { File.WriteAllText(report, "no icon in the folder"); return; }
                    drag = new DragState { Tile = tile, Item = tile.Item, Origin = DragOrigin.Folder, Folder = openFolder, Start = new Point() };
                    bool captured = tile.CaptureMouse();
                    BeginDrag();
                    var p = canvas.TranslatePoint(cp, this);
                    UpdateDrag(p);
                    var hold = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    hold.Tick += delegate
                    {
                        hold.Stop();
                        bool dragging = drag != null && drag.Tile == tile;
                        string text = "grabbed: " + captured + "\r\nstill dragging after 1 s: " + dragging
                                    + "\r\nstill holding the mouse: " + tile.IsMouseCaptured + "\r\nfolder faded: " + (overlay.Opacity == 0);
                        if (dragging)
                        {
                            // Let go, exactly as the mouse button coming up does.
                            var d = drag;
                            drag = null;
                            tile.ReleaseMouseCapture();
                            EndDrag(d, p);
                            text += "\r\nlet go";
                        }
                        File.WriteAllText(report, text);
                    };
                    hold.Start();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            wait.Start();
        }
    }
}
