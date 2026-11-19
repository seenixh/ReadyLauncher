using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// Settings for one divider line (direction, length, thickness, color) or text label (text, font, size, bold,
    /// italic, underline, color, shadow, outline, and an optional link). Every change is previewed live in the
    /// launcher; Cancel puts it back.
    /// </summary>
    internal class DecorationDialog : Window
    {
        static readonly string[] Swatches = { "#FFFFFF", "#000000", "#B8A600", "#FFB900", "#E81123", "#1F4E79", "#2E5E3A", "#5B2C6F" };
        static readonly string[] LinkKinds = { "Not a link", "Web address", "Windows command or file", "Key combination" };
        static List<string> fontNames;

        readonly Decoration d;
        readonly Action<Decoration> preview;
        readonly Color autoColor;
        readonly LauncherApp app;
        // Line
        ComboBox direction;
        Slider length, thickness;
        bool lengthTouched, thicknessTouched;
        // Text
        TextBox text;
        ComboBox font;
        Slider size;
        CheckBox bold, italic, underline;
        ComboBox shadow, outline, linkKind;
        TextBox shadowColor, outlineColor, linkTarget;
        StackPanel linkTargetRow, linkKeysRow;
        TextBlock linkKeysText, linkHint;
        ModifierKeysAndKey linkKeys;
        ContentControl sample;
        // Both
        TextBox color;

        struct ModifierKeysAndKey { public System.Windows.Input.ModifierKeys Mods; public System.Windows.Input.Key Key; }

        public static bool Edit(Window owner, LauncherApp app, Decoration d, bool isNew, Color autoColor, Color background, Action<Decoration> preview)
        {
            var dlg = new DecorationDialog(app, d, isNew, autoColor, background, preview);
            SettingsTheme.Use(dlg, app.Data.Settings.SettingsTheme);   // the Settings colors (no bright white checkboxes)
            if (owner != null && owner.IsVisible)
            {
                dlg.Owner = owner;
                dlg.Loaded += delegate { dlg.PlaceBeside(owner); };
            }
            else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            app.ModalOpen = true;
            try { return dlg.ShowDialog() == true; }
            finally { app.ModalOpen = false; }
        }

        DecorationDialog(LauncherApp app, Decoration d, bool isNew, Color autoColor, Color background, Action<Decoration> preview)
        {
            this.app = app;
            this.d = d;
            this.preview = preview;
            this.autoColor = autoColor;
            bool line = d.Kind == DecorKind.Line;
            Title = line ? (isNew ? "Add a divider line" : "Divider line")
                : isNew ? (d.Link != null ? "Add a text link" : "Add text") : "Text";
            Width = 640;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var g = SettingsWindow.Page();
            g.MinHeight = 0;
            if (line) BuildLine(g); else BuildText(g);
            color = SettingsWindow.AddColorRow(g, "Color", d.Color, "Auto", "Match the launcher's text color", Swatches);
            color.TextChanged += delegate { Changed(); };
            if (!line)
            {
                BuildEffects(g);
                BuildLink(g);
                sample = new ContentControl { Focusable = false };
                SettingsWindow.AddRow(g, "Preview", new Border
                {
                    Background = new SolidColorBrush(background), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8),
                    MinHeight = 48, MaxHeight = 170, ClipToBounds = true, Child = sample, Margin = new Thickness(0, 8, 0, 0)
                });
                UpdateSample();
            }
            SettingsWindow.AddRow(g, "", new TextBlock
            {
                Text = line
                    ? "In the launcher: drag the line to move it, drag its end to change its length, double-click it to change these settings, or right-click it for more."
                    : "In the launcher: drag the text to move it, double-click it to change it, or right-click it for more. "
                      + "Text that is a link opens with one click; right-click it to change it.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 10, 0, 0)
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 4, 12, 12) };
            var ok = new Button { Content = isNew ? "Add" : "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 4, 0, 4) };
            ok.Click += delegate { if (Validate()) DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true, Padding = new Thickness(0, 4, 0, 4) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(g);
            Content = root;

            Loaded += delegate
            {
                if (text != null) { text.Focus(); text.SelectAll(); }
                else direction.Focus();
            };
        }

        void BuildLine(Grid g)
        {
            direction = SettingsWindow.Combo(d.Vertical ? 1 : 0, "Horizontal (across)", "Vertical (up and down)");
            SettingsWindow.AddRow(g, "Direction", direction);
            direction.SelectionChanged += delegate { Changed(); };
            length = SettingsWindow.AddSliderRow(g, "Length", (int)Math.Round(d.Length), 10, 2000, " px");
            length.ValueChanged += delegate { lengthTouched = true; Changed(); };
            thickness = SettingsWindow.AddSliderRow(g, "Thickness", (int)Math.Round(d.Thickness), 1, 30, " px");
            thickness.ValueChanged += delegate { thicknessTouched = true; Changed(); };
        }

        void BuildText(Grid g)
        {
            text = new TextBox
            {
                Text = d.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 52, MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(4)
            };
            SettingsWindow.AddRow(g, "Text", text);
            text.TextChanged += delegate { Changed(); };

            // Each font name is shown in its own font.
            var item = new FrameworkElementFactory(typeof(TextBlock));
            item.SetBinding(TextBlock.TextProperty, new Binding());
            item.SetBinding(TextBlock.FontFamilyProperty, new Binding());
            item.SetValue(TextBlock.FontSizeProperty, 14.0);
            font = new ComboBox
            {
                Padding = new Thickness(6, 3, 6, 3), MaxDropDownHeight = 420,
                ItemTemplate = new DataTemplate { VisualTree = item },
                ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)))
            };
            string current = string.IsNullOrWhiteSpace(d.Font) ? "Segoe UI" : d.Font;
            var names = FontNames();
            if (!names.Any(n => string.Equals(n, current, StringComparison.OrdinalIgnoreCase))) font.Items.Add(current);
            foreach (var n in names) font.Items.Add(n);
            font.SelectedItem = font.Items.Cast<string>().First(n => string.Equals(n, current, StringComparison.OrdinalIgnoreCase));
            font.SelectionChanged += delegate { Changed(); };
            SettingsWindow.AddRow(g, "Font", font);

            size = SettingsWindow.AddSliderRow(g, "Size", (int)Math.Round(d.FontSize), 8, 96, "");
            size.TickFrequency = 1;
            size.ValueChanged += delegate { Changed(); };

            bold = new CheckBox { Content = "Bold", IsChecked = d.Bold, FontWeight = FontWeights.Bold };
            italic = new CheckBox { Content = "Italic", IsChecked = d.Italic, FontStyle = FontStyles.Italic, Margin = new Thickness(18, 0, 0, 0) };
            underline = new CheckBox { Content = new TextBlock { Text = "Underline", TextDecorations = TextDecorations.Underline }, IsChecked = d.Underline, Margin = new Thickness(18, 0, 0, 0) };
            bold.Click += delegate { Changed(); };
            italic.Click += delegate { Changed(); };
            underline.Click += delegate { Changed(); };
            var style = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 9, 0, 3) };
            style.Children.Add(bold);
            style.Children.Add(italic);
            style.Children.Add(underline);
            SettingsWindow.AddRow(g, "Style", style);
        }

        /// <summary>Drop shadow and outline, each with its own color.</summary>
        void BuildEffects(Grid g)
        {
            var shadowRow = new StackPanel { Orientation = Orientation.Horizontal };
            shadow = SettingsWindow.Combo(d.Shadow, TextStyle.ShadowNames);
            shadow.Width = 120;
            shadowRow.Children.Add(shadow);
            shadowColor = SettingsWindow.CompactColor(shadowRow, d.ShadowColor, "Shadow color");
            SettingsWindow.AddRow(g, "Drop shadow", shadowRow);

            var outlineRow = new StackPanel { Orientation = Orientation.Horizontal };
            outline = SettingsWindow.Combo(d.Outline, TextStyle.OutlineNames);
            outline.Width = 120;
            outlineRow.Children.Add(outline);
            outlineColor = SettingsWindow.CompactColor(outlineRow, d.OutlineColor, "Outline color");
            SettingsWindow.AddRow(g, "Outline", outlineRow);

            shadow.SelectionChanged += delegate { Changed(); };
            outline.SelectionChanged += delegate { Changed(); };
            shadowColor.TextChanged += delegate { Changed(); };
            outlineColor.TextChanged += delegate { Changed(); };
        }

        /// <summary>Optionally make the text open a web address, run a command or press keys when clicked.</summary>
        void BuildLink(Grid g)
        {
            var link = d.Link;
            int kind = link == null ? 0 : link.Kind == ItemKind.Link ? 1 : link.Kind == ItemKind.Keys ? 3 : 2;
            linkKind = SettingsWindow.Combo(kind, LinkKinds);
            SettingsWindow.AddRow(g, "When clicked", linkKind);

            linkTarget = new TextBox { Width = 330, Padding = new Thickness(3), VerticalContentAlignment = VerticalAlignment.Center };
            if (kind == 1) linkTarget.Text = link.Path;
            else if (kind == 2) linkTarget.Text = JoinCommand(link.Path, link.Arguments);
            var browse = new Button { Content = "Browse...", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(6, 0, 0, 0) };
            browse.Click += delegate
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Choose a program or file to open", DereferenceLinks = false, Filter = "Programs and files|*.*" };
                if (dlg.ShowDialog(this) == true) linkTarget.Text = JoinCommand(dlg.FileName, "");
            };
            linkTargetRow = new StackPanel { Orientation = Orientation.Horizontal };
            linkTargetRow.Children.Add(linkTarget);
            linkTargetRow.Children.Add(browse);
            SettingsWindow.AddRow(g, "", linkTargetRow);

            if (kind == 3) linkKeys = new ModifierKeysAndKey { Mods = link.KeyMods, Key = link.KeyKey };
            linkKeysText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), FontWeight = FontWeights.SemiBold };
            var setKeys = new Button { Content = "Choose keys...", Padding = new Thickness(8, 1, 8, 1) };
            setKeys.Click += delegate
            {
                string name = text.Text;
                var mods = linkKeys.Mods;
                var key = linkKeys.Key;
                if (!KeyShortcutDialog.Edit(this, app, ref name, ref mods, ref key)) return;
                linkKeys = new ModifierKeysAndKey { Mods = mods, Key = key };
                ShowLinkState();
            };
            linkKeysRow = new StackPanel { Orientation = Orientation.Horizontal };
            linkKeysRow.Children.Add(setKeys);
            linkKeysRow.Children.Add(linkKeysText);
            SettingsWindow.AddRow(g, "", linkKeysRow);

            linkHint = new TextBlock { FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            SettingsWindow.AddRow(g, "", linkHint);

            linkKind.SelectionChanged += delegate { ShowLinkState(); };
            ShowLinkState();
        }

        void ShowLinkState()
        {
            int k = linkKind.SelectedIndex;
            linkTargetRow.Visibility = k == 1 || k == 2 ? Visibility.Visible : Visibility.Collapsed;
            ((FrameworkElement)linkTargetRow.Children[1]).Visibility = k == 2 ? Visibility.Visible : Visibility.Collapsed;
            linkKeysRow.Visibility = k == 3 ? Visibility.Visible : Visibility.Collapsed;
            linkKeysText.Text = linkKeys.Key == System.Windows.Input.Key.None ? "(none chosen yet)" : AppSettings.HotkeyText(linkKeys.Mods, linkKeys.Key);
            linkHint.Text = k == 1 ? "For example https://www.wikipedia.org"
                : k == 2 ? "A program, file, folder or command, for example  notepad,  \"C:\\Reports\\May.xlsx\",  control  or  ms-settings:display"
                : k == 3 ? "Clicking the text sends these keys to the app you were using, like a keyboard shortcut icon."
                : "Plain text. Choose an option above to make the text open something when clicked.";
        }

        /// <summary>Split a typed command into the program/file and its arguments.</summary>
        static void SplitCommand(string cmd, out string file, out string args)
        {
            cmd = (cmd ?? "").Trim();
            file = cmd; args = "";
            if (cmd.StartsWith("\""))
            {
                int end = cmd.IndexOf('"', 1);
                if (end > 0) { file = cmd.Substring(1, end - 1); args = cmd.Substring(end + 1).Trim(); }
                return;
            }
            if (System.IO.File.Exists(cmd) || System.IO.Directory.Exists(cmd) || ShellHelper.IsShellPath(cmd)) return;   // a path with spaces, unquoted
            int sp = cmd.IndexOf(' ');
            if (sp > 0) { file = cmd.Substring(0, sp); args = cmd.Substring(sp + 1).Trim(); }
        }

        static string JoinCommand(string file, string args)
        {
            file = file ?? "";
            string f = file.Contains(" ") && !ShellHelper.IsShellPath(file) ? "\"" + file + "\"" : file;
            return string.IsNullOrEmpty(args) ? f : f + " " + args;
        }

        LauncherItem ReadLink()
        {
            switch (linkKind.SelectedIndex)
            {
                case 1:
                    string url = linkTarget.Text.Trim();
                    if (url.Length > 0 && !url.Contains("://")) url = "https://" + url;
                    return new LauncherItem { Kind = ItemKind.Link, Name = text.Text, Path = url };
                case 2:
                    string file, args;
                    SplitCommand(linkTarget.Text, out file, out args);
                    return new LauncherItem { Kind = ItemKind.App, Name = text.Text, Path = file, Arguments = args };
                case 3:
                    return new LauncherItem { Kind = ItemKind.Keys, Name = text.Text, KeyMods = linkKeys.Mods, KeyKey = linkKeys.Key };
                default:
                    return null;
            }
        }

        static List<string> FontNames()
        {
            if (fontNames == null)
                fontNames = Fonts.SystemFontFamilies.Select(f => f.Source)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
            return fontNames;
        }

        void ReadInto()
        {
            if (d.Kind == DecorKind.Line)
            {
                d.Vertical = direction.SelectedIndex == 1;
                if (lengthTouched) d.Length = length.Value;
                if (thicknessTouched) d.Thickness = thickness.Value;
            }
            else
            {
                d.Text = text.Text;
                d.Font = font.SelectedItem as string ?? d.Font;
                d.FontSize = size.Value;
                d.Bold = bold.IsChecked == true;
                d.Italic = italic.IsChecked == true;
                d.Underline = underline.IsChecked == true;
                if (shadow != null)
                {
                    d.Shadow = Math.Max(0, shadow.SelectedIndex);
                    d.Outline = Math.Max(0, outline.SelectedIndex);
                    if (SettingsWindow.ValidColor(shadowColor.Text)) d.ShadowColor = shadowColor.Text.Trim();
                    if (SettingsWindow.ValidColor(outlineColor.Text)) d.OutlineColor = outlineColor.Text.Trim();
                }
                if (linkKind != null) d.Link = ReadLink();
            }
            string c = color.Text.Trim();
            if (c.Length == 0 || SettingsWindow.ValidColor(c)) d.Color = c;   // keep the last good color while one is being typed
        }

        void Changed()
        {
            if (color == null) return;   // still being built
            ReadInto();
            UpdateSample();
            if (preview != null) preview(d);
        }

        void UpdateSample()
        {
            if (sample == null) return;
            bool empty = string.IsNullOrEmpty(d.Text);
            var shown = d.Clone();
            if (empty) shown.Text = "Your text";
            double ppd = 1;
            try { ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch (Exception) { }
            var body = TextStyle.Build(shown, autoColor, ppd);
            body.Opacity = empty ? 0.5 : 1;
            body.Margin = new Thickness(4);   // room for the shadow and outline
            sample.Content = body;
        }

        bool Validate()
        {
            string c = color.Text.Trim();
            if (c.Length > 0 && !SettingsWindow.ValidColor(c))
            {
                MessageBox.Show(this, "The color should look like #FFFFFF, or click Auto.", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (text != null && string.IsNullOrWhiteSpace(text.Text))
            {
                MessageBox.Show(this, "Type the text you want to show.", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                text.Focus();
                return false;
            }
            if (shadow != null && (!SettingsWindow.ValidColor(shadowColor.Text) || !SettingsWindow.ValidColor(outlineColor.Text)))
            {
                MessageBox.Show(this, "The shadow and outline colors should look like #000000.", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            if (linkKind != null)
            {
                int k = linkKind.SelectedIndex;
                string target = linkTarget.Text.Trim();
                string problem = null;
                if (k == 1 && !ShellHelper.IsUrl(target.Contains("://") ? target : "https://" + target)) problem = "Type the web address the text should open, for example https://www.wikipedia.org";
                else if (k == 2 && target.Length == 0) problem = "Type the program, file or command the text should open, or click Browse.";
                else if (k == 3 && linkKeys.Key == System.Windows.Input.Key.None) problem = "Click \"Choose keys...\" to pick the keys the text should press.";
                if (problem != null)
                {
                    MessageBox.Show(this, problem, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
            }
            ReadInto();
            return true;
        }

        /// <summary>Sit beside the launcher (not on top of it) so the live preview stays visible.</summary>
        void PlaceBeside(Window owner)
        {
            double w = ActualWidth, h = ActualHeight;
            double screenL = SystemParameters.VirtualScreenLeft, screenT = SystemParameters.VirtualScreenTop;
            double screenR = screenL + SystemParameters.VirtualScreenWidth, screenB = screenT + SystemParameters.VirtualScreenHeight;
            double top = Math.Max(screenT, Math.Min(owner.Top + 40, screenB - h));
            if (owner.Left + owner.ActualWidth + w <= screenR) { Left = owner.Left + owner.ActualWidth; Top = top; }
            else if (owner.Left - w >= screenL) { Left = owner.Left - w; Top = top; }
            else
            {
                Left = owner.Left + (owner.ActualWidth - w) / 2;
                Top = Math.Max(screenT, Math.Min(owner.Top + owner.ActualHeight - h - 20, screenB - h));
            }
        }
    }
}
