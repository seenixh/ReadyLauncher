using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>The "Hardware monitoring" tab: which live readings to show above the launcher, and how.</summary>
    internal partial class SettingsWindow
    {
        ComboBox sensorAlign, sensorSize, sensorFill, sensorStyle, sensorGraph, sensorTileText;
        TextBlock graphSpan;
        CheckBox sensorWarnings;
        List<SensorAlert> alerts = new List<SensorAlert>();   // the warning levels being edited here (kept with OK or Apply)
        List<string> sensorOrder = new List<string>();
        static readonly int[] GraphLengths = { 30, 60, 120, 240 };
        TextBox sensorEvery, pingHost, sensorText, sensorBack;
        CheckBox externalSensors;
        TextBlock monitorStatus;
        StackPanel windowsList, hwinfoList, libreList;
        TabItem hwinfoTab, libreTab;
        readonly List<Tuple<CheckBox, SensorInfo>> sensorChecks = new List<Tuple<CheckBox, SensorInfo>>();
        // HWiNFO / Libre Hardware Monitor sensors found so far (kept, in order, while that app is closed).
        readonly List<KnownSensor> known = new List<KnownSensor>();
        string shownSources = "";   // which apps were sharing when the list was last built, e.g. "H" or "HL"
        DispatcherTimer sourceWatch;
        bool sourceCheckRunning;
        Button pawnRemove;
        TextBlock pawnStatus;
        bool pawnBusy, pawnChecking, pawnListed;

        UIElement BuildHardware(AppSettings s)
        {
            known.AddRange(s.KnownSensors);
            alerts = s.SensorAlerts.Select(a => new SensorAlert { Id = a.Id, Warning = a.Warning, Critical = a.Critical, LowIsBad = a.LowIsBad }).ToList();
            sensorOrder = s.Sensors.ToList();
            var g = Page();
            AddRow(g, "", new TextBlock
            {
                Text = "Pick the readings to show in a bar just above the launcher. They update live while the launcher is open, "
                     + "and you can drag them on the bar to change their order.",
                TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Margin = new Thickness(0, 2, 0, 4)
            });

            sensorAlign = Combo((int)s.SensorAlign, "Left", "Center", "Right");
            AddRow(g, "Line them up", sensorAlign);
            sensorFill = Combo(s.SensorFillBottom ? 1 : 0, "Top row first (extra rows go below it)", "Bottom row first, next to the launcher (extra rows go above it)");
            AddRow(g, "Fill rows", sensorFill);
            sensorSize = Combo(s.SensorSize, "Small", "Medium", "Large");
            AddRow(g, "Size", sensorSize);
            sensorStyle = Combo(s.SensorTiles ? 1 : 0, "Readings (name and value)", "Tiles with mini graphs");
            AddRow(g, "Show them as", sensorStyle);
            int lengthAt = Array.IndexOf(GraphLengths, s.SensorGraphLength);
            if (lengthAt < 0) lengthAt = GraphLengths.Count(n => n < s.SensorGraphLength);   // a hand-edited length: the next one up
            sensorGraph = Combo(lengthAt, GraphLengths.Select(n => "Last " + n + " readings").ToArray());
            graphSpan = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Opacity = 0.8 };
            var graphRow = new StackPanel { Orientation = Orientation.Horizontal };
            graphRow.Children.Add(sensorGraph);
            graphRow.Children.Add(graphSpan);
            AddRow(g, "Graph length", graphRow);
            sensorTileText = Combo(s.SensorTilesCentered ? 1 : 0, "Top left", "Centered (across, and up and down)");
            AddRow(g, "Tile text", sensorTileText);
            sensorWarnings = Check("", s.SensorWarnings);
            sensorWarnings.Content = new TextBlock { Text = "Color readings amber or red when they reach their warning levels", TextWrapping = TextWrapping.Wrap };
            var levelsButton = new Button
            {
                Content = "Warning levels...", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Set the warning and critical level of each chosen sensor, or go back to the built-in levels"
            };
            levelsButton.Click += delegate { EditLevels(); };
            var warnPanel = new StackPanel();
            warnPanel.Children.Add(sensorWarnings);
            warnPanel.Children.Add(levelsButton);
            AddRow(g, "Warning colors", warnPanel);
            sensorStyle.SelectionChanged += delegate { ShowGraphSpan(); };
            sensorGraph.SelectionChanged += delegate { ShowGraphSpan(); };

            var every = new StackPanel { Orientation = Orientation.Horizontal };
            every.Children.Add(new TextBlock { Text = "Every", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            sensorEvery = new TextBox { Text = s.SensorInterval.ToString("0.##"), Width = 60, Padding = new Thickness(3), ToolTip = "Any number of seconds from 0.5 to 3600, e.g. 5 or 2.5" };
            every.Children.Add(sensorEvery);
            every.Children.Add(new TextBlock { Text = "seconds", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
            AddRow(g, "Check sensors", every);
            sensorEvery.TextChanged += delegate { ShowGraphSpan(); };
            ShowGraphSpan();

            sensorText = AddColorRow(g, "Text color", s.SensorTextColor, "Auto", "Automatic: whatever reads well on the background", new[] { "#FFFFFF", "#000000", "#FFB900", "#4A9EFF", "#3CCB6A", "#FF5C5C", "#B8A600" });
            sensorBack = AddColorRow(g, "Background color", s.SensorBackColor, "Auto", "The same color as the launcher", null);

            externalSensors = Check("", s.ExternalSensors);
            externalSensors.Content = new TextBlock { Text = "Always look for HWiNFO and Libre Hardware Monitor, and add their sensors when they're found", TextWrapping = TextWrapping.Wrap };
            externalSensors.Margin = new Thickness(0, 8, 0, 0);
            externalSensors.Click += delegate { FillSensorList(CheckedSensors()); };
            AddRow(g, "", externalSensors);

            // The sensor picker: one tab per provider, plus where to get more sensors.
            pingHost = new TextBox { Text = s.PingHost, Width = 170, Padding = new Thickness(3), ToolTip = "A website or server to measure the ping time to, e.g. 1.1.1.1 or www.google.com" };
            windowsList = new StackPanel();
            hwinfoList = new StackPanel();
            libreList = new StackPanel();
            var picker = new TabControl { Height = 270 };
            picker.Items.Add(new TabItem { Header = "Windows", Content = ListScroller(windowsList) });
            hwinfoTab = new TabItem { Header = "HWiNFO", Content = ListScroller(hwinfoList) };
            libreTab = new TabItem { Header = "Libre Hardware Monitor", Content = ListScroller(libreList) };
            picker.Items.Add(hwinfoTab);
            picker.Items.Add(libreTab);
            picker.Items.Add(new TabItem { Header = "Sensor Information", Content = new ScrollViewer { Content = BuildMoreSensors(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 6, 8, 6) } });
            FillSensorList(new HashSet<string>(s.Sensors));

            var none = new Button { Content = "Show none", Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            none.Click += delegate { foreach (var c in sensorChecks) c.Item1.IsChecked = false; };
            var pickStack = new StackPanel();
            pickStack.Children.Add(picker);
            pickStack.Children.Add(none);
            AddRow(g, "Sensors", pickStack);

            // The PawnIO driver (Libre Hardware Monitor installs it): whether it's on this PC, and a button to remove it completely.
            pawnRemove = new Button
            {
                Content = "Remove PawnIO driver", Padding = new Thickness(10, 2, 10, 2), IsEnabled = false, VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Completely removes the PawnIO driver from this PC. Windows asks for permission first."
            };
            ToolTipService.SetShowOnDisabled(pawnRemove, true);
            pawnRemove.Click += delegate { RemovePawnIo(); };
            pawnStatus = new TextBlock { Text = "Checking for the PawnIO driver...", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var pawnRow = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            DockPanel.SetDock(pawnRemove, Dock.Left);
            pawnRow.Children.Add(pawnRemove);
            pawnRow.Children.Add(pawnStatus);
            AddRow(g, "PawnIO driver", pawnRow);
            ((TextBlock)g.Children[g.Children.Count - 2]).Margin = new Thickness(0, 16, 16, 0);
            RefreshPawnIo(null);

            // Notice HWiNFO / Libre Hardware Monitor starting, stopping or switching sharing on while this window is open,
            // and PawnIO being installed or removed.
            sourceWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            sourceWatch.Tick += delegate { WatchSources(); WatchPawnIo(); };
            sourceWatch.Start();
            Closed += delegate { sourceWatch.Stop(); };
            return g;
        }

        /// <summary>The graph length only matters for tiles; next to it, how much time it covers at the chosen check rate.</summary>
        void ShowGraphSpan()
        {
            if (sensorGraph == null || graphSpan == null || sensorEvery == null) return;
            bool on = sensorStyle.SelectedIndex == 1;
            sensorGraph.IsEnabled = on;
            if (sensorTileText != null) sensorTileText.IsEnabled = on;
            graphSpan.Opacity = on ? 0.8 : 0.4;
            double secs;
            if (!ParseSeconds(sensorEvery.Text, out secs)) { graphSpan.Text = ""; return; }
            double total = secs * GraphLengths[Math.Max(0, sensorGraph.SelectedIndex)];
            graphSpan.Text = "about " + (total < 120 ? Math.Round(total) + " seconds"
                : total < 7200 ? Math.Round(total / 60) + " minutes" : (total / 3600).ToString("0.#") + " hours") + " at the current check rate";
        }

        /// <summary>The warning levels of every ticked sensor that measures a number, in the bar's order.</summary>
        void EditLevels()
        {
            var readings = app.Launcher != null ? app.Launcher.SensorReadingsNow() : new List<SensorReading>();
            var ticked = sensorChecks.Where(c => c.Item1.IsChecked == true).Select(c => c.Item2).GroupBy(i => i.Id).Select(x => x.First())
                .OrderBy(i => { int k = sensorOrder.IndexOf(i.Id); return k < 0 ? int.MaxValue : k; }).ToList();
            var rows = new List<LevelRow>();
            foreach (var info in ticked)
            {
                var r = readings.FirstOrDefault(x => x.Info.Id == info.Id);
                var row = SensorLevels.Row(alerts, info, r);
                if (row != null) rows.Add(row);
            }
            if (rows.Count == 0)
            {
                Say("None of the chosen sensors measures a number, so there are no warning levels to set. Tick a sensor like CPU load, ping or a temperature first.",
                    "Warning levels", MessageBoxButton.OK);
                return;
            }
            var chosen = WarningLevelsWindow.Edit(this, app, theme, rows, sensorWarnings.IsChecked == true);
            if (chosen == null) return;
            foreach (var row in rows) SensorLevels.Keep(alerts, row, chosen[row.Info.Id]);
        }

        static ScrollViewer ListScroller(UIElement content)
        {
            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 6, 6, 6) };
        }

        UIElement BuildMoreSensors()
        {
            var more = new StackPanel();
            more.Children.Add(Para("Windows doesn't share CPU temperature, fan speeds, voltages or motherboard temperatures with other apps. "
                                 + "To get more sensors like these, you can choose from one or both of the following options listed below.", 0));

            var pawnWarning = new TextBlock { TextWrapping = TextWrapping.Wrap };
            pawnWarning.Inlines.Add(new Run("*WARNING:") { FontWeight = FontWeights.Bold });
            pawnWarning.Inlines.Add(new Run("  Installing the PawnIO DRIVER for Libre Hardware Monitor: if you play online games, some strict "
                                          + "anti-cheat systems, such as FACEIT, have flagged PawnIO before and blocked users' PCs from playing."));
            more.Children.Add(WarnBox(pawnWarning));

            more.Children.Add(Heading("Install Libre Hardware Monitor Software"));
            more.Children.Add(Para("Download and install the Libre Hardware Monitor software from its official site. The following needs to be done "
                                 + "for the software to communicate with the launcher app.", 2));
            more.Children.Add(Step(1, Para("Installation of the *PawnIO driver. Libre Hardware Monitor will prompt to automatically install this driver.", 0)));
            more.Children.Add(Step(2, Para("Libre Hardware Monitor must be running in order for the launcher app to retrieve all sensor data.", 0)));
            more.Children.Add(Step(3, Para("Enable Libre Hardware Monitor's webserver by opening the Libre app, click \"Options\", mouse over "
                                         + "\"Remote Web Server\" and select \"Run\"", 0)));

            more.Children.Add(Heading("Install HWiNFO Monitoring Software (requires payment after free use expires)"));
            more.Children.Add(Para("Download and install the HWiNFO monitoring software from its official site. The following needs to be done "
                                 + "for the software to communicate with the launcher app.", 2));
            more.Children.Add(Step(1, Para("HWiNFO monitoring software must be running in order for the launcher app to retrieve all sensor data.", 0)));
            var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            note.Inlines.Add(new Run("NOTE:") { FontWeight = FontWeights.Bold });
            note.Inlines.Add(new Run(" This feature is only free the first 12 hours of the app's usage, then you need to pay for the upgraded tier "
                                   + "to get full support."));
            more.Children.Add(Step(2,
                Para("In order for HWiNFO to communicate with the launcher, you need to enable the \"Shared Memory Support\" option.", 0),
                Para("Open the HWiNFO app, DO NOT CLICK START YET, click the \"Settings\" button. Under the \"General/User Interface\" tab, "
                   + "look for the \"Shared Memory Support\" option and tick it to enable.", 4),
                note));

            var check = new Button { Content = "Check for HWiNFO and Libre Hardware Monitor", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            monitorStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), FontWeight = FontWeights.SemiBold };
            check.Click += delegate
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                try
                {
                    monitorStatus.Text = MonitorApps.Check();
                    FillSensorList(CheckedSensors());   // show anything newly found
                }
                finally { System.Windows.Input.Mouse.OverrideCursor = null; }
            };
            more.Children.Add(check);
            more.Children.Add(monitorStatus);

            var links = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            links.Inlines.Add(new Run("Official downloads:") { FontWeight = FontWeights.SemiBold });
            links.Inlines.Add(new LineBreak());
            links.Inlines.Add(new Run("HWiNFO:  "));
            links.Inlines.Add(Link(MonitorApps.HwinfoUrl));
            links.Inlines.Add(new LineBreak());
            links.Inlines.Add(new Run("Libre Hardware Monitor:  "));
            links.Inlines.Add(Link(MonitorApps.LibreUrl));
            more.Children.Add(links);

            var warning = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = "⚠  Always download apps only from their official sites, like the links above. Copies on other download sites "
                     + "can be out of date, or bundled with adware or malware."
            };
            more.Children.Add(WarnBox(warning));
            return more;
        }

        static TextBlock Para(string text, double top)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, 0) };
        }

        static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 0) };
        }

        /// <summary>A numbered step with a hanging indent; extra lines sit under the step's text.</summary>
        static UIElement Step(int number, params UIElement[] lines)
        {
            var g = new Grid { Margin = new Thickness(6, 5, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.Children.Add(new TextBlock { Text = number + "." });
            var body = new StackPanel();
            foreach (var line in lines) body.Children.Add(line);
            Grid.SetColumn(body, 1);
            g.Children.Add(body);
            return g;
        }

        /// <summary>A warning in a tinted box; its colors follow the Settings theme.</summary>
        static Border WarnBox(TextBlock text)
        {
            text.SetResourceReference(TextBlock.ForegroundProperty, "T.WarnText");
            var box = new Border
            {
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 8, 0, 0),
                Child = text
            };
            box.SetResourceReference(Border.BackgroundProperty, "T.WarnBack");
            box.SetResourceReference(Border.BorderBrushProperty, "T.WarnBorder");
            return box;
        }

        /// <summary>Checks for the PawnIO driver (off the UI thread) and shows the result; message replaces the usual note if it's still here.</summary>
        void RefreshPawnIo(string message)
        {
            pawnChecking = true;
            Task.Factory.StartNew(() =>
            {
                string version;
                var state = PawnIo.Check(out version);
                return Tuple.Create(state, version, PawnIo.QuickLook());
            }).ContinueWith(t =>
            {
                pawnChecking = false;
                if (t.IsFaulted || pawnBusy) return;
                var state = t.Result.Item1;
                pawnListed = t.Result.Item3;
                pawnRemove.IsEnabled = state == PawnIo.State.Installed;
                pawnStatus.ToolTip = null;
                if (state == PawnIo.State.Installed)
                {
                    pawnStatus.Text = message ?? "PawnIO driver installed";
                    if (!string.IsNullOrEmpty(t.Result.Item2)) pawnStatus.ToolTip = "Version " + t.Result.Item2;
                }
                else if (state == PawnIo.State.RestartNeeded) pawnStatus.Text = "PawnIO driver removed. Restart Windows to finish.";
                else pawnStatus.Text = "PawnIO driver is not installed";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void WatchPawnIo()
        {
            if (pawnBusy || pawnChecking) return;
            Task.Factory.StartNew(() => PawnIo.QuickLook()).ContinueWith(t =>
            {
                if (!t.IsFaulted && !pawnBusy && !pawnChecking && t.Result != pawnListed) RefreshPawnIo(null);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void RemovePawnIo()
        {
            if (SensorCatalog.LibreRunning())
            {
                MessageBox.Show(this, "Libre Hardware Monitor is running, and it uses the PawnIO driver. Close Libre Hardware Monitor first, then try again.",
                    Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show(this, "Completely remove the PawnIO driver from this PC?\n\n"
                    + "Libre Hardware Monitor (and any other app that uses PawnIO) won't be able to read sensors like CPU temperature until "
                    + "the driver is installed again. Libre Hardware Monitor offers to install it again the next time it starts.\n\n"
                    + "Windows will ask for permission to remove it.",
                    Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

            pawnBusy = true;
            pawnRemove.IsEnabled = false;
            pawnStatus.ToolTip = null;
            pawnStatus.Text = "Removing the PawnIO driver...";
            Task.Factory.StartNew(() => PawnIo.Remove()).ContinueWith(t =>
            {
                pawnBusy = false;
                RefreshPawnIo(t.IsFaulted ? "The PawnIO driver couldn't be removed." : t.Result);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void WatchSources()
        {
            if (sourceCheckRunning || externalSensors.IsChecked != true) return;
            sourceCheckRunning = true;
            Task.Factory.StartNew(() =>
            {
                string problem;
                bool h = HwinfoSensors.IsSharing();
                bool l = SensorCatalog.LibreRunning() && LibreSensors.Read(out problem) != null;
                return (h ? "H" : "") + (l ? "L" : "");
            }).ContinueWith(t =>
            {
                sourceCheckRunning = false;
                if (!t.IsFaulted && t.Result != shownSources) FillSensorList(CheckedSensors());
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        HashSet<string> CheckedSensors()
        {
            return new HashSet<string>(sensorChecks.Where(c => c.Item1.IsChecked == true).Select(c => c.Item2.Id));
        }

        /// <summary>
        /// (Re)build the three provider tabs, keeping <paramref name="chosen"/> ticked. Sensors from HWiNFO / Libre Hardware
        /// Monitor stay listed while that app isn't active, greyed out, with a note saying why.
        /// </summary>
        void FillSensorList(HashSet<string> chosen)
        {
            bool external = externalSensors.IsChecked == true;
            List<SensorInfo> catalog;
            try { catalog = SensorCatalog.Available(external); } catch (Exception) { catalog = new List<SensorInfo>(); }

            // Remember every HWiNFO / Libre Hardware Monitor sensor found (saved with the settings).
            var knownIds = new HashSet<string>(known.Select(k => k.Id));
            foreach (var c in catalog.Where(c => c.Source.Length > 0 && !knownIds.Contains(c.Id))) { known.Add(c.ToKnown()); knownIds.Add(c.Id); }

            var active = new HashSet<string>(catalog.Where(c => c.Source.Length > 0).Select(c => c.Source));
            shownSources = (active.Contains("H") ? "H" : "") + (active.Contains("L") ? "L" : "");

            sensorChecks.Clear();
            var pingParent = pingHost.Parent as Panel;
            if (pingParent != null) pingParent.Children.Remove(pingHost);
            var listed = new HashSet<string>();

            FillGroups(windowsList, catalog.Where(c => c.Source.Length == 0).ToList(), chosen, listed);
            foreach (var src in new[] { "H", "L" })
            {
                var panel = src == "H" ? hwinfoList : libreList;
                var tab = src == "H" ? hwinfoTab : libreTab;
                string name = src == "H" ? "HWiNFO" : "Libre Hardware Monitor";
                var items = new List<SensorInfo>();
                if (external)
                {
                    if (active.Contains(src))
                    {
                        var live = catalog.Where(c => c.Source == src).ToList();
                        var liveIds = new HashSet<string>(live.Select(c => c.Id));
                        items.AddRange(live);
                        // Chosen before but not reported right now: keep it, greyed out.
                        items.AddRange(known.Where(k => k.Source == src && chosen.Contains(k.Id) && !liveIds.Contains(k.Id)).Select(SensorInfo.FromKnown));
                    }
                    else items.AddRange(known.Where(k => k.Source == src).Select(SensorInfo.FromKnown));
                }
                FillGroups(panel, items, chosen, listed);
                if (!external)
                    panel.Children.Insert(0, Note("Looking for " + name + " is switched off. Tick \"Always look for HWiNFO and Libre Hardware Monitor\" above to use its sensors."));
                else if (!active.Contains(src))
                    panel.Children.Insert(0, Note(items.Count > 0 ? InactiveNote(src)
                        : name + " hasn't been found yet. " + (src == "H"
                            ? "Start HWiNFO with \"Shared Memory Support\" switched on in its Settings, and its sensors appear here."
                            : "Start it as administrator, or turn on Options > Remote Web Server > Run, and its sensors appear here.")
                          + " See \"Sensor Information\" for where to download it."));
                // Greyed out while that app isn't active.
                tab.Header = new TextBlock { Text = name + (items.Count > 0 ? " (" + items.Count + ")" : ""), Opacity = active.Contains(src) ? 1 : 0.55 };
            }

            // Anything chosen that isn't listed (e.g. looking for the apps is switched off) stays chosen.
            foreach (var id in chosen.Where(id => !listed.Contains(id)))
                sensorChecks.Add(Tuple.Create(new CheckBox { IsChecked = true, Visibility = Visibility.Collapsed }, new SensorInfo(id, "", id, id)));
        }

        static TextBlock Note(string text)
        {
            var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic, Margin = new Thickness(0, 0, 0, 6) };
            note.SetResourceReference(TextBlock.ForegroundProperty, "T.Note");   // follows the Settings theme
            return note;
        }

        /// <summary>
        /// Each group: a heading, then its sensors in two columns. Rows are only as tall as their text (names that
        /// don't fit are shortened with "..." and shown in full when pointed at), so there are no gaps.
        /// </summary>
        void FillGroups(StackPanel panel, List<SensorInfo> items, HashSet<string> chosen, HashSet<string> listed)
        {
            panel.Children.Clear();
            foreach (var group in items.GroupBy(c => c.Group))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = group.Key, FontWeight = FontWeights.SemiBold, Opacity = group.All(x => x.Inactive) ? 0.55 : 1,
                    Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 10, 0, 2), TextTrimming = TextTrimming.CharacterEllipsis
                });
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                int i = 0;
                foreach (var info in group)
                {
                    if (i % 2 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var cb = new CheckBox
                    {
                        Content = new TextBlock { Text = info.Name, TextTrimming = TextTrimming.CharacterEllipsis },
                        IsChecked = chosen.Contains(info.Id), Margin = new Thickness(0, 2, 10, 2), ToolTip = info.Name + "\n" + SensorHelp(info)
                    };
                    if (info.Inactive)
                    {
                        cb.Opacity = 0.5;   // greyed out, but can still be chosen for when the app is running
                        cb.ToolTip = info.Name + "\n" + SensorHelp(info) + "\n" + info.SourceName + " isn't active right now, so this reading can't be retrieved.";
                    }
                    Grid.SetRow(cb, i / 2);
                    Grid.SetColumn(cb, i % 2);
                    grid.Children.Add(cb);
                    sensorChecks.Add(Tuple.Create(cb, info));
                    listed.Add(info.Id);
                    i++;
                }
                panel.Children.Add(grid);
                if (group.Key == SensorCatalog.Internet)
                {
                    var ping = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                    ping.Children.Add(new TextBlock { Text = "Ping address", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                    ping.Children.Add(pingHost);
                    panel.Children.Add(ping);
                }
            }
        }

        static string InactiveNote(string source)
        {
            return source == "H"
                ? "HWiNFO isn't active right now, so these sensors can't be read. Start HWiNFO with \"Shared Memory Support\" switched on in its Settings to get them again."
                : "Libre Hardware Monitor isn't active right now, so these sensors can't be read. Start it as administrator, or turn on Options > Remote Web Server > Run, to get them again.";
        }

        static Hyperlink Link(string url)
        {
            var h = new Hyperlink(new Run(url)) { NavigateUri = new Uri(url), ToolTip = "Open in your web browser" };
            h.RequestNavigate += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); } catch (Exception) { }
                e.Handled = true;
            };
            return h;
        }

        /// <summary>Seconds typed in either "2.5" or the PC's own style ("2,5").</summary>
        static bool ParseSeconds(string text, out double seconds)
        {
            text = (text ?? "").Trim();
            return (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out seconds)
                 || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                 && seconds >= 0.5 && seconds <= 3600;
        }

        /// <summary>What's wrong on this tab, or null if everything can be saved.</summary>
        string HardwareProblem()
        {
            double secs;
            if (!ParseSeconds(sensorEvery.Text, out secs))
                return "Under Hardware monitoring, type how often to check the sensors: a number of seconds from 0.5 to 3600.";
            if (sensorText.Text.Trim().Length > 0 && !ValidColor(sensorText.Text))
                return "The sensor text color should look like #FFFFFF, or click Auto.";
            if (sensorBack.Text.Trim().Length > 0 && !ValidColor(sensorBack.Text))
                return "The sensor background color should look like #202020, or click Auto.";
            return null;
        }

        void ApplyHardware(AppSettings s)
        {
            double secs;
            if (ParseSeconds(sensorEvery.Text, out secs)) s.SensorInterval = secs;
            s.SensorAlign = (DockAlignment)Math.Max(0, sensorAlign.SelectedIndex);
            s.SensorFillBottom = sensorFill.SelectedIndex == 1;
            s.SensorSize = Math.Max(0, sensorSize.SelectedIndex);
            s.SensorTiles = sensorStyle.SelectedIndex == 1;
            s.SensorGraphLength = GraphLengths[Math.Max(0, sensorGraph.SelectedIndex)];
            s.SensorTilesCentered = sensorTileText.SelectedIndex == 1;
            s.SensorWarnings = sensorWarnings.IsChecked == true;
            s.SensorAlerts = alerts.Select(a => new SensorAlert { Id = a.Id, Warning = a.Warning, Critical = a.Critical, LowIsBad = a.LowIsBad }).ToList();
            s.SensorTextColor = sensorText.Text.Trim();
            s.SensorBackColor = sensorBack.Text.Trim();
            s.ExternalSensors = externalSensors.IsChecked == true;
            s.KnownSensors = known.ToList();
            // Keep the order already arranged on the bar; newly ticked sensors go at the end in list order.
            var ticked = sensorChecks.Where(c => c.Item1.IsChecked == true).Select(c => c.Item2.Id).Distinct().ToList();
            if (!new HashSet<string>(ticked).SetEquals(s.Sensors)) s.SensorsHidden = false;   // sensors just chosen here should show
            s.Sensors = s.Sensors.Where(ticked.Contains).Concat(ticked.Where(id => !s.Sensors.Contains(id))).ToList();
            s.PingHost = string.IsNullOrWhiteSpace(pingHost.Text) ? "1.1.1.1" : pingHost.Text.Trim();
        }
    }
}
