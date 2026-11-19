using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Tiny "type a value" dialog used for rename and add-link.</summary>
    internal class InputDialog : Window
    {
        readonly TextBox box;

        InputDialog(string title, string prompt, string initial)
        {
            Title = title;
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6) });
            box = new TextBox { Text = initial ?? "", Padding = new Thickness(4) };
            panel.Children.Add(box);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var ok = new Button { Content = "OK", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            ok.Click += delegate { DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            Content = panel;

            Loaded += delegate { box.Focus(); box.SelectAll(); };
        }

        public static string Ask(Window owner, LauncherApp app, string title, string prompt, string initial)
        {
            var d = new InputDialog(title, prompt, initial);
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            app.ModalOpen = true;
            try { return d.ShowDialog() == true ? d.box.Text : null; }
            finally { app.ModalOpen = false; }
        }
    }
}
