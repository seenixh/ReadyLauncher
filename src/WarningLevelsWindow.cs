using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// Set the warning and critical level of one sensor (from its right-click menu) or of all the chosen sensors
    /// (from Settings): higher or lower is worse, the two numbers, and a way back to the built-in levels.
    /// </summary>
    internal class WarningLevelsWindow : Window
    {
        readonly List<LevelRow> rows;
        readonly List<ComboBox> worse = new List<ComboBox>();
        readonly List<TextBox> warnBoxes = new List<TextBox>(), critBoxes = new List<TextBox>();
        readonly TextBlock problem;
        Dictionary<string, Levels> result;

        WarningLevelsWindow(List<LevelRow> rows, bool warningsOn)
        {
            this.rows = rows;
            Title = rows.Count == 1 ? "Warning levels: " + rows[0].Info.Label : "Warning levels";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "A reading turns amber when it reaches its warning level, and red at its critical level. "
                     + "Leave a box empty for no level there, or empty both to never color the sensor.",
                TextWrapping = TextWrapping.Wrap, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10)
            });
            if (!warningsOn)
                panel.Children.Add(new TextBlock
                {
                    Text = "Warning colors are switched off, so these levels show once you switch them on (Settings > Hardware monitoring > Warning colors).",
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 10)
                });

            var grid = new Grid();
            foreach (var w in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto })
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            Action<int, int, UIElement> put = (row, col, el) => { Grid.SetRow(el, row); Grid.SetColumn(el, col); grid.Children.Add(el); };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] heads = { "Sensor", "Now", "Worse when it's", "Warning", "Critical", "", "" };
            for (int c = 0; c < heads.Length; c++)
                if (heads[c].Length > 0) put(0, c, new TextBlock { Text = heads[c], Opacity = 0.75, Margin = new Thickness(0, 0, 14, 4) });

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int at = i + 1;
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                string where = r.Info.Source.Length > 0 ? "  (from " + r.Info.SourceName + ")" : "";
                put(at, 0, new TextBlock
                {
                    Text = r.Info.Label, ToolTip = r.Info.Name + where, MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 14, 3)
                });
                put(at, 1, new TextBlock
                {
                    Text = double.IsNaN(r.Now) ? "-" : WithUnit(r.Now, r.Unit), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 14, 3),
                    ToolTip = double.IsNaN(r.Now) ? "No reading right now" : "The latest reading"
                });
                var combo = new ComboBox { Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 3, 14, 3), MinWidth = 90, ToolTip = "Higher: a reading at or above a level is colored (load, temperature, ping). Lower: at or below it (battery charge, Wi-Fi signal)." };
                combo.Items.Add("Higher");
                combo.Items.Add("Lower");
                combo.SelectedIndex = r.Current.LowIsBad ? 1 : 0;
                System.Windows.Automation.AutomationProperties.SetName(combo, r.Info.Label + ": worse when it's");
                worse.Add(combo);
                put(at, 2, combo);
                var warn = Box(r.Current.Warning, "The warning level (amber), in " + UnitName(r.Unit) + ". Empty: none.");
                var crit = Box(r.Current.Critical, "The critical level (red), in " + UnitName(r.Unit) + ". Empty: none.");
                System.Windows.Automation.AutomationProperties.SetName(warn, r.Info.Label + ": warning level");
                System.Windows.Automation.AutomationProperties.SetName(crit, r.Info.Label + ": critical level");
                warnBoxes.Add(warn);
                critBoxes.Add(crit);
                put(at, 3, warn);
                put(at, 4, crit);
                put(at, 5, new TextBlock { Text = r.Unit, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 14, 3) });
                var reset = new Button
                {
                    Content = "Default", Padding = new Thickness(10, 1, 10, 1), Margin = new Thickness(0, 3, 0, 3), VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Go back to the built-in levels: " + Describe(r.Default, r.Unit)
                };
                int k = i;
                reset.Click += delegate
                {
                    var d = rows[k].Default;
                    worse[k].SelectedIndex = d.LowIsBad ? 1 : 0;
                    warnBoxes[k].Text = SensorLevels.Show(d.Warning);
                    critBoxes[k].Text = SensorLevels.Show(d.Critical);
                };
                put(at, 6, reset);
                combo.SelectionChanged += delegate { ClearProblem(); };
            }
            panel.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 460, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            problem = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x4F, 0x4F)), Visibility = Visibility.Collapsed
            };
            panel.Children.Add(problem);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var ok = new Button { Content = "OK", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            ok.Click += delegate { Finish(); };
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
            Loaded += delegate { if (warnBoxes.Count > 0) { warnBoxes[0].Focus(); warnBoxes[0].SelectAll(); } };
        }

        TextBox Box(double v, string tip)
        {
            var b = new TextBox { Text = SensorLevels.Show(v), Width = 70, Padding = new Thickness(3), Margin = new Thickness(0, 3, 14, 3), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip };
            b.TextChanged += delegate { ClearProblem(); };
            return b;
        }

        void ClearProblem() { if (problem != null) problem.Visibility = Visibility.Collapsed; }

        static string WithUnit(double v, string unit) { return SensorLevels.WithUnit(v, unit); }

        static string UnitName(string unit) { return unit.Length == 0 ? "the sensor's own unit" : unit == "%" ? "percent" : unit; }

        /// <summary>"warning at 85% or higher, critical at 95% or higher", or "none".</summary>
        static string Describe(Levels l, string unit)
        {
            if (!l.Any) return "none (this sensor isn't colored unless you set levels)";
            string dir = l.LowIsBad ? " or lower" : " or higher";
            var parts = new List<string>();
            if (!double.IsNaN(l.Warning)) parts.Add("warning at " + WithUnit(l.Warning, unit) + dir);
            if (!double.IsNaN(l.Critical)) parts.Add("critical at " + WithUnit(l.Critical, unit) + dir);
            return string.Join(", ", parts);
        }

        void Finish()
        {
            var chosen = new Dictionary<string, Levels>();
            for (int i = 0; i < rows.Count; i++)
            {
                string name = rows[i].Info.Label;
                double w, c;
                if (!SensorLevels.Parse(warnBoxes[i].Text, out w)) { Problem(name + ": type a number for the warning level, or leave it empty.", warnBoxes[i]); return; }
                if (!SensorLevels.Parse(critBoxes[i].Text, out c)) { Problem(name + ": type a number for the critical level, or leave it empty.", critBoxes[i]); return; }
                bool low = worse[i].SelectedIndex == 1;
                if (!double.IsNaN(w) && !double.IsNaN(c))
                {
                    if (!low && c <= w) { Problem(name + ": when higher is worse, the critical level should be above the warning level.", critBoxes[i]); return; }
                    if (low && c >= w) { Problem(name + ": when lower is worse, the critical level should be below the warning level.", critBoxes[i]); return; }
                }
                chosen[rows[i].Info.Id] = new Levels { Warning = w, Critical = c, LowIsBad = low };
            }
            result = chosen;
            DialogResult = true;
        }

        void Problem(string text, TextBox box)
        {
            problem.Text = text;
            problem.Visibility = Visibility.Visible;
            box.Focus();
            box.SelectAll();
        }

        /// <summary>The levels chosen for each row's sensor (by id), or null if the window was canceled.</summary>
        public static Dictionary<string, Levels> Edit(Window owner, LauncherApp app, SettingsThemeMode theme, List<LevelRow> rows, bool warningsOn)
        {
            var d = new WarningLevelsWindow(rows, warningsOn);
            SettingsTheme.Apply(d, theme);
            d.SourceInitialized += delegate { SettingsTheme.Apply(d, theme); };
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            bool wasModal = app.ModalOpen;
            app.ModalOpen = true;
            try { return d.ShowDialog() == true ? d.result : null; }
            finally { app.ModalOpen = wasModal; }
        }
    }
}
