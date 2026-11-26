using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ReadyLauncher
{
    /// <summary>A color wheel (hue around the edge, richness toward the edge) with a brightness slider.</summary>
    internal class ColorPickerDialog : Window
    {
        const int WheelSize = 240;
        double hue, sat = 1, val = 1;
        readonly Image wheel = new Image { Width = WheelSize, Height = WheelSize };
        readonly Ellipse marker = new Ellipse { Width = 14, Height = 14, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        readonly Slider brightness;
        readonly Border before, after;
        readonly TextBox hex;
        bool updating;

        /// <summary>Test copies only: the color to pick instead of showing the wheel ("": as if canceled).</summary>
        internal static string TestPick;

        /// <summary>Returns the chosen color as #RRGGBB, or null if canceled.</summary>
        public static string Pick(Window owner, string current)
        {
            if (Program.TestMode && TestPick != null) return TestPick.Length == 0 ? null : TestPick;
            var dlg = new ColorPickerDialog(current);
            if (owner != null && owner.IsVisible) dlg.Owner = owner; else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return dlg.ShowDialog() == true ? dlg.hex.Text.Trim().ToUpperInvariant() : null;
        }

        ColorPickerDialog(string current)
        {
            Title = "Pick a color";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var start = TextStyle.ParseColor(current, Color.FromRgb(0xB8, 0xA6, 0x00));
            ToHsv(start, out hue, out sat, out val);

            var wheelHost = new Grid { Width = WheelSize, Height = WheelSize, Cursor = Cursors.Cross, Background = Brushes.Transparent };
            var overlay = new Canvas { IsHitTestVisible = false };
            overlay.Children.Add(marker);
            wheelHost.Children.Add(wheel);
            wheelHost.Children.Add(overlay);
            wheelHost.MouseLeftButtonDown += (s, e) => { wheelHost.CaptureMouse(); PickAt(e.GetPosition(wheelHost)); };
            wheelHost.MouseMove += (s, e) => { if (wheelHost.IsMouseCaptured) PickAt(e.GetPosition(wheelHost)); };
            wheelHost.MouseLeftButtonUp += delegate { wheelHost.ReleaseMouseCapture(); };

            brightness = new Slider
            {
                Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, Value = val * 100, Height = WheelSize,
                Margin = new Thickness(16, 0, 0, 0), ToolTip = "Brightness", IsMoveToPointEnabled = true
            };
            brightness.ValueChanged += delegate { if (updating) return; val = brightness.Value / 100; DrawWheel(); Changed(true); };

            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(wheelHost);
            top.Children.Add(brightness);

            before = new Border { Width = 56, Height = 28, Background = new SolidColorBrush(start), ToolTip = "Current color", BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            after = new Border { Width = 56, Height = 28, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), ToolTip = "New color" };
            hex = new TextBox { Width = 90, Padding = new Thickness(3), Margin = new Thickness(14, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
            hex.TextChanged += delegate
            {
                if (updating || !SettingsWindow.ValidColor(hex.Text)) return;
                ToHsv(TextStyle.ParseColor(hex.Text, start), out hue, out sat, out val);
                updating = true; brightness.Value = val * 100; updating = false;
                DrawWheel();
                Changed(false);
            };
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            swatches.Children.Add(before);
            swatches.Children.Add(new TextBlock { Text = "→", Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            swatches.Children.Add(after);
            swatches.Children.Add(hex);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var ok = new Button { Content = "OK", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            ok.Click += delegate { if (SettingsWindow.ValidColor(hex.Text)) DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(top);
            root.Children.Add(swatches);
            root.Children.Add(buttons);
            Content = root;

            DrawWheel();
            Changed(true);
        }

        void PickAt(Point p)
        {
            double r = WheelSize / 2.0, dx = p.X - r, dy = p.Y - r;
            hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360;
            sat = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / r);
            if (val < 0.05) { val = 1; updating = true; brightness.Value = 100; updating = false; DrawWheel(); }
            Changed(true);
        }

        /// <param name="writeHex">false while the hex box is the one being typed in.</param>
        void Changed(bool writeHex)
        {
            var c = FromHsv(hue, sat, val);
            after.Background = new SolidColorBrush(c);
            double r = WheelSize / 2.0, a = hue * Math.PI / 180;
            Canvas.SetLeft(marker, r + Math.Cos(a) * sat * r - 7);
            Canvas.SetTop(marker, r + Math.Sin(a) * sat * r - 7);
            marker.Stroke = val > 0.6 && sat < 0.35 ? Brushes.Black : Brushes.White;
            if (writeHex) { updating = true; hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); updating = false; }
        }

        void DrawWheel()
        {
            int n = WheelSize;
            var px = new byte[n * n * 4];
            double r = n / 2.0;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double dx = x + 0.5 - r, dy = y + 0.5 - r, d = Math.Sqrt(dx * dx + dy * dy);
                    double alpha = Math.Max(0, Math.Min(1, r - d));   // soft edge
                    if (alpha <= 0) continue;
                    var c = FromHsv((Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360, Math.Min(1, d / r), val);
                    int i = (y * n + x) * 4;
                    px[i] = (byte)(c.B * alpha); px[i + 1] = (byte)(c.G * alpha); px[i + 2] = (byte)(c.R * alpha); px[i + 3] = (byte)(255 * alpha);
                }
            wheel.Source = BitmapSource.Create(n, n, 96, 96, PixelFormats.Pbgra32, null, px, n * 4);
        }

        static Color FromHsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            v = max;
            s = max == 0 ? 0 : d / max;
            if (d == 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }
    }
}
