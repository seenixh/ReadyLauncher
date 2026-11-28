using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Asks before something is deleted (a custom lamp color, showing it; a saved number pattern or lamp activity; the picked numbers).</summary>
    internal class DeleteDialog : Window
    {
        /// <summary>Test copies only: the answer to give instead of showing the window, and what it was asked about.</summary>
        internal static bool? TestAnswer;
        internal static string TestAsked;

        DeleteDialog(string title, string question, string what, string note, Color? swatch)
        {
            Title = title;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(18, 16, 18, 16), MinWidth = 300 };
            var line = new StackPanel { Orientation = Orientation.Horizontal };
            if (swatch.HasValue)
                line.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 30, Height = 30, Fill = new SolidColorBrush(swatch.Value), Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), StrokeThickness = 1,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0)
                });
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            words.Children.Add(new TextBlock { Text = question, FontSize = 14, FontWeight = FontWeights.SemiBold });
            words.Children.Add(new TextBlock { Text = what, Opacity = 0.75, Margin = new Thickness(0, 2, 0, 0), MaxWidth = 340, TextWrapping = TextWrapping.Wrap });
            line.Children.Add(words);
            panel.Children.Add(line);
            panel.Children.Add(new TextBlock
            {
                Text = note, FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 300, HorizontalAlignment = HorizontalAlignment.Left
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            var delete = new Button { Content = "Delete", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            delete.Click += delegate { DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            buttons.Children.Add(delete);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
        }

        static DeleteDialog ForColor(string hex, Color swatch)
        {
            return new DeleteDialog("Delete a custom color", "Delete this custom color?", hex, "It comes off your list of lamp colors. You can add it again with the color wheel.", swatch);
        }

        static DeleteDialog ForPattern(string name, string pattern)
        {
            return new DeleteDialog("Delete a saved number pattern", "Delete this saved number pattern?", name == pattern ? pattern : name + ": " + pattern,
                "It comes off your list of saved patterns. The pattern the strip picks numbers with now stays as it is.", null);
        }

        static DeleteDialog ForPicks(int count)
        {
            return new DeleteDialog("Clear the picked numbers", "Clear the list of picked numbers?", count == 1 ? "1 pick on the list" : count + " picks on the list",
                "They come off the list for good. Your saved patterns, and numbers on the strip now, stay as they are.", null);
        }

        /// <summary>Whether to clear the list of the <paramref name="count"/> numbers picked.</summary>
        public static bool AskPicks(Window owner, SettingsThemeMode theme, int count)
        {
            if (Program.TestMode && TestAnswer != null) { TestAsked = count + " picks"; return TestAnswer.Value; }
            return Show(ForPicks(count), owner, theme);
        }

        /// <summary>Test copies only: the same for clearing the picked numbers.</summary>
        internal static void SavePicksPicture(int count, SettingsThemeMode theme, string file)
        {
            SavePicture(ForPicks(count), theme, file);
        }

        static DeleteDialog ForActivity(string name, string summary)
        {
            return new DeleteDialog("Delete a saved lamp activity", "Delete this saved lamp activity?", name + ". " + summary,
                "It comes off your list of saved activities. The lamps keep the activity they have now.", null);
        }

        /// <summary>Whether to delete the saved custom lamp activity called <paramref name="name"/>.</summary>
        public static bool AskActivity(Window owner, SettingsThemeMode theme, string name, string summary)
        {
            if (Program.TestMode && TestAnswer != null) { TestAsked = name; return TestAnswer.Value; }
            return Show(ForActivity(name, summary), owner, theme);
        }

        /// <summary>Test copies only: the same for a saved lamp activity.</summary>
        internal static void SaveActivityPicture(string name, string summary, SettingsThemeMode theme, string file)
        {
            SavePicture(ForActivity(name, summary), theme, file);
        }

        static DeleteDialog ForColorSet(string name, string colors)
        {
            return new DeleteDialog("Delete saved lamp colors", "Delete these saved lamp colors?", name + ": " + colors,
                "They come off your list of saved colors. The lamps keep the colors they have now.", null);
        }

        /// <summary>Whether to delete the lamp colors saved as <paramref name="name"/>.</summary>
        public static bool AskColorSet(Window owner, SettingsThemeMode theme, string name, string colors)
        {
            if (Program.TestMode && TestAnswer != null) { TestAsked = name; return TestAnswer.Value; }
            return Show(ForColorSet(name, colors), owner, theme);
        }

        /// <summary>Whether to delete the custom color <paramref name="hex"/> (shown as <paramref name="swatch"/>).</summary>
        public static bool Ask(Window owner, SettingsThemeMode theme, string hex, Color swatch)
        {
            if (Program.TestMode && TestAnswer != null) { TestAsked = hex; return TestAnswer.Value; }
            return Show(ForColor(hex, swatch), owner, theme);
        }

        /// <summary>Whether to delete the saved number pattern called <paramref name="name"/>.</summary>
        public static bool AskPattern(Window owner, SettingsThemeMode theme, string name, string pattern)
        {
            if (Program.TestMode && TestAnswer != null) { TestAsked = name; return TestAnswer.Value; }
            return Show(ForPattern(name, pattern), owner, theme);
        }

        static bool Show(DeleteDialog d, Window owner, SettingsThemeMode theme)
        {
            SettingsTheme.Use(d, theme);
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return d.ShowDialog() == true;
        }

        /// <summary>Test copies only: draws the color's window far off screen, never focused, and saves it as a picture.</summary>
        internal static void SavePicture(string hex, Color swatch, SettingsThemeMode theme, string file)
        {
            SavePicture(ForColor(hex, swatch), theme, file);
        }

        /// <summary>Test copies only: the same for a saved number pattern.</summary>
        internal static void SavePatternPicture(string name, string pattern, SettingsThemeMode theme, string file)
        {
            SavePicture(ForPattern(name, pattern), theme, file);
        }

        static void SavePicture(DeleteDialog d, SettingsThemeMode theme, string file)
        {
            d.ShowActivated = false;
            d.WindowStartupLocation = WindowStartupLocation.Manual;
            d.Left = d.Top = -30000;
            SettingsTheme.Use(d, theme);
            d.Show();
            var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            later.Tick += delegate
            {
                later.Stop();
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(d.ActualWidth), (int)Math.Ceiling(d.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                rtb.Render(d);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = System.IO.File.Create(file)) enc.Save(fs);
                d.Close();
            };
            later.Start();
        }
    }
}
