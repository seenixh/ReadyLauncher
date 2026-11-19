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
            if (lampsBehind) grid.Children.Add(new LampPanel(S.LampMode, true) { Corners = inner });
            if (lampsUnder)
            {
                var withLamps = new DockPanel { LastChildFill = true };
                var lamps = new LampPanel(S.LampMode) { Corners = new CornerRadius(0, 0, inner.BottomRight, inner.BottomLeft) };
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
                ToolTip = (frequent ? "Showing your most used apps" : "Showing your most recently installed apps") + " - click to change",
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = horizontal ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 10)
            };
            button.MouseEnter += delegate { button.Background = hover; };
            button.MouseLeave += delegate { button.Background = normal; };
            button.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            button.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                var menu = new ContextMenu { PlacementTarget = button };
                switch (S.DockPos)
                {
                    case DockPosition.Bottom: menu.Placement = PlacementMode.Top; break;
                    case DockPosition.Top: menu.Placement = PlacementMode.Bottom; break;
                    case DockPosition.Left: menu.Placement = PlacementMode.Right; break;
                    default: menu.Placement = PlacementMode.Left; break;
                }
                menu.Items.Add(Item("Most used apps", () => SetDockSource(DockSource.Frequent), S.Dock == DockSource.Frequent, ""));
                menu.Items.Add(Item("Recently installed apps", () => SetDockSource(DockSource.RecentlyInstalled), S.Dock == DockSource.RecentlyInstalled, ""));
                menu.Items.Add(new Separator());
                menu.Items.Add(Item("Quick launch settings...", () => app.OpenSettings(2), false, ""));
                menu.IsOpen = true;
            };
            return button;
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
            // Test copies only: a fixed list of apps, so screenshots don't show this PC's own usage.
            string demo = Program.TestMode ? Environment.GetEnvironmentVariable("READYLAUNCHER_DEMO_DOCK") : null;
            Task.Factory.StartNew(() => !string.IsNullOrEmpty(demo)
                    ? demo.Split('|').Take(count).Select(p => ShellHelper.ItemForPath(p)).ToList()
                    : source == DockSource.Frequent
                    ? ShellHelper.MostFrequent(count, usage)
                    : ShellHelper.RecentlyInstalled(count))
                .ContinueWith(t =>
                {
                    if (req != dockRequest || drag != null) return;
                    FillDock(t.IsFaulted ? new List<LauncherItem>() : t.Result);
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
                    Text = S.Dock == DockSource.Frequent ? "Apps you use often will appear here" : "No recently installed apps found",
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
