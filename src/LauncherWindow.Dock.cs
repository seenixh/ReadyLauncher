using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>The quick-launch strip: most-used or recently-installed apps, with its own look.</summary>
    internal partial class LauncherWindow
    {
        Border dockArea;
        Color dockFgColor;
        Brush dockFg;
        StackPanel dockItems;
        int dockRequest;

        double DockIconSize
        {
            get
            {
                switch (S.StripIconSize)
                {
                    case StripIconMode.Tiny: return 18;
                    case StripIconMode.ExtraSmall: return 24;
                    case StripIconMode.Small: return 30;
                    case StripIconMode.Medium: return 36;
                    case StripIconMode.Large: return 44;
                    case StripIconMode.ExtraLarge: return 56;
                }
                switch (S.IconSize)   // "match the launcher"
                {
                    case IconSizeMode.Tiny: return 22;
                    case IconSizeMode.ExtraSmall: return 26;
                    case IconSizeMode.Small: return 30;
                    case IconSizeMode.Large: return 40;
                    default: return 34;
                }
            }
        }

        bool DockHorizontal { get { return S.DockPos == DockPosition.Bottom || S.DockPos == DockPosition.Top; } }

        FrameworkElement BuildDockArea()
        {
            dockArea = null;
            dockItems = null;
            if (S.Dock == DockSource.Hidden) return null;
            bool horizontal = DockHorizontal;
            double stripOpacity = 1 - Math.Max(0, Math.Min(100, S.DockTransparency)) / 100.0;

            bool detached = S.DockDetached;

            // The strip's text color follows the strip's own color / picture when it has one.
            // A detached strip sits on the launcher's color (not its picture or the window behind it).
            double lum = detached ? Luminance(bg) : windowLum;
            if (!string.IsNullOrWhiteSpace(S.DockBackgroundColor))
                lum = lum * (1 - stripOpacity) + Luminance(ParseColor(S.DockBackgroundColor, bg)) * stripOpacity;
            lum = BlendPicture(lum, S.DockImageOff ? "" : S.DockBackgroundImage, stripOpacity * (1 - S.DockImageTransparency / 100.0));
            bool lampsBehind = S.LampPanel == LampPanelPlace.StripBackground, lampsUnder = S.LampPanel == LampPanelPlace.UnderStrip;
            if (lampsBehind) lum = 0.04;   // the lamp panel behind the icons is nearly black
            dockFgColor = TextColorFor(lum);
            dockFg = new SolidColorBrush(dockFgColor);

            if (detached)
            {
                // Its own rounded panel in the launcher's color, so it is visible even without a strip color.
                double baseAlpha = Math.Max(1 / 255.0, bgAlpha * stripOpacity);
                dockArea = new Border
                {
                    CornerRadius = new CornerRadius(Corner),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Max(0.4, baseAlpha)), FrameBorder.R, FrameBorder.G, FrameBorder.B)),
                    Background = new SolidColorBrush(Color.FromArgb((byte)(255 * baseAlpha), bg.R, bg.G, bg.B))
                };
            }
            else
            {
                dockArea = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(0xB0 * Math.Max(0.3, stripOpacity)), fgColor.R, fgColor.G, fgColor.B)),
                    Background = Brushes.Transparent   // edge to edge; the window's rounded clip shapes its corners
                };
                double t = S.DockDivider ? 1.5 : 0;
                switch (S.DockPos)
                {
                    case DockPosition.Bottom: dockArea.BorderThickness = new Thickness(0, t, 0, 0); DockPanel.SetDock(dockArea, Dock.Bottom); break;
                    case DockPosition.Top: dockArea.BorderThickness = new Thickness(0, 0, 0, t); DockPanel.SetDock(dockArea, Dock.Top); break;
                    case DockPosition.Left: dockArea.BorderThickness = new Thickness(0, 0, t, 0); DockPanel.SetDock(dockArea, Dock.Left); break;
                    default: dockArea.BorderThickness = new Thickness(t, 0, 0, 0); DockPanel.SetDock(dockArea, Dock.Right); break;
                }
            }
            // Empty space on the strip moves the launcher (a detached strip follows it).
            var strip = dockArea;
            strip.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == strip && e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch (InvalidOperationException) { }
                }
            };

            // Optional strip background: a color and/or a picture, under one transparency setting.
            var layers = new Grid { Opacity = stripOpacity, IsHitTestVisible = false };
            var radius = new CornerRadius(detached ? Corner - 1 : 0);
            if (!string.IsNullOrWhiteSpace(S.DockBackgroundColor))
                layers.Children.Add(new Border { CornerRadius = radius, Background = new SolidColorBrush(ParseColor(S.DockBackgroundColor, bg)) });
            var picture = ImageBrushFor(S.DockImageOff ? "" : S.DockBackgroundImage, S.DockImageTransparency);
            if (picture != null) layers.Children.Add(new Border { CornerRadius = radius, Background = picture });

            var content = new DockPanel { LastChildFill = true, Margin = horizontal ? new Thickness(14, 6, 14, 6) : new Thickness(6, 12, 6, 12) };
            var toggle = BuildDockMenuButton(horizontal);
            DockPanel.SetDock(toggle, horizontal ? Dock.Left : Dock.Top);
            content.Children.Add(toggle);

            dockItems = new StackPanel { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
            if (horizontal)
            {
                dockItems.VerticalAlignment = VerticalAlignment.Center;
                dockItems.HorizontalAlignment = S.DockAlign == DockAlignment.Center ? HorizontalAlignment.Center
                    : S.DockAlign == DockAlignment.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            }
            else
            {
                dockItems.HorizontalAlignment = HorizontalAlignment.Center;
                dockItems.VerticalAlignment = S.DockAlign == DockAlignment.Center ? VerticalAlignment.Center
                    : S.DockAlign == DockAlignment.End ? VerticalAlignment.Bottom : VerticalAlignment.Top;
            }
            content.Children.Add(dockItems);

            var grid = new Grid();
            grid.Children.Add(layers);
            // The blinking lamp strip can fill the strip behind its icons, or run along its bottom.
            var inner = new CornerRadius(detached ? Corner - 1 : 0);
            // Right-clicking them changes their activity; dragging them moves the launcher, like the strip's empty space.
            if (lampsBehind)
            {
                var behind = new LampPanel(S.LampMode, S.LampRows, true) { Corners = inner, Palette = LampPalette.From(S) };
                HookLampStrip(behind, true);
                grid.Children.Add(behind);
            }
            if (lampsUnder)
            {
                var withLamps = new DockPanel { LastChildFill = true };
                var lamps = new LampPanel(S.LampMode, S.LampRows) { Corners = new CornerRadius(0, 0, inner.BottomRight, inner.BottomLeft), Palette = LampPalette.From(S) };
                HookLampStrip(lamps, true);
                DockPanel.SetDock(lamps, Dock.Bottom);
                withLamps.Children.Add(lamps);
                withLamps.Children.Add(content);
                grid.Children.Add(withLamps);
            }
            else grid.Children.Add(content);
            dockArea.Child = grid;
            RefreshDock();
            return dockArea;
        }

        /// <summary>Swap just the strip (used by the in-window toggle), keeping its place in the layout.</summary>
        void RebuildDock()
        {
            if (S.DockDetached)
            {
                BuildDockArea();
                UpdateDetachedDock();
                return;
            }
            int idx = dockArea == null ? -1 : main.Children.IndexOf(dockArea);
            if (idx >= 0) main.Children.RemoveAt(idx);
            var d = BuildDockArea();
            if (d != null) main.Children.Insert(idx >= 0 ? idx : 0, d);
        }

        // ---------- detached strip ----------

        DockWindow dockWindow;

        /// <summary>Create, refresh, show or hide the floating strip to match the settings and the launcher.</summary>
        void UpdateDetachedDock()
        {
            bool wanted = S.DockDetached && S.Dock != DockSource.Hidden && dockArea != null;
            if (!wanted)
            {
                if (dockWindow != null) { dockWindow.Close(); dockWindow = null; }
                return;
            }
            if (dockWindow == null)
            {
                dockWindow = new DockWindow();
                dockWindow.Topmost = !inBackground;   // behind other windows too while the launcher is sent to the background
                // It sizes itself to the icons after being shown, so line it up again once its size settles.
                dockWindow.SizeChanged += delegate { PlaceSatellites(); };
            }
            if (dockWindow.Content != dockArea) dockWindow.SetStrip(dockArea, S.DockPos);
            if (!IsVisible) { dockWindow.Hide(); return; }
            if (dockWindow.Owner == null) dockWindow.Owner = this;
            dockWindow.Show();
            PlaceDetachedDock();
        }

        /// <summary>Keep the floating strip lined up with the launcher, the chosen distance away.</summary>
        void PlaceDetachedDock()
        {
            if (dockWindow == null || !dockWindow.IsVisible) return;
            double gap = Math.Max(0, S.DockGap) / DpiScale;
            double panelL = Left + Shadow, panelT = Top + Shadow;
            double panelW = ActualWidth - 2 * Shadow, panelH = ActualHeight - 2 * Shadow;
            if (DockHorizontal) dockWindow.Width = panelW; else dockWindow.Height = panelH;
            dockWindow.UpdateLayout();
            double w = dockWindow.ActualWidth, h = dockWindow.ActualHeight;
            switch (S.DockPos)
            {
                // above or below the launcher, or beyond a lamp strip floating there
                case DockPosition.Bottom: dockWindow.Left = panelL; dockWindow.Top = BottomEdge() + gap; break;
                case DockPosition.Top: dockWindow.Left = panelL; dockWindow.Top = TopEdge() - gap - h; break;
                case DockPosition.Left: dockWindow.Left = panelL - gap - w; dockWindow.Top = panelT; break;
                default: dockWindow.Left = panelL + panelW + gap; dockWindow.Top = panelT; break;
            }
        }

        /// <summary>One small round button showing the current choice; clicking it opens a menu of options.</summary>
        FrameworkElement BuildDockMenuButton(bool horizontal)
        {
            bool frequent = S.Dock == DockSource.Frequent;
            var glyph = new TextBlock
            {
                Text = frequent ? "" : "", FontFamily = TileView.IconFont, FontSize = 13, Foreground = dockFg,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var c = PartColor(S.ButtonsColor, dockFgColor);   // the same color as the buttons at the top, if one is chosen
            glyph.Foreground = new SolidColorBrush(c);
            var normal = new SolidColorBrush(Color.FromArgb(0x1C, c.R, c.G, c.B));
            var hover = new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B));
            var button = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = normal, Child = glyph, Cursor = Cursors.Hand,
                ToolTip = (frequent ? "Showing your most used apps" : "Showing your most recently installed apps") + " - click to change, right-click to see the apps you've hidden",
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = horizontal ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 10)
            };
            button.MouseEnter += delegate { button.Background = hover; };
            button.MouseLeave += delegate { button.Background = normal; };
            button.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                var menu = DockButtonMenu(button);
                menu.Items.Add(Item("Most used apps", () => SetDockSource(DockSource.Frequent), S.Dock == DockSource.Frequent, ""));
                menu.Items.Add(Item("Recently installed apps", () => SetDockSource(DockSource.RecentlyInstalled), S.Dock == DockSource.RecentlyInstalled, ""));
                menu.Items.Add(new Separator());
                menu.Items.Add(Item("Quick launch settings...", () => app.OpenSettings(SettingsWindow.QuickLaunchTab), false, ""));
                menu.IsOpen = true;
            };
            // Right-click: the apps hidden from the strip, to bring one back.
            button.MouseRightButtonDown += (s, e) => { e.Handled = true; };
            button.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                var menu = DockButtonMenu(button);
                foreach (var i in HiddenDockMenuItems()) menu.Items.Add(SettingsTheme.MenuEntry(i));
                menu.IsOpen = true;
            };
            dockButton = button;
            return button;
        }

        Border dockButton;   // the strip's round button (for the test copies' checks)

        /// <summary>A menu for the round button, opening toward the launcher.</summary>
        ContextMenu DockButtonMenu(FrameworkElement button)
        {
            var menu = SettingsTheme.ThemeMenu(new ContextMenu { PlacementTarget = button });
            switch (S.DockPos)
            {
                case DockPosition.Bottom: menu.Placement = PlacementMode.Top; break;
                case DockPosition.Top: menu.Placement = PlacementMode.Bottom; break;
                case DockPosition.Left: menu.Placement = PlacementMode.Right; break;
                default: menu.Placement = PlacementMode.Left; break;
            }
            return menu;
        }

        /// <summary>The round button's right-click list: every app hidden from the strip; clicking one brings it back.</summary>
        List<object> HiddenDockMenuItems()
        {
            var items = new List<object>();
            var hidden = S.DockHidden.OrderBy(h => DockAppName(h), StringComparer.CurrentCultureIgnoreCase).ToList();
            if (hidden.Count == 0)
            {
                items.Add(new TextBlock { Text = "No hidden apps", Opacity = 0.7, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 2) });
                items.Add(new TextBlock { Text = "Tip: right-click an app in quick launch to hide it", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false });
                return items;
            }
            items.Add(new TextBlock { Text = "Hidden apps: click one to show it again", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 4) });
            foreach (var h in hidden)
            {
                var one = h;
                string name = DockAppName(h), lists = DockListNames(h);
                var mi = Item(name + " (removed from " + lists + ")", () => RestoreToDock(one), false, null);
                mi.ToolTip = "Show " + name + " in " + lists + " again";
                var icon = string.IsNullOrEmpty(h.Path) ? null : IconCache.Get(h.Path, 32);
                if (icon != null) mi.Icon = new Image { Source = icon, Width = 16, Height = 16 };
                items.Add(mi);
            }
            if (hidden.Count > 1)
            {
                items.Add(new Separator());
                items.Add(Item("Show all " + hidden.Count + " again", () => RestoreToDock(null), false, ""));
            }
            return items;
        }

        /// <summary>A strip list's short name: Most Used or Recently Installed.</summary>
        static string DockListName(DockSource list)
        {
            return list == DockSource.Frequent ? "Most Used" : list == DockSource.RecentlyInstalled ? "Recently Installed" : "quick launch";
        }

        /// <summary>The lists an app was removed from: "Most Used", "Recently Installed" or "Most Used & Recently Installed".</summary>
        static string DockListNames(HiddenDockApp h)
        {
            return string.Join(" & ", h.HiddenLists().Select(DockListName));
        }

        static string DockAppName(HiddenDockApp h)
        {
            if (!string.IsNullOrEmpty(h.Name)) return h.Name;
            try { return System.IO.Path.GetFileNameWithoutExtension(h.Path); } catch (Exception) { return h.Path; }
        }

        /// <summary>
        /// Takes an app off the list the strip is showing (it still shows in the other list); the next app in line takes
        /// its place.
        /// </summary>
        void HideFromDock(LauncherItem it)
        {
            if (it == null || S.Dock == DockSource.Hidden) return;
            var known = S.DockHidden.FirstOrDefault(h => h.Matches(it));
            if (known == null) S.DockHidden.Add(known = new HiddenDockApp { Name = it.Name ?? "", Path = it.Path ?? "" });
            known.SetHidden(S.Dock, true);
            app.Save();
            if (dockItems != null)
            {
                var tile = dockItems.Children.OfType<TileView>().FirstOrDefault(v => v.Item == it);
                if (tile != null) dockItems.Children.Remove(tile);   // right away, before the list is read again
            }
            RefreshDock();
        }

        /// <summary>Puts a hidden app back in every list it was removed from (null: all of them).</summary>
        void RestoreToDock(HiddenDockApp h)
        {
            if (h == null) S.DockHidden.Clear();
            else S.DockHidden.Remove(h);
            app.Save();
            RefreshDock();
        }

        /// <summary>
        /// Test copies only: steps (separated by ;) two seconds apart, written to dock-test.txt. list: the apps on the
        /// strip; menu:n: app n's right-click menu; remove:n: its "Remove from quick launch"; hidden: the round button's
        /// right-click list; restore:name and restoreall: click those in it; tip: the round button's tooltip;
        /// source:Frequent or source:RecentlyInstalled: switch the strip's list.
        /// </summary>
        void TestDock(string script)
        {
            var steps = new Queue<string>(script.Split(';'));
            string log = System.IO.Path.Combine(LauncherData.DataFolder, "dock-test.txt");
            Action<string> write = line => { try { System.IO.File.AppendAllText(log, line + "\r\n"); } catch (Exception) { } };
            Func<IEnumerable<object>, string> describe = list => string.Join(" | ", list.Select(o =>
                o is Separator ? "---" : o is MenuItem ? (string)((MenuItem)o).Header + (((MenuItem)o).Icon is Image ? " [icon]" : "")
                : o is TextBlock ? "(" + ((TextBlock)o).Text + ")" : "?"));
            Func<IEnumerable<object>, string, MenuItem> find = (list, header) =>
                list.OfType<MenuItem>().FirstOrDefault(m => string.Equals((string)m.Header, header, StringComparison.OrdinalIgnoreCase));
            Func<int, TileView> tile = n => dockItems == null ? null : dockItems.Children.OfType<TileView>().ElementAtOrDefault(n);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += delegate
            {
                if (steps.Count == 0) { timer.Stop(); return; }
                string step = steps.Dequeue();
                string arg = step.Contains(":") ? step.Substring(step.IndexOf(':') + 1) : "";
                try
                {
                    if (step == "list")
                        write("strip: " + (dockItems == null ? "(none)" : string.Join(", ", dockItems.Children.OfType<FrameworkElement>()
                            .Select(c => c is TileView ? ((TileView)c).Item.Name : c is TextBlock ? "(" + ((TextBlock)c).Text + ")" : "?"))));
                    else if (step.StartsWith("menu:"))
                        write("menu of " + tile(int.Parse(arg)).Item.Name + ": " + describe(ItemMenuItems(tile(int.Parse(arg)), DragOrigin.Dock)));
                    else if (step.StartsWith("remove:"))
                    {
                        var t = tile(int.Parse(arg));
                        var mi = find(ItemMenuItems(t, DragOrigin.Dock), "Remove from quick launch");
                        write("remove " + t.Item.Name + ": " + (mi == null ? "NO SUCH ITEM" : "clicked"));
                        if (mi != null) mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    }
                    else if (step == "hidden") write("hidden list: " + describe(HiddenDockMenuItems()));
                    else if (step.StartsWith("restore:") || step == "restoreall")
                    {
                        var list = HiddenDockMenuItems();
                        var mi = step == "restoreall" ? list.OfType<MenuItem>().LastOrDefault() : find(list, arg);
                        write(step + ": " + (mi == null ? "NO SUCH ITEM" : "clicked " + mi.Header));
                        if (mi != null) mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    }
                    else if (step == "tip") write("round button tooltip: " + (dockButton == null ? "(none)" : dockButton.ToolTip));
                    else if (step.StartsWith("source:"))
                    {
                        SetDockSource((DockSource)Enum.Parse(typeof(DockSource), arg));
                        write("strip now shows " + DockListName(S.Dock));
                    }
                }
                catch (Exception ex) { write(step + ": FAILED " + ex.Message); }
            };
            timer.Start();
        }

        void SetDockSource(DockSource src)
        {
            if (S.Dock == src) return;
            S.Dock = src;
            app.Save();
            RebuildDock();
        }

        void RefreshDock()
        {
            if (S.Dock == DockSource.Hidden || dockItems == null) return;
            int req = ++dockRequest;
            var source = S.Dock;
            int count = Math.Max(1, Math.Min(30, S.DockCount));
            var usage = Data.Usage.ToList();
            var hidden = S.DockHidden.Where(h => h.HiddenFrom(source)).ToList();   // only those removed from this list
            int ask = count + hidden.Count;   // enough to fill the strip once the hidden apps are left out
            // Test copies only: a fixed list of apps, so screenshots don't show this PC's own usage.
            string demo = Program.TestMode ? Environment.GetEnvironmentVariable("READYLAUNCHER_DEMO_DOCK") : null;
            Task.Factory.StartNew(() => !string.IsNullOrEmpty(demo)
                    ? demo.Split('|').Take(ask).Select(p => ShellHelper.ItemForPath(p)).ToList()
                    : source == DockSource.Frequent
                    ? ShellHelper.MostFrequent(ask, usage)
                    : ShellHelper.RecentlyInstalled(ask))
                .ContinueWith(t =>
                {
                    var all = t.IsFaulted ? new List<LauncherItem>() : t.Result;
                    if (req != dockRequest || drag != null) return;
                    FillDock(all.Where(i => !hidden.Any(h => h.Matches(i))).Take(count).ToList());
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void FillDock(List<LauncherItem> items)
        {
            if (dockItems == null) return;
            dockItems.Children.Clear();
            if (items.Count == 0)
            {
                dockItems.Children.Add(new TextBlock
                {
                    Text = S.DockHidden.Any(h => h.HiddenFrom(S.Dock)) ? "Every app here is hidden: right-click the round button to bring one back"
                        : S.Dock == DockSource.Frequent ? "Apps you use often will appear here" : "No recently installed apps found",
                    Foreground = dockFg, Opacity = 0.7, Margin = new Thickness(8, 10, 8, 10), TextWrapping = TextWrapping.Wrap
                });
                return;
            }
            foreach (var it in items)
            {
                var t = new TileView(it, DockIconSize, false, dockFg, 0) { Margin = new Thickness(3) };
                AttachTileHandlers(t, DragOrigin.Dock);
                dockItems.Children.Add(t);
            }
        }
    }
}
