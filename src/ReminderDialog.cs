using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>A lamp strip reminder's date, time and text: a new one, or one you have (which can be deleted here too).</summary>
    internal class ReminderDialog : Window
    {
        internal enum Answer { Cancel, Keep, Delete }

        readonly ComboBox month, day, year, time;
        readonly TextBox text;
        readonly Button ok;
        readonly DateTime was;        // a time that isn't on the half hour is kept, as its own choice
        readonly bool ownTime;
        Answer answer = Answer.Cancel;

        /// <summary>When a new reminder is due unless you choose something else: tomorrow, 9:00 AM.</summary>
        public static DateTime UsualTime { get { return DateTime.Today.AddDays(1).AddHours(9); } }

        ReminderDialog(LampReminder r, bool isNew)
        {
            was = r.At;
            Title = isNew ? "Add a reminder" : "Reminder";
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            var c = CultureInfo.CurrentCulture;
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = "Text to flash on the lamp strip:", Margin = new Thickness(0, 0, 0, 6) });
            text = new TextBox { Text = r.Text ?? "", MaxLength = 200, Padding = new Thickness(4) };
            panel.Children.Add(text);

            panel.Children.Add(new TextBlock { Text = "Starting on:", Margin = new Thickness(0, 12, 0, 6) });
            month = SettingsWindow.Combo(was.Month - 1, c.DateTimeFormat.MonthNames.Take(12).ToArray());
            month.Width = 116;
            day = SettingsWindow.Combo(was.Day - 1, Enumerable.Range(1, 31).Select(d => d.ToString(c)).ToArray());
            day.Width = 58;
            day.Margin = new Thickness(6, 0, 0, 0);
            int first = Math.Min(DateTime.Today.Year, was.Year), last = Math.Max(DateTime.Today.Year + 5, was.Year);
            year = SettingsWindow.Combo(was.Year - first, Enumerable.Range(first, last - first + 1).Select(y => y.ToString(c)).ToArray());
            year.Width = 74;
            year.Margin = new Thickness(6, 0, 0, 0);
            var times = Enumerable.Range(0, 48).Select(k => DateTime.Today.AddMinutes(30 * k).ToString("t", c)).ToList();
            int half = was.Hour * 2 + was.Minute / 30;
            ownTime = was.TimeOfDay != TimeSpan.FromMinutes(30 * half);
            if (ownTime) times.Insert(0, was.ToString("t", c));
            time = SettingsWindow.Combo(ownTime ? 0 : half, times.ToArray());
            time.Width = 100;
            var when = new StackPanel { Orientation = Orientation.Horizontal };
            when.Children.Add(month);
            when.Children.Add(day);
            when.Children.Add(year);
            when.Children.Add(new TextBlock { Text = "at", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) });
            when.Children.Add(time);
            panel.Children.Add(when);
            panel.Children.Add(new TextBlock
            {
                Text = "From then on the strip flashes it until you right-click the strip and dismiss it.",
                FontSize = 11.5, Opacity = 0.7, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
            if (!isNew)
            {
                var delete = new Button { Content = "Delete", Width = 84, Padding = new Thickness(0, 3, 0, 3), ToolTip = "Delete this reminder" };
                delete.Click += delegate { Close(Answer.Delete); };
                DockPanel.SetDock(delete, Dock.Left);
                buttons.Children.Add(delete);
            }
            var cancel = new Button { Content = "Cancel", Width = 84, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };
            DockPanel.SetDock(cancel, Dock.Right);
            buttons.Children.Add(cancel);
            ok = new Button { Content = isNew ? "Add" : "OK", Width = 84, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            ok.Click += delegate { Close(Answer.Keep); };
            DockPanel.SetDock(ok, Dock.Right);
            buttons.Children.Add(ok);
            panel.Children.Add(buttons);
            Content = panel;

            Action state = () => ok.IsEnabled = text.Text.Trim().Length > 0;
            text.TextChanged += delegate { state(); };
            state();
            Loaded += delegate { text.Focus(); text.SelectAll(); };
        }

        void Close(Answer a)
        {
            answer = a;
            DialogResult = true;
        }

        /// <summary>The date and time chosen; a day the month doesn't have becomes its last day.</summary>
        DateTime Chosen
        {
            get
            {
                int y = int.Parse((string)year.SelectedItem, CultureInfo.CurrentCulture), m = month.SelectedIndex + 1;
                int d = Math.Min(day.SelectedIndex + 1, DateTime.DaysInMonth(y, m)), k = time.SelectedIndex;
                var t = ownTime ? (k <= 0 ? was.TimeOfDay : TimeSpan.FromMinutes(30 * (k - 1))) : TimeSpan.FromMinutes(30 * Math.Max(0, k));
                return new DateTime(y, m, d).Add(t);
            }
        }

        void Take(LampReminder r)
        {
            r.At = Chosen;
            r.Text = text.Text.Trim();
        }

        /// <summary>Asks for the reminder's date, time and text; unless it's canceled or deleted, they're put in it.</summary>
        public static Answer Ask(Window owner, LauncherApp app, LampReminder r, bool isNew)
        {
            var d = new ReminderDialog(r, isNew);
            SettingsTheme.Use(d, app.Data.Settings.SettingsTheme);
            if (owner != null && owner.IsVisible) d.Owner = owner;
            else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            app.ModalOpen = true;
            if (Program.TestMode && TestClick != null) d.ClickInTest();
            try
            {
                if (d.ShowDialog() != true) return Answer.Cancel;
                if (d.answer == Answer.Keep) d.Take(r);
                return d.answer;
            }
            finally { app.ModalOpen = false; }
        }

        /// <summary>Test copies only: the text to type (or null) and the button to click in the next dialog, opened far off screen without the focus.</summary>
        internal static string[] TestClick;

        void ClickInTest()
        {
            var what = TestClick;
            TestClick = null;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Top = -30000;
            var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.5) };
            later.Tick += delegate
            {
                later.Stop();
                if (what[0] != null) text.Text = what[0];
                var b = ((DockPanel)((StackPanel)Content).Children.OfType<DockPanel>().First()).Children.OfType<Button>().FirstOrDefault(x => (string)x.Content == what[1]);
                if (b == null || !b.IsEnabled || b.IsCancel) { DialogResult = false; return; }   // (Cancel and Esc close it the same way)
                b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            later.Start();
        }

        /// <summary>
        /// Test copies only: the dialog for this reminder, never shown: what it starts with, whether Add can be clicked
        /// without text, and what it makes of other choices ("month/day/year/time index", and text).
        /// </summary>
        internal static string Test(LampReminder r, bool isNew, string choices, string words)
        {
            var d = new ReminderDialog(r, isNew);
            Func<string> now = () => "'" + d.text.Text + "' " + d.month.SelectedItem + " " + d.day.SelectedItem + " " + d.year.SelectedItem + " " + d.time.SelectedItem
                + ", '" + d.ok.Content + "' " + (d.ok.IsEnabled ? "on" : "off");
            string log = now();
            var p = choices.Split('/').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            if (p.Length == 4)
            {
                d.month.SelectedIndex = p[0];
                d.day.SelectedIndex = p[1];
                d.year.SelectedIndex = p[2];
                d.time.SelectedIndex = p[3];
            }
            if (words != null) d.text.Text = words;
            log += " -> " + now();
            if (d.ok.IsEnabled) d.Take(r);
            return log + " -> " + r.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " '" + r.Text + "'";
        }

        /// <summary>Test copies only: draws the dialog far off screen, never focused, and saves it as a picture.</summary>
        internal static void SavePicture(LampReminder r, bool isNew, SettingsThemeMode theme, string file)
        {
            var d = new ReminderDialog(r, isNew) { ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -30000, Top = -30000 };
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
