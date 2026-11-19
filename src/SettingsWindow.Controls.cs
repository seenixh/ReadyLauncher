using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Small builders for the settings pages (rows, sliders, color and picture pickers).</summary>
    internal partial class SettingsWindow
    {
        static readonly string[] Presets = { "#B8A600", "#202020", "#F3F3F3", "#1F4E79", "#2E5E3A", "#5B2C6F", "#8B2E2E" };

        internal static Grid Page()
        {
            var g = new Grid { Margin = new Thickness(14, 10, 14, 12), MinHeight = 400 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return g;
        }

        internal static void AddRow(Grid g, string label, UIElement control)
        {
            int row = g.RowDefinitions.Count;
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (!string.IsNullOrEmpty(label))
            {
                var t = new TextBlock { Text = label, Margin = new Thickness(0, 9, 16, 0), VerticalAlignment = VerticalAlignment.Top };
                Grid.SetRow(t, row);
                g.Children.Add(t);
            }
            var fe = control as FrameworkElement;
            if (fe != null && fe.Margin == default(Thickness)) fe.Margin = new Thickness(0, 6, 0, 0);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            g.Children.Add(control);
        }

        internal static ComboBox Combo(int selected, params string[] items)
        {
            var c = new ComboBox { Padding = new Thickness(6, 4, 6, 4) };
            foreach (var i in items) c.Items.Add(i);
            c.SelectedIndex = Math.Max(0, Math.Min(selected, items.Length - 1));
            return c;
        }

        internal static CheckBox Check(string text, bool value)
        {
            return new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 3, 0, 3) };
        }

        internal static Slider AddSliderRow(Grid g, string label, int value, int min, int max, string suffix)
        {
            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)),
                IsSnapToTickEnabled = true, TickFrequency = max - min > 40 ? 5 : 1,
                SmallChange = 1, LargeChange = 10, Width = 250, VerticalAlignment = VerticalAlignment.Center
            };
            var text = new TextBlock { Width = 48, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Text = (int)slider.Value + suffix;
            slider.ValueChanged += delegate { text.Text = (int)slider.Value + suffix; };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(slider);
            row.Children.Add(text);
            AddRow(g, label, row);
            return slider;
        }

        internal static bool ValidColor(string s)
        {
            try { ColorConverter.ConvertFromString(s.Trim()); return true; }
            catch (Exception) { return false; }
        }

        static TextBox AddColorRow(Grid g, string label, string value, bool allowNone)
        {
            return AddColorRow(g, label, value, allowNone ? "None" : null, "No color", null);
        }

        /// <summary>
        /// Preset swatches + a hex box. With <paramref name="emptyLabel"/>, a button empties the box
        /// (meaning "no color" or "automatic", as the caller decides).
        /// </summary>
        internal static TextBox AddColorRow(Grid g, string label, string value, string emptyLabel, string emptyTip, string[] presets)
        {
            var box = new TextBox { Text = value ?? "", Width = 80, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) };
            var swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            Action update = () =>
            {
                swatch.Background = ValidColor(box.Text) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(box.Text.Trim())) : (Brush)Brushes.Transparent;
            };
            box.TextChanged += delegate { update(); };
            update();

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var p in presets ?? Presets)
            {
                string hex = p;
                var b = new Border
                {
                    Width = 22, Height = 22, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 4, 0), Cursor = Cursors.Hand,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), ToolTip = hex
                };
                b.MouseLeftButtonUp += delegate { box.Text = hex; };
                b.IsEnabledChanged += delegate { b.Opacity = b.IsEnabled ? 1 : 0.35; };   // a switched-off row fades its presets too
                row.Children.Add(b);
            }
            row.Children.Add(box);
            row.Children.Add(swatch);
            row.Children.Add(WheelButton(box));
            if (emptyLabel != null)
            {
                var none = new Button { Content = emptyLabel, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0), ToolTip = emptyTip };
                none.Click += delegate { box.Text = ""; };
                row.Children.Add(none);
            }
            AddRow(g, label, row);
            return box;
        }

        /// <summary>A button that opens the color wheel and puts the chosen color in <paramref name="box"/>.</summary>
        internal static Button WheelButton(TextBox box)
        {
            var b = new Button
            {
                Content = new TextBlock { Text = "", FontFamily = TileView.IconFont, FontSize = 15 },
                Width = 30, Height = 26, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0),
                ToolTip = "Pick any color from a color wheel"
            };
            b.Click += delegate
            {
                string picked = ColorPickerDialog.Pick(Window.GetWindow(b), ValidColor(box.Text) ? box.Text : "#808080");
                if (picked != null) box.Text = picked;
            };
            return b;
        }

        /// <summary>A small hex box + swatch + color wheel button, added to <paramref name="row"/>.</summary>
        internal static TextBox CompactColor(Panel row, string value, string tip)
        {
            var box = new TextBox { Text = value ?? "", Width = 76, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 6, 0), ToolTip = tip };
            var swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            Action update = () =>
            {
                swatch.Background = ValidColor(box.Text) ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(box.Text.Trim())) : (Brush)Brushes.Transparent;
            };
            box.TextChanged += delegate { update(); };
            update();
            row.Children.Add(box);
            row.Children.Add(swatch);
            row.Children.Add(WheelButton(box));
            return box;
        }

        /// <summary>A picture setting: where the picture is, and whether it's switched off (the location is kept either way).</summary>
        internal class ImageRow
        {
            public TextBox Box;
            public bool Off;
            public Action Refresh;   // show Box and Off as they are now (after a theme changed them)
        }

        ImageRow AddImageRow(Grid g, string label, string value, bool off)
        {
            var result = new ImageRow { Off = off };
            var box = new TextBox { Text = value ?? "", Width = 250, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };
            result.Box = box;
            var toggle = new Button { Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0), MinWidth = 64 };
            Action show = () =>
            {
                toggle.Content = result.Off ? "Enable" : "Disable";
                toggle.ToolTip = result.Off ? "Use this picture again" : "Turn the picture off, but keep it chosen for later";
                box.Opacity = result.Off ? 0.5 : 1;   // greyed out while switched off
                toggle.IsEnabled = box.Text.Trim().Length > 0;
            };
            toggle.Click += delegate { result.Off = !result.Off; show(); };
            box.TextChanged += delegate { show(); };
            result.Refresh = show;
            var browse = new Button { Content = "Browse...", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += delegate
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Choose a picture",
                    Filter = "Pictures|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.jfif;*.webp|All files|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };
                if (dlg.ShowDialog(this) == true)
                {
                    box.Text = dlg.FileName;
                    result.Off = false;   // a newly chosen picture is meant to be seen
                    show();
                }
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(box);
            row.Children.Add(browse);
            row.Children.Add(toggle);
            AddRow(g, label, row);
            show();
            return result;
        }
    }
}
