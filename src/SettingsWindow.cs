using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    internal partial class SettingsWindow : Window
    {
        readonly LauncherApp app;
        readonly TabControl tabs;
        ModifierKeys mods;
        Key key;

        // The tabs, in order.
        public const int GeneralTab = 0, AppearanceTab = 1, LampStripTab = 2, QuickLaunchTab = 3, NotesTab = 4, AppsTab = 5, HardwareTab = 6;

        public void ShowTab(int index)
        {
            if (index >= 0 && index < tabs.Items.Count) tabs.SelectedIndex = index;
        }

        /// <summary>Scrolls the Lamp strip tab to its reminders.</summary>
        public void ShowReminder()
        {
            Dispatcher.BeginInvoke(new Action(() => { if (addReminder != null) addReminder.BringIntoView(); }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // General
        TextBox hotkeyBox, customW, customH;
        TextBlock hotkeyHint;
        ComboBox windowSize, animation, volume, volumeButtonHeight, lampPlace, lampMode, lampMonitor, lampColors, lampRows, lampTailRows, lampTextMode, themeBox;
        TextBox lampText, reminderText;
        CheckBox lampTextSides, lampTextTails, lampEightBall, lampEightBallInstant;
        bool monitorForBall;   // the monitor was put there for the ball: unticking the ball takes it away again
        Dictionary<LampHue, CheckBox> lampHues = new Dictionary<LampHue, CheckBox>();
        CheckBox lampPastel;
        List<LampCustomColor> customs;   // colors from the wheel as edited here; kept with OK or Apply
        WrapPanel customBoxes;
        Button addCustom;
        int tailRowsWanted;        // the lamps chosen for tails (kept while a lower strip has room for fewer)
        bool fillingTailRows;
        Slider lampScrollSpeed;
        TextBox lampNumbers;
        TextBlock lampNumbersExample;
        ComboBox lampPatterns;
        Button savePattern, deletePattern;
        List<LampNumberPattern> patterns;   // saved number patterns as edited here; kept with OK or Apply
        readonly Slider[] customLevel = new Slider[6];   // the custom activity: strip speed and lamps, monitor speed and lamps, mini lamps' speed and lamps
        Grid customLevelGrid;
        ComboBox lampActivities;
        Button saveActivity, deleteActivity;
        FrameworkElement activityLine;
        List<LampActivityLevels> activities;   // saved custom activities as edited here; kept with OK or Apply
        bool fillingActivities, puttingLevels;
        ComboBox reminderMonth, reminderDay, reminderYear, reminderTime;
        StackPanel reminderList;
        Button addReminder;
        List<LampReminder> reminders;   // the reminders as edited here; kept with OK or Apply
        CheckBox volumeHideSlider, volumeButtonsOn, volumeCloseAfter;
        SavedLook ownLook;   // your own look, kept while a theme is chosen
        CheckBox hideAfter, hideDeact, startup, doubleClick;
        Slider animationSpeed;
        CheckBox animateAll, animateNotes, animateSensors, animateDock, animateVolume;
        // Appearance
        ComboBox iconSize, arrange;
        // The "Icon layout" choices, in the order the list shows them.
        static readonly ArrangeMode[] ArrangeOrder = { ArrangeMode.Free, ArrangeMode.FreeAlign, ArrangeMode.Grid, ArrangeMode.Auto };
        CheckBox showLabels;
        TextBox colorBox;
        ImageRow imageRow;
        Slider winTransparency, imageTransparency;
        // Colors of the launcher's parts ("" = Auto)
        TextBox buttonsColorBox, volumeColorBox, iconTextColorBox, notesChromeColorBox, allPartsColorBox;
        CheckBox sameColor;
        static readonly string[] PartPresets = { "#FFFFFF", "#1B1B1B", "#BDBDBD", "#FFB900", "#3A96DD", "#16C60C", "#E74856" };

        // The open / close animations in the order Settings lists them: none, randomize, then the newer, fancier ones.
        static readonly OpenAnimation[] AnimationOrder =
        {
            OpenAnimation.None, OpenAnimation.Random,
            OpenAnimation.Static, OpenAnimation.Flip, OpenAnimation.Rain, OpenAnimation.Beam, OpenAnimation.Glass,
            OpenAnimation.Fade, OpenAnimation.SlideUp, OpenAnimation.SlideDown, OpenAnimation.Zoom,
            OpenAnimation.Pop, OpenAnimation.Blur
        };
        static readonly string[] AnimationNames =
        {
            "No animation", "Randomize",
            "TV static in / out", "Flip back (in) / away (out)", "Digital rain in / out", "Beam in / out (transporter light)",
            "Glass: shimmer in / shatter out",
            "Fade in / out", "Slide up (in) / down (out)", "Slide down (in) / up (out)", "Zoom",
            "Pop (springy zoom)", "Blur in / out"
        };
        const string AutoPartTip = "Automatic: dark on light backgrounds, white on dark ones";
        // Quick launch
        ComboBox dockSource, dockPos, dockAlign;
        Slider dockCount, dockTransparency, dockImageTransparency, dockGap, volumeGap;
        Slider volumeButtonCount;
        CheckBox dockDivider, dockDetached;
        TextBox dockColorBox;
        ImageRow dockImageRow;
        // Icon area
        CheckBox scrollEnabled;
        // Notes
        ComboBox notesPos;
        Slider notesGap, notesWidth, notesFont;
        TextBox notesBack;
        // Sizes and apps
        ComboBox volumeSize, stripIconSize, autoAddWhere;
        CheckBox autoAdd;
        // This window's look, automatic backups, and updates
        readonly System.Collections.Generic.List<Button> themeSwatches = new System.Collections.Generic.List<Button>();
        TextBlock themeName, autoBackupStatus;
        CheckBox autoBackup;
        TextBox autoBackupDays, autoBackupFolder;
        SettingsThemeMode theme;
        Button checkUpdates;
        TextBlock updateStatus;
        bool closed;

        public SettingsWindow(LauncherApp app)
        {
            this.app = app;
            var s = app.Data.Settings;
            mods = s.HotkeyModifiers;
            key = s.HotkeyKey;

            Title = Program.AppName + " settings";
            Width = 660;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            MaxHeight = SystemParameters.WorkArea.Height;   // never taller than the screen: a page that doesn't fit scrolls
            SourceInitialized += delegate { FitScreen(); };
            LocationChanged += delegate { FitScreen(); };

            // The theme: colors now, the title bar once the window exists, and again whenever Windows switches
            // between light and dark (for "Follow system").
            theme = s.SettingsTheme;
            SettingsTheme.Apply(this, theme);
            SourceInitialized += delegate { SettingsTheme.Apply(this, theme); };
            Microsoft.Win32.UserPreferenceChangedEventHandler systemChanged = (o, e) =>
                Dispatcher.BeginInvoke(new Action(() => { if (!closed && theme == SettingsThemeMode.FollowSystem) SettingsTheme.Apply(this, theme); }));
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += systemChanged;
            Closed += delegate { closed = true; Microsoft.Win32.SystemEvents.UserPreferenceChanged -= systemChanged; };
            // Closing, Settings goes at once. Fading out, it stayed on screen a moment longer, and the launcher (which comes
            // forward as Settings closes) and the windows around it came up over it, part by part.
            Closing += delegate { GoNow(); };

            tabs = new TabControl { Margin = new Thickness(12, 12, 12, 0) };
            tabs.Items.Add(new TabItem { Header = "General", Content = Scrolling(BuildGeneral(s)) });
            tabs.Items.Add(new TabItem { Header = "Appearance", Content = Scrolling(BuildAppearance(s)) });
            tabs.Items.Add(new TabItem { Header = "Lamp strip", Content = Scrolling(BuildLampStrip(s)) });
            tabs.Items.Add(new TabItem { Header = "Quick launch strip", Content = Scrolling(BuildQuickLaunch(s)) });
            tabs.Items.Add(new TabItem { Header = "Notes", Content = Scrolling(BuildNotes(s)) });
            tabs.Items.Add(new TabItem { Header = "Apps", Content = Scrolling(BuildApps(s)) });
            tabs.Items.Add(new TabItem { Header = "Hardware monitoring", Content = Scrolling(BuildHardware(s)) });
            RestoreIfThemeGone(s);
            colorsKey = LampColorsKey();   // (so OK keeps saved or random colors from the strip's menu, unless colors are changed here)

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
            var ok = new Button { Content = "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4) };
            ok.Click += delegate { ApplyAndClose(); };
            var apply = new Button { Content = "Apply", Width = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4), ToolTip = "Try the changes and keep this window open" };
            apply.Click += delegate { Apply(); };
            var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true, Padding = new Thickness(0, 4, 0, 4) };
            cancel.Click += delegate { Close(); };
            buttons.Children.Add(ok);
            buttons.Children.Add(apply);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(tabs);
            Content = root;
            ExplainEverything(buttons);   // a description on every title, option and button
            if (Program.TestMode)
                Loaded += delegate
                {
                    // Test copies: list anything left without a description.
                    string report = Environment.GetEnvironmentVariable("READYLAUNCHER_TOOLTIP_REPORT");
                    if (!string.IsNullOrEmpty(report)) System.IO.File.WriteAllLines(report, Undescribed());
                };
        }

        /// <summary>No taller than the screen it's on (its taskbar left free).</summary>
        void FitScreen()
        {
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;
            double most = System.Windows.Forms.Screen.FromHandle(h).WorkingArea.Height / VisualTreeHelper.GetDpi(this).DpiScaleY, test;
            if (Program.TestMode && double.TryParse(Environment.GetEnvironmentVariable("READYLAUNCHER_WORK_HEIGHT") ?? "", out test)) most = test;
            most = Math.Max(300, most);
            if (Math.Abs(MaxHeight - most) > 0.5) MaxHeight = most;
        }

        /// <summary>
        /// A page that scrolls when it doesn't fit the window. The mouse wheel over a closed list scrolls the page rather
        /// than changing the list's choice.
        /// </summary>
        static ScrollViewer Scrolling(UIElement page)
        {
            var sv = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
            sv.PreviewMouseWheel += (o, e) =>
            {
                if (sv.ScrollableHeight <= 0) return;
                for (var d = e.OriginalSource as DependencyObject; d != null && d != sv; d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                {
                    var list = d as ComboBox;
                    if (list == null) continue;
                    if (list.IsDropDownOpen) return;
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.4);
                    e.Handled = true;
                    return;
                }
            };
            return sv;
        }

        UIElement BuildGeneral(AppSettings s)
        {
            var g = Page();
            hotkeyBox = new TextBox { Text = AppSettings.HotkeyText(mods, key), IsReadOnly = true, Padding = new Thickness(4), Cursor = Cursors.Arrow };
            hotkeyBox.PreviewKeyDown += OnHotkeyKey;
            hotkeyHint = new TextBlock { Text = "Click the box, then press the new key combination.", FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            var hk = new StackPanel();
            hk.Children.Add(hotkeyBox);
            hk.Children.Add(hotkeyHint);
            AddRow(g, "Open launcher with", hk);

            windowSize = Combo((int)s.WindowSize, "Small (640 x 420)", "Medium (900 x 560)", "Large (1200 x 760)", "Custom size");
            customW = new TextBox { Text = s.CustomWidthPx.ToString(), Width = 64, Padding = new Thickness(3) };
            customH = new TextBox { Text = s.CustomHeightPx.ToString(), Width = 64, Padding = new Thickness(3) };
            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            custom.Children.Add(new TextBlock { Text = "Width", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            custom.Children.Add(customW);
            custom.Children.Add(new TextBlock { Text = "Height", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 6, 0) });
            custom.Children.Add(customH);
            custom.Children.Add(new TextBlock { Text = "pixels", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Opacity = 0.7 });
            custom.IsEnabled = windowSize.SelectedIndex == (int)WindowSizeMode.Custom;
            windowSize.SelectionChanged += delegate { custom.IsEnabled = windowSize.SelectedIndex == (int)WindowSizeMode.Custom; };
            var sizeStack = new StackPanel();
            sizeStack.Children.Add(windowSize);
            sizeStack.Children.Add(custom);
            AddRow(g, "Launcher size", sizeStack);

            animation = Combo(Math.Max(0, Array.IndexOf(AnimationOrder, s.Animation)), AnimationNames);
            AddRow(g, "Open / close animation", animation);
            animationSpeed = AddSliderRow(g, "Animation speed", s.AnimationSpeed, 25, 300, "%");

            // Which windows beside the launcher animate with it: all of them, or some.
            animateAll = Check("All of them", s.AnimateNotes && s.AnimateSensors && s.AnimateDock && s.AnimateVolume);
            animateNotes = Check("Notes window", s.AnimateNotes);
            animateSensors = Check("Sensor bar", s.AnimateSensors);
            animateDock = Check("Quick launch strip (when detached)", s.AnimateDock);
            animateVolume = Check("Volume control (when outside the launcher)", s.AnimateVolume);
            var each = new[] { animateNotes, animateSensors, animateDock, animateVolume };
            bool syncing = false;
            animateAll.Click += delegate
            {
                syncing = true;
                foreach (var c in each) c.IsChecked = animateAll.IsChecked == true;
                syncing = false;
            };
            foreach (var c in each)
            {
                c.Margin = new Thickness(20, 2, 0, 0);   // under "All of them"
                c.Checked += delegate { if (!syncing) animateAll.IsChecked = Array.TrueForAll(each, x => x.IsChecked == true); };
                c.Unchecked += delegate { if (!syncing) animateAll.IsChecked = false; };
            }
            var animateBox = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            animateBox.Children.Add(animateAll);
            foreach (var c in each) animateBox.Children.Add(c);
            AddRow(g, "Also animate", animateBox);
            Action animationState = () =>
            {
                bool on = animation.SelectedIndex >= 0 && AnimationOrder[animation.SelectedIndex] != OpenAnimation.None;
                ((FrameworkElement)animationSpeed.Parent).IsEnabled = on;
                animateBox.IsEnabled = on;
            };
            animation.SelectionChanged += delegate { animationState(); };
            animationState();

            volume = Combo((int)s.Volume, "Off", "Inside the launcher, left", "Inside the launcher, right", "Outside the launcher, left", "Outside the launcher, right");
            AddRow(g, "Volume control", volume);
            volumeSize = Combo((int)s.VolumeSize, "Small", "Medium", "Large", "Extra large");
            AddRow(g, "Volume control size", volumeSize);
            // Hide Slider [ ]  Buttons [ ]  ----o---- 5 : volume buttons on or off, how many (1 to 20), and buttons only.
            bool hadButtons = s.VolumeStyle != VolumeStyleMode.Slider;
            Func<string, CheckBox, string, StackPanel> labeled = (text, box, tip) =>
            {
                // The name sits before its box, and clicking the name ticks the box too.
                var name = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0), ToolTip = tip, Background = Brushes.Transparent };
                name.MouseLeftButtonUp += delegate { if (box.IsEnabled) box.IsChecked = box.IsChecked != true; };
                box.VerticalAlignment = VerticalAlignment.Center;
                box.Margin = new Thickness(0);
                box.ToolTip = tip;
                System.Windows.Automation.AutomationProperties.SetName(box, text);
                var part = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
                part.Children.Add(name);
                part.Children.Add(box);
                return part;
            };
            volumeHideSlider = new CheckBox { IsChecked = hadButtons && !s.VolumeSliderToo };
            volumeButtonsOn = new CheckBox { IsChecked = hadButtons };
            var hidePart = labeled("Hide Slider", volumeHideSlider, "Show only the volume buttons, without the volume slider");
            var buttonsPart = labeled("Buttons", volumeButtonsOn, "Show a column of volume buttons that each set one volume");
            volumeButtonCount = new Slider
            {
                Minimum = 1, Maximum = AppSettings.MaxVolumeButtons, Value = Math.Max(1, Math.Min(AppSettings.MaxVolumeButtons, hadButtons ? s.VolumeStepCount() : s.VolumeButtonCount)),
                IsSnapToTickEnabled = true, TickFrequency = 1, SmallChange = 1, LargeChange = 5, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "How many volume buttons, from 1 to 20"
            };
            System.Windows.Automation.AutomationProperties.SetName(volumeButtonCount, "Number of volume buttons");
            var countText = new TextBlock { MinWidth = 22, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            countText.Text = ((int)volumeButtonCount.Value).ToString();
            volumeButtonCount.ValueChanged += delegate { countText.Text = ((int)volumeButtonCount.Value).ToString(); };
            var styleRow = new Grid();
            foreach (var w in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
                styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            styleRow.Children.Add(hidePart);
            Grid.SetColumn(buttonsPart, 1); styleRow.Children.Add(buttonsPart);
            Grid.SetColumn(volumeButtonCount, 2); styleRow.Children.Add(volumeButtonCount);
            Grid.SetColumn(countText, 3); styleRow.Children.Add(countText);
            AddRow(g, "Volume control style", styleRow);
            volumeButtonHeight = Combo((int)s.VolumeButtonHeight, "Small", "Medium", "Large");
            AddRow(g, "Volume button height", volumeButtonHeight);
            volumeCloseAfter = Check("Close the launcher after choosing a volume", s.VolumeCloseAfter);
            var volumeChecks = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            volumeChecks.Children.Add(volumeCloseAfter);
            AddRow(g, "Volume options", volumeChecks);
            volumeGap = AddSliderRow(g, "Volume distance from window", s.VolumeGap, 0, 200, " px");
            Action volumeGapState = () =>
            {
                ((FrameworkElement)volumeGap.Parent).IsEnabled =
                    volume.SelectedIndex == (int)VolumePlacement.OutsideLeft || volume.SelectedIndex == (int)VolumePlacement.OutsideRight;
                bool on = volume.SelectedIndex != (int)VolumePlacement.Off;
                bool buttons = on && volumeButtonsOn.IsChecked == true;
                volumeSize.IsEnabled = on;
                buttonsPart.IsEnabled = on;
                hidePart.IsEnabled = buttons;          // buttons only: there have to be buttons
                volumeButtonCount.IsEnabled = buttons;
                countText.IsEnabled = buttons;
                countText.Opacity = buttons ? 1 : 0.5;
                volumeButtonHeight.IsEnabled = buttons;
                volumeChecks.IsEnabled = on;
            };
            volume.SelectionChanged += delegate { volumeGapState(); };
            volumeButtonsOn.Checked += delegate { volumeGapState(); };
            volumeButtonsOn.Unchecked += delegate
            {
                volumeHideSlider.IsChecked = false;   // without buttons, the slider can't be hidden
                volumeGapState();
            };
            volumeGapState();

            doubleClick = Check("Open icons with a double-click (instead of a single click)", s.OpenWithDoubleClick);
            hideAfter = Check("Hide the launcher after opening an app", s.HideAfterLaunch);
            hideDeact = Check("Hide the launcher when I click outside it", s.HideOnDeactivate);
            startup = Check("Start the launcher when I sign in to Windows", LauncherApp.IsStartupEnabled());
            var checks = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            checks.Children.Add(doubleClick);
            checks.Children.Add(hideAfter);
            checks.Children.Add(hideDeact);
            checks.Children.Add(startup);
            AddRow(g, "", checks);

            // The Settings theme: one square button per theme, in that theme's color.
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 16, 0, 4) };
            for (int i = 0; i < SettingsTheme.Names.Length; i++)
            {
                var mode = (SettingsThemeMode)i;
                var b = new Button { Background = SettingsTheme.Swatch(mode), ToolTip = SettingsTheme.Names[i] };
                b.SetResourceReference(StyleProperty, "ThemeSwatch");
                System.Windows.Automation.AutomationProperties.SetName(b, SettingsTheme.Names[i]);
                b.Click += delegate
                {
                    theme = mode;
                    SettingsTheme.Apply(this, theme);   // shown right away; kept with OK or Apply
                    ShowThemeChoice();
                };
                themeSwatches.Add(b);
                swatches.Children.Add(b);
            }
            themeName = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 };
            swatches.Children.Add(themeName);
            ShowThemeChoice();
            AddRow(g, "Settings theme", swatches);
            ((TextBlock)g.Children[g.Children.Count - 2]).Margin = new Thickness(0, 19, 16, 0);

            var export = new Button { Content = "Export...", Padding = new Thickness(12, 2, 12, 2), ToolTip = "Save everything to a file" };
            var import = new Button { Content = "Import...", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Load everything from a file you exported" };
            export.Click += delegate { ExportBackup(); };
            import.Click += delegate { ImportBackup(); };
            var backupButtons = new StackPanel { Orientation = Orientation.Horizontal };
            backupButtons.Children.Add(export);
            backupButtons.Children.Add(import);
            var backup = new StackPanel();
            backup.Children.Add(backupButtons);
            backup.Children.Add(new TextBlock
            {
                Text = "Export saves all your icons, folders, lines, text, notes and settings (and background pictures) to one file. Import it here or on another PC to get them all back.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            BuildAutoBackup(s, backup);
            AddSection(g, "Backup", backup);
            AddSection(g, "Updates", BuildUpdates());
            return g;
        }

        void ShowThemeChoice()
        {
            for (int i = 0; i < themeSwatches.Count; i++) themeSwatches[i].Tag = i == (int)theme ? "on" : "";
            themeName.Text = SettingsTheme.Names[(int)theme];
        }

        // ---------- automatic backups ----------

        void BuildAutoBackup(AppSettings s, Panel backup)
        {
            autoBackup = new CheckBox { Content = "Back up automatically every", IsChecked = s.AutoBackup, VerticalAlignment = VerticalAlignment.Center };
            autoBackupDays = new TextBox
            {
                Text = s.AutoBackupDays.ToString(), Width = 44, Padding = new Thickness(3), Margin = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Any number of days from 1 to 365"
            };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            line.Children.Add(autoBackup);
            line.Children.Add(autoBackupDays);
            line.Children.Add(new TextBlock { Text = "days", VerticalAlignment = VerticalAlignment.Center });
            backup.Children.Add(line);

            autoBackupFolder = new TextBox { Text = s.BackupFolder, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "A folder on a drive of this PC" };
            var browse = new Button { Content = "Browse...", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += delegate { BrowseBackupFolder(); };
            var now = new Button { Content = "Back up now", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Save a backup in this folder right away" };
            now.Click += delegate { BackupNow(); };
            var folder = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var saveTo = new TextBlock { Text = "Save in", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(saveTo, Dock.Left);
            DockPanel.SetDock(now, Dock.Right);
            DockPanel.SetDock(browse, Dock.Right);
            folder.Children.Add(saveTo);
            folder.Children.Add(now);
            folder.Children.Add(browse);
            folder.Children.Add(autoBackupFolder);
            backup.Children.Add(folder);

            autoBackupStatus = new TextBlock { FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
            backup.Children.Add(autoBackupStatus);
            UpdateAutoBackupStatus();
            Action state = () => { autoBackupDays.IsEnabled = autoBackup.IsChecked == true; };
            autoBackup.Checked += delegate { state(); };
            autoBackup.Unchecked += delegate { state(); };
            state();
        }

        void UpdateAutoBackupStatus()
        {
            var s = app.Data.Settings;
            string text = "Each backup is a new file named with the date and time it was made, like \""
                        + LauncherApp.BackupFileName(new DateTime(2026, 11, 16, 14, 5, 32)) + "\".";
            if (s.LastAutoBackup != DateTime.MinValue) text += " Last backup: " + s.LastAutoBackup.ToString("f") + ".";
            if (s.AutoBackup)
            {
                DateTime next = s.LastAutoBackup == DateTime.MinValue ? DateTime.Now : s.LastAutoBackup.AddDays(Math.Max(1, s.AutoBackupDays));
                text += " Next: " + (next <= DateTime.Now ? "now" : next.ToString("f")) + ".";
            }
            autoBackupStatus.Text = text;
        }

        /// <summary>null when <paramref name="folder"/> can hold backups: a folder on a drive of this PC (not a network drive).</summary>
        static string BackupFolderProblem(string folder)
        {
            if (folder.Length == 0) return "Choose a folder for the backups.";
            if (folder.StartsWith(@"\\") || !System.IO.Path.IsPathRooted(folder) || System.IO.Path.GetPathRoot(folder).Length < 3)
                return "The backup folder should be on a drive of this PC, like D:\\Backups.";
            try
            {
                var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(folder)));
                if (drive.DriveType == System.IO.DriveType.Network) return "The backup folder should be on a drive of this PC, not a network drive.";
                if (!drive.IsReady) return "The drive " + drive.Name + " isn't available right now.";
            }
            catch (Exception) { return "That backup folder can't be used:\n" + folder; }
            return null;
        }

        void BrowseBackupFolder()
        {
            string current = autoBackupFolder.Text.Trim();
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Choose the folder for your backups (on a drive of this PC).",
                ShowNewFolderButton = true,
                SelectedPath = System.IO.Directory.Exists(current) ? current : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            })
            {
                if (dlg.ShowDialog(new OwnerWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle)) == System.Windows.Forms.DialogResult.OK)
                    autoBackupFolder.Text = dlg.SelectedPath;
            }
        }

        void BackupNow()
        {
            string problem = BackupFolderProblem(autoBackupFolder.Text.Trim());
            if (problem != null) { Fail(problem); return; }
            if (!Apply()) return;   // keeps the folder (and anything else changed here) first
            string file = app.AutoBackupIfDue(true, out problem);
            UpdateAutoBackupStatus();
            if (file == null) Fail("The backup couldn't be saved.\n\n" + problem);
            else autoBackupStatus.Text = "Saved \"" + System.IO.Path.GetFileName(file) + "\" in " + System.IO.Path.GetDirectoryName(file) + ".";
        }

        /// <summary>Lets a Windows Forms dialog (the folder picker) belong to this window, so it opens in front of it.</summary>
        sealed class OwnerWindow : System.Windows.Forms.IWin32Window
        {
            readonly IntPtr handle;
            public OwnerWindow(IntPtr handle) { this.handle = handle; }
            public IntPtr Handle { get { return handle; } }
        }

        /// <summary>A row set a little apart from the one above, with its label lined up with its first line.</summary>
        static void AddSection(Grid g, string label, FrameworkElement content)
        {
            content.Margin = new Thickness(0, 14, 0, 0);
            int row = g.RowDefinitions.Count;
            AddRow(g, "", content);
            var t = new TextBlock { Text = label, Margin = new Thickness(0, 17, 16, 0) };
            Grid.SetRow(t, row);
            g.Children.Add(t);
        }

        // ---------- updates ----------

        FrameworkElement BuildUpdates()
        {
            var stack = new StackPanel();
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            line.Children.Add(new TextBlock { Text = "Version " + Updates.CurrentText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            checkUpdates = new Button { Content = "Check for updates", Padding = new Thickness(12, 2, 12, 2), ToolTip = "See if GitHub has a newer version" };
            checkUpdates.Click += delegate { CheckForUpdates(); };
            line.Children.Add(checkUpdates);
            stack.Children.Add(line);
            // A portable copy says where it keeps everything (or why it can't).
            string where = LauncherData.PortableFolder != null ? "Portable copy: your icons, notes and settings are kept in " + LauncherData.PortableFolder + "."
                : LauncherData.PortableBlocked ? "This copy has " + LauncherData.PortableMarker + " beside it, but it can't write to its folder, so your icons, notes and settings are kept in "
                    + LauncherData.DataFolder + "." : null;
            if (where != null) stack.Children.Add(new TextBlock { Text = where, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0), Opacity = 0.8 });
            updateStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0), Visibility = Visibility.Collapsed };
            stack.Children.Add(updateStatus);
            var project = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            project.Inlines.Add(new System.Windows.Documents.Run("Project page on GitHub:  "));
            project.Inlines.Add(Link(Updates.ProjectUrl));
            stack.Children.Add(project);
            return stack;
        }

        void ShowUpdateStatus(string text)
        {
            updateStatus.Text = text;
            updateStatus.Visibility = Visibility.Visible;
        }

        void CheckForUpdates()
        {
            checkUpdates.IsEnabled = false;
            ShowUpdateStatus("Checking GitHub for a newer version...");
            System.Threading.Tasks.Task.Factory.StartNew(() => Updates.Latest()).ContinueWith(t =>
            {
                if (closed) return;
                checkUpdates.IsEnabled = true;
                if (t.IsFaulted)
                {
                    ShowUpdateStatus("Couldn't check for updates. " + t.Exception.GetBaseException().Message);
                    return;
                }
                var u = t.Result;
                if (u.Version <= Updates.Current)
                {
                    ShowUpdateStatus("You have the latest version (" + Updates.CurrentText + ").");
                    return;
                }
                ShowUpdateStatus("Version " + u.VersionText + " is available. You have " + Updates.CurrentText + ".");
                OfferUpdate(u);
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        void OfferUpdate(UpdateInfo u)
        {
            string notes = (u.Notes ?? "").Trim();
            if (notes.Length > 600) notes = notes.Substring(0, 600).TrimEnd() + "...";
            string intro = Program.AppName + " " + u.VersionText + " is available. You have " + Updates.CurrentText + "."
                         + (notes.Length > 0 ? "\n\nWhat's new:\n" + notes : "");
            if (u.ExeUrl == null)
            {
                // Nothing to download directly: send them to the release page.
                if (MessageBox.Show(this, intro + "\n\nOpen its download page?", Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    OpenWebPage(u.PageUrl);
                return;
            }
            if (MessageBox.Show(this, intro + "\n\nDownload and install it now? The launcher restarts by itself, and your icons, notes and settings are kept.",
                    Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            checkUpdates.IsEnabled = false;
            string downloading = "Downloading version " + u.VersionText + "...";
            ShowUpdateStatus(downloading);
            System.Threading.Tasks.Task.Factory.StartNew(() => Updates.Download(u, pct =>
                Dispatcher.BeginInvoke(new Action(() => { if (!closed && checkUpdates.IsEnabled == false) ShowUpdateStatus(downloading + " " + pct + "%"); }))))
            .ContinueWith(t =>
            {
                if (closed) return;
                if (t.IsFaulted)
                {
                    checkUpdates.IsEnabled = true;
                    ShowUpdateStatus("The update couldn't be downloaded. " + t.Exception.GetBaseException().Message);
                    return;
                }
                ShowUpdateStatus("Installing version " + u.VersionText + "...");
                string problem = app.InstallUpdate(t.Result);   // closes this window and restarts the launcher
                if (problem != null && !closed)
                {
                    checkUpdates.IsEnabled = true;
                    ShowUpdateStatus(problem);
                }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        }

        static void OpenWebPage(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        UIElement BuildAppearance(AppSettings s)
        {
            var g = Page();

            // Themes: a picture and colors for everything, all at once; built in, saved from your own look, or shared.
            AddThemeRows(g, s);
            AddRow(g, "", new TextBlock
            {
                Text = "A theme changes the background picture and the colors of every window and button. Save your own look as a theme, or share one with Export and Import. Choose None to go back to your own look, exactly as it was.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 10), TextWrapping = TextWrapping.Wrap
            });

            iconSize = Combo(Array.IndexOf(AppSettings.IconSizeOrder, s.IconSize), "Tiny", "Extra small", "Small", "Medium", "Large");
            AddRow(g, "Icon size", iconSize);
            showLabels = Check("Show names under icons", s.ShowLabels);
            AddRow(g, "", showLabels);
            arrange = Combo(Math.Max(0, Array.IndexOf(ArrangeOrder, s.Arrange)), "Free placement (drag anywhere)", "Free placement, align to nearby icons",
                            "Align icons to grid", "Auto arrange icons");
            arrange.ToolTip = "Align to nearby icons: a dropped icon lines up with the icons beside, above or below it, with guide lines while you drag.";
            AddRow(g, "Icon layout", arrange);
            scrollEnabled = Check("Scroll when there are more icons than fit (scroll bar and mouse wheel)", s.ScrollEnabled);
            AddRow(g, "", scrollEnabled);

            colorBox = AddColorRow(g, "Background color", s.BackgroundColor, false);
            winTransparency = AddSliderRow(g, "Background transparency", s.WindowTransparency, 0, 95, "%");
            AddRow(g, "", new TextBlock { Text = "Icons and text stay solid; only the background becomes see-through.", FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 6) });
            imageRow = AddImageRow(g, "Background picture", s.BackgroundImage, s.BackgroundImageOff);
            imageTransparency = AddSliderRow(g, "Picture transparency", s.BackgroundImageTransparency, 0, 100, "%");

            AddRow(g, "", new TextBlock
            {
                Text = "Colors of the launcher's parts. Auto keeps each one readable on what's behind it.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 14, 0, 4), TextWrapping = TextWrapping.Wrap
            });
            buttonsColorBox = AddColorRow(g, "Top buttons color", s.ButtonsColor, "Auto", AutoPartTip, PartPresets);
            volumeColorBox = AddColorRow(g, "Volume control color", s.VolumeColor, "Auto", AutoPartTip, PartPresets);
            iconTextColorBox = AddColorRow(g, "Icon names color", s.IconTextColor, "Auto", AutoPartTip, PartPresets);
            notesChromeColorBox = AddColorRow(g, "Notes title and tabs color", s.NotesChromeColor, "Auto", AutoPartTip, PartPresets);
            sameColor = Check("Use the same color for all four", s.SameColorForAll);
            AddRow(g, "", sameColor);
            allPartsColorBox = AddColorRow(g, "Color for all four", s.AllPartsColor, "Auto", AutoPartTip, PartPresets);
            Action partsState = () =>
            {
                bool same = sameColor.IsChecked == true;
                foreach (var b in new[] { buttonsColorBox, volumeColorBox, iconTextColorBox, notesChromeColorBox }) ((FrameworkElement)b.Parent).IsEnabled = !same;
                ((FrameworkElement)allPartsColorBox.Parent).IsEnabled = same;
            };
            sameColor.Checked += delegate { partsState(); };
            sameColor.Unchecked += delegate { partsState(); };
            partsState();

            return g;
        }

        /// <summary>
        /// The lamp strip's tab: where it goes and how it looks, the monitor and its Magic 8 Ball, text, random numbers and
        /// reminders.
        /// </summary>
        UIElement BuildLampStrip(AppSettings s)
        {
            var g = Page();
            AddRow(g, "", new TextBlock
            {
                Text = "A strip of blinking computer lamps, like the front of an early-1980s mainframe. Right-click the strip for a menu with all of these settings.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 6), TextWrapping = TextWrapping.Wrap
            });
            lampPlace = Combo((int)s.LampPanel, "Off", "Along the top (inside, buttons below it)", "Along the bottom (inside)",
                              "Above the launcher (outside)", "Below the launcher (outside)",
                              "At the bottom of the quick launch strip", "Behind the quick launch strip's icons");
            AddRow(g, "Position", lampPlace);
            lampColors = Combo(Array.IndexOf(LauncherWindow.ColorChoices, s.LampColors), LauncherWindow.ColorChoices.Select(LauncherWindow.ColorsText).ToArray());
            AddRow(g, "Lamp colors", lampColors);
            // Saved colors, or ones picked at random, from the strip's menu: said here, and kept unless colors are chosen here.
            lampColorsNote = new TextBlock { FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 4), TextWrapping = TextWrapping.Wrap };
            ShowLampColorsNote(s);
            AddRow(g, "", lampColorsNote);
            var hueBoxes = new WrapPanel { Margin = new Thickness(0, 3, 0, 0) };
            lampPastel = Check("Pastel (softer colors)", s.LampPastel);
            foreach (var h in LauncherWindow.Hues)
            {
                var one = h;
                var dot = new System.Windows.Shapes.Ellipse { Width = 13, Height = 13, StrokeThickness = 1, Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), VerticalAlignment = VerticalAlignment.Center };
                Action paint = () => dot.Fill = new SolidColorBrush(LampPalette.Swatch(one, lampPastel.IsChecked == true));
                paint();
                lampPastel.Checked += delegate { paint(); };
                lampPastel.Unchecked += delegate { paint(); };
                var words = new StackPanel { Orientation = Orientation.Horizontal };
                words.Children.Add(dot);
                words.Children.Add(new TextBlock { Text = one.ToString(), Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                var box = new CheckBox { Content = words, IsChecked = s.LampPicked.Contains(one), Width = 86, Margin = new Thickness(0, 2, 2, 2), VerticalContentAlignment = VerticalAlignment.Center };
                lampHues[one] = box;
                hueBoxes.Children.Add(box);
            }
            AddRow(g, "Your own colors", hueBoxes);
            customs = LampPalette.Tidy(s.LampCustom);
            customBoxes = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var addWords = new StackPanel { Orientation = Orientation.Horizontal };
            addWords.Children.Add(new TextBlock { Text = "\ue790", FontFamily = TileView.IconFont, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            addWords.Children.Add(new TextBlock { Text = "Add a color...", VerticalAlignment = VerticalAlignment.Center });
            addCustom = new Button { Content = addWords, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Left,
                                     ToolTip = "Pick any color from a color wheel for the lamps" };
            addCustom.Click += delegate { PickCustom(null); };
            var customLine = new StackPanel();
            customLine.Children.Add(customBoxes);
            customLine.Children.Add(addCustom);
            AddRow(g, "Custom colors", customLine);
            lampPastel.Checked += delegate { FillCustoms(); };
            lampPastel.Unchecked += delegate { FillCustoms(); };
            AddRow(g, "", lampPastel);
            lampRows = Combo(s.LampRows - LampFont.MinRows, Enumerable.Range(LampFont.MinRows, LampFont.MaxRows - LampFont.MinRows + 1).Select(LauncherWindow.HeightText).ToArray());
            AddRow(g, "Lamp strip height", lampRows);
            lampTextTails = Check("Leave room for the tails of g, j, p, q and y", s.LampTextTails);
            AddRow(g, "", lampTextTails);
            tailRowsWanted = s.LampTailRows;
            lampTailRows = Combo(0);
            lampTailRows.SelectionChanged += delegate { if (!fillingTailRows && lampTailRows.SelectedIndex >= 0) tailRowsWanted = lampTailRows.SelectedIndex + 1; };
            AddRow(g, "Lamps for tails", lampTailRows);
            lampMode = Combo((int)s.LampMode, "Standby (slowest, few lamps lit)", "Idle (slow, few lamps lit)", "Computing", "Alert (busier, with a sweep of light)",
                             LauncherWindow.RandomLampsText, LauncherWindow.CustomLampsText);
            AddRow(g, "Lamp activity", lampMode);
            lampActivityNote = new TextBlock { FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 2, 0, 4), TextWrapping = TextWrapping.Wrap };
            ShowLampActivityNote(s);
            AddRow(g, "", lampActivityNote);

            // Your own activity: how quick, and how many lit, for the strip's lamps, the monitor's lamps and its mini
            // lamps; and ones saved under a name. Moving a slider chooses Custom above.
            var levels = s.LampCustomActivity ?? new LampActivityLevels();
            customLevelGrid = new Grid { Margin = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var w in new[] { GridLength.Auto, new GridLength(120), new GridLength(34), new GridLength(120), new GridLength(40) })
                customLevelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            for (int r = 0; r < 4; r++) customLevelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Action<UIElement, int, int> place = (e, r, col) => { Grid.SetRow(e, r); Grid.SetColumn(e, col); customLevelGrid.Children.Add(e); };
            place(new TextBlock { Text = "Speed", Opacity = 0.7, FontSize = 11.5 }, 0, 1);
            place(new TextBlock { Text = "Lamps lit", Opacity = 0.7, FontSize = 11.5 }, 0, 3);
            string[] levelParts = { "Lamp strip", "Monitor lamps", "Monitor mini lamps" };
            int[] levelNow = { levels.StripSpeed, levels.StripLamps, levels.MonitorSpeed, levels.MonitorLamps, levels.LedSpeed, levels.LedLamps };
            for (int k = 0; k < 6; k++)
            {
                if (k % 2 == 0) place(new TextBlock { Text = levelParts[k / 2], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }, k / 2 + 1, 0);
                bool speed = k % 2 == 0;
                string unit = speed ? "" : "%";
                var sl = new Slider
                {
                    Minimum = speed ? LampActivityLevels.MinSpeed : 0, Maximum = speed ? LampActivityLevels.MaxSpeed : 100, IsSnapToTickEnabled = true, TickFrequency = 1,
                    SmallChange = 1, LargeChange = speed ? 2 : 10, VerticalAlignment = VerticalAlignment.Center, ToolTip = speed ? LauncherWindow.SpeedTip : LauncherWindow.LampsTip
                };
                sl.Value = Math.Max(sl.Minimum, Math.Min(sl.Maximum, levelNow[k]));
                var shows = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), Text = (int)sl.Value + unit };
                sl.ValueChanged += delegate
                {
                    shows.Text = (int)sl.Value + unit;
                    if (puttingLevels) return;
                    lampMode.SelectedIndex = (int)LampActivity.Custom;
                    FillActivities();
                };
                customLevel[k] = sl;
                place(sl, k / 2 + 1, speed ? 1 : 3);
                place(shows, k / 2 + 1, speed ? 2 : 4);
            }
            AddRow(g, "Custom activity", customLevelGrid);
            activities = LampActivityLevels.TidyList(s.LampActivities);
            lampActivities = new ComboBox { Width = 250, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Choose a saved activity to put it on the sliders above and use it" };
            saveActivity = new Button { Content = "Save...", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Keep the sliders' settings under a name, to use again later" };
            deleteActivity = new Button { Content = "Delete", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Delete the saved activity chosen here" };
            var activityRow = new StackPanel { Orientation = Orientation.Horizontal };
            activityRow.Children.Add(lampActivities);
            activityRow.Children.Add(saveActivity);
            activityRow.Children.Add(deleteActivity);
            activityLine = activityRow;
            AddRow(g, "Saved activities", activityRow);
            lampActivities.SelectionChanged += delegate
            {
                if (fillingActivities || lampActivities.SelectedIndex < 0) return;
                PutLevels(activities[lampActivities.SelectedIndex]);
                lampMode.SelectedIndex = (int)LampActivity.Custom;
            };
            saveActivity.Click += delegate
            {
                var l = LevelsChosen();
                var had = activities.FirstOrDefault(a => a.SameAs(l));
                string name = InputDialog.Ask(this, app, "Save the lamp activity", "A name for this lamp activity:\n\n" + l.Summary(), had != null ? had.Name : LampActivityLevels.NextName(activities));
                if (name == null) return;
                activities = LampActivityLevels.Save(activities, name, l);
                lampMode.SelectedIndex = (int)LampActivity.Custom;
                FillActivities();
            };
            deleteActivity.Click += delegate
            {
                if (lampActivities.SelectedIndex < 0) return;
                var a = activities[lampActivities.SelectedIndex];
                if (!DeleteDialog.AskActivity(this, theme, a.Name, a.Summary())) return;
                activities.Remove(a);
                FillActivities();
            };
            FillActivities();
            lampMonitor = Combo((int)s.LampMonitor, "None", "At the left end of the strip", "At the right end of the strip");
            lampMonitor.ToolTip = "A computer cabinet lettered WOPR, with a few lamps blinking in its window, standing at one end of the strip and rising above it. It shows when the strip is above or below the launcher (outside).";
            AddRow(g, "Monitor", lampMonitor);
            lampTextMode = Combo((int)s.LampTextMode, "None (the lamps just blink)", "Scroll it across the strip", "Flash it on the strip", "Keep it still on the strip", "Fade it in out of the blinking lamps, and back");
            lampTextMode.ToolTip = "Spells out your text in lamps on the strip, instead of blinking.";
            AddRow(g, "Lamp text", lampTextMode);
            lampText = new TextBox { Text = s.LampText ?? "", Width = 250, MaxLength = 200, Padding = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Left,
                                     ToolTip = "Letters, numbers and common punctuation (small letters show as capitals when the strip is 6 lamps high)" };
            AddRow(g, "Text to show", lampText);
            lampTextSides = Check("Keep the lamps blinking on either side of the text", s.LampTextSides);
            AddRow(g, "", lampTextSides);
            lampScrollSpeed = AddSliderRow(g, "Text scroll speed", s.LampScrollSpeed, AppSettings.MinScrollSpeed, AppSettings.MaxScrollSpeed, "%");

            // The Magic 8 Ball: in the monitor (a monitor goes at the right end if there's none, and goes again when the
            // ball is unticked), and Instant Response.
            lampEightBall = Check("In the monitor (click it for an answer)", s.LampEightBall && s.LampMonitor != LampMonitorPlace.None);
            lampEightBallInstant = Check("Instant Response (the answer shows at once and stays until you click the ball again)", s.LampEightBallInstant);
            lampEightBallInstant.Margin = new Thickness(22, 0, 0, 3);
            monitorForBall = s.LampEightBall && s.LampMonitorForBall;
            lampEightBall.Checked += delegate
            {
                monitorForBall = lampMonitor.SelectedIndex == (int)LampMonitorPlace.None;
                if (monitorForBall) lampMonitor.SelectedIndex = (int)LampMonitorPlace.Right;
            };
            lampEightBall.Unchecked += delegate
            {
                bool back = monitorForBall;
                monitorForBall = false;
                if (back) lampMonitor.SelectedIndex = (int)LampMonitorPlace.None;   // as it was before the ball
            };
            lampMonitor.SelectionChanged += delegate { if (lampMonitor.SelectedIndex == (int)LampMonitorPlace.None) lampEightBall.IsChecked = false; };
            AddRow(g, "Magic 8 Ball", lampEightBall);
            AddRow(g, "", lampEightBallInstant);

            // Random numbers: the pattern they're picked with (an example of what it picks, or what's wrong with it), and
            // patterns saved for later. Right-clicking the strip picks them.
            lampNumbers = new TextBox { Text = s.LampNumbers ?? "", Width = 250, MaxLength = 300, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center,
                                        ToolTip = "Groups separated by commas, each like xxx/0-999: an x for each digit, then a slash and the lowest and highest number" };
            savePattern = new Button { Content = "Save...", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Keep this pattern under a name, to use again later" };
            var numberLine = new StackPanel { Orientation = Orientation.Horizontal };
            numberLine.Children.Add(lampNumbers);
            numberLine.Children.Add(savePattern);
            lampNumbersExample = new TextBlock { FontSize = 11.5, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
            var numberRow = new StackPanel();
            numberRow.Children.Add(numberLine);
            numberRow.Children.Add(lampNumbersExample);
            AddRow(g, "Random numbers", numberRow);
            patterns = LauncherWindow.TidyPatterns(s.LampNumberPatterns);
            lampPatterns = new ComboBox { Width = 250, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Choose a saved pattern to use it" };
            deletePattern = new Button { Content = "Delete", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Delete the saved pattern chosen here" };
            var savedLine = new StackPanel { Orientation = Orientation.Horizontal };
            savedLine.Children.Add(lampPatterns);
            savedLine.Children.Add(deletePattern);
            AddRow(g, "Saved patterns", savedLine);
            Action showNumbers = () =>
            {
                string problem;
                var p = NumberPattern.Parse(lampNumbers.Text, out problem);
                lampNumbersExample.Text = p == null ? problem : "For example: " + p.Pick() + "  (right-click the strip and choose Pick random numbers)";
                lampNumbersExample.Opacity = p == null ? 1 : 0.7;
                // what's wrong, in red; an example, in the usual color
                if (p == null) lampNumbersExample.Foreground = new SolidColorBrush(Color.FromRgb(0xD2, 0x3C, 0x3C));
                else lampNumbersExample.ClearValue(TextBlock.ForegroundProperty);
                lampNumbersExample.FontWeight = p == null ? FontWeights.SemiBold : FontWeights.Normal;
                savePattern.IsEnabled = p != null && lampNumbers.IsEnabled;
                FillPatterns();
            };
            lampNumbers.TextChanged += delegate { showNumbers(); };
            lampPatterns.SelectionChanged += delegate
            {
                if (fillingPatterns || lampPatterns.SelectedIndex < 0) return;
                lampNumbers.Text = patterns[lampPatterns.SelectedIndex].Pattern;
            };
            savePattern.Click += delegate
            {
                string pattern = NumberPattern.Tidy(lampNumbers.Text);
                var had = patterns.FirstOrDefault(p => p.Pattern == pattern);
                string name = InputDialog.Ask(this, app, "Save the number pattern", "A name for " + pattern + " (leave it empty to call it by the pattern itself):", had != null ? had.Name : "");
                if (name == null) return;
                patterns = LauncherWindow.SavePattern(patterns, name, pattern);
                lampNumbers.Text = pattern;
                FillPatterns();
            };
            deletePattern.Click += delegate
            {
                if (lampPatterns.SelectedIndex < 0) return;
                var p = patterns[lampPatterns.SelectedIndex];
                if (!DeleteDialog.AskPattern(this, theme, p.Name, p.Pattern)) return;
                patterns.Remove(p);
                FillPatterns();
            };
            showNumbers();

            // Reminders: each flashes on the strip from its date and time on, until it's dismissed. A new one's date
            // starts at tomorrow, 9:00 AM.
            var c = System.Globalization.CultureInfo.CurrentCulture;
            reminders = s.Reminders.Select(r => new LampReminder { At = r.At, Text = r.Text }).ToList();
            reminderList = new StackPanel();
            AddRow(g, "Reminders", reminderList);
            var at = DateTime.Today.AddDays(1).AddHours(9);
            reminderMonth = Combo(at.Month - 1, c.DateTimeFormat.MonthNames.Take(12).ToArray());
            reminderMonth.Width = 116;
            reminderDay = Combo(at.Day - 1, Enumerable.Range(1, 31).Select(d => d.ToString(c)).ToArray());
            reminderDay.Width = 58;
            reminderDay.Margin = new Thickness(6, 0, 0, 0);
            reminderYear = Combo(0, Enumerable.Range(DateTime.Today.Year, 6).Select(y => y.ToString(c)).ToArray());
            reminderYear.SelectedItem = at.Year.ToString(c);
            reminderYear.Width = 74;
            reminderYear.Margin = new Thickness(6, 0, 0, 0);
            reminderTime = Combo(at.Hour * 2, Enumerable.Range(0, 48).Select(k => DateTime.Today.AddMinutes(30 * k).ToString("t", c)).ToArray());
            reminderTime.Width = 100;
            var when = new StackPanel { Orientation = Orientation.Horizontal };
            when.Children.Add(reminderMonth);
            when.Children.Add(reminderDay);
            when.Children.Add(reminderYear);
            when.Children.Add(new TextBlock { Text = "at", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
            when.Children.Add(reminderTime);
            AddRow(g, "New reminder", when);
            reminderText = new TextBox { Width = 250, MaxLength = 200, Padding = new Thickness(3), ToolTip = "What the strip flashes when the reminder is due" };
            addReminder = new Button { Content = "Add", Padding = new Thickness(14, 2, 14, 2), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Put this reminder on the list" };
            addReminder.Click += delegate { AddReminderFromFields(); };
            var textLine = new StackPanel { Orientation = Orientation.Horizontal };
            textLine.Children.Add(reminderText);
            textLine.Children.Add(addReminder);
            AddRow(g, "Reminder text", textLine);
            FillReminderList();

            Action lampState = () =>
            {
                bool strip = lampPlace.SelectedIndex != (int)LampPanelPlace.Off;
                lampMode.IsEnabled = lampRows.IsEnabled = lampColors.IsEnabled = lampPastel.IsEnabled = strip;
                hueBoxes.IsEnabled = customBoxes.IsEnabled = strip && LampColorsChosen == LampColors.Picked;
                addCustom.IsEnabled = strip;
                lampTextTails.IsEnabled = strip && LampFont.TailRowsFor(LampRowsChosen) > 0;
                FillTailRows();
                lampTailRows.IsEnabled = lampTextTails.IsEnabled && lampTextTails.IsChecked == true;
                lampTextMode.IsEnabled = lampText.IsEnabled = lampTextSides.IsEnabled = strip;
                ((FrameworkElement)lampScrollSpeed.Parent).IsEnabled = strip && lampTextMode.SelectedIndex == (int)LampTextEffect.Scroll;
                lampMonitor.IsEnabled = lampEightBall.IsEnabled = lampPlace.SelectedIndex == (int)LampPanelPlace.AboveLauncher || lampPlace.SelectedIndex == (int)LampPanelPlace.BelowLauncher;
                lampEightBallInstant.IsEnabled = lampEightBall.IsEnabled && lampEightBall.IsChecked == true;
                reminderList.IsEnabled = reminderMonth.IsEnabled = reminderDay.IsEnabled = reminderYear.IsEnabled = reminderTime.IsEnabled = strip;
                reminderText.IsEnabled = addReminder.IsEnabled = strip;
                lampNumbers.IsEnabled = lampPatterns.IsEnabled = strip;
                customLevelGrid.IsEnabled = strip;
                FillActivities();
                savePattern.IsEnabled = strip && NumberPattern.IsPattern(lampNumbers.Text);
                deletePattern.IsEnabled = strip && lampPatterns.SelectedIndex >= 0;
            };
            lampPlace.SelectionChanged += delegate { lampState(); };
            lampTextMode.SelectionChanged += delegate { lampState(); };
            lampRows.SelectionChanged += delegate { lampState(); };
            lampColors.SelectionChanged += delegate
            {
                if (LampColorsChosen == LampColors.Picked && ColorsTicked == 0) lampHues[LampHue.Blue].IsChecked = true;   // something to start from
                lampState();
            };
            foreach (var box in lampHues.Values)   // your own colors: at least one stays ticked
            {
                var one = box;
                one.Unchecked += delegate { if (LampColorsChosen == LampColors.Picked && ColorsTicked == 0) one.IsChecked = true; };
            }
            lampTextTails.Checked += delegate { lampState(); };
            lampEightBall.Checked += delegate { lampState(); };
            lampEightBall.Unchecked += delegate { lampState(); };
            lampTextTails.Unchecked += delegate { lampState(); };
            customsChanged = lampState;
            FillCustoms();
            lampState();
            return g;
        }

        Action customsChanged = delegate { };

        /// <summary>How many of your own colors are ticked, ones from the wheel too.</summary>
        int ColorsTicked { get { return HuesPicked.Count + customs.Count(c => c.On); } }

        static Button SmallButton(string glyph, string tip)
        {
            return new Button { Content = new TextBlock { Text = glyph, FontFamily = TileView.IconFont, FontSize = 12 }, Width = 24, Height = 24, Padding = new Thickness(0),
                                Margin = new Thickness(0, 1, 2, 1), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
        }

        /// <summary>The colors from the wheel: a checkbox for each, and buttons to change it or take it off the list.</summary>
        void FillCustoms()
        {
            customBoxes.Children.Clear();
            foreach (var c in customs)
            {
                var one = c;
                var words = new StackPanel { Orientation = Orientation.Horizontal };
                System.Windows.Shapes.Ellipse dot;
                var change = LauncherWindow.ClickableDot(LampPalette.Swatch(one.Color, lampPastel.IsChecked == true), "Change this color with the color wheel", out dot, () => PickCustom(one));
                change.Margin = new Thickness(-2);
                words.Children.Add(change);
                words.Children.Add(new TextBlock { Text = one.Color, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                var box = new CheckBox { Content = words, IsChecked = one.On, Width = 98, VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                                         ToolTip = "Use this color for the lamps" };
                box.Checked += delegate { one.On = true; };
                box.Unchecked += delegate
                {
                    one.On = false;
                    if (LampColorsChosen == LampColors.Picked && ColorsTicked == 0) box.IsChecked = true;   // at least one stays ticked
                };
                var remove = SmallButton("\ue711", "Delete this color");
                remove.Click += delegate
                {
                    if (!DeleteDialog.Ask(this, theme, one.Color, LampPalette.Swatch(one.Color, lampPastel.IsChecked == true))) return;
                    customs.Remove(one);
                    if (LampColorsChosen == LampColors.Picked && ColorsTicked == 0) lampHues[LampHue.Blue].IsChecked = true;
                    FillCustoms();
                    customsChanged();
                };
                var chip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 2) };
                chip.Children.Add(box);
                chip.Children.Add(remove);
                customBoxes.Children.Add(chip);
            }
        }

        /// <summary>The color wheel: a new color, or another in place of one on the list; it's ticked, and your own colors used.</summary>
        void PickCustom(LampCustomColor change)
        {
            string start = change != null ? change.Color : customs.Count > 0 ? customs[customs.Count - 1].Color
                : LampPalette.Hex(LampPalette.Swatch(HuesPicked.Count > 0 ? HuesPicked[0] : LampHue.Amber, false).ToString());
            string picked = LampPalette.Hex(ColorPickerDialog.Pick(this, start));
            if (picked == null) return;
            if (change != null) { change.Color = picked; change.On = true; }
            else if (customs.All(c => c.Color != picked)) customs.Add(new LampCustomColor { Color = picked, On = true });
            foreach (var c in customs) if (c.Color == picked) c.On = true;   // one that was there already
            customs = LampPalette.Tidy(customs);
            lampColors.SelectedIndex = Array.IndexOf(LauncherWindow.ColorChoices, LampColors.Picked);
            FillCustoms();
            customsChanged();
        }

        LampColors LampColorsChosen { get { return LauncherWindow.ColorChoices[Math.Max(0, lampColors.SelectedIndex)]; } }
        List<LampHue> HuesPicked { get { return LauncherWindow.Hues.Where(h => lampHues[h].IsChecked == true).ToList(); } }
        int LampRowsChosen { get { return LampFont.MinRows + Math.Max(0, lampRows.SelectedIndex); } }

        /// <summary>The custom activity's sliders, as they're set.</summary>
        LampActivityLevels LevelsChosen()
        {
            Func<int, int> v = i => (int)customLevel[i].Value;
            return new LampActivityLevels { StripSpeed = v(0), StripLamps = v(1), MonitorSpeed = v(2), MonitorLamps = v(3), LedSpeed = v(4), LedLamps = v(5) };
        }

        /// <summary>Sets the custom activity's sliders (without choosing Custom).</summary>
        void PutLevels(LampActivityLevels l)
        {
            puttingLevels = true;
            try
            {
                int[] v = { l.StripSpeed, l.StripLamps, l.MonitorSpeed, l.MonitorLamps, l.LedSpeed, l.LedLamps };
                for (int k = 0; k < 6; k++) customLevel[k].Value = v[k];
            }
            finally { puttingLevels = false; }
            FillActivities();
        }

        /// <summary>The saved custom activities, the one with the sliders' settings chosen (none if they aren't saved).</summary>
        void FillActivities()
        {
            if (lampActivities == null || activities == null || customLevel[5] == null) return;
            fillingActivities = true;
            try
            {
                var names = activities.Select(a => a.Name).ToList();
                if (!names.SequenceEqual(lampActivities.Items.Cast<string>()))
                {
                    lampActivities.Items.Clear();
                    foreach (var x in names) lampActivities.Items.Add(x);
                }
                var now = LevelsChosen();
                lampActivities.SelectedIndex = activities.FindIndex(a => a.SameAs(now));
                bool strip = customLevelGrid.IsEnabled;
                lampActivities.IsEnabled = activities.Count > 0 && strip;
                deleteActivity.IsEnabled = lampActivities.SelectedIndex >= 0 && strip;
                saveActivity.IsEnabled = strip;
            }
            finally { fillingActivities = false; }
        }

        /// <summary>Scrolls the Lamp strip tab to the custom activity.</summary>
        /// <summary>Test copies only: scrolls to the Magic 8 Ball.</summary>
        public void ShowEightBall()
        {
            Dispatcher.BeginInvoke(new Action(() => { if (lampEightBallInstant != null) lampEightBallInstant.BringIntoView(); }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Test copies only: ticking the Magic 8 Ball with no monitor and unticking it (the monitor comes and goes again),
        /// the same with a monitor already there (it stays), Instant Response, and that Apply keeps them.
        /// </summary>
        internal string TestEightBall()
        {
            var log = new List<string>();
            Func<string> now = () => "monitor " + lampMonitor.SelectedItem + "; ball " + lampEightBall.IsChecked + "; Instant Response " + lampEightBallInstant.IsChecked + (lampEightBallInstant.IsEnabled ? "" : " (off)");
            Func<string> kept = () => { var st = app.Data.Settings; return "saved: monitor " + st.LampMonitor + (st.LampMonitorForBall ? " (for the ball)" : "") + ", ball " + st.LampEightBall + ", instant " + st.LampEightBallInstant; };
            log.Add("at first: " + now());
            lampMonitor.SelectedIndex = (int)LampMonitorPlace.None;
            log.Add("no monitor: " + now());
            lampEightBall.IsChecked = true;
            log.Add("ball ticked: " + now());
            lampEightBall.IsChecked = false;
            log.Add("ball unticked: " + now());
            lampMonitor.SelectedIndex = (int)LampMonitorPlace.Left;
            lampEightBall.IsChecked = true;
            log.Add("monitor at the left, ball ticked: " + now());
            lampEightBall.IsChecked = false;
            log.Add("ball unticked: " + now());
            lampMonitor.SelectedIndex = (int)LampMonitorPlace.None;
            lampEightBall.IsChecked = true;
            lampEightBallInstant.IsChecked = true;
            log.Add("no monitor, then ball and Instant Response ticked: " + now());
            Apply();
            log.Add("after Apply: " + kept());
            lampMonitor.SelectedIndex = (int)LampMonitorPlace.None;
            log.Add("monitor set to None: " + now());
            Apply();
            log.Add("after Apply: " + kept());
            return string.Join("\r\n", log);
        }

        public void ShowActivity()
        {
            Dispatcher.BeginInvoke(new Action(() => { if (activityLine != null) activityLine.BringIntoView(); }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Test copies only: moving the custom activity's sliders (Custom is chosen), saving them under a name and with
        /// none, choosing a saved one, deleting one (asked first), and that Apply keeps them.
        /// </summary>
        internal string TestLampActivity()
        {
            var log = new List<string>();
            Func<string> now = () => "activity " + lampMode.SelectedItem + "; sliders: " + LevelsChosen().Summary() + " | saved: " + string.Join(" / ", lampActivities.Items.Cast<string>())
                + ", chosen " + lampActivities.SelectedIndex + (saveActivity.IsEnabled ? "" : ", can't save") + (deleteActivity.IsEnabled ? "" : ", can't delete");
            log.Add("at first: " + now());
            customLevel[0].Value = 3;
            customLevel[5].Value = 30;
            log.Add("two sliders moved: " + now());
            InputDialog.TestAnswers.Enqueue("Calm");
            saveActivity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("saved as Calm: " + now() + "; asked: " + (InputDialog.TestAsked ?? "").Replace("\n", " / "));
            customLevel[0].Value = 17;
            customLevel[1].Value = 80;
            InputDialog.TestAnswers.Enqueue("");
            saveActivity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("saved with no name: " + now());
            customLevel[2].Value = 1;
            InputDialog.TestAnswers.Enqueue("<cancel>");
            saveActivity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("save canceled: " + now());
            lampMode.SelectedIndex = (int)LampActivity.Idle;
            lampActivities.SelectedIndex = 0;
            log.Add("Idle, then Calm chosen: " + now());
            DeleteDialog.TestAnswer = false;
            deleteActivity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("Delete, then Cancel (asked about " + DeleteDialog.TestAsked + "): " + now());
            lampActivities.SelectedIndex = 1;
            DeleteDialog.TestAnswer = true;
            deleteActivity.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DeleteDialog.TestAnswer = null;
            log.Add("the second deleted: " + now());
            lampActivities.SelectedIndex = 0;
            Apply();
            var st = app.Data.Settings;
            log.Add("after Apply: saved " + st.LampMode + ", " + st.LampCustomActivity.Summary() + " | " + string.Join(" / ", st.LampActivities.Select(a => a.Name + " = " + a.Summary())));
            var look = LookFromControls();
            log.Add("the look has " + look.LampMode + ", " + (look.LampCustomActivity == null ? "no custom activity" : look.LampCustomActivity.Summary()));
            PutLevels(new LampActivityLevels { StripSpeed = 5 });
            log.Add("sliders put from elsewhere (Custom isn't chosen by that): " + now());
            PutLook(look);
            log.Add("the look put back: " + now());
            return string.Join("\r\n", log);
        }

        bool fillingPatterns;

        /// <summary>The saved patterns ("name: pattern"), the one in the pattern box chosen (none if it isn't saved).</summary>
        void FillPatterns()
        {
            if (lampPatterns == null || patterns == null) return;
            fillingPatterns = true;
            try
            {
                var texts = patterns.Select(p => p.Name == p.Pattern ? p.Pattern : p.Name + ":  " + p.Pattern).ToList();
                if (!texts.SequenceEqual(lampPatterns.Items.Cast<string>()))
                {
                    lampPatterns.Items.Clear();
                    foreach (var x in texts) lampPatterns.Items.Add(x);
                }
                string now = NumberPattern.Tidy(lampNumbers.Text);
                lampPatterns.SelectedIndex = patterns.FindIndex(p => p.Pattern == now);
                lampPatterns.IsEnabled = patterns.Count > 0 && lampNumbers.IsEnabled;
                deletePattern.IsEnabled = lampPatterns.SelectedIndex >= 0 && lampNumbers.IsEnabled;
            }
            finally { fillingPatterns = false; }
        }

        /// <summary>
        /// Test copies only: typing number patterns (what's shown under the box), saving and choosing them, deleting one
        /// (asked first), and that Apply keeps them (a pattern that isn't one isn't kept).
        /// </summary>
        internal string TestNumbers()
        {
            var log = new List<string>();
            Func<string> now = () => "'" + lampNumbers.Text + "': " + lampNumbersExample.Text + " | saved: " + string.Join(" / ", lampPatterns.Items.Cast<string>()) + ", chosen " + lampPatterns.SelectedIndex
                + (savePattern.IsEnabled ? "" : ", can't save") + (deletePattern.IsEnabled ? "" : ", can't delete");
            log.Add("at first: " + now());
            foreach (var t in new[] { "xx/1-49, xx/1-49,xx/1-49", "x/1-6", "XXX", "xx/0-999", "xx/50-10", "abc", "x/1-6,,x/1-6", "", "xxxxxxxxxx", string.Join(",", Enumerable.Repeat("x", 21)) })
            {
                lampNumbers.Text = t;
                log.Add("typed " + now());
            }
            lampNumbers.Text = "xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49";
            InputDialog.TestAnswers.Enqueue("Lottery");
            savePattern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("saved as Lottery: " + now());
            lampNumbers.Text = "x/1-6,x/1-6";
            InputDialog.TestAnswers.Enqueue("");
            savePattern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("saved with no name: " + now());
            lampNumbers.Text = "xxx/100-999";
            InputDialog.TestAnswers.Enqueue("<cancel>");
            savePattern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("save canceled: " + now());
            lampPatterns.SelectedIndex = 0;
            log.Add("Lottery chosen: " + now());
            DeleteDialog.TestAnswer = false;
            deletePattern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("Delete, then Cancel (asked about " + DeleteDialog.TestAsked + "): " + now());
            lampPatterns.SelectedIndex = 1;
            DeleteDialog.TestAnswer = true;
            deletePattern.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DeleteDialog.TestAnswer = null;
            log.Add("the dice deleted: " + now());
            lampNumbers.Text = "xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49";
            Apply();
            log.Add("after Apply: saved " + app.Data.Settings.LampNumbers + "; " + string.Join(" / ", app.Data.Settings.LampNumberPatterns.Select(p => p.Name + " = " + p.Pattern)));
            lampNumbers.Text = "not one";
            Apply();
            log.Add("after Apply with 'not one': saved " + app.Data.Settings.LampNumbers);
            return string.Join("\r\n", log);
        }

        /// <summary>The choices of lamps for tails, for the strip height chosen (a lower strip has room for fewer).</summary>
        void FillTailRows()
        {
            int rows = LampRowsChosen, most = LampFont.TailRowsFor(rows);
            var texts = Enumerable.Range(1, Math.Max(1, most)).Select(k => LauncherWindow.TailText(Math.Max(rows, LampFont.MinRows + 1), k)).ToList();
            fillingTailRows = true;
            try
            {
                if (!texts.SequenceEqual(lampTailRows.Items.Cast<string>()))
                {
                    lampTailRows.Items.Clear();
                    foreach (var x in texts) lampTailRows.Items.Add(x);
                }
                lampTailRows.SelectedIndex = Math.Min(tailRowsWanted, texts.Count) - 1;
            }
            finally { fillingTailRows = false; }
        }

        /// <summary>The reminders, soonest first, each with its own Delete button.</summary>
        void FillReminderList()
        {
            var c = System.Globalization.CultureInfo.CurrentCulture;
            reminderList.Children.Clear();
            if (reminders.Count == 0)
            {
                reminderList.Children.Add(new TextBlock { Text = "None yet: choose a date and time below, type the text, and click Add.", Opacity = 0.7,
                                                          Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
                return;
            }
            foreach (var r in reminders.OrderBy(r => r.At))
            {
                var one = r;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var delete = new Button { Content = "Delete", Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Delete this reminder" };
                delete.Click += delegate { reminders.Remove(one); FillReminderList(); };
                DockPanel.SetDock(delete, Dock.Right);
                row.Children.Add(delete);
                string when = one.At.ToString(one.At.Year == DateTime.Today.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy", c) + ", " + one.At.ToString("t", c);
                row.Children.Add(new TextBlock
                {
                    Text = when + (one.At <= DateTime.Now ? " (flashing now)" : "") + "  \u2014  " + one.Text,
                    VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = one.Text
                });
                reminderList.Children.Add(row);
            }
        }

        /// <summary>Puts the reminder in the fields on the list (when it has some text) and empties the text box.</summary>
        bool AddReminderFromFields()
        {
            string text = reminderText.Text.Trim();
            if (text.Length == 0) return false;
            int y = int.Parse((string)reminderYear.SelectedItem, System.Globalization.CultureInfo.CurrentCulture), m = reminderMonth.SelectedIndex + 1;
            int d = Math.Min(reminderDay.SelectedIndex + 1, DateTime.DaysInMonth(y, m)), half = Math.Max(0, reminderTime.SelectedIndex);
            reminders.Add(new LampReminder { At = new DateTime(y, m, d, half / 2, half % 2 * 30, 0), Text = text });
            reminderText.Text = "";
            FillReminderList();
            return true;
        }

        /// <summary>
        /// Test copies only: adds two reminders through the fields and the Add button, deletes the first in the list
        /// with its Delete button, leaves a third typed but not added, and keeps the changes with Apply.
        /// </summary>
        internal string TestReminders()
        {
            var log = new List<string>();
            Func<string> list = () => string.Join(" | ", reminderList.Children.OfType<FrameworkElement>().Select(e =>
                e is DockPanel ? ((DockPanel)e).Children.OfType<TextBlock>().First().Text : e is TextBlock ? "(" + ((TextBlock)e).Text + ")" : "?"));
            log.Add("list at first: " + list());
            var next = (DateTime.Today.Year + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
            reminderYear.SelectedItem = next;
            reminderMonth.SelectedIndex = 11; reminderDay.SelectedIndex = 24; reminderTime.SelectedIndex = 16;
            reminderText.Text = "Christmas morning";
            addReminder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            reminderMonth.SelectedIndex = 1; reminderDay.SelectedIndex = 29; reminderTime.SelectedIndex = 29;
            reminderText.Text = "Pay rent";
            addReminder.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("after adding two: " + list() + "; text box now: '" + reminderText.Text + "'");
            var firstDelete = reminderList.Children.OfType<DockPanel>().First().Children.OfType<Button>().First();
            firstDelete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("after deleting the first: " + list());
            reminderText.Text = "Typed, never added";
            reminderMonth.SelectedIndex = 0; reminderDay.SelectedIndex = 0; reminderTime.SelectedIndex = 0;
            Apply();
            log.Add("after Apply, the list here: " + list());
            return string.Join("\r\n", log);
        }

        /// <summary>
        /// Test copies only: the lamp strip's height and scroll speed: when they can be changed, that Apply keeps them,
        /// and that saved looks bring the height along (a look from before 1.88 leaves it alone).
        /// </summary>
        internal string TestLampSize()
        {
            var log = new List<string>();
            Func<string> now = () => "'" + lampColors.SelectedItem + "'" + (lampColors.IsEnabled ? "" : " (off)") + ", height '" + lampRows.SelectedItem + "'" + (lampRows.IsEnabled ? "" : " (off)")
                + ", tails " + (lampTextTails.IsChecked == true ? "ticked" : "not ticked") + (lampTextTails.IsEnabled ? "" : " (off)")
                + ", '" + lampTailRows.SelectedItem + "'" + (lampTailRows.IsEnabled ? "" : " (off)")
                + ", scroll speed " + (int)lampScrollSpeed.Value + "%" + (lampScrollSpeed.IsEnabled ? "" : " (off)");
            log.Add("at first: " + now());
            lampTextMode.SelectedIndex = (int)LampTextEffect.Scroll;
            log.Add("text scrolling: " + now());
            lampTextMode.SelectedIndex = (int)LampTextEffect.Flash;
            log.Add("text flashing: " + now());
            lampTextMode.SelectedIndex = (int)LampTextEffect.Still;
            log.Add("text still ('" + lampTextMode.SelectedItem + "'): " + now());
            int place = lampPlace.SelectedIndex;
            lampPlace.SelectedIndex = 0;
            log.Add("no strip: " + now());
            lampPlace.SelectedIndex = place;
            lampTextMode.SelectedIndex = (int)LampTextEffect.Scroll;
            lampTextTails.IsChecked = true;
            lampRows.SelectedIndex = 3;
            lampTailRows.SelectedIndex = 3;
            foreach (int k in new[] { 3, 1, 0, 2, 4 })   // 9, 7, 6, 8 and 10 lamps: the choice of 4 for tails comes back
            {
                lampRows.SelectedIndex = k;
                log.Add((k + 6) + " lamps: " + now() + " of " + lampTailRows.Items.Count);
            }
            lampColors.SelectedIndex = 1;
            lampTextMode.SelectedIndex = (int)LampTextEffect.Emerge;
            log.Add("text fading in ('" + lampTextMode.SelectedItem + "'): " + now());
            lampTailRows.SelectedIndex = 2;
            lampScrollSpeed.Value = 175;
            Apply();
            log.Add("after Apply: saved " + app.Data.Settings.LampRows + " lamps, tails " + app.Data.Settings.LampTextTails + ", " + app.Data.Settings.LampTailRows + " rows for them, " + app.Data.Settings.LampScrollSpeed + "%, text " + app.Data.Settings.LampTextMode + ", " + app.Data.Settings.LampColors);
            var look = LookFromControls();
            log.Add("the look now has " + look.LampRows + " lamps, " + look.LampColors);
            look.LampRows = 6;
            look.LampColors = LampColors.RedGreen;
            PutLook(look);
            log.Add("a look with 6 lamps chosen: " + now());
            lampRows.SelectedIndex = 2;
            look.LampRows = 0;
            look.LampColors = LampColors.AsIs;
            PutLook(look);
            log.Add("a look from before 1.88 chosen with 8 lamps on: " + now());
            log.Add("not applied, so still saved: " + app.Data.Settings.LampRows + " lamps");
            return string.Join("\r\n", log);
        }

        /// <summary>
        /// Test copies only: picking lamp colors of your own: when they can be ticked, that one always stays ticked, that
        /// Apply keeps them, and that a saved look brings them along (one from before 1.93 leaves them alone).
        /// </summary>
        internal string TestLampColors()
        {
            var log = new List<string>();
            Func<string> now = () => "'" + lampColors.SelectedItem + "', ticked: " + string.Join(" ", HuesPicked) + (lampHues[LampHue.Pink].IsEnabled ? "" : " (off)")
                + ", pastel " + (lampPastel.IsChecked == true ? "ticked" : "not ticked") + (lampPastel.IsEnabled ? "" : " (off)");
            log.Add("at first: " + now());
            lampColors.SelectedIndex = Array.IndexOf(LauncherWindow.ColorChoices, LampColors.Picked);
            log.Add("your own colors chosen: " + now());
            lampHues[LampHue.Blue].IsChecked = false;
            log.Add("the only one cleared: " + now());
            lampHues[LampHue.White].IsChecked = true;
            lampHues[LampHue.Gold].IsChecked = true;
            lampHues[LampHue.Blue].IsChecked = false;
            log.Add("gold and white ticked, then blue cleared: " + now());
            Func<string> swatch = () => ((System.Windows.Shapes.Ellipse)((StackPanel)lampHues[LampHue.Red].Content).Children[0]).Fill.ToString();
            string plain = swatch();
            lampPastel.IsChecked = true;
            log.Add("pastel ticked: " + now() + "; red's dot " + plain + " -> " + swatch());
            Apply();
            var st = app.Data.Settings;
            log.Add("after Apply: saved " + st.LampColors + ", " + string.Join(" ", st.LampPicked) + ", pastel " + st.LampPastel);
            var look = LookFromControls();
            log.Add("the look has " + look.LampColors + ", " + string.Join(" ", look.LampPicked) + ", pastel " + look.LampPastel);
            look.LampPicked = new List<LampHue> { LampHue.Purple, LampHue.Cyan };
            look.LampPastel = false;
            PutLook(Themes.Copy(look));
            log.Add("a look with cyan and purple chosen: " + now());
            look.LampColors = LampColors.ChristmasLights;
            look.LampPicked = null;
            PutLook(look);
            log.Add("a look with the Christmas lights of 1.93 chosen: " + now());
            look.LampColors = LampColors.AsIs;
            look.LampPicked = null;
            PutLook(Themes.Copy(look));
            log.Add("a look from before 1.93 chosen: " + now());
            lampColors.SelectedIndex = 0;
            log.Add("the usual colors chosen: " + now());
            lampHues[LampHue.Cyan].IsChecked = false;
            lampHues[LampHue.Purple].IsChecked = false;
            log.Add("all cleared: " + now());
            lampColors.SelectedIndex = Array.IndexOf(LauncherWindow.ColorChoices, LampColors.Picked);
            log.Add("your own chosen again: " + now());
            Func<string> list = () => now() + "; from the wheel: " + string.Join(" ", customs.Select(c => c.Color + (c.On ? "+" : "-"))) + ", shown " + string.Join(" ", customBoxes.Children.OfType<StackPanel>().Select(x =>
                ((TextBlock)((StackPanel)((CheckBox)x.Children[0]).Content).Children[1]).Text + (((CheckBox)x.Children[0]).IsChecked == true ? "+" : "-"))) + (customBoxes.IsEnabled ? "" : " (off)") + (addCustom.IsEnabled ? "" : ", can't add");
            Action<string> wheel = hex => { ColorPickerDialog.TestPick = hex; addCustom.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            Func<int, int, System.Windows.Controls.Primitives.ButtonBase> part = (k, n) => (System.Windows.Controls.Primitives.ButtonBase)((StackPanel)customBoxes.Children[k]).Children[n];
            lampColors.SelectedIndex = 0;
            log.Add("no custom colors: " + list());
            wheel("");
            log.Add("wheel canceled: " + list());
            wheel("#12fa90");
            log.Add("#12fa90 from the wheel: " + list());
            lampHues[LampHue.Blue].IsChecked = false;
            ((CheckBox)part(0, 0)).IsChecked = false;
            log.Add("blue cleared, then the only one cleared: " + list());
            wheel("#12FA90");
            wheel("#FF00AA");
            log.Add("the same again, then #FF00AA: " + list());
            ColorPickerDialog.TestPick = "#3355FF";
            var firstDot = (FrameworkElement)((StackPanel)((CheckBox)((StackPanel)customBoxes.Children[0]).Children[0]).Content).Children[0];
            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
            firstDot.RaiseEvent(down);
            firstDot.RaiseEvent(up);
            log.Add("the first one's dot clicked, #3355FF from the wheel (the press " + (down.Handled ? "kept" : "PASSED ON") + ", the release " + (up.Handled ? "kept" : "PASSED ON") + "): " + list());
            Apply();
            log.Add("after Apply: saved " + string.Join(" ", st.LampCustom.Select(c => c.Color + (c.On ? "+" : "-"))) + ", " + st.LampColors + ", palette " + LampPalette.From(st));
            var mine = LookFromControls();
            DeleteDialog.TestAnswer = false;
            part(1, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            log.Add("the second one's X, then Cancel (asked about " + DeleteDialog.TestAsked + "): " + list());
            DeleteDialog.TestAnswer = true;
            part(1, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            part(0, 1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DeleteDialog.TestAnswer = null;
            log.Add("both deleted: " + list());
            PutLook(Themes.Copy(mine));
            log.Add("the look from before, put back: " + list());
            foreach (var h in new[] { "#000000", "#101010", "#00FF00", "#FFFFFF", "#ABCDEF" }) wheel(h);
            log.Add("five more: " + list());
            lampPlace.SelectedIndex = 0;
            log.Add("no strip: " + list());
            lampPlace.SelectedIndex = (int)LampPanelPlace.BelowLauncher;
            Apply();
            return string.Join("\r\n", log);
        }

        /// <summary>
        /// A theme was chosen: put its picture and colors into every setting it covers, on every tab, so they show here
        /// and are kept with OK or Apply. Choosing None puts your own look back.
        /// </summary>
        void ChooseTheme()
        {
            if (themeListBusy) return;
            string id = SelectedThemeId;
            if (id.Length == 0)
            {
                if (ownLook != null) PutLook(ownLook);
                ownLook = null;
                return;
            }
            if (ownLook == null) ownLook = LookFromControls();   // your own look, to go back to
            var old = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;   // the first time, a built-in theme's picture is drawn
            try
            {
                var look = Themes.LookForId(app.Data.Settings, id);
                if (look != null) PutLook(look);
            }
            catch (Exception ex) { MessageBox.Show(this, "The theme's picture couldn't be made: " + ex.Message, Program.AppName + " settings"); }
            finally { Mouse.OverrideCursor = old; }
        }

        SavedLook LookFromControls()
        {
            return new SavedLook
            {
                BackgroundColor = colorBox.Text.Trim(), BackgroundImage = imageRow.Box.Text.Trim(), BackgroundImageOff = imageRow.Off,
                BackgroundImageTransparency = (int)imageTransparency.Value,
                ButtonsColor = buttonsColorBox.Text.Trim(), VolumeColor = volumeColorBox.Text.Trim(),
                IconTextColor = iconTextColorBox.Text.Trim(), NotesChromeColor = notesChromeColorBox.Text.Trim(),
                SameColorForAll = sameColor.IsChecked == true, AllPartsColor = allPartsColorBox.Text.Trim(),
                NotesBackColor = notesBack.Text.Trim(),
                DockBackgroundColor = dockColorBox.Text.Trim(), DockBackgroundImage = dockImageRow.Box.Text.Trim(),
                DockImageOff = dockImageRow.Off, DockImageTransparency = (int)dockImageTransparency.Value,
                SensorTextColor = sensorText.Text.Trim(), SensorBackColor = sensorBack.Text.Trim(),
                SettingsTheme = theme,
                Full = true,
                WindowTransparency = (int)winTransparency.Value, DockTransparency = (int)dockTransparency.Value,
                Animation = AnimationOrder[Math.Max(0, animation.SelectedIndex)], AnimationSpeed = (int)animationSpeed.Value,
                AnimateNotes = animateNotes.IsChecked == true, AnimateSensors = animateSensors.IsChecked == true,
                AnimateDock = animateDock.IsChecked == true, AnimateVolume = animateVolume.IsChecked == true,
                LampPanel = (LampPanelPlace)Math.Max(0, lampPlace.SelectedIndex), LampMode = (LampActivity)Math.Max(0, lampMode.SelectedIndex),
                LampCustomActivity = LevelsChosen(),
                LampMonitor = (LampMonitorPlace)Math.Max(0, lampMonitor.SelectedIndex), LampRows = LampRowsChosen,
                LampColors = LampColorsChosen, LampPicked = HuesPicked, LampCustom = LampPalette.Tidy(customs), LampPastel = lampPastel.IsChecked == true
            };
        }

        void PutLook(SavedLook l)
        {
            colorBox.Text = l.BackgroundColor ?? "";
            imageRow.Off = l.BackgroundImageOff;
            imageRow.Box.Text = l.BackgroundImage ?? "";
            imageRow.Refresh();
            imageTransparency.Value = l.BackgroundImageTransparency;
            buttonsColorBox.Text = l.ButtonsColor ?? "";
            volumeColorBox.Text = l.VolumeColor ?? "";
            iconTextColorBox.Text = l.IconTextColor ?? "";
            notesChromeColorBox.Text = l.NotesChromeColor ?? "";
            sameColor.IsChecked = l.SameColorForAll;
            allPartsColorBox.Text = l.AllPartsColor ?? "";
            notesBack.Text = l.NotesBackColor ?? "";
            dockColorBox.Text = l.DockBackgroundColor ?? "";
            dockImageRow.Off = l.DockImageOff;
            dockImageRow.Box.Text = l.DockBackgroundImage ?? "";
            dockImageRow.Refresh();
            dockImageTransparency.Value = l.DockImageTransparency;
            sensorText.Text = l.SensorTextColor ?? "";
            sensorBack.Text = l.SensorBackColor ?? "";
            if (l.Full)
            {
                // a whole look (yours, or a theme you saved) also brings see-through, animation and the lamp strip
                winTransparency.Value = l.WindowTransparency;
                dockTransparency.Value = l.DockTransparency;
                animation.SelectedIndex = Math.Max(0, Array.IndexOf(AnimationOrder, l.Animation));
                animationSpeed.Value = l.AnimationSpeed;
                animateNotes.IsChecked = l.AnimateNotes;
                animateSensors.IsChecked = l.AnimateSensors;
                animateDock.IsChecked = l.AnimateDock;
                animateVolume.IsChecked = l.AnimateVolume;
                lampPlace.SelectedIndex = Math.Max(0, (int)l.LampPanel);
                lampMode.SelectedIndex = Math.Max(0, (int)l.LampMode);
                if (l.LampCustomActivity != null) PutLevels(l.LampCustomActivity.Copy("").Tidy());
                lampMonitor.SelectedIndex = Math.Max(0, (int)l.LampMonitor);
                if (l.LampColors != LampColors.AsIs)
                {
                    var colors = l.LampColors;
                    var picked = l.LampPicked;
                    LampPalette.Retire(ref colors, ref picked);
                    picked = LampPalette.Tidy(picked);
                    var wheel = LampPalette.Tidy(l.LampCustom).Where(c => c.On).Select(c => c.Color).ToList();
                    if (picked.Count + wheel.Count > 0)
                    {
                        // The look's colors from the wheel are ticked (ones that aren't on the list join it), any others cleared.
                        foreach (var c in customs) c.On = wheel.Contains(c.Color);
                        foreach (var x in wheel) if (customs.All(c => c.Color != x)) customs.Add(new LampCustomColor { Color = x, On = true });
                        customs = LampPalette.Tidy(customs);
                        FillCustoms();
                        customsChanged();
                    }
                    if (picked.Count + wheel.Count > 0)   // the new ones first, so there's always at least one
                    {
                        foreach (var h in picked) lampHues[h].IsChecked = true;
                        foreach (var h in LauncherWindow.Hues) if (!picked.Contains(h)) lampHues[h].IsChecked = false;
                    }
                    lampPastel.IsChecked = l.LampPastel;
                    lampColors.SelectedIndex = Array.IndexOf(LauncherWindow.ColorChoices, colors);
                }
                if (l.LampRows > 0) lampRows.SelectedIndex = l.LampRows - LampFont.MinRows;
            }
            theme = l.SettingsTheme;
            SettingsTheme.Apply(this, theme);   // this window takes the theme's colors right away
            ShowThemeChoice();
        }

        UIElement BuildQuickLaunch(AppSettings s)
        {
            var g = Page();
            dockSource = Combo((int)s.Dock, "Most frequently used apps", "Most recently installed apps", "Nothing (hide the strip)");
            AddRow(g, "Strip shows", dockSource);
            dockCount = AddSliderRow(g, "Number of apps", s.DockCount, 1, 30, "");
            stripIconSize = Combo(Array.IndexOf(AppSettings.StripSizeOrder, s.StripIconSize), "Same as the launcher's icons", "Tiny", "Extra small", "Small", "Medium", "Large", "Extra large");
            AddRow(g, "Icon size", stripIconSize);
            dockPos = Combo((int)s.DockPos, "Bottom", "Top", "Left side", "Right side");
            AddRow(g, "Position", dockPos);
            dockDetached = Check("Detach the strip from the launcher window (it floats beside it)", s.DockDetached);
            AddRow(g, "", dockDetached);
            dockGap = AddSliderRow(g, "Distance from window", s.DockGap, 0, 200, " px");
            Action dockGapState = () => { ((FrameworkElement)dockGap.Parent).IsEnabled = dockDetached.IsChecked == true; };
            dockDetached.Click += delegate { dockGapState(); };
            dockGapState();
            dockAlign = Combo((int)s.DockAlign, "Left (top when on a side)", "Center", "Right (bottom when on a side)");
            AddRow(g, "Align icons", dockAlign);
            dockDivider = Check("Show the divider line", s.DockDivider);
            AddRow(g, "", dockDivider);
            dockDivider.IsEnabled = !s.DockDetached;   // a floating strip has no divider
            dockDetached.Click += delegate { dockDivider.IsEnabled = dockDetached.IsChecked != true; };
            dockColorBox = AddColorRow(g, "Strip color", s.DockBackgroundColor, true);
            dockImageRow = AddImageRow(g, "Strip picture", s.DockBackgroundImage, s.DockImageOff);
            dockImageTransparency = AddSliderRow(g, "Picture transparency", s.DockImageTransparency, 0, 100, "%");
            dockTransparency = AddSliderRow(g, "Strip transparency", s.DockTransparency, 0, 100, "%");
            return g;
        }

        UIElement BuildNotes(AppSettings s)
        {
            var g = Page();
            notesPos = Combo((int)s.NotesPos, "Off", "On the left of the launcher", "On the right of the launcher");
            AddRow(g, "Notes window", notesPos);
            notesGap = AddSliderRow(g, "Distance from window", s.NotesGap, 0, 200, " px");
            notesWidth = AddSliderRow(g, "Width", s.NotesWidth, 160, 800, " px");
            notesFont = AddSliderRow(g, "Text size", s.NotesFontSize, 10, 28, "");
            notesBack = AddColorRow(g, "Background color", s.NotesBackColor, "Auto", "A light tint of the launcher color behind the text",
                new[] { "#1B1B1B", "#3A3A3A", "#FFFFFF", "#1F3A5F", "#0F5E5E", "#FFF4A3", "#F8D7E3" });
            AddRow(g, "", new TextBlock
            {
                Text = "The notes window opens and closes with the launcher and is as tall as it. What you type is saved automatically. "
                     + "You can also switch it on or off with the notes button at the top of the launcher, and drag its outer edge to make it wider or narrower.\n\n"
                     + "Tabs: click + for a new tab, double-click a tab to rename it, right-click it to move, color or delete it (a deleted tab can be undone for a few seconds). "
                     + "Right-click the text for bold, italic, underline, strikethrough, text size, text color, highlight, title and heading styles, bullets, "
                     + "numbering, checkboxes (ticking one strikes its line through), the date and time (F5) and a color for the tab. Ctrl+F finds words in all tabs.\n\n"
                     + "The background color here is for the area behind the text in every tab that doesn't have its own color; the window keeps the launcher color.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            Action state = () =>
            {
                bool on = notesPos.SelectedIndex != (int)NotesPlacement.Off;
                foreach (var sl in new[] { notesGap, notesWidth, notesFont }) ((FrameworkElement)sl.Parent).IsEnabled = on;
                ((FrameworkElement)notesBack.Parent).IsEnabled = on;
            };
            notesPos.SelectionChanged += delegate { state(); };
            state();
            return g;
        }

        UIElement BuildApps(AppSettings s)
        {
            var g = Page();
            var addAll = new Button { Content = "Add all installed apps...", Padding = new Thickness(12, 3, 12, 3), HorizontalAlignment = HorizontalAlignment.Left };
            addAll.Click += delegate
            {
                app.Launcher.OpenAddAllApps(this);
                // Adding apps can switch scrolling on and labeled groups use free placement; keep this window in step.
                scrollEnabled.IsChecked = app.Data.Settings.ScrollEnabled;
                arrange.SelectedIndex = (int)app.Data.Settings.Arrange;
            };
            var addStack = new StackPanel();
            addStack.Children.Add(addAll);
            addStack.Children.Add(new TextBlock
            {
                Text = "Adds a shortcut for every app on this PC, desktop and Microsoft Store apps alike. "
                     + "First you choose how to group them into folders (by category, company, first letter, type or install date) and how to order them.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            AddRow(g, "Installed apps", addStack);

            autoAdd = Check("Add a shortcut automatically when a new app is installed", s.AutoAddNewApps);
            autoAdd.Margin = new Thickness(0, 18, 0, 0);
            AddRow(g, "New apps", autoAdd);
            autoAddWhere = Combo(s.AutoAddToFolder ? 0 : 1, "Into a folder named \"" + LauncherWindow.NewAppsFolder + "\"", "As icons on the launcher");
            AddRow(g, "Put them", autoAddWhere);
            AddRow(g, "", new TextBlock
            {
                Text = "Works for desktop apps and Microsoft Store apps. Apps installed while the launcher isn't running are added the next time it starts. "
                     + "Apps that are already installed when you switch this on are left alone.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            Action state = () => { autoAddWhere.IsEnabled = autoAdd.IsChecked == true; };
            autoAdd.Click += delegate { state(); };
            state();
            return g;
        }

        // ---------- export / import ----------

        static string Documents { get { return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); } }

        void ExportBackup()
        {
            if (!Apply()) return;   // export what this window shows
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export launcher icons and settings",
                FileName = LauncherApp.BackupFileName(DateTime.Now),
                DefaultExt = ".xml",
                Filter = "Launcher backup (*.xml)|*.xml|All files|*.*",
                InitialDirectory = Documents,
                OverwritePrompt = true
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                app.ExportTo(dlg.FileName);
                MessageBox.Show(this, "Saved " + Backup.Describe(app.Data) + " to:\n\n" + dlg.FileName, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Fail("The backup couldn't be saved.\n\n" + ex.Message);
            }
        }

        void ImportBackup()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import launcher icons and settings",
                Filter = "Launcher backup (*.xml)|*.xml|All files|*.*",
                InitialDirectory = Documents
            };
            if (dlg.ShowDialog(this) != true) return;
            LauncherBackup b;
            try { b = ReadyLauncher.Backup.Read(dlg.FileName); }
            catch (Exception ex)
            {
                Fail("That file couldn't be read as a launcher backup.\n\n" + (ex.InnerException ?? ex).Message);
                return;
            }
            var answer = MessageBox.Show(this,
                "Replace everything in your launcher with this backup?\n\n"
                + "It has " + ReadyLauncher.Backup.Describe(b.Data) + ".\n\n"
                + "Your current setup is kept as \"" + LauncherApp.BeforeImportFile + "\" in\n" + LauncherData.DataFolder + "\nin case you want it back.",
                Program.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;
            try { app.ImportBackup(b); }
            catch (Exception ex)
            {
                Fail("The backup couldn't be imported.\n\n" + ex.Message);
                return;
            }
            Close();   // its controls still show the old settings
        }

        void OnHotkeyKey(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            ModifierKeys m;
            Key k;
            if (!KeySender.TryRead(e, out m, out k) || k == Key.Tab) return;
            if (m == ModifierKeys.None || m == ModifierKeys.Shift)
            {
                hotkeyHint.Text = "Include Ctrl, Alt or Win so the shortcut doesn't clash with typing.";
                return;
            }
            mods = m;
            key = k;
            hotkeyBox.Text = AppSettings.HotkeyText(mods, key);
            hotkeyHint.Text = "The new shortcut is used once you click OK or Apply.";
        }

        bool Fail(string message)
        {
            if (quietFailures) { TestFailure = message; return false; }
            MessageBox.Show(this, message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        TextBlock lampColorsNote, lampActivityNote;
        string colorsKey;   // the colors chosen here, as Settings opened (or was last applied)

        /// <summary>The colors chosen on the Lamp strip tab, in a line: to see whether they've been changed.</summary>
        string LampColorsKey()
        {
            return LampColorsChosen + "|" + string.Join(",", HuesPicked) + "|" + string.Join(",", customs.Select(c => c.Color + (c.On ? "+" : "-")));
        }

        /// <summary>Saved colors or random ones in use (chosen in the strip's menu), said under the colors here.</summary>
        void ShowLampColorsNote(AppSettings s)
        {
            string text = null;
            if (s.LampColorList.Count > 0)
            {
                var set = s.LampColorsAtRandom ? null : s.LampColorSets.FirstOrDefault(o => o.List().Count == s.LampColorList.Count && !o.List().Except(s.LampColorList).Any());
                text = "The lamps have " + (s.LampColorsAtRandom ? "colors picked at random" : set != null ? "your saved colors \u201c" + set.Name + "\u201d" : "saved colors")
                    + " now, chosen in the strip's right-click menu (Activity & Colors). Choosing colors here puts these in their place.";
            }
            if (s.LampRandomColors != LampRandomColors.None)
                text = (text == null ? "" : text + " ") + "New colors are picked at random each time the launcher opens, until colors are chosen here or in that menu.";
            var own = new List<string>();
            if (s.LampStripColors.Count > 0) own.Add("the strip");
            if (s.LampMonitorColors.Count > 0) own.Add("the monitor's lamps");
            if (s.LampMiniColors.Count > 0) own.Add("its mini lamps");
            var thirdsOwn = new[] { "left", "middle", "right" }.Where((w, k) => (k == 0 ? s.LampLeftColors : k == 1 ? s.LampMiddleColors : s.LampRightColors).Count > 0).ToList();
            if (thirdsOwn.Count > 0) own.Add("the strip's " + string.Join(" and ", thirdsOwn) + (thirdsOwn.Count == 1 ? " third" : " thirds"));
            if (own.Count > 0)
                text = (text == null ? "" : text + " ") + "Colors of their own (chosen by right-clicking a color in that menu): "
                    + (own.Count == 1 ? own[0] : string.Join(", ", own.Take(own.Count - 1)) + " and " + own[own.Count - 1]) + ".";
            lampColorsNote.Text = text ?? "";
            lampColorsNote.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Random activity, when it's on (in the strip's menu), said under the activity here.</summary>
        void ShowLampActivityNote(AppSettings s)
        {
            var parts = new List<string>();
            if ((s.LampActivityParts & LampActivityParts.Strip) != 0) parts.Add("the strip's lamps");
            if ((s.LampActivityParts & LampActivityParts.Monitor) != 0) parts.Add("the monitor's lamps");
            if ((s.LampActivityParts & LampActivityParts.Red) != 0) parts.Add("its mini lamps");
            string which = parts.Count <= 1 ? string.Join("", parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
            lampActivityNote.Text = s.LampRandomActivity
                ? "Random activity is on (in the strip's right-click menu, Activity & Colors): each time the launcher opens, " + which
                  + " get a speed and a number lit, picked at random. Choosing another activity here switches it off."
                : "";
            lampActivityNote.Visibility = s.LampRandomActivity ? Visibility.Visible : Visibility.Collapsed;
        }

        bool closingAfterOk, quietFailures;
        /// <summary>Test copies only: why the test's OK couldn't apply the settings (no message is shown then).</summary>
        internal string TestFailure;
        /// <summary>Whether Windows took Settings out of sight at once, with nothing fading, as it went.</summary>
        internal bool WentAtOnce;
        /// <summary>Test copies only: told when Settings goes out of sight.</summary>
        internal Action Gone;

        /// <summary>
        /// OK: Settings goes out of sight at once, and only then is the launcher changed (its windows made again in the
        /// new colors, which come up in front of everything); then Settings closes. Changed while Settings was still on
        /// screen, the launcher's windows came up over it, part by part, and over Windows' fading copy of it as it closed.
        /// </summary>
        void ApplyAndClose()
        {
            if (closingAfterOk) return;
            if (!Apply(true)) return;   // checked first, so a setting that can't be used is said with Settings still there
            closingAfterOk = true;
            GoNow();
            Apply();
            Close();
        }

        /// <summary>Out of sight at once: no fade by Windows (it's kept out of sight by Windows' compositor, then hidden).</summary>
        void GoNow()
        {
            if (wentOut) return;
            wentOut = true;
            var h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            bool noFade = Native.NoTransitions(h);
            WentAtOnce = Native.Cloak(h, true) || noFade;
            if (IsVisible) Hide();
            if (Gone != null) Gone();
        }

        bool wentOut;

        /// <summary>Test copies only: OK, as if clicked; a change that can't be applied is noted, not shown in a message.</summary>
        internal void TestOk()
        {
            quietFailures = true;
            try { ApplyAndClose(); }
            finally { quietFailures = false; }
        }

        /// <summary>Use the settings as they are now in this window; with <paramref name="checkOnly"/>, only see that they can be.</summary>
        bool Apply(bool checkOnly = false)
        {
            var s = app.Data.Settings;
            string hardwareProblem = HardwareProblem();
            if (hardwareProblem != null) return Fail(hardwareProblem);
            if (notesBack.Text.Trim().Length > 0 && !ValidColor(notesBack.Text))
                return Fail("The notes background color should look like #202020, or click Auto.");
            string color = colorBox.Text.Trim();
            string dockColor = dockColorBox.Text.Trim();
            if (!ValidColor(color)) return Fail("The background color should look like #B8A600.");
            if (dockColor.Length > 0 && !ValidColor(dockColor)) return Fail("The strip color should look like #202020, or click None.");
            var partBoxes = new[] { buttonsColorBox, volumeColorBox, iconTextColorBox, notesChromeColorBox, allPartsColorBox };
            var partNames = new[] { "top buttons", "volume control", "icon names", "notes title and tabs", "color for all four" };
            for (int i = 0; i < partBoxes.Length; i++)
                if (partBoxes[i].Text.Trim().Length > 0 && !ValidColor(partBoxes[i].Text))
                    return Fail("The " + partNames[i] + " color should look like #FFFFFF, or click Auto.");
            int backupDays;
            bool backupDaysOk = int.TryParse(autoBackupDays.Text.Trim(), out backupDays) && backupDays >= 1 && backupDays <= 365;
            string backupFolder = autoBackupFolder.Text.Trim();
            if (autoBackup.IsChecked == true)
            {
                if (!backupDaysOk) return Fail("Back up automatically every 1 to 365 days.");
                string folderProblem = BackupFolderProblem(backupFolder);
                if (folderProblem != null) return Fail(folderProblem);
            }

            var sizeMode = (WindowSizeMode)windowSize.SelectedIndex;
            int cw = s.CustomWidthPx, ch = s.CustomHeightPx;
            if (sizeMode == WindowSizeMode.Custom)
            {
                if (!int.TryParse(customW.Text.Trim(), out cw) || !int.TryParse(customH.Text.Trim(), out ch) || cw < 400 || ch < 300 || cw > 7680 || ch > 4320)
                    return Fail("Enter a custom size between 400 x 300 and 7680 x 4320 pixels.");
            }
            if (checkOnly) return true;

            bool resize = sizeMode != s.WindowSize || (sizeMode == WindowSizeMode.Custom && (cw != s.CustomWidthPx || ch != s.CustomHeightPx));

            var newArrange = ArrangeOrder[Math.Max(0, arrange.SelectedIndex)];
            if (newArrange != s.Arrange) app.Launcher.SetArrange(newArrange);
            app.Launcher.ChangeIconMetrics(AppSettings.IconSizeOrder[Math.Max(0, iconSize.SelectedIndex)], showLabels.IsChecked == true);

            s.HotkeyModifiers = mods;
            s.HotkeyKey = key;
            s.WindowSize = sizeMode;
            s.CustomWidthPx = cw;
            s.CustomHeightPx = ch;
            s.Animation = AnimationOrder[Math.Max(0, animation.SelectedIndex)];
            s.AnimationSpeed = (int)animationSpeed.Value;
            s.AnimateNotes = animateNotes.IsChecked == true;
            s.AnimateSensors = animateSensors.IsChecked == true;
            s.AnimateDock = animateDock.IsChecked == true;
            s.AnimateVolume = animateVolume.IsChecked == true;
            s.OpenWithDoubleClick = doubleClick.IsChecked == true;
            s.Volume = (VolumePlacement)volume.SelectedIndex;
            s.VolumeGap = (int)volumeGap.Value;
            s.VolumeStyle = volumeButtonsOn.IsChecked == true ? VolumeStyleMode.ButtonsCustom : VolumeStyleMode.Slider;
            s.VolumeButtonCount = (int)volumeButtonCount.Value;
            s.VolumeSliderToo = volumeHideSlider.IsChecked != true;
            s.VolumeButtonHeight = (VolumeButtonSize)Math.Max(0, volumeButtonHeight.SelectedIndex);
            s.VolumeCloseAfter = volumeCloseAfter.IsChecked == true;
            s.DockDetached = dockDetached.IsChecked == true;
            s.DockGap = (int)dockGap.Value;
            s.HideAfterLaunch = hideAfter.IsChecked == true;
            s.HideOnDeactivate = hideDeact.IsChecked == true;

            s.BackgroundColor = color;
            s.WindowTransparency = (int)winTransparency.Value;
            s.BackgroundImage = imageRow.Box.Text.Trim();
            s.BackgroundImageOff = imageRow.Off && s.BackgroundImage.Length > 0;   // switched off, but still chosen
            s.BackgroundImageTransparency = (int)imageTransparency.Value;
            s.ButtonsColor = buttonsColorBox.Text.Trim();
            s.VolumeColor = volumeColorBox.Text.Trim();
            s.IconTextColor = iconTextColorBox.Text.Trim();
            s.NotesChromeColor = notesChromeColorBox.Text.Trim();
            s.SameColorForAll = sameColor.IsChecked == true;
            s.AllPartsColor = allPartsColorBox.Text.Trim();
            s.Theme = SelectedThemeId;
            s.OwnLook = s.Theme.Length > 0 ? ownLook : null;
            s.LampPanel = (LampPanelPlace)Math.Max(0, lampPlace.SelectedIndex);
            s.LampMode = (LampActivity)Math.Max(0, lampMode.SelectedIndex);
            if (s.LampMode != LampActivity.Custom) s.LampRandomActivity = false;   // another activity chosen: it stays
            s.LampCustomActivity = LevelsChosen();
            s.LampActivities = LampActivityLevels.TidyList(activities);
            s.LampMonitor = (LampMonitorPlace)Math.Max(0, lampMonitor.SelectedIndex);
            s.LampEightBall = lampEightBall.IsChecked == true;
            s.LampEightBallInstant = lampEightBallInstant.IsChecked == true;
            s.LampMonitorForBall = s.LampEightBall && monitorForBall;
            string colorsNow = LampColorsKey();
            if (colorsKey != null && colorsNow != colorsKey)   // colors chosen here: saved or random ones from the strip's menu go
            {
                s.LampColorList.Clear();
                s.LampColorsAtRandom = false;
                s.LampRandomColors = LampRandomColors.None;
            }
            colorsKey = colorsNow;
            s.LampColors = LampColorsChosen;
            s.LampPicked = HuesPicked;
            s.LampCustom = LampPalette.Tidy(customs);
            s.LampPastel = lampPastel.IsChecked == true;
            s.LampRows = LampRowsChosen;
            s.LampText = lampText.Text.Trim();
            s.LampTextMode = s.LampText.Length == 0 ? LampTextEffect.None : (LampTextEffect)Math.Max(0, lampTextMode.SelectedIndex);
            s.LampTextSides = lampTextSides.IsChecked == true;
            s.LampTextTails = lampTextTails.IsChecked == true;
            s.LampTailRows = tailRowsWanted;
            s.LampScrollSpeed = (int)lampScrollSpeed.Value;
            if (NumberPattern.IsPattern(lampNumbers.Text)) s.LampNumbers = NumberPattern.Tidy(lampNumbers.Text);   // (one that isn't a pattern isn't kept)
            var hadPatterns = s.LampNumberPatterns;
            s.LampNumberPatterns = LauncherWindow.TidyPatterns(patterns);
            foreach (var p in s.LampNumberPatterns)   // clicks on their buttons in the strip's menu while this was open count too
            {
                var had = hadPatterns.FirstOrDefault(o => o.Pattern == p.Pattern);
                if (had != null) p.Clicks = Math.Max(p.Clicks, had.Clicks);
            }
            AddReminderFromFields();   // text typed for a new reminder but not added yet is kept too
            s.Reminders = reminders.Select(r => new LampReminder { At = r.At, Text = r.Text }).ToList();

            s.Dock = (DockSource)dockSource.SelectedIndex;
            s.DockCount = (int)dockCount.Value;
            s.DockPos = (DockPosition)dockPos.SelectedIndex;
            s.DockAlign = (DockAlignment)dockAlign.SelectedIndex;
            s.DockDivider = dockDivider.IsChecked == true;
            s.DockBackgroundColor = dockColor;
            s.DockBackgroundImage = dockImageRow.Box.Text.Trim();
            s.DockImageOff = dockImageRow.Off && s.DockBackgroundImage.Length > 0;
            s.DockImageTransparency = (int)dockImageTransparency.Value;
            s.DockTransparency = (int)dockTransparency.Value;

            s.ScrollEnabled = scrollEnabled.IsChecked == true;
            s.NotesPos = (NotesPlacement)notesPos.SelectedIndex;
            s.NotesGap = (int)notesGap.Value;
            s.NotesWidth = (int)notesWidth.Value;
            s.NotesFontSize = (int)notesFont.Value;
            s.NotesBackColor = notesBack.Text.Trim();

            s.VolumeSize = (VolumeSizeMode)volumeSize.SelectedIndex;
            s.StripIconSize = AppSettings.StripSizeOrder[Math.Max(0, stripIconSize.SelectedIndex)];
            bool autoOn = autoAdd.IsChecked == true;
            if (autoOn && !s.AutoAddNewApps) app.Data.SeenApps.Clear();   // apps installed before now aren't "new"
            s.AutoAddNewApps = autoOn;
            s.AutoAddToFolder = autoAddWhere.SelectedIndex == 0;
            s.SettingsTheme = theme;
            s.AutoBackup = autoBackup.IsChecked == true;
            if (backupDaysOk) s.AutoBackupDays = backupDays;
            if (BackupFolderProblem(backupFolder) == null)
                s.AutoBackupFolder = string.Equals(backupFolder.TrimEnd('\\'), AppSettings.DefaultBackupFolder, StringComparison.OrdinalIgnoreCase) ? "" : backupFolder;
            ApplyHardware(s);

            ShowLampColorsNote(s);
            ShowLampActivityNote(s);
            app.SettingsChanged(startup.IsChecked == true, resize);
            Themes.CleanUpPictures(s);   // copies made for your own themes that nothing uses any more
            if (s.AutoBackup)
            {
                app.AutoBackupIfDue(false);   // the first one (or one that's due) right away
                UpdateAutoBackupStatus();
            }
            return true;
        }
    }
}
