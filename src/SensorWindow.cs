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
    /// <summary>A sensor's recent numbers (newest last), for its mini graph.</summary>
    internal class SensorHistory
    {
        public const int MaxLength = 240;
        public readonly List<double> Values = new List<double>();
        public readonly List<DateTime> Times = new List<DateTime>();   // when each value was read
        public GraphScale Scale;

        public void Add(double v, GraphScale s, DateTime when)
        {
            Scale = s;
            Values.Add(v);
            Times.Add(when);
            if (Values.Count > MaxLength)
            {
                Values.RemoveRange(0, Values.Count - MaxLength);
                Times.RemoveRange(0, Times.Count - MaxLength);
            }
        }
    }

    /// <summary>
    /// A row of live readings ("CPU 23%", "RAM 8.1 / 16 GB"...) that sits just above the launcher, either as compact
    /// readings or as tiles with a mini graph. Readings can be dragged to a new place; one from HWiNFO / Libre Hardware
    /// Monitor is greyed out while that app isn't active.
    /// </summary>
    internal class SensorWindow : Window
    {
        readonly Grid root = new Grid();
        readonly StackPanel rows = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
        readonly Canvas overlay = new Canvas { IsHitTestVisible = false };
        readonly Rectangle dropMark = new Rectangle { Width = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = new SolidColorBrush(LauncherWindow.SelectColor), Visibility = Visibility.Collapsed };
        readonly List<FrameworkElement> chips = new List<FrameworkElement>();
        readonly Brush chipBack, chipBorder, textBrush, labelBrush;
        readonly bool darkBack;
        readonly Dictionary<string, TextBlock> values = new Dictionary<string, TextBlock>();
        readonly Dictionary<string, FrameworkElement> chipById = new Dictionary<string, FrameworkElement>();
        readonly Dictionary<string, Sparkline> graphs = new Dictionary<string, Sparkline>();
        readonly Dictionary<string, FrameworkElement> gears = new Dictionary<string, FrameworkElement>();
        List<string> shown = new List<string>();
        string shownKey = "";
        List<SensorReading> last;
        HorizontalAlignment align = HorizontalAlignment.Center;
        double laidOutWidth;
        int size = 1;   // 0 small, 1 medium, 2 large
        bool fillBottom;
        bool tiles, tileCentered;
        int graphLength = 60;
        HashSet<string> noGraph = new HashSet<string>();

        /// <summary>The launcher's record of recent numbers per sensor (it outlives this window).</summary>
        public Dictionary<string, SensorHistory> History = new Dictionary<string, SensorHistory>();

        /// <summary>A tile's graph was switched on or off from its right-click menu: the sensor id, and on / off.</summary>
        public event Action<string, bool> GraphToggled;

        /// <summary>"Warning levels..." was chosen in a reading's right-click menu (with its latest reading).</summary>
        public event Action<SensorReading> LevelsRequested;

        /// <summary>A reading's warning levels, or null when warning colors are off (set by the launcher).</summary>
        public Func<SensorReading, Levels> LevelsFor;

        // Warning (amber) and critical (red): the value text and a thicker border in that color.
        Brush warnText, critText;
        Color warnColor, critColor;
        static readonly List<SensorAlert> NoAlerts = new List<SensorAlert>();

        bool locked;

        /// <summary>The launcher's layout lock: while it's on, readings can't be dragged to a new place.</summary>
        public void SetLocked(bool on)
        {
            if (locked == on) return;
            locked = on;
            foreach (var c in chips) c.Cursor = on ? null : Cursors.SizeAll;
            Refresh();   // the tooltips say whether they can be moved
        }

        /// <summary>Show the latest readings again (new warning levels, say).</summary>
        public void Refresh()
        {
            if (last != null) ShowReadings(last);
        }

        /// <summary>true: the row next to the launcher fills first and extra rows go above it.</summary>
        public void SetFillBottom(bool on)
        {
            if (fillBottom == on) return;
            fillBottom = on;
            laidOutWidth = -1;
            Relayout();
        }

        // dragging a reading to a new place
        FrameworkElement dragChip;
        Point dragStart;
        bool dragging;
        int dropIndex = -1;

        /// <summary>The readings were rearranged by dragging: the sensor ids in their new order.</summary>
        public event Action<List<string>> OrderChanged;

        public SensorWindow(Brush chipBack, Color text, Brush chipBorder)
        {
            Title = "Sensors";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            SizeToContent = SizeToContent.Height;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            this.chipBack = chipBack;
            this.chipBorder = chipBorder;
            textBrush = new SolidColorBrush(text);
            labelBrush = new SolidColorBrush(Color.FromArgb(0xB8, text.R, text.G, text.B));
            // Graph colors: lighter shades on a dark background, deeper ones on a light background.
            var back = chipBack as SolidColorBrush;
            darkBack = back == null ? true : (0.299 * back.Color.R + 0.587 * back.Color.G + 0.114 * back.Color.B) / 255 < 0.5;
            warnColor = (Color)ColorConverter.ConvertFromString(darkBack ? "#FF8A3D" : "#C2410C");
            critColor = (Color)ColorConverter.ConvertFromString(darkBack ? "#FF5C5C" : "#DC2626");
            warnText = Frozen(new SolidColorBrush(warnColor));
            critText = Frozen(new SolidColorBrush(critColor));
            overlay.Children.Add(dropMark);
            root.Children.Add(rows);
            root.Children.Add(overlay);
            Content = root;
            SizeChanged += (s, e) => { if (e.WidthChanged) Relayout(); };
        }

        public void SetSize(int s)
        {
            s = Math.Max(0, Math.Min(2, s));
            if (s == size) return;
            size = s;
            Rebuild();
        }

        /// <param name="on">Tiles with mini graphs instead of compact readings.</param>
        /// <param name="length">How many recent readings a graph shows.</param>
        /// <param name="off">Sensors whose graph is switched off.</param>
        /// <param name="centered">The tiles' text centered across, and up and down when there's room (instead of top left).</param>
        public void SetTiles(bool on, int length, IEnumerable<string> off, bool centered)
        {
            var offSet = new HashSet<string>(off);
            length = Math.Max(10, Math.Min(SensorHistory.MaxLength, length));
            if (on == tiles && length == graphLength && offSet.SetEquals(noGraph) && centered == tileCentered) return;
            tiles = on;
            graphLength = length;
            noGraph = offSet;
            tileCentered = centered;
            Rebuild();
        }

        /// <summary>Build the chips again (new size or style), with the latest readings if there are any.</summary>
        void Rebuild()
        {
            shown = new List<string>();
            shownKey = "";
            if (last != null) ShowReadings(last);
        }

        public void SetAlignment(DockAlignment a)
        {
            align = a == DockAlignment.Start ? HorizontalAlignment.Left : a == DockAlignment.End ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            laidOutWidth = -1;
            Relayout();
        }

        bool HasGraph(string id) { return tiles && History.ContainsKey(id) && !noGraph.Contains(id); }

        static readonly System.Text.RegularExpressions.Regex Glue = new System.Text.RegularExpressions.Regex(@"(?<=[\d↓↑]) (?=[\d\p{L}%°])");

        /// <summary>
        /// A value that wraps onto two lines on a tile breaks between its parts, not inside one: "↓ 65.1 GB  ↑ 1.9 GB"
        /// keeps "↓ 65.1 GB" together (a number stays with its unit and arrow).
        /// </summary>
        static string KeepTogether(string value) { return value == null ? "" : Glue.Replace(value, " "); }

        /// <summary>Which sensors are shown in which order, and which of them have a graph: a change means building them again.</summary>
        string KeyFor(IEnumerable<string> ids) { return string.Join("\n", ids.Select(id => HasGraph(id) ? id + "#" : id)); }

        public void ShowReadings(List<SensorReading> readings)
        {
            last = readings;
            if (dragging) return;   // don't rearrange while a reading is being dragged
            var ids = readings.Select(r => r.Info.Id).ToList();
            string key = KeyFor(ids);
            bool rebuilt = false;
            if (key != shownKey)
            {
                chips.Clear();
                values.Clear();
                chipById.Clear();
                graphs.Clear();
                gears.Clear();
                foreach (var r in readings) chips.Add(tiles ? Tile(r) : Chip(r.Info));
                shown = ids;
                shownKey = key;
                rebuilt = true;
            }
            foreach (var r in readings)
            {
                TextBlock tb;
                string text = tiles ? KeepTogether(r.Value) : r.Value;
                if (values.TryGetValue(r.Info.Id, out tb) && tb.Text != text) tb.Text = text;
                // Past its warning or critical level: amber or red value, border and tint (the graph colors its own part).
                Levels lv = LevelsFor == null || r.Inactive ? null : LevelsFor(r);
                int state = lv == null ? SensorLevels.Normal : SensorLevels.Level(lv, r.Number, r.Factor);
                if (tb != null) tb.Foreground = state == SensorLevels.Critical ? critText : state == SensorLevels.Warn ? warnText : textBrush;
                Sparkline g;
                SensorHistory h;
                if (graphs.TryGetValue(r.Info.Id, out g) && History.TryGetValue(r.Info.Id, out h)) g.Show(h, graphLength, lv, r.Factor);
                FrameworkElement chip;
                if (chipById.TryGetValue(r.Info.Id, out chip))
                {
                    chip.Opacity = r.Inactive ? 0.45 : 1;   // greyed out while its app isn't active
                    var border = chip as Border;
                    if (border != null)
                    {
                        // A 2 px border in the warning color; the padding gives back the extra pixel, so nothing moves.
                        bool flagged = state != SensorLevels.Normal;
                        var pad = BasePadding();
                        border.BorderBrush = state == SensorLevels.Critical ? critText : state == SensorLevels.Warn ? warnText : chipBorder;
                        border.BorderThickness = new Thickness(flagged ? 2 : 1);
                        border.Padding = flagged ? new Thickness(Math.Max(0, pad.Left - 1), Math.Max(0, pad.Top - 1), Math.Max(0, pad.Right - 1), Math.Max(0, pad.Bottom - 1)) : pad;
                    }
                    string name = tiles ? r.Info.Name + ": " + r.Value : r.Info.Name;   // a tile may cut a long value short
                    bool graphMenu = tiles && History.ContainsKey(r.Info.Id), levelsMenu = SensorLevels.Row(NoAlerts, r.Info, r) != null;
                    // Tiles keep the how-to in their gear's tooltip; readings (no gear) keep it at the end of their own.
                    string more = tiles ? "" : (locked ? "\nLocked in place (the padlock at the top left of the launcher unlocks it)." : "\nDrag to move it.")
                        + (levelsMenu ? " Right-click to set its warning levels." : "");
                    FrameworkElement gear;
                    if (gears.TryGetValue(r.Info.Id, out gear)) gear.ToolTip = GearTip(graphMenu, levelsMenu);
                    if (state != SensorLevels.Normal && lv != null)
                        more = "\n" + (state == SensorLevels.Critical ? "Past its critical level (" : "Past its warning level (")
                            + SensorLevels.Show(state == SensorLevels.Critical ? lv.Critical : lv.Warning) + (r.Unit == "%" ? "%" : " " + r.Unit) + ")." + more;
                    SensorHistory seen;
                    if (!r.Inactive && graphs.ContainsKey(r.Info.Id) && History.TryGetValue(r.Info.Id, out seen))
                    {
                        string range = RangeLine(r, seen);
                        if (range.Length > 0) more = "\n" + range + more;
                    }
                    chip.ToolTip = r.Inactive
                        ? r.Info.Name + "\n" + r.Info.SourceName + " isn't active, so this reading can't be retrieved right now." + more
                        : name + (r.Info.Source.Length > 0 ? "  (from " + r.Info.SourceName + ")" : "") + more;
                }
            }
            if (rebuilt) { laidOutWidth = -1; Relayout(); return; }
            if (tiles) return;   // tiles have a fixed width, so a longer value never changes the rows
            // A reading that grew ("9 ms" -> "120 ms") may no longer fit its row: pack the rows again.
            double width = ActualWidth;
            foreach (StackPanel r in rows.Children)
            {
                r.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (width > 0 && r.DesiredSize.Width > width) { laidOutWidth = -1; Relayout(); break; }
            }
        }

        /// <summary>
        /// Pack the chips into rows that fit the bar's width; every row (not just the block) follows the
        /// chosen alignment, and extra rows stack upward, away from the launcher. Tiles share each row evenly.
        /// </summary>
        void Relayout()
        {
            double width = ActualWidth > 0 ? ActualWidth : Width;
            if (double.IsNaN(width) || width <= 0 || width == laidOutWidth) return;
            laidOutWidth = width;
            foreach (StackPanel old in rows.Children) old.Children.Clear();
            rows.Children.Clear();
            StackPanel row = null;
            if (tiles && chips.Count > 0)
            {
                // As many columns as fit at the size's narrowest tile; just a few tiles get wider ones (up to a point).
                double narrowest = size == 0 ? 100 : size == 2 ? 168 : 132;
                int columns = Math.Max(1, (int)Math.Floor(width / narrowest));
                double cell = Math.Floor(width / columns);
                if (chips.Count < columns) cell = Math.Min(Math.Floor(width / chips.Count), Math.Floor(narrowest * 1.6));
                double margin = size == 0 ? 2 : 3;
                for (int i = 0; i < chips.Count; i++)
                {
                    if (i % columns == 0)
                    {
                        row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = align };
                        rows.Children.Add(row);
                    }
                    chips[i].Width = cell - 2 * margin;
                    row.Children.Add(chips[i]);
                }
            }
            else
            {
                double used = 0;
                double room = width - 10;   // a little spare room per row for a reading that grows ("9%" -> "100%")
                foreach (var chip in chips)
                {
                    chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double w = chip.DesiredSize.Width;
                    if (row == null || (used + w > room && row.Children.Count > 0))
                    {
                        row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = align };
                        rows.Children.Add(row);
                        used = 0;
                    }
                    row.Children.Add(chip);
                    used += w;
                }
            }
            if (fillBottom && rows.Children.Count > 1)
            {
                // The first (full) row goes at the bottom, next to the launcher; later rows stack above it.
                var ordered = rows.Children.Cast<UIElement>().Reverse().ToList();
                rows.Children.Clear();
                foreach (var r in ordered) rows.Children.Add(r);
            }
        }

        double FontSizeNow { get { return size == 0 ? 10.5 : size == 2 ? 15 : 12; } }

        /// <summary>
        /// For a graph's tooltip, over the readings the graph shows: the highest and lowest and when they were last
        /// reached ("In the last 60 readings: / Highest 87% at 2:41:05 PM / Lowest 12% at 2:39:50 PM"), or for
        /// online / offline, how many were online and when it was last offline.
        /// </summary>
        string RangeLine(SensorReading r, SensorHistory h)
        {
            int start = Math.Max(0, h.Values.Count - graphLength), count = 0, first = -1, high = -1, low = -1, lastOff = -1, on = 0;
            for (int i = start; i < h.Values.Count; i++)
            {
                double v = h.Values[i];
                if (double.IsNaN(v)) continue;
                count++;
                if (first < 0) first = i;
                // ">=" and "<=": the most recent time a value was reached
                if (high < 0 || v >= h.Values[high]) high = i;
                if (low < 0 || v <= h.Values[low]) low = i;
                if (v > 0.5) on++; else lastOff = i;
            }
            if (count == 0) return "";
            Func<int, string> when = i => i < h.Times.Count ? Stamp(h.Times[i]) : "?";
            string span = count == 1 ? "In the last reading" : "In the last " + count + " readings";
            if (h.Scale == GraphScale.OnOff)
            {
                bool net = r.Info.Id == "inet.status";
                string yes = net ? "online" : "on", no = net ? "offline" : "off";
                if (on == count) return span + ": " + yes + (count == 1 ? " at " : " every time, since ") + when(first);
                if (on == 0) return span + ": " + no + (count == 1 ? " at " : " every time, since ") + when(first);
                return span + ": " + yes + " " + on + " times, " + no + " " + (count - on) + " times (last " + no + " at " + when(lastOff) + ")";
            }
            string hi = SensorLevels.Format(r, h.Values[high]), lo = SensorLevels.Format(r, h.Values[low]);
            if (hi == lo) return span + ": steady at " + hi + (count == 1 ? " at " : " since ") + when(first);
            return span + ":\nHighest " + hi + " at " + when(high) + "\nLowest " + lo + " at " + when(low);
        }

        /// <summary>"2:41:05 PM", or with the day when it wasn't today ("Nov 17, 2:41:05 PM").</summary>
        static string Stamp(DateTime t)
        {
            string time = t.ToString("T", System.Globalization.CultureInfo.CurrentCulture);
            return t.Date == DateTime.Today ? time : t.ToString("MMM d", System.Globalization.CultureInfo.CurrentCulture) + ", " + time;
        }

        /// <summary>A tile's gear: click it (or right-click the tile) for the tile's settings; its tooltip says how tiles work.</summary>
        FrameworkElement Gear(string id, double font)
        {
            var glyph = new TextBlock { Text = "", FontFamily = TileView.IconFont, FontSize = Math.Round(font * 0.95), Foreground = labelBrush };
            var gear = new Border
            {
                Child = glyph, Background = Brushes.Transparent, Padding = new Thickness(2), Margin = new Thickness(0, -2, -3, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand, Opacity = 0.55
            };
            System.Windows.Automation.AutomationProperties.SetName(gear, "Sensor settings");
            gear.MouseEnter += delegate { gear.Opacity = 1; };
            gear.MouseLeave += delegate { gear.Opacity = 0.55; };
            gear.MouseLeftButtonDown += (o, e) => { e.Handled = true; };   // a click, not the start of a drag
            gear.MouseLeftButtonUp += (o, e) => { e.Handled = true; ShowMenu(gear, id); };
            gear.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowMenu(gear, id); };
            gears[id] = gear;
            return gear;
        }

        /// <summary>The gear's tooltip: what its menu has, and how to move the tile (or that the tiles are locked).</summary>
        string GearTip(bool graph, bool levels)
        {
            string what = graph && levels ? "show or hide its graph, and set its warning levels" : graph ? "show or hide its graph" : "set its warning levels";
            return "Settings for this sensor: " + what + ". Right-clicking the tile opens them too.\n"
                + (locked ? "The tiles are locked in place (the padlock at the top left of the launcher unlocks them)." : "Drag a tile to move it.");
        }

        /// <summary>The padding inside a reading or tile (with its normal 1 px border).</summary>
        Thickness BasePadding()
        {
            if (tiles) return size == 0 ? new Thickness(7, 3, 7, 4) : size == 2 ? new Thickness(12, 7, 12, 8) : new Thickness(9, 5, 9, 6);
            return size == 0 ? new Thickness(7, 1, 7, 2) : size == 2 ? new Thickness(13, 5, 13, 6) : new Thickness(10, 3, 10, 4);
        }

        FrameworkElement Chip(SensorInfo s)
        {
            double font = FontSizeNow;
            // The font is set on the text itself (not just inherited from the window), so a chip measured while
            // it's off the bar is exactly as wide as it will be on it.
            var value = new TextBlock { Text = "…", FontFamily = FontFamily, FontWeight = FontWeights.SemiBold, FontSize = font, Foreground = textBrush };
            values[s.Id] = value;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text = s.Label, FontFamily = FontFamily, FontSize = font, Foreground = labelBrush, Margin = new Thickness(0, 0, font / 2, 0) });
            content.Children.Add(value);
            var chip = new Border
            {
                CornerRadius = new CornerRadius(size == 0 ? 7 : size == 2 ? 12 : 9), Background = chipBack, BorderBrush = chipBorder, BorderThickness = new Thickness(1),
                Padding = size == 0 ? new Thickness(7, 1, 7, 2) : size == 2 ? new Thickness(13, 5, 13, 6) : new Thickness(10, 3, 10, 4),
                Margin = new Thickness(size == 0 ? 2 : 3), Child = content, ToolTip = s.Name, Tag = s.Id, Cursor = locked ? null : Cursors.SizeAll
            };
            chipById[s.Id] = chip;
            Draggable(chip);
            chip.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowMenu(chip, s.Id); };
            return chip;
        }

        /// <summary>A reading's right-click menu: its graph on or off (tiles), and its warning levels.</summary>
        void ShowMenu(FrameworkElement chip, string id)
        {
            var r = last == null ? null : last.FirstOrDefault(x => x.Info.Id == id);
            var menu = SettingsTheme.ThemeMenu(new ContextMenu { PlacementTarget = chip });
            if (gears.ContainsValue(chip)) menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;   // from the gear: just below it
            if (tiles && History.ContainsKey(id))   // a reading with no number (an IP address, an app name) has no graph
            {
                bool on = !noGraph.Contains(id);
                var item = new MenuItem { Header = "Show graph", IsCheckable = true, IsChecked = on };
                item.Click += delegate { if (GraphToggled != null) GraphToggled(id, !on); };
                menu.Items.Add(item);
            }
            if (r != null && SensorLevels.Row(NoAlerts, r.Info, r) != null)
            {
                var levels = new MenuItem { Header = "Warning levels..." };
                levels.Click += delegate { if (LevelsRequested != null) LevelsRequested(r); };
                menu.Items.Add(levels);
            }
            if (menu.Items.Count > 0) menu.IsOpen = true;
        }

        static Brush Frozen(Brush b) { b.Freeze(); return b; }

        /// <summary>A tile: the sensor's name, its value, and (when it has a number) a mini graph of its recent readings.</summary>
        FrameworkElement Tile(SensorReading r)
        {
            var s = r.Info;
            double font = FontSizeNow, big = Math.Round(font * 1.3, 1), line = Math.Round(big * 1.3);
            double graphH = size == 0 ? 18 : size == 2 ? 32 : 24, gap = Math.Round(font * 0.3);
            var across = tileCentered ? TextAlignment.Center : TextAlignment.Left;
            var label = new TextBlock { Text = s.Label, FontFamily = FontFamily, FontSize = font, Foreground = labelBrush, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = across };
            // A small gear at the top right opens the tile's settings (graph, warning levels), when it has any;
            // the name keeps clear of it (on both sides when centered, so it stays in the middle).
            bool withGear = History.ContainsKey(s.Id) || SensorLevels.Row(NoAlerts, s, r) != null;
            double gearRoom = Math.Round(font * 1.35);
            if (withGear) label.Margin = tileCentered ? new Thickness(gearRoom, 0, gearRoom, 0) : new Thickness(0, 0, gearRoom, 0);
            var value = new TextBlock
            {
                Text = "…", FontFamily = FontFamily, FontSize = big, FontWeight = FontWeights.SemiBold, Foreground = textBrush, TextAlignment = across,
                TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = line, LineStackingStrategy = LineStackingStrategy.BlockLineHeight
            };
            values[s.Id] = value;
            FrameworkElement content;
            if (HasGraph(s.Id))
            {
                var stack = new StackPanel();
                stack.Children.Add(label);
                // One line: a long value ("24.1 / 31.4 GB" on a small tile) gets a little smaller instead of being cut off.
                value.TextTrimming = TextTrimming.None;
                stack.Children.Add(new Viewbox
                {
                    Child = value, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Height = line,
                    HorizontalAlignment = tileCentered ? HorizontalAlignment.Center : HorizontalAlignment.Left
                });
                var colors = GraphColors(s.Id, r.Kind);
                var g = new Sparkline(colors[0], colors[1], colors[2], warnColor, critColor) { Height = graphH, Margin = new Thickness(0, gap, 0, 0) };
                graphs[s.Id] = g;
                stack.Children.Add(g);
                content = stack;
            }
            else if (!tileCentered)
            {
                // No graph: the value may take two lines in its place, so every tile is the same height.
                var stack = new StackPanel();
                stack.Children.Add(label);
                value.TextWrapping = TextWrapping.Wrap;
                value.Height = line + gap + graphH;
                stack.Children.Add(value);
                content = stack;
            }
            else
            {
                // No graph, centered: the name and value sit in the middle of a tile as tall as one with a graph
                // (an invisible copy of that layout keeps the height the same).
                var room = new StackPanel { Visibility = Visibility.Hidden };
                room.Children.Add(new TextBlock { Text = s.Label, FontFamily = FontFamily, FontSize = font });
                room.Children.Add(new Border { Height = line + gap + graphH });
                value.TextWrapping = TextWrapping.Wrap;
                value.MaxHeight = line + gap + graphH;
                var middle = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                middle.Children.Add(label);
                middle.Children.Add(value);
                var grid = new Grid();
                grid.Children.Add(room);
                grid.Children.Add(middle);
                content = grid;
            }
            UIElement inside = content;
            if (withGear)
            {
                var layered = new Grid();
                layered.Children.Add(content);
                layered.Children.Add(Gear(s.Id, font));
                inside = layered;
            }
            var tile = new Border
            {
                CornerRadius = new CornerRadius(size == 0 ? 7 : size == 2 ? 12 : 9), Background = chipBack, BorderBrush = chipBorder, BorderThickness = new Thickness(1),
                Padding = size == 0 ? new Thickness(7, 3, 7, 4) : size == 2 ? new Thickness(12, 7, 12, 8) : new Thickness(9, 5, 9, 6),
                Margin = new Thickness(size == 0 ? 2 : 3), Child = inside, ToolTip = s.Name, Tag = s.Id, Cursor = locked ? null : Cursors.SizeAll
            };
            chipById[s.Id] = tile;
            Draggable(tile);
            tile.MouseRightButtonUp += (o, e) => { e.Handled = true; ShowMenu(tile, s.Id); };
            return tile;
        }

        /// <summary>[line, on, off] colors for a sensor's graph: by what it measures, so the same kind of reading always looks the same.</summary>
        Color[] GraphColors(string id, string kind)
        {
            string hue;
            // Amber and red are kept for warnings, so no normal reading uses them.
            if (kind.Length > 0)
                hue = kind == "temp" ? "pink" : kind == "power" || kind == "clock" ? "purple" : kind == "data" ? "blue" : "teal";
            else if (id.StartsWith("gpu") || id.EndsWith("|up")) hue = "purple";
            else if (id.StartsWith("mem.") || id.StartsWith("disk|") || id.StartsWith("drive|") || id == "inet.ping") hue = "blue";
            else if (id.StartsWith("bat.")) hue = "green";
            else hue = "teal";   // processor, download, Wi-Fi signal
            Func<string, string, Color> pick = (light, dark) => (Color)ColorConverter.ConvertFromString(darkBack ? dark : light);
            Color c;
            switch (hue)
            {
                case "purple": c = pick("#7F77DD", "#AFA9EC"); break;
                case "blue": c = pick("#378ADD", "#85B7EB"); break;
                case "pink": c = pick("#D4537E", "#ED93B1"); break;
                case "green": c = pick("#639922", "#97C459"); break;
                default: c = pick("#1D9E75", "#5DCAA5"); break;
            }
            return new[] { c, pick("#639922", "#97C459"), critColor };
        }

        void Draggable(FrameworkElement chip)
        {
            chip.MouseLeftButtonDown += (o, e) =>
            {
                e.Handled = true;
                if (locked) return;   // the layout is locked: readings stay where they are
                dragChip = chip;
                dragStart = e.GetPosition(this);
                chip.CaptureMouse();
            };
            chip.MouseMove += (o, e) =>
            {
                if (dragChip != chip) return;
                var p = e.GetPosition(this);
                if (!dragging)
                {
                    if (Math.Abs(p.X - dragStart.X) < 4 && Math.Abs(p.Y - dragStart.Y) < 4) return;
                    dragging = true;
                    chip.Opacity = 0.5;
                }
                ShowDropMark(p);
            };
            chip.MouseLeftButtonUp += (o, e) =>
            {
                if (dragChip != chip) return;
                e.Handled = true;
                bool moved = dragging;
                int target = dropIndex;
                chip.ReleaseMouseCapture();   // LostMouseCapture tidies up
                if (moved && target >= 0) MoveChip(chip, target);
            };
            chip.LostMouseCapture += delegate
            {
                if (dragChip != chip) return;
                dragChip = null;
                dragging = false;
                dropIndex = -1;
                chip.Opacity = 1;
                dropMark.Visibility = Visibility.Collapsed;
            };
        }

        /// <summary>Show where the dragged reading would go: before the reading under the pointer, or at the end of its row.</summary>
        void ShowDropMark(Point p)
        {
            dropIndex = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < chips.Count; i++)
            {
                var c = chips[i];
                if (!c.IsVisible) continue;
                var topLeft = c.TranslatePoint(new Point(0, 0), this);
                var r = new Rect(topLeft, new Size(c.ActualWidth, c.ActualHeight));
                double dy = p.Y < r.Top ? r.Top - p.Y : p.Y > r.Bottom ? p.Y - r.Bottom : 0;
                // Each chip offers two places: before it (its left edge) and after it (its right edge).
                foreach (var edge in new[] { Tuple.Create(r.Left, i), Tuple.Create(r.Right, i + 1) })
                {
                    double d = Math.Abs(p.X - edge.Item1) + dy * 4;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        dropIndex = edge.Item2;
                        Canvas.SetLeft(dropMark, edge.Item1 - 1.5 - (edge.Item2 == i ? 3 : -3) * 0.5);
                        Canvas.SetTop(dropMark, r.Top);
                        dropMark.Height = r.Height;
                    }
                }
            }
            dropMark.Visibility = dropIndex >= 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Test copies only: the same move a drag makes, without a mouse.</summary>
        public void TestMove(int from, int to)
        {
            if (from >= 0 && from < chips.Count) MoveChip(chips[from], to);
        }

        /// <summary>Test copies only: how the bar is laid out right now.</summary>
        public string TestReport()
        {
            return (tiles ? "tiles " : "readings ") + chips.Count + ", graphs " + graphs.Count + ", gears " + gears.Count + ", rows " + rows.Children.Count
                + ", first width " + (chips.Count > 0 ? chips[0].ActualWidth.ToString("0") : "-") + ", window " + ActualWidth.ToString("0") + " x " + ActualHeight.ToString("0")
                + ", history " + string.Join(" ", History.Select(h => h.Value.Values.Count))
                + "\r\ntooltips: " + string.Join("\r\n          ", chips.Select(c => (c.ToolTip as string ?? "").Replace("\n", " / ")))
                + "\r\ngear tips: " + string.Join("\r\n           ", gears.Select(g => g.Key + ": " + (g.Value.ToolTip as string ?? "").Replace("\n", " / ")).Distinct().Take(3));
        }

        /// <summary>Test copies only: what clicking a tile's gear does (opens its settings menu).</summary>
        public void TestGear(int index)
        {
            if (index < 0 || index >= chips.Count) return;
            FrameworkElement gear;
            if (gears.TryGetValue((string)chips[index].Tag, out gear)) ShowMenu(gear, (string)chips[index].Tag);
        }

        /// <summary>Test copies only: what "Warning levels..." in a reading's right-click menu does.</summary>
        public void TestLevels(int index)
        {
            if (index < 0 || index >= chips.Count || last == null) return;
            string id = (string)chips[index].Tag;
            var r = last.FirstOrDefault(x => x.Info.Id == id);
            if (r != null && LevelsRequested != null) LevelsRequested(r);
        }

        /// <summary>Test copies only: what a tile's right-click menu does.</summary>
        public void TestToggleGraph(int index)
        {
            if (index < 0 || index >= chips.Count) return;
            string id = (string)chips[index].Tag;
            if (GraphToggled != null && History.ContainsKey(id)) GraphToggled(id, noGraph.Contains(id));
        }

        void MoveChip(FrameworkElement chip, int target)
        {
            if (locked) return;
            int from = chips.IndexOf(chip);
            if (from < 0) return;
            if (target > from) target--;
            if (target == from) return;
            chips.RemoveAt(from);
            chips.Insert(Math.Max(0, Math.Min(target, chips.Count)), chip);
            shown = chips.Select(c => (string)c.Tag).ToList();
            shownKey = KeyFor(shown);
            if (last != null)   // the latest readings in the new order, so a rebuild keeps it
                last = shown.Select(id => last.FirstOrDefault(r => r.Info.Id == id)).Where(r => r != null).ToList();
            laidOutWidth = -1;
            Relayout();
            if (OrderChanged != null) OrderChanged(shown.ToList());
        }
    }

    /// <summary>
    /// A tile's mini graph: a line with a light fill under it, newest reading at the right edge. Percentages use 0 to 100,
    /// speeds start at 0, other readings (temperatures, clocks...) span their recent lowest to highest value; an on/off
    /// reading (online / offline) is a row of green and red marks. A missing reading leaves a gap.
    /// </summary>
    internal class Sparkline : FrameworkElement
    {
        readonly Pen pen, warnDash, critDash;
        readonly Brush lineBrush, fill, onBrush, offBrush;
        readonly Color lineColor, warnColor, critColor;
        double[] data = new double[0];
        int slots = 60;
        GraphScale scale;
        double warnAt = double.NaN, critAt = double.NaN;   // warning levels in the reading's own units (NaN: none)
        bool lowIsBad;

        public Sparkline(Color line, Color on, Color off, Color warn, Color crit)
        {
            lineColor = line;
            warnColor = warn;
            critColor = crit;
            lineBrush = Frozen(new SolidColorBrush(line));
            pen = new Pen(lineBrush, 1.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            fill = Frozen(new SolidColorBrush(Color.FromArgb(0x40, line.R, line.G, line.B)));
            onBrush = Frozen(new SolidColorBrush(on));
            offBrush = Frozen(new SolidColorBrush(off));
            Func<Color, Pen> dash = c =>
            {
                var p = new Pen(new SolidColorBrush(Color.FromArgb(0x80, c.R, c.G, c.B)), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
                p.Freeze();
                return p;
            };
            warnDash = dash(warn);
            critDash = dash(crit);
        }

        static Brush Frozen(Brush b) { b.Freeze(); return b; }

        /// <param name="levels">The sensor's warning levels (null: none), with <paramref name="factor"/> to turn them into the reading's own units.</param>
        public void Show(SensorHistory h, int length, Levels levels, double factor)
        {
            slots = Math.Max(2, length);
            int n = Math.Min(h.Values.Count, slots);
            data = h.Values.Skip(h.Values.Count - n).ToArray();
            scale = h.Scale;
            double f = factor > 0 ? factor : 1;
            warnAt = levels == null ? double.NaN : levels.Warning * f;
            critAt = levels == null ? double.NaN : levels.Critical * f;
            lowIsBad = levels != null && levels.LowIsBad;
            InvalidateVisual();
        }

        /// <summary>
        /// A top-to-bottom brush that is the normal color where readings are fine, amber past the warning level and red
        /// past the critical level (hard edges, so the line changes color exactly where it crosses a level).
        /// </summary>
        Brush Zones(byte alpha, double yWarn, double yCrit, double h)
        {
            Func<double, double> at = y => Math.Max(0, Math.Min(1, y / h));
            double w, c;
            var parts = new List<Tuple<Color, double, double>>();
            if (!lowIsBad)
            {
                c = double.IsNaN(yCrit) ? 0 : at(yCrit);
                w = double.IsNaN(yWarn) ? c : Math.Max(c, at(yWarn));
                parts.Add(Tuple.Create(critColor, 0.0, c));
                parts.Add(Tuple.Create(warnColor, c, w));
                parts.Add(Tuple.Create(lineColor, w, 1.0));
            }
            else
            {
                c = double.IsNaN(yCrit) ? 1 : at(yCrit);
                w = double.IsNaN(yWarn) ? c : Math.Min(c, at(yWarn));
                parts.Add(Tuple.Create(lineColor, 0.0, w));
                parts.Add(Tuple.Create(warnColor, w, c));
                parts.Add(Tuple.Create(critColor, c, 1.0));
            }
            var stops = new GradientStopCollection();
            foreach (var p in parts)
            {
                var col = Color.FromArgb(alpha, p.Item1.R, p.Item1.G, p.Item1.B);
                stops.Add(new GradientStop(col, p.Item2));
                stops.Add(new GradientStop(col, p.Item3));
            }
            var b = new LinearGradientBrush(stops, new Point(0, 0), new Point(0, h)) { MappingMode = BrushMappingMode.Absolute };
            b.Freeze();
            return b;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));   // the empty part still shows the tooltip and can be dragged
            int n = data.Length;
            if (n == 0) return;
            if (scale == GraphScale.OnOff)
            {
                double slot = w / slots, bw = Math.Max(1, slot - 1), bh = Math.Round(h * 0.55), top = Math.Round((h - bh) / 2);
                for (int i = 0; i < n; i++)
                {
                    if (double.IsNaN(data[i])) continue;
                    dc.DrawRectangle(data[i] > 0.5 ? onBrush : offBrush, null, new Rect(w - (n - i) * slot + 0.5, top, bw, bh));
                }
                return;
            }
            double lo, hi;
            Range(out lo, out hi);
            double step = w / (slots - 1), pad = 1.5;
            Func<double, double> yOf = v => double.IsNaN(v) ? double.NaN : pad + (h - 2 * pad) * (1 - (v - lo) / (hi - lo));
            Brush areaBrush = fill;
            Pen linePen = pen;
            if (!double.IsNaN(warnAt) || !double.IsNaN(critAt))
            {
                double yWarn = yOf(warnAt), yCrit = yOf(critAt);
                areaBrush = Zones(0x40, yWarn, yCrit, h);
                linePen = new Pen(Zones(0xFF, yWarn, yCrit, h), 1.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                linePen.Freeze();
                // Faint dashed lines where the levels are, when they're inside the graph's range.
                if (!double.IsNaN(warnAt) && warnAt > lo && warnAt < hi) dc.DrawLine(warnDash, new Point(0, Math.Round(yWarn) + 0.5), new Point(w, Math.Round(yWarn) + 0.5));
                if (!double.IsNaN(critAt) && critAt > lo && critAt < hi) dc.DrawLine(critDash, new Point(0, Math.Round(yCrit) + 0.5), new Point(w, Math.Round(yCrit) + 0.5));
            }
            Func<int, Point> at = i =>
            {
                double v = Math.Max(lo, Math.Min(hi, data[i]));
                return new Point(w - (n - 1 - i) * step, pad + (h - 2 * pad) * (1 - (v - lo) / (hi - lo)));
            };
            int start = -1;
            for (int i = 0; i <= n; i++)
            {
                bool gap = i == n || double.IsNaN(data[i]);
                if (!gap) { if (start < 0) start = i; continue; }
                if (start >= 0) Segment(dc, at, start, i - 1, h, areaBrush, linePen);
                start = -1;
            }
        }

        void Segment(DrawingContext dc, Func<int, Point> at, int a, int b, double h, Brush fill, Pen pen)
        {
            if (a == b) { dc.DrawEllipse(pen.Brush, null, at(a), 1.5, 1.5); return; }
            var area = new StreamGeometry();
            using (var c = area.Open())
            {
                c.BeginFigure(new Point(at(a).X, h), true, true);
                for (int i = a; i <= b; i++) c.LineTo(at(i), false, false);
                c.LineTo(new Point(at(b).X, h), false, false);
            }
            area.Freeze();
            dc.DrawGeometry(fill, null, area);
            var line = new StreamGeometry();
            using (var c = line.Open())
            {
                c.BeginFigure(at(a), false, false);
                for (int i = a + 1; i <= b; i++) c.LineTo(at(i), true, true);
            }
            line.Freeze();
            dc.DrawGeometry(null, pen, line);
        }

        void Range(out double lo, out double hi)
        {
            if (scale == GraphScale.Percent) { lo = 0; hi = 100; return; }
            double min = double.MaxValue, max = double.MinValue;
            foreach (var v in data) if (!double.IsNaN(v)) { min = Math.Min(min, v); max = Math.Max(max, v); }
            if (min > max) { lo = 0; hi = 1; return; }
            if (scale == GraphScale.FromZero) { lo = 0; hi = max > 0 ? max * 1.15 : 1; return; }
            // Other readings: their recent lowest to highest, with a little room. Small wobbles (45 -> 46 °C) don't
            // fill the whole height, and a steady reading is a line through the middle.
            double least = Math.Max(Math.Max(Math.Abs(max), Math.Abs(min)) * 0.05, 1e-6), span = max - min;
            if (span < least) { double mid = (max + min) / 2; lo = mid - least / 2; hi = mid + least / 2; return; }
            lo = min - span * 0.1;
            hi = max + span * 0.1;
        }
    }
}
