using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Tiny "type a value" dialog used for rename and add-link.</summary>
    internal class InputDialog : Window
    {
        readonly TextBox box;

        /// <param name="problem">What was wrong with what was typed last time (shown in red above the box), or null.</param>
        InputDialog(string title, string prompt, string initial, string problem = null)
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
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });   // a long one goes on to more lines
            if (problem != null)
                panel.Children.Add(new TextBlock { Text = problem, Margin = new Thickness(0, 2, 0, 6), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold,
                                                   Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x2B)) });
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

        /// <summary>Test copies only: what to answer instead of showing the window, one after another ("&lt;cancel&gt;": as if canceled).</summary>
        internal static readonly System.Collections.Generic.Queue<string> TestAnswers = new System.Collections.Generic.Queue<string>();
        internal static string TestAsked;

        public static string Ask(Window owner, LauncherApp app, string title, string prompt, string initial, string problem = null)
        {
            if (Program.TestMode && TestAnswers.Count > 0)
            {
                TestAsked = title + " | " + prompt + " | " + initial + (problem != null ? " | problem: " + problem : "");
                string a = TestAnswers.Dequeue();
                return a == "<cancel>" ? null : a;
            }
            var d = new InputDialog(title, prompt, initial, problem);
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            bool was = app.ModalOpen;   // (Settings may be open, and stays so)
            app.ModalOpen = true;
            try { return d.ShowDialog() == true ? d.box.Text : null; }
            finally { app.ModalOpen = was; }
        }

        /// <summary>Test copies only: draws the window far off screen, never focused, and saves it as a picture.</summary>
        internal static void SavePicture(string title, string prompt, string initial, string problem, string file)
        {
            var d = new InputDialog(title, prompt, initial, problem) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
            d.Show();
            var later = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(1) };
            later.Tick += delegate
            {
                later.Stop();
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)System.Math.Ceiling(d.ActualWidth), (int)System.Math.Ceiling(d.ActualHeight), 96, 96, PixelFormats.Pbgra32);
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
