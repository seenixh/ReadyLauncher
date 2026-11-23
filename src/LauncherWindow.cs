using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>
    /// The launcher window. It is a layered (see-through) window: a transparent margin holds the shadow and
    /// resize grips, and the rounded panel inside has a color layer and an optional picture layer.
    /// </summary>
    internal partial class LauncherWindow : Window
    {
        const double Shadow = 14;   // transparent margin around the panel
        const double Corner = 12;
        static readonly Color FrameBorder = Color.FromRgb(0x2A, 0x2A, 0x2A);
        static readonly uint OwnPid = (uint)Process.GetCurrentProcess().Id;

        readonly LauncherApp app;
        Grid root, shell, contentGrid, canvasHost, bgLayers;
        Border shadowBorder, bgColor, bgImage, frame;
        DockPanel main;
        ScrollViewer scroll;
        // The icons' scroll bar, at the launcher's right edge (outside an inside volume control, which never scrolls).
        System.Windows.Controls.Primitives.ScrollBar sideBar;
        Border sideBarHost;
        bool syncingSideBar;
        Canvas canvas, dragLayer;
        TextBlock emptyHint;
        VolumePanel insideVolume;
        VolumeWindow outsideVolume;
        readonly Dictionary<string, TileView> tiles = new Dictionary<string, TileView>();
        Brush fg;
        Color fgColor, bg;
        double bgAlpha = 1;
        IntPtr prevForeground;
        public bool AllowClose;

        LauncherData Data { get { return app.Data; } }
        AppSettings S { get { return app.Data.Settings; } }

        public LauncherWindow(LauncherApp app)
        {
            this.app = app;
            Title = Program.AppName;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            Topmost = true;
            MinWidth = 420 + 2 * Shadow;
            MinHeight = 300 + 2 * Shadow;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(Shadow + 2),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            Width = S.WinWidth;
            Height = S.WinHeight;
            if (double.IsNaN(S.WinLeft) || !OnScreen(S.WinLeft, S.WinTop)) WindowStartupLocation = WindowStartupLocation.CenterScreen;
            else { Left = S.WinLeft; Top = S.WinTop; }

            BuildLayout();
            ApplySettings();

            Deactivated += OnDeactivated;
            PreviewKeyDown += OnKey;
            LocationChanged += delegate { PlaceSatellites(); };
            SizeChanged += delegate { PlaceSatellites(); };
            IsVisibleChanged += delegate { if (IsVisible) ApplyLampText(); };   // a reminder that came due while hidden
            Closing += OnClosing;
        }

        static bool OnScreen(double left, double top)
        {
            return left + 60 > SystemParameters.VirtualScreenLeft && top + 20 > SystemParameters.VirtualScreenTop
                && left + 60 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
                && top + 20 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        }

        void BuildLayout()
        {
            // Alpha 1 keeps the transparent margin clickable, so the resize grips work.
            root = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
            shell = new Grid { Margin = new Thickness(Shadow), RenderTransformOrigin = new Point(0.5, 0.5) };
            InitShellTransforms();

            shadowBorder = new Border
            {
                CornerRadius = new CornerRadius(Corner),
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Direction = 270, Opacity = 0.35 }
            };
            bgColor = new Border { CornerRadius = new CornerRadius(Corner) };
            bgImage = new Border { CornerRadius = new CornerRadius(Corner) };
            bgLayers = new Grid();
            bgLayers.Children.Add(bgColor);
            bgLayers.Children.Add(bgImage);

            contentGrid = new Grid();
            contentGrid.SizeChanged += delegate { ClipContent(); };
            frame = new Border { CornerRadius = new CornerRadius(Corner), BorderThickness = new Thickness(1), Child = contentGrid };

            shell.Children.Add(shadowBorder);
            shell.Children.Add(bgLayers);
            shell.Children.Add(frame);
            root.Children.Add(shell);

            main = new DockPanel { LastChildFill = true };
            scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,   // scrolls; its bar is sideBar
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false
            };
            canvas = new Canvas { Background = Brushes.Transparent, AllowDrop = true };
            scroll.Content = canvas;
            scroll.SizeChanged += delegate { OnCanvasResized(); };
            sideBar = new System.Windows.Controls.Primitives.ScrollBar { Orientation = Orientation.Vertical, SmallChange = 40 };
            sideBar.ValueChanged += (s, e) => { if (!syncingSideBar) scroll.ScrollToVerticalOffset(e.NewValue); };
            System.Windows.Automation.AutomationProperties.SetName(sideBar, "Scroll the icons");
            sideBarHost = new Border { Child = sideBar, Visibility = Visibility.Collapsed };
            scroll.ScrollChanged += delegate { SyncSideBar(); };
            emptyHint = new TextBlock
            {
                Text = "Drag apps, files or web links here\nor click  +  to browse for one",
                TextAlignment = TextAlignment.Center, FontSize = 15, Opacity = 0.75,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            canvasHost = new Grid();
            canvasHost.Children.Add(scroll);
            canvasHost.Children.Add(emptyHint);

            contentGrid.Children.Add(main);
            contentGrid.Children.Add(BuildOverlay());
            dragLayer = new Canvas { IsHitTestVisible = false };   // window coordinates
            root.Children.Add(dragLayer);
            Content = root;

            canvas.DragOver += OnExternalDragOver;
            canvas.DragLeave += delegate { SetMergeTarget(null); ClearGuides(); };
            canvas.Drop += OnExternalDrop;
            InitSelection();
            canvas.MouseRightButtonUp += (s, e) =>
            {
                if (e.OriginalSource == canvas) { ShowCanvasMenu(e.GetPosition(canvas)); e.Handled = true; }
            };
        }

        public void ApplySettings()
        {
            SettingsTheme.MenuMode = S.SettingsTheme;   // right-click menus in the Settings theme's colors
            bg = ParseColor(S.BackgroundColor, Color.FromRgb(0xB8, 0xA6, 0x00));
            // Text color follows what is actually behind it: the color, blended with the picture if there is one.
            string picture = S.BackgroundImageOff ? "" : S.BackgroundImage;   // a switched-off picture isn't shown
            windowLum = BlendPicture(Luminance(bg), picture, 1 - S.BackgroundImageTransparency / 100.0);
            fgColor = TextColorFor(windowLum);
            fg = new SolidColorBrush(fgColor);

            bgAlpha = 1 - Math.Max(0, Math.Min(95, S.WindowTransparency)) / 100.0;
            bgColor.Background = new SolidColorBrush(bg);
            bgImage.Background = ImageBrushFor(picture, S.BackgroundImageTransparency);
            bgLayers.Opacity = bgAlpha;
            shadowBorder.Background = new SolidColorBrush(bg);
            shadowBorder.Visibility = bgAlpha >= 1 ? Visibility.Visible : Visibility.Hidden;
            frame.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Max(0.4, bgAlpha)), FrameBorder.R, FrameBorder.G, FrameBorder.B));
            emptyHint.Foreground = fg;
            StyleOverlay();
            ApplyPanelCorners();

            main.Children.Clear();
            // The blinking lamp strip inside the launcher: along the very top edge (the buttons sit below it), or along
            // the bottom (inside an attached quick launch strip). Outside the launcher it has its own window; on the
            // quick launch strip, see BuildDockArea.
            LampPanel lampStrip = null;
            ApplyLampText();   // what the strips spell, if anything
            if (S.LampPanel == LampPanelPlace.Top || S.LampPanel == LampPanelPlace.Bottom)
            {
                lampStrip = new LampPanel(S.LampMode);
                HookLampStrip(lampStrip, true);   // the strip moves the launcher, like the buttons row does; right-click: activity
                DockPanel.SetDock(lampStrip, S.LampPanel == LampPanelPlace.Top ? Dock.Top : Dock.Bottom);
            }
            if (lampStrip != null && S.LampPanel == LampPanelPlace.Top) main.Children.Add(lampStrip);   // first: the top edge

            // The quick launch strip goes in next so it runs the full width (or height) of the window, out to the edge.
            // A detached strip lives in its own window instead (see UpdateDetachedDock).
            var dock = BuildDockArea();
            if (dock != null && !S.DockDetached) main.Children.Add(dock);

            var header = BuildHeader();
            DockPanel.SetDock(header, Dock.Top);
            main.Children.Add(header);

            if (lampStrip != null && S.LampPanel == LampPanelPlace.Bottom) main.Children.Add(lampStrip);

            DockPanel.SetDock(sideBarHost, Dock.Right);   // before the volume control, so it's outside it
            main.Children.Add(sideBarHost);

            insideVolume = null;
            if (S.Volume == VolumePlacement.InsideLeft || S.Volume == VolumePlacement.InsideRight)
            {
                double k = AppSettings.VolumeScale(S.VolumeSize);
                insideVolume = new VolumePanel(new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)), new SolidColorBrush(VolumeColor(false)), null, VolumeLookNow())
                {
                    Margin = new Thickness(10, 4, 10, 12), VerticalAlignment = VerticalAlignment.Bottom,
                    LayoutTransform = Math.Abs(k - 1) < 0.01 ? Transform.Identity : new ScaleTransform(k, k)
                };
                HookVolume(insideVolume);
                DockPanel.SetDock(insideVolume, S.Volume == VolumePlacement.InsideLeft ? Dock.Left : Dock.Right);
                main.Children.Add(insideVolume);
            }

            main.Children.Add(canvasHost);

            scroll.VerticalScrollBarVisibility = S.ScrollEnabled ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            if (!S.ScrollEnabled) scroll.ScrollToTop();
            StyleScrollBar();
            SyncSideBar();

            if (outsideVolume != null) { outsideVolume.Close(); outsideVolume = null; } // recreate in the new colors
            CloseNotes();
            CloseSensors();   // recreated in the new colors
            CloseLampWindow();   // recreated with the new activity and edge color
            UpdateLampWindow();
            UpdateDetachedDock();
            UpdateSensors();
            UpdateNotes();
            UpdateOutsideVolume();
            RenderItems();
            UpdateAppWatching();
        }

        /// <summary>
        /// The icon area's scroll bar in the launcher's background color: a slim bar a shade lighter (on a dark
        /// background) or darker (on a light one) than the background, a little stronger when pointed at.
        /// </summary>
        void StyleScrollBar()
        {
            if (sideBarHost.Resources.MergedDictionaries.Count == 0)
            {
                try { sideBarHost.Resources.MergedDictionaries.Add(SettingsTheme.ScrollBarStyles()); }
                catch (Exception) { return; }   // keep Windows' own scroll bar
            }
            Color toward = Luminance(bg) > 0.5 ? Colors.Black : Colors.White;
            var thumb = new SolidColorBrush(Mix(bg, toward, 0.35));
            var hover = new SolidColorBrush(Mix(bg, toward, 0.55));
            thumb.Freeze();
            hover.Freeze();
            sideBarHost.Resources["T.Thumb"] = thumb;
            sideBarHost.Resources["T.ThumbHover"] = hover;
        }

        /// <summary>The scroll bar follows the icons: shown only while they scroll, at their position.</summary>
        void SyncSideBar()
        {
            bool show = S.ScrollEnabled && scroll.ScrollableHeight > 0.5;
            sideBarHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            syncingSideBar = true;
            sideBar.Minimum = 0;
            sideBar.Maximum = scroll.ScrollableHeight;
            sideBar.ViewportSize = scroll.ViewportHeight;
            sideBar.LargeChange = Math.Max(1, scroll.ViewportHeight - 20);
            sideBar.Value = scroll.VerticalOffset;
            syncingSideBar = false;
        }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        static Color ParseColor(string s, Color fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(s); }
            catch (Exception) { return fallback; }
        }

        double windowLum;

        static double Luminance(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255; }

        static Color TextColorFor(double lum) { return lum > 0.5 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White; }

        static readonly Dictionary<string, double> pictureLum = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Mix a base brightness with a picture's average brightness, weighted by the picture's opacity.</summary>
        static double BlendPicture(double baseLum, string path, double pictureOpacity)
        {
            var img = LoadImage(path) as BitmapSource;
            if (img == null || pictureOpacity <= 0) return baseLum;
            double lum;
            if (!pictureLum.TryGetValue(path, out lum))
            {
                lum = baseLum;
                try
                {
                    var small = new TransformedBitmap(img, new ScaleTransform(24.0 / img.PixelWidth, 24.0 / img.PixelHeight));
                    var conv = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
                    int w = conv.PixelWidth, h = conv.PixelHeight;
                    var px = new byte[w * h * 4];
                    conv.CopyPixels(px, w * 4, 0);
                    double sum = 0;
                    for (int i = 0; i < px.Length; i += 4) sum += (0.114 * px[i] + 0.587 * px[i + 1] + 0.299 * px[i + 2]) / 255;
                    lum = sum / (w * h);
                }
                catch (Exception) { }
                pictureLum[path] = lum;
            }
            pictureOpacity = Math.Max(0, Math.Min(1, pictureOpacity));
            return baseLum * (1 - pictureOpacity) + lum * pictureOpacity;
        }

        static readonly Dictionary<string, ImageSource> images = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            ImageSource img;
            if (images.TryGetValue(path, out img)) return img;
            try
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.UriSource = new Uri(path);
                b.DecodePixelWidth = 2560;
                b.EndInit();
                b.Freeze();
                img = b;
            }
            catch (Exception) { img = null; }
            images[path] = img;
            return img;
        }

        static Brush ImageBrushFor(string path, int transparencyPercent)
        {
            var img = LoadImage(path);
            if (img == null) return null;
            return new ImageBrush(img)
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 1 - Math.Max(0, Math.Min(100, transparencyPercent)) / 100.0
            };
        }

        // ---------- size ----------

        double DpiScale
        {
            get { try { return VisualTreeHelper.GetDpi(this).DpiScaleX; } catch (Exception) { return 1; } }
        }

        static Size PresetSize(WindowSizeMode m)
        {
            switch (m)
            {
                case WindowSizeMode.Small: return new Size(640, 420);
                case WindowSizeMode.Large: return new Size(1200, 760);
                default: return new Size(900, 560);
            }
        }

        /// <summary>Resize to the chosen preset, or to the custom size (given in screen pixels).</summary>
        public void ApplyWindowSize()
        {
            Size panel = S.WindowSize == WindowSizeMode.Custom
                ? new Size(S.CustomWidthPx / DpiScale, S.CustomHeightPx / DpiScale)
                : PresetSize(S.WindowSize);
            Width = Math.Max(MinWidth, panel.Width + 2 * Shadow);
            Height = Math.Max(MinHeight, panel.Height + 2 * Shadow);
        }

        /// <summary>Store position/size; a hand-resized window becomes a "custom" size.</summary>
        public void RememberBounds()
        {
            if (!IsLoaded) return;
            S.WinLeft = Left; S.WinTop = Top; S.WinWidth = ActualWidth; S.WinHeight = ActualHeight;
            var panel = new Size(ActualWidth - 2 * Shadow, ActualHeight - 2 * Shadow);
            if (S.WindowSize != WindowSizeMode.Custom)
            {
                var p = PresetSize(S.WindowSize);
                if (Math.Abs(p.Width - panel.Width) > 2 || Math.Abs(p.Height - panel.Height) > 2) S.WindowSize = WindowSizeMode.Custom;
            }
            if (S.WindowSize == WindowSizeMode.Custom)
            {
                S.CustomWidthPx = (int)Math.Round(panel.Width * DpiScale);
                S.CustomHeightPx = (int)Math.Round(panel.Height * DpiScale);
            }
        }

        // ---------- show / hide ----------

        public void ShowLauncher()
        {
            RememberForeground();
            bool appearing = !IsVisible;
            if (hiding) StopAnimations();
            if (!IsVisible) { if (appearing) PrepareAnimateIn(); Show(); }
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Topmost = !inBackground;   // sent to the background: comes to the front, but not always on top
            Activate();
            Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);
            Focus();
            if (appearing) AnimateIn();
            PrepareCompanions(appearing);   // the notes, sensors, strip and volume windows that animate too start invisible
            UpdateLampWindow();
            UpdateDetachedDock();
            UpdateSensors();
            UpdateNotes();
            UpdateOutsideVolume();
            if (appearing) AnimateCompanionsIn();
            if (S.Dock != DockSource.Hidden) RefreshDock();
            app.RaiseSettings();   // an open Settings window stays in front
        }

        public void HideLauncher()
        {
            CloseFolder();
            FlushNotes();
            // The windows beside the launcher close with it (animated, if they're chosen to animate too).
            HideCompanion(outsideVolume);
            HideCompanion(dockWindow);
            HideCompanion(lampWindow);
            HideCompanion(notesWindow);
            if (sensorHub != null) sensorHub.Stop();
            HideCompanion(sensorWindow);
            if (!IsVisible || hiding) { if (!IsVisible) LeaveBackground(); return; }
            app.CloseSettings();   // Settings closes with the launcher
            RememberBounds();
            app.Save();
            AnimateOut(() => { Hide(); StopAnimations(); LeaveBackground(); });   // closed: always on top again next time
        }

        public void Toggle()
        {
            if (IsVisible && (IsActive || NotesActive) && !hiding) HideLauncher();
            else ShowLauncher();
        }

        /// <summary>Remember which app was in front, so key-combo shortcuts can be sent back to it.</summary>
        void RememberForeground()
        {
            IntPtr w = Native.GetForegroundWindow();
            if (w == IntPtr.Zero) return;
            uint pid;
            Native.GetWindowThreadProcessId(w, out pid);
            if (pid == OwnPid) return;
            string cls = Native.ClassName(w);
            if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls.StartsWith("NotifyIconOverflow")
                || cls == "TopLevelWindowForOverflowXamlIsland") return;
            prevForeground = w;
        }

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (AllowClose) return;
            e.Cancel = true;
            HideLauncher();
        }

        void OnDeactivated(object sender, EventArgs e)
        {
            // Sent to the background: it stays open when other windows are used.
            if (!S.HideOnDeactivate || inBackground || app.ModalOpen || drag != null || decorDrag != null || groupMove != null || boxing) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (Window w in Application.Current.Windows) if (w.IsActive) return;
                if (IsVisible && !app.ModalOpen) HideLauncher();
            }), DispatcherPriority.Background);
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && VolumePanel.IsExactVolumeBox(e.OriginalSource)) return;   // cancels typing a volume, not the launcher
            bool typing = e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase;
            if (e.Key == Key.Delete && !typing && openFolder == null && SelectionCount > 0) { RemoveSelected(); e.Handled = true; return; }
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control && !typing && openFolder == null) { SelectAll(); e.Handled = true; return; }
            if (e.Key != Key.Escape) return;
            if (openFolder != null) CloseFolder();
            else if (SelectionCount > 0) ClearSelection();
            else HideLauncher();
            e.Handled = true;
        }

        // ---------- volume control outside the window ----------

        void UpdateOutsideVolume()
        {
            bool outside = S.Volume == VolumePlacement.OutsideLeft || S.Volume == VolumePlacement.OutsideRight;
            if (!outside)
            {
                if (outsideVolume != null) { outsideVolume.Close(); outsideVolume = null; }
                return;
            }
            if (!IsVisible) { if (outsideVolume != null) outsideVolume.Hide(); return; }
            if (outsideVolume == null)
            {
                var back = new SolidColorBrush(Color.FromArgb((byte)(255 * bgAlpha), bg.R, bg.G, bg.B));
                outsideVolume = new VolumeWindow(back, new SolidColorBrush(VolumeColor(true)), new SolidColorBrush(FrameBorder), VolumeLookNow());
                HookVolume(outsideVolume.Panel);
                outsideVolume.Deactivated += OnDeactivated;   // clicking away from it counts as clicking away from the launcher
                outsideVolume.Owner = this;
                outsideVolume.Topmost = !inBackground;
                outsideVolume.SizeChanged += delegate { PlaceOutsideVolume(); };
            }
            outsideVolume.SetSide(S.Volume == VolumePlacement.OutsideLeft);
            outsideVolume.SetScale(AppSettings.VolumeScale(S.VolumeSize));
            outsideVolume.Show();
            PlaceOutsideVolume();
        }

        VolumeLook VolumeLookNow()
        {
            return new VolumeLook { Style = S.VolumeStyle, SliderToo = S.VolumeSliderToo, Size = S.VolumeButtonHeight, Steps = S.VolumeSteps() };
        }

        void HookVolume(VolumePanel p)
        {
            p.StepEditRequested += EditVolumeStep;
            p.VolumeChosen += delegate
            {
                if (!S.VolumeCloseAfter || inBackground) return;
                // A moment to see the new volume first.
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                t.Tick += delegate { t.Stop(); if (IsVisible) HideLauncher(); };
                t.Start();
            };
        }

        /// <summary>Right-click on a volume button: type the volume it sets (empty puts back the one it came with).</summary>
        void EditVolumeStep(int index)
        {
            int count = S.VolumeStepCount();
            var now = S.VolumeSteps();
            if (index < 0 || index >= count) return;
            int standard = AppSettings.DefaultVolumeSteps(count)[index];
            string prompt = "The volume this button sets, from 0 to 100%. Leave it empty to put back " + standard + "%.";
            string initial = now[index].ToString();
            while (true)
            {
                string answer = InputDialog.Ask(this, app, "Volume button", prompt, initial);
                if (answer == null) return;   // canceled
                string text = answer.Trim().TrimEnd('%').Trim();
                int v;
                if (text.Length == 0) v = standard;
                else if (!int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out v) || v < 0 || v > 100)
                {
                    prompt = "Type a whole number from 0 to 100, or leave it empty to put back " + standard + "%.";
                    initial = answer;
                    continue;
                }
                S.SetVolumeStep(index, v);
                app.Save();
                var steps = S.VolumeSteps();
                if (insideVolume != null) insideVolume.SetSteps(steps);
                if (outsideVolume != null) outsideVolume.Panel.SetSteps(steps);
                return;
            }
        }

        /// <summary>Line up everything that floats beside the launcher: lamp strip, detached strip, sensors, notes, then volume.</summary>
        void PlaceSatellites()
        {
            PlaceLampWindow();
            PlaceDetachedDock();
            PlaceSensors();
            PlaceNotes();
            PlaceOutsideVolume();
        }

        void PlaceOutsideVolume()
        {
            if (outsideVolume == null || !outsideVolume.IsVisible) return;
            double gap = Math.Max(0, S.VolumeGap) / DpiScale;
            outsideVolume.UpdateLayout();
            double w = outsideVolume.ActualWidth, h = outsideVolume.ActualHeight;

            // Measure from the launcher's visible edge, or from a detached strip / notes window on the same side.
            outsideVolume.Left = S.Volume == VolumePlacement.OutsideLeft ? OuterLeft(true) - w - gap : OuterRight(true) + gap;
            outsideVolume.Top = Top + ActualHeight - Shadow - h;
        }

        // ---------- launching ----------

        void LaunchItem(LauncherItem item, bool asAdmin)
        {
            if (item.IsFolder) { OpenFolder(item, false); return; }
            if (item.Kind == ItemKind.Keys) { SendKeys(item); return; }
            try { ShellHelper.Launch(item.Path, item.Arguments, asAdmin); }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223) return; // UAC prompt canceled
                MessageBox.Show(this, "Could not open \"" + item.Name + "\".\n\n" + ex.Message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open \"" + item.Name + "\".\n\n" + ex.Message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            item.LaunchCount++;
            item.LastLaunched = DateTime.Now;
            Data.RecordLaunch(item.Path, item.Name);
            app.Save();
            if (S.HideAfterLaunch && !inBackground) HideLauncher();   // sent to the background: stays open
        }

        /// <summary>Give focus back to the app that was in front, then type the key combination into it.</summary>
        void SendKeys(LauncherItem item)
        {
            var target = prevForeground;
            item.LaunchCount++;
            item.LastLaunched = DateTime.Now;
            app.Save();
            if (S.HideAfterLaunch && !inBackground) HideLauncher();   // sent to the background: stays open
            if (target != IntPtr.Zero && Native.IsWindow(target)) Native.SetForegroundWindow(target);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            timer.Tick += delegate { timer.Stop(); KeySender.Send(item.KeyMods, item.KeyKey); };
            timer.Start();
        }
    }
}
