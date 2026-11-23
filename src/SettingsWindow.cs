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

        public void ShowTab(int index)
        {
            if (index >= 0 && index < tabs.Items.Count) tabs.SelectedIndex = index;
        }

        /// <summary>Scrolls the Appearance tab to the lamp strip's reminder.</summary>
        public void ShowReminder()
        {
            Dispatcher.BeginInvoke(new Action(() => { if (addReminder != null) addReminder.BringIntoView(); }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // General
        TextBox hotkeyBox, customW, customH;
        TextBlock hotkeyHint;
        ComboBox windowSize, animation, volume, volumeButtonHeight, lampPlace, lampMode, lampMonitor, lampTextMode, themeBox;
        TextBox lampText, reminderText;
        CheckBox lampTextSides;
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

            // The theme: colors now, the title bar once the window exists, and again whenever Windows switches
            // between light and dark (for "Follow system").
            theme = s.SettingsTheme;
            SettingsTheme.Apply(this, theme);
            SourceInitialized += delegate { SettingsTheme.Apply(this, theme); };
            Microsoft.Win32.UserPreferenceChangedEventHandler systemChanged = (o, e) =>
                Dispatcher.BeginInvoke(new Action(() => { if (!closed && theme == SettingsThemeMode.FollowSystem) SettingsTheme.Apply(this, theme); }));
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += systemChanged;
            Closed += delegate { closed = true; Microsoft.Win32.SystemEvents.UserPreferenceChanged -= systemChanged; };

            tabs = new TabControl { Margin = new Thickness(12, 12, 12, 0) };
            tabs.Items.Add(new TabItem { Header = "General", Content = BuildGeneral(s) });
            tabs.Items.Add(new TabItem { Header = "Appearance", Content = BuildAppearance(s) });
            tabs.Items.Add(new TabItem { Header = "Quick launch strip", Content = BuildQuickLaunch(s) });
            tabs.Items.Add(new TabItem { Header = "Notes", Content = BuildNotes(s) });
            tabs.Items.Add(new TabItem { Header = "Apps", Content = BuildApps(s) });
            tabs.Items.Add(new TabItem { Header = "Hardware monitoring", Content = BuildHardware(s) });
            RestoreIfThemeGone(s);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
            var ok = new Button { Content = "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4) };
            ok.Click += delegate { if (Apply()) Close(); };
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

            AddRow(g, "", new TextBlock
            {
                Text = "A strip of blinking red and amber computer lamps, like the front of an early-1980s mainframe.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 14, 0, 4), TextWrapping = TextWrapping.Wrap
            });
            lampPlace = Combo((int)s.LampPanel, "Off", "Along the top (inside, buttons below it)", "Along the bottom (inside)",
                              "Above the launcher (outside)", "Below the launcher (outside)",
                              "At the bottom of the quick launch strip", "Behind the quick launch strip's icons");
            AddRow(g, "Lamp strip", lampPlace);
            lampMode = Combo((int)s.LampMode, "Idle (slow, few lamps lit)", "Computing", "Alert (busier, with a sweep of light)", LauncherWindow.RandomLampsText);
            AddRow(g, "Lamp activity", lampMode);
            lampMonitor = Combo((int)s.LampMonitor, "None", "At the left end of the strip", "At the right end of the strip");
            lampMonitor.ToolTip = "A computer cabinet lettered WOPR, with a few lamps blinking in its window, standing at one end of the strip and rising above it. It shows when the strip is above or below the launcher (outside).";
            AddRow(g, "Monitor", lampMonitor);
            lampTextMode = Combo((int)s.LampTextMode, "None (the lamps just blink)", "Scroll it across the strip", "Flash it on the strip");
            lampTextMode.ToolTip = "Spells out your text in lamps on the strip, instead of blinking.";
            AddRow(g, "Lamp text", lampTextMode);
            lampText = new TextBox { Text = s.LampText ?? "", Width = 250, MaxLength = 200, Padding = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Left,
                                     ToolTip = "Letters, numbers and common punctuation (small letters show as capitals)" };
            AddRow(g, "Text to show", lampText);
            lampTextSides = Check("Keep the lamps blinking on either side of the text", s.LampTextSides);
            AddRow(g, "", lampTextSides);

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
                lampMode.IsEnabled = strip;
                lampTextMode.IsEnabled = lampText.IsEnabled = lampTextSides.IsEnabled = strip;
                lampMonitor.IsEnabled = lampPlace.SelectedIndex == (int)LampPanelPlace.AboveLauncher || lampPlace.SelectedIndex == (int)LampPanelPlace.BelowLauncher;
                reminderList.IsEnabled = reminderMonth.IsEnabled = reminderDay.IsEnabled = reminderYear.IsEnabled = reminderTime.IsEnabled = strip;
                reminderText.IsEnabled = addReminder.IsEnabled = strip;
            };
            lampPlace.SelectionChanged += delegate { lampState(); };
            lampState();
            return g;
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
                LampMonitor = (LampMonitorPlace)Math.Max(0, lampMonitor.SelectedIndex)
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
                lampMonitor.SelectedIndex = Math.Max(0, (int)l.LampMonitor);
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
            MessageBox.Show(this, message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        bool Apply()
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
            s.LampMonitor = (LampMonitorPlace)Math.Max(0, lampMonitor.SelectedIndex);
            s.LampText = lampText.Text.Trim();
            s.LampTextMode = s.LampText.Length == 0 ? LampTextEffect.None : (LampTextEffect)Math.Max(0, lampTextMode.SelectedIndex);
            s.LampTextSides = lampTextSides.IsChecked == true;
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
