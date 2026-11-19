using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Records a name and a key combination for a "press these keys" shortcut.</summary>
    internal class KeyShortcutDialog : Window
    {
        readonly TextBox nameBox, comboBox;
        readonly TextBlock hint;
        readonly Button ok;
        ModifierKeys mods;
        Key key;
        bool nameTouched;

        KeyShortcutDialog(string name, ModifierKeys m, Key k)
        {
            mods = m;
            key = k;
            Title = "Keyboard shortcut";
            Width = 440;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "Keys to press (click the box, then press the combination):", Margin = new Thickness(0, 0, 0, 6) });
            comboBox = new TextBox { IsReadOnly = true, Padding = new Thickness(4), FontSize = 15, Cursor = Cursors.Arrow };
            comboBox.Text = key == Key.None ? "" : AppSettings.HotkeyText(mods, key);
            comboBox.PreviewKeyDown += OnKey;
            panel.Children.Add(comboBox);

            var modRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            modRow.Children.Add(new TextBlock { Text = "Hold:", Margin = new Thickness(0, 0, 8, 0), Opacity = 0.8 });
            modChecks = new[]
            {
                ModCheck("Ctrl", ModifierKeys.Control), ModCheck("Alt", ModifierKeys.Alt),
                ModCheck("Shift", ModifierKeys.Shift), ModCheck("Win", ModifierKeys.Windows)
            };
            foreach (var c in modChecks) modRow.Children.Add(c);
            panel.Children.Add(modRow);

            hint = new TextBlock
            {
                Text = "For Windows-key shortcuts such as Win + D, press just the letter and tick Win (Windows grabs those keys first). "
                     + "Clicking the icon sends the keys to the window you were using before you opened the launcher.",
                FontSize = 11.5, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12)
            };
            panel.Children.Add(hint);

            panel.Children.Add(new TextBlock { Text = "Name to show under the icon:", Margin = new Thickness(0, 0, 0, 6) });
            nameBox = new TextBox { Text = name ?? "", Padding = new Thickness(4) };
            nameBox.TextChanged += delegate { if (nameBox.IsKeyboardFocused) nameTouched = true; };
            nameTouched = !string.IsNullOrEmpty(name);
            panel.Children.Add(nameBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            ok = new Button { Content = "OK", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3), IsEnabled = key != Key.None };
            ok.Click += delegate { DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;
            Loaded += delegate { comboBox.Focus(); };
        }

        readonly CheckBox[] modChecks;
        bool syncing;

        CheckBox ModCheck(string text, ModifierKeys flag)
        {
            var c = new CheckBox { Content = text, Tag = flag, IsChecked = (mods & flag) != 0, Margin = new Thickness(0, 0, 12, 0) };
            c.Click += delegate
            {
                if (syncing) return;
                mods = c.IsChecked == true ? mods | flag : mods & ~flag;
                if (key != Key.None) ShowCombo();
            };
            return c;
        }

        void ShowCombo()
        {
            comboBox.Text = AppSettings.HotkeyText(mods, key);
            ok.IsEnabled = key != Key.None;
            if (!nameTouched) nameBox.Text = comboBox.Text;
            syncing = true;
            foreach (var c in modChecks) c.IsChecked = (mods & (ModifierKeys)c.Tag) != 0;
            syncing = false;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            // Tab / Enter / Esc keep their normal dialog meaning.
            if (Keyboard.Modifiers == ModifierKeys.None && (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)) return;
            e.Handled = true;
            ModifierKeys m;
            Key k;
            if (!KeySender.TryRead(e, out m, out k)) return;
            mods = m;
            key = k;
            ShowCombo();
        }

        /// <summary>Shows the dialog; returns false if canceled. Updates name/mods/key on OK.</summary>
        public static bool Edit(Window owner, LauncherApp app, ref string name, ref ModifierKeys mods, ref Key key)
        {
            var d = new KeyShortcutDialog(name, mods, key);
            SettingsTheme.Use(d, app.Data.Settings.SettingsTheme);   // the Settings colors (no bright white checkboxes)
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            app.ModalOpen = true;
            try
            {
                if (d.ShowDialog() != true) return false;
            }
            finally { app.ModalOpen = false; }
            mods = d.mods;
            key = d.key;
            name = string.IsNullOrWhiteSpace(d.nameBox.Text) ? d.comboBox.Text : d.nameBox.Text.Trim();
            return true;
        }
    }
}
