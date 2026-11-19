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
    /// <summary>One divider line or text label on the launcher, with a dashed outline while hovered or dragged.</summary>
    internal class DecorView : Grid
    {
        public Decoration D;
        public double Pad;          // invisible margin that makes thin lines easy to grab
        public Rectangle Outline;
    }

    internal class DecorDrag
    {
        public DecorView View;
        public Point Start;
        public double X0, Y0, Length0;
        public bool Resize;         // dragging the end of a line changes its length
        public bool Active;
        public bool Blocked;        // a drag while the layout is locked: nothing moves, and it isn't a click
        public int Clicks;          // 2 for the second click of a double-click
    }

    /// <summary>Divider lines and text labels: drawing them, dragging, resizing, and their menus.</summary>
    internal partial class LauncherWindow
    {
        const double LineGrab = 6;   // px around a line that still counts as "on the line"
        const double EndGrab = 10;   // px at a line's far end that resize instead of move
        readonly Dictionary<string, DecorView> decorViews = new Dictionary<string, DecorView>();
        DecorDrag decorDrag;

        void RenderDecorations()
        {
            decorViews.Clear();
            foreach (var d in Data.Decorations)
            {
                var v = new DecorView { D = d, Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
                FillDecorView(v, d);
                AttachDecorHandlers(v);
                canvas.Children.Add(v);
                decorViews[d.Id] = v;
            }
        }

        Brush DecorBrush(Decoration d)
        {
            if (!string.IsNullOrWhiteSpace(d.Color)) return new SolidColorBrush(ParseColor(d.Color, fgColor));
            // "Auto" follows the launcher's text color; lines are a little softer than text.
            return d.Kind == DecorKind.Line ? new SolidColorBrush(Color.FromArgb(0xB0, fgColor.R, fgColor.G, fgColor.B)) : fg;
        }

        /// <summary>(Re)draw a view from <paramref name="d"/>, which may be a preview copy of the view's decoration.</summary>
        void FillDecorView(DecorView v, Decoration d)
        {
            bool showOutline = v.Outline != null && v.Outline.Visibility == Visibility.Visible;
            v.Children.Clear();
            FrameworkElement body;
            if (d.Kind == DecorKind.Line)
            {
                v.Pad = LineGrab;
                double len = Math.Max(4, d.Length), th = Math.Max(1, Math.Min(60, d.Thickness));
                body = new Border
                {
                    Width = d.Vertical ? th : len, Height = d.Vertical ? len : th,
                    CornerRadius = new CornerRadius(Math.Min(th, 6) / 2), Background = DecorBrush(d)
                };
                v.ToolTip = null;
            }
            else
            {
                v.Pad = 3;
                body = TextStyle.Build(d, fgColor, PixelsPerDip);
                v.ToolTip = d.Link == null ? null : "Click to open " + LinkDescription(d.Link) + "\nDrag to move it, right-click to change it";
            }
            v.Cursor = d.Kind == DecorKind.Text && d.Link != null ? Cursors.Hand : Cursors.SizeAll;
            body.Margin = new Thickness(v.Pad);
            body.IsHitTestVisible = false;
            v.Outline = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromArgb(0x99, fgColor.R, fgColor.G, fgColor.B)), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 3, 3 }, RadiusX = 3, RadiusY = 3, IsHitTestVisible = false,
                Visibility = showOutline ? Visibility.Visible : Visibility.Hidden
            };
            v.Children.Add(v.Outline);
            v.Children.Add(body);
            Canvas.SetLeft(v, d.X - v.Pad);
            Canvas.SetTop(v, d.Y - v.Pad);
            v.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (v.D != null && selDecor.Contains(v.D)) ShowDecorSelected(v);
        }

        double PixelsPerDip
        {
            get { try { return VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch (Exception) { return 1; } }
        }

        static bool IsLink(Decoration d) { return d.Kind == DecorKind.Text && d.Link != null; }

        public static string LinkDescription(LauncherItem link)
        {
            if (link.Kind == ItemKind.Keys) return "(presses " + AppSettings.HotkeyText(link.KeyMods, link.KeyKey) + ")";
            string target = string.IsNullOrEmpty(link.Arguments) ? link.Path : link.Path + " " + link.Arguments;
            return target ?? "";
        }

        bool NearLineEnd(DecorView v, Point inView)
        {
            if (v.D.Kind != DecorKind.Line) return false;
            var size = v.DesiredSize;
            return v.D.Vertical ? inView.Y >= size.Height - v.Pad - EndGrab : inView.X >= size.Width - v.Pad - EndGrab;
        }

        void AttachDecorHandlers(DecorView v)
        {
            v.MouseEnter += delegate { v.Outline.Visibility = Visibility.Visible; };
            v.MouseLeave += delegate { if (!v.IsMouseCaptured && !selDecor.Contains(v.D)) v.Outline.Visibility = Visibility.Hidden; };
            v.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { ToggleSelected(v.D); return; }
                if (InMultiSelection(v.D) && !S.LayoutLocked)
                {
                    StartGroupMove(v, e.GetPosition(canvas));   // drag all the selected items together
                    v.CaptureMouse();
                    return;
                }
                ClearSelection();
                if (e.ClickCount == 2 && !IsLink(v.D))   // a link opens on a click (or double-click) instead
                {
                    decorDrag = null;
                    v.ReleaseMouseCapture();
                    EditDecoration(v.D, false);
                    return;
                }
                decorDrag = new DecorDrag
                {
                    View = v, Start = e.GetPosition(canvas), X0 = v.D.X, Y0 = v.D.Y, Length0 = v.D.Length,
                    Resize = NearLineEnd(v, e.GetPosition(v)) && !S.LayoutLocked, Clicks = e.ClickCount
                };
                v.CaptureMouse();
            };
            v.MouseMove += (s, e) =>
            {
                if (groupMove != null && groupMove.Source == v) { UpdateGroupMove(e.GetPosition(canvas)); return; }
                if (decorDrag == null || decorDrag.View != v)
                {
                    v.Cursor = IsLink(v.D) ? Cursors.Hand
                        : S.LayoutLocked ? Cursors.Arrow   // locked: nothing to move or resize
                        : NearLineEnd(v, e.GetPosition(v)) ? (v.D.Vertical ? Cursors.SizeNS : Cursors.SizeWE) : Cursors.SizeAll;
                    return;
                }
                var p = e.GetPosition(canvas);
                double dx = p.X - decorDrag.Start.X, dy = p.Y - decorDrag.Start.Y;
                if (!decorDrag.Active)
                {
                    if (Math.Abs(dx) < 3 && Math.Abs(dy) < 3) return;
                    if (S.LayoutLocked) { decorDrag.Blocked = true; return; }
                    decorDrag.Active = true;
                }
                var d = v.D;
                if (decorDrag.Resize)
                {
                    d.Length = Math.Max(10, Math.Round(decorDrag.Length0 + (d.Vertical ? dy : dx)));
                    FillDecorView(v, d);
                }
                else
                {
                    double viewW = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
                    d.X = Math.Max(0, Math.Min(viewW - 12, Math.Round(decorDrag.X0 + dx)));
                    d.Y = Math.Max(0, Math.Round(decorDrag.Y0 + dy));
                    Canvas.SetLeft(v, d.X - v.Pad);
                    Canvas.SetTop(v, d.Y - v.Pad);
                }
            };
            v.MouseLeftButtonUp += (s, e) =>
            {
                if (groupMove != null && groupMove.Source == v)
                {
                    var g = groupMove;
                    groupMove = null;
                    v.ReleaseMouseCapture();
                    e.Handled = true;
                    if (g.Active) EndGroupMove(g, e.GetPosition(canvas)); else ClearSelection();
                    return;
                }
                if (decorDrag == null || decorDrag.View != v) return;
                e.Handled = true;
                bool clicked = !decorDrag.Active && !decorDrag.Blocked && (!S.OpenWithDoubleClick || decorDrag.Clicks >= 2);
                var d = v.D;
                v.ReleaseMouseCapture();   // LostMouseCapture finishes the drag
                if (clicked && IsLink(d)) LaunchItem(d.Link, false);
            };
            v.LostMouseCapture += delegate
            {
                if (!v.IsMouseOver && !selDecor.Contains(v.D)) v.Outline.Visibility = Visibility.Hidden;
                if (groupMove != null && groupMove.Source == v) { var g = groupMove; groupMove = null; CancelGroupMove(g); return; }
                if (decorDrag == null || decorDrag.View != v) return;
                bool moved = decorDrag.Active;
                decorDrag = null;
                if (moved) { app.Save(); UpdateCanvasExtent(); }
            };
            v.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (InMultiSelection(v.D)) { ShowMenu(null, SelectionMenuItems()); return; }
                ClearSelection();
                ShowDecorMenu(v.D);
            };
        }

        void ShowDecorMenu(Decoration d)
        {
            var items = new List<object>();
            if (d.Kind == DecorKind.Line)
            {
                items.Add(Item("Change line...", () => EditDecoration(d, false), false, ""));
                items.Add(Item(d.Vertical ? "Make it horizontal" : "Make it vertical", () => { d.Vertical = !d.Vertical; app.Save(); RenderItems(); }, false, ""));
            }
            else
            {
                if (d.Link != null) items.Add(Item("Open link", () => LaunchItem(d.Link, false), false, ""));
                items.Add(Item("Change text...", () => EditDecoration(d, false), false, ""));
            }
            items.Add(Item("Duplicate", () => DuplicateDecoration(d), false, ""));
            items.Add(new Separator());
            items.Add(Item(d.Kind == DecorKind.Line ? "Remove line" : "Remove text", () => { Data.Decorations.Remove(d); app.Save(); RenderItems(); }, false, ""));
            ShowMenu(null, items);
        }

        void DuplicateDecoration(Decoration d)
        {
            var c = d.Clone();
            c.Id = Guid.NewGuid().ToString("N");
            if (d.Kind == DecorKind.Line && !d.Vertical) c.Y += 16;
            else if (d.Kind == DecorKind.Line) c.X += 16;
            else { c.X += 16; c.Y += 16; }
            Data.Decorations.Add(c);
            app.Save();
            RenderItems();
        }

        /// <summary>The middle of what is currently visible in the icon area, in canvas coordinates.</summary>
        Point VisibleCenter()
        {
            double w = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
            double h = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : 300;
            return new Point(w / 2, scroll.VerticalOffset + h / 2);
        }

        void AddLine(bool vertical, Point? at)
        {
            double w = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
            double h = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : 300;
            var p = at ?? VisibleCenter();
            var d = new Decoration { Kind = DecorKind.Line, Vertical = vertical, Thickness = 2 };
            if (vertical)
            {
                d.Length = Math.Max(40, Math.Round(h - 2 * Pad));
                d.X = Math.Round(Math.Max(0, p.X));
                d.Y = Math.Round(scroll.VerticalOffset + Pad);
            }
            else
            {
                d.Length = Math.Max(40, Math.Round(w - 2 * Pad));
                d.X = Pad;
                d.Y = Math.Round(Math.Max(0, p.Y));
            }
            AddDecoration(d);
        }

        /// <summary>New text starts at the very top, centered (and stays centered while it is being typed).</summary>
        void AddText(Point? at)
        {
            var d = new Decoration { Kind = DecorKind.Text, FontSize = 20, Font = "Segoe UI" };
            d.X = Math.Round(ViewWidth / 2);
            d.Y = Math.Round(scroll.VerticalOffset + Pad);
            AddDecoration(d, true);
        }

        double ViewWidth { get { return scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40; } }

        /// <summary>Move a text label so it sits centered across the icon area.</summary>
        void CenterText(DecorView v, Decoration d)
        {
            double w = Math.Max(0, v.DesiredSize.Width - 2 * v.Pad);
            d.X = Math.Round(Math.Max(0, (ViewWidth - w) / 2));
            Canvas.SetLeft(v, d.X - v.Pad);
        }

        /// <summary>A clickable text label that opens what <paramref name="it"/> opens, placed just under the icon.</summary>
        void AddTextLinkFor(LauncherItem it)
        {
            var link = it.CloneShortcut();
            link.IconPath = "";
            var d = new Decoration { Kind = DecorKind.Text, FontSize = 16, Font = "Segoe UI", Text = it.Name, Underline = true, Link = link };
            double viewW = scroll.ViewportWidth > 0 ? scroll.ViewportWidth : Width - 2 * Shadow - 40;
            if (Data.Items.Contains(it))
            {
                d.X = Math.Round(Math.Max(0, Math.Min(it.X + 6, viewW - 120)));
                d.Y = Math.Round(it.Y + CellH + 2);
            }
            else   // an icon inside a folder: put the link in the middle of the view
            {
                var c = VisibleCenter();
                d.X = Math.Round(Math.Max(0, c.X - 60));
                d.Y = Math.Round(Math.Max(0, c.Y - 12));
            }
            AddDecoration(d);
        }

        /// <summary>Show the new line / text straight away and open its settings; Cancel takes it away again.</summary>
        void AddDecoration(Decoration d, bool keepCentered = false)
        {
            ClearSelection();
            Data.Decorations.Add(d);
            RenderItems();
            if (!EditDecoration(d, true, keepCentered))
            {
                Data.Decorations.Remove(d);
                RenderItems();
            }
        }

        bool EditDecoration(Decoration d, bool isNew, bool keepCentered = false)
        {
            DecorView v;
            decorViews.TryGetValue(d.Id, out v);
            var work = d.Clone();
            if (keepCentered && v != null) CenterText(v, work);
            bool ok = DecorationDialog.Edit(this, app, work, isNew, fgColor, bg, c =>
            {
                if (v == null) return;
                FillDecorView(v, c);
                if (keepCentered) CenterText(v, c);
                UpdateCanvasExtent();
            });
            if (ok && work.Kind == DecorKind.Text && string.IsNullOrWhiteSpace(work.Text)) ok = false;
            if (ok)
            {
                d.CopyFrom(work);
                app.Save();
            }
            RenderItems();   // also undoes the preview after Cancel
            return ok;
        }

        /// <summary>Test copies only (see Program): open a dialog the way the menus would.</summary>
        /// <summary>Test copies only: the first text in a window that reads exactly <paramref name="text"/>.</summary>
        static TextBlock FindText(DependencyObject root, string text)
        {
            var tb = root as TextBlock;
            if (tb != null && tb.Text == text) return tb;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                var d = child as DependencyObject;
                if (d == null) continue;
                var found = FindText(d, text);
                if (found != null) return found;
            }
            return null;
        }

        public void RunTestAction(string action)
        {
            switch (action)
            {
                case "addline": AddLine(false, null); break;
                case "addvline": AddLine(true, null); break;
                case "addtext": AddText(null); break;
                case "editfirst": if (Data.Decorations.Count > 0) EditDecoration(Data.Decorations[0], false); break;
                case "addallapps": OpenAddAllApps(null); break;
                case "sortcompany": SortItems(CompareCompany); break;
                case "sortcategory": SortItems(CompareCategory); break;
                default:
                    if (RunSnapTestAction(action)) break;
                    if (action.StartsWith("themeexport:") || action.StartsWith("themeimport:"))
                    {
                        // Test copies only: export the theme in use to a file, or import one, without the file dialogs.
                        string file = action.Substring(12), report = System.IO.Path.Combine(LauncherData.DataFolder, "themefile.txt");
                        try
                        {
                            if (action.StartsWith("themeexport:"))
                            {
                                var mine = Themes.FindMine(S, S.Theme);
                                var builtIn = Themes.Find(S.Theme);
                                string name = mine != null ? mine.Name : builtIn != null ? builtIn.Name : "My look";
                                ThemeFiles.Export(name, Themes.LookForId(S, S.Theme), file);
                                System.IO.File.AppendAllText(report, "exported " + name + "\r\n");
                            }
                            else
                            {
                                var u = ThemeFiles.Import(file, S);
                                app.Save();
                                System.IO.File.AppendAllText(report, "imported " + u.Name + " | picture " + System.IO.Path.GetFileName(u.Look.BackgroundImage) + " | buttons " + u.Look.ButtonsColor + " | names " + u.Look.IconTextColor + " | background " + u.Look.BackgroundColor + "\r\n");
                            }
                        }
                        catch (Exception ex) { System.IO.File.AppendAllText(report, "refused: " + ex.Message + "\r\n"); }
                    }
                    else if (action.StartsWith("animcheck:")) TestAnimCheck(action.Substring(10));
                    else if (action.StartsWith("animshots:")) TestAnimShots(action.Substring(10));
                    else if (action.StartsWith("animframes:")) TestAnimFrames(action.Substring(11));
                    else if (action.StartsWith("animfps:")) TestAnimFps(action.Substring(8));
                    else if (action.StartsWith("sensormove:"))
                    {
                        // sensormove:from,to  (after the first readings arrive)
                        var n = action.Substring(11).Split(',').Select(int.Parse).ToArray();
                        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                        t.Tick += delegate { t.Stop(); if (sensorWindow != null) sensorWindow.TestMove(n[0], n[1]); };
                        t.Start();
                    }
                    else if (action.StartsWith("sensorshot:") || action.StartsWith("sensorgraph:") || action.StartsWith("sensorlevels:") || action.StartsWith("sensorgear:"))
                    {
                        // sensorshot:<seconds>:<png file>   save the sensor window (and a line about it in <file>.txt) after a while
                        // sensorgraph:<seconds>:<tile>      what a tile's right-click "Show graph" does
                        string rest = action.Substring(action.IndexOf(':') + 1);
                        int colon = rest.IndexOf(':');
                        double wait = double.Parse(rest.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture);
                        string arg = rest.Substring(colon + 1);
                        bool shot = action.StartsWith("sensorshot:");
                        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(wait) };
                        t.Tick += delegate
                        {
                            t.Stop();
                            if (sensorWindow == null || !sensorWindow.IsVisible) return;
                            if (action.StartsWith("sensorlevels:")) { sensorWindow.TestLevels(int.Parse(arg)); return; }   // opens the levels window
                            if (action.StartsWith("sensorgear:")) { sensorWindow.TestGear(int.Parse(arg)); return; }       // opens a tile's gear menu
                            if (!shot) { sensorWindow.TestToggleGraph(int.Parse(arg)); return; }
                            var w = sensorWindow;
                            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * 2), (int)Math.Ceiling(w.ActualHeight * 2), 192, 192, System.Windows.Media.PixelFormats.Pbgra32);
                            rtb.Render(w);
                            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                            using (var fs = System.IO.File.Create(arg)) enc.Save(fs);
                            System.IO.File.AppendAllText(arg + ".txt", w.TestReport() + "\r\n");
                        };
                        t.Start();
                    }
                    else if (action.StartsWith("settingstip:"))
                    {
                        // settingstip:<seconds>:<label>   show the tooltip of a label in the open Settings window, as pointing at it does
                        string rest = action.Substring(12);
                        int colon = rest.IndexOf(':');
                        double wait = double.Parse(rest.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture);
                        string label = rest.Substring(colon + 1);
                        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(wait) };
                        t.Tick += delegate
                        {
                            t.Stop();
                            foreach (Window w in Application.Current.Windows)
                            {
                                if (!(w is SettingsWindow)) continue;
                                var found = FindText(w, label);
                                if (found == null || !(found.ToolTip is string)) return;
                                var tip = new System.Windows.Controls.ToolTip { Content = found.ToolTip };
                                found.ToolTip = tip;   // the label's own tooltip (it takes the window's styles)
                                tip.PlacementTarget = found;
                                tip.IsOpen = true;
                            }
                        };
                        t.Start();
                    }
                    else if (action.StartsWith("textlink:"))
                    {
                        var it = Data.Items.FirstOrDefault(i => i.Name == action.Substring(9));
                        if (it != null) AddTextLinkFor(it);
                    }
                    else if (action.StartsWith("select"))
                    {
                        // select:x,y,w,h  select+move:x,y,w,h,dx,dy  select+delete:x,y,w,h  select+folder:x,y,w,h
                        var n = action.Substring(action.IndexOf(':') + 1).Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        boxStartItems = new HashSet<LauncherItem>();
                        boxStartDecor = new HashSet<Decoration>();
                        SelectInBox(new Rect(n[0], n[1], n[2], n[3]));
                        if (action.StartsWith("select+move:"))
                        {
                            var src = tiles.Values.FirstOrDefault(t => selItems.Contains(t.Item));
                            if (src != null)
                            {
                                var from = new Point(Canvas.GetLeft(src) + 20, Canvas.GetTop(src) + 20);
                                StartGroupMove(src, from);
                                var g = groupMove;
                                groupMove = null;
                                EndGroupMove(g, new Point(from.X + n[4], from.Y + n[5]));
                            }
                        }
                        else if (action.StartsWith("select+delete:")) RemoveSelected();
                        else if (action.StartsWith("select+folder:")) FolderFromSelection();
                    }
                    break;
            }
        }

        /// <summary>Lowest point of any line or text, so the icon area can scroll down to it.</summary>
        double DecorationsBottom()
        {
            double bottom = 0;
            foreach (var v in decorViews.Values)
                bottom = Math.Max(bottom, Canvas.GetTop(v) + v.DesiredSize.Height);
            return bottom;
        }
    }
}
