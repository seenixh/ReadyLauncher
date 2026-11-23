using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>The blinking lamp strip in its own window, right against the launcher's top or bottom edge.</summary>
    internal partial class LauncherWindow
    {
        LampWindow lampWindow;

        bool LampAbove { get { return S.LampPanel == LampPanelPlace.AboveLauncher; } }
        bool LampBelow { get { return S.LampPanel == LampPanelPlace.BelowLauncher; } }
        CornerRadius panelCorners = new CornerRadius(Corner);

        /// <summary>
        /// The launcher panel's corners: rounded, except where the lamp strip sits right against it, so the two meet
        /// in a clean straight line with no notch at either end.
        /// </summary>
        void ApplyPanelCorners()
        {
            double top = LampAbove ? 0 : Corner, bottom = LampBelow ? 0 : Corner;
            panelCorners = new CornerRadius(top, top, bottom, bottom);
            shadowBorder.CornerRadius = panelCorners;
            bgColor.CornerRadius = panelCorners;
            bgImage.CornerRadius = panelCorners;
            frame.CornerRadius = panelCorners;
            ClipContent();
        }

        /// <summary>Clip the icons and strips to the inside of the panel's frame.</summary>
        void ClipContent()
        {
            var c = panelCorners;
            contentGrid.Clip = Shapes.Rounded(new Rect(0, 0, contentGrid.ActualWidth, contentGrid.ActualHeight),
                new CornerRadius(Math.Max(0, c.TopLeft - 1), Math.Max(0, c.TopRight - 1), Math.Max(0, c.BottomRight - 1), Math.Max(0, c.BottomLeft - 1)));
        }

        /// <summary>Create, show or hide the floating lamp strip to match the settings and the launcher.</summary>
        void UpdateLampWindow()
        {
            if (!LampAbove && !LampBelow)
            {
                CloseLampWindow();
                return;
            }
            if (lampWindow == null)
            {
                var edge = Color.FromArgb((byte)(255 * Math.Max(0.4, bgAlpha)), FrameBorder.R, FrameBorder.G, FrameBorder.B);
                lampWindow = new LampWindow(S.LampMode, edge, Corner, LampAbove, S.LampMonitor);
                HookLampStrip(lampWindow.Lamps, false);
                if (lampWindow.Monitor != null) HookLampMenu(lampWindow.Monitor);
                lampWindow.Topmost = !inBackground;   // behind other windows too while the launcher is sent to the background
                lampWindow.SizeChanged += delegate { PlaceSatellites(); };
            }
            if (!IsVisible) { lampWindow.Hide(); return; }
            if (lampWindow.Owner == null) lampWindow.Owner = this;
            lampWindow.Show();
            PlaceSatellites();
        }

        void CloseLampWindow()
        {
            if (lampWindow != null) { lampWindow.Close(); lampWindow = null; }
        }

        /// <summary>The launcher's width, touching its top or bottom edge (no gap).</summary>
        void PlaceLampWindow()
        {
            if (lampWindow == null || !lampWindow.IsVisible) return;
            double panelW = ActualWidth - 2 * Shadow;
            lampWindow.Width = panelW;
            lampWindow.UpdateLayout();
            lampWindow.Left = Left + Shadow;
            // A monitor stands on the strip and rises above it: below the launcher it overlaps the launcher's bottom edge.
            double rise = Math.Max(0, lampWindow.ActualHeight - lampWindow.StripHeight);
            lampWindow.Top = LampAbove ? Top + Shadow - lampWindow.ActualHeight : Top + ActualHeight - Shadow - rise;
        }

        /// <summary>The top edge of the launcher, or of a floating lamp strip above it (what a strip or sensors above line up to).</summary>
        double TopEdge()
        {
            if (LampAbove && lampWindow != null && lampWindow.IsVisible) return lampWindow.Top;
            return Top + Shadow;
        }

        /// <summary>The bottom edge of the launcher, or of a floating lamp strip below it.</summary>
        double BottomEdge()
        {
            if (LampBelow && lampWindow != null && lampWindow.IsVisible) return lampWindow.Top + lampWindow.ActualHeight;
            return Top + ActualHeight - Shadow;
        }

        // ---------- right-clicking the strip: how busy the lamps look ----------

        Action<string> lampMenuProbe;   // test copies: describe the menu instead of opening it

        /// <summary>
        /// Right-clicking a lamp strip offers its activities. Where it's part of the launcher or the quick launch strip,
        /// dragging it also moves the launcher, as the space around it does.
        /// </summary>
        void HookLampStrip(LampPanel lamps, bool movesLauncher)
        {
            lamps.IsHitTestVisible = true;
            if (movesLauncher)
                lamps.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ButtonState != MouseButtonState.Pressed) return;
                    try { DragMove(); } catch (InvalidOperationException) { }
                };
            HookLampMenu(lamps);
        }

        /// <summary>Right-clicking it shows the lamp strip's menu.</summary>
        void HookLampMenu(UIElement el)
        {
            el.MouseRightButtonDown += (s, e) => { e.Handled = true; };
            el.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowLampMenu(); };
        }

        void ShowLampMenu()
        {
            var items = LampMenuItems();
            if (lampMenuProbe != null) { lampMenuProbe(DescribeMenu(items)); return; }
            ShowMenu(null, items);
        }

        /// <summary>The lamp strip's right-click menu: the activities (the one in use ticked) and the strip's settings.</summary>
        List<object> LampMenuItems()
        {
            var items = new List<object>();
            items.Add(new TextBlock { Text = "Lamp activity", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 4) });
            items.Add(LampChoice("Idle (slow, few lamps lit)", LampActivity.Idle));
            items.Add(LampChoice("Computing", LampActivity.Computing));
            items.Add(LampChoice("Alert (busier, with a sweep of light)", LampActivity.Alert));
            var random = LampChoice(RandomLampsText, LampActivity.Random);
            random.ToolTip = "Switches between idle, computing and alert on its own, every 20 to 60 seconds";
            items.Add(random);
            if (LampAbove || LampBelow)
            {
                // The monitor stands on a strip outside the launcher.
                items.Add(new Separator());
                items.Add(new TextBlock { Text = "Monitor", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 4) });
                items.Add(Item("No monitor", () => SetLampMonitor(LampMonitorPlace.None), S.LampMonitor == LampMonitorPlace.None, null));
                items.Add(Item("At the left end", () => SetLampMonitor(LampMonitorPlace.Left), S.LampMonitor == LampMonitorPlace.Left, null));
                items.Add(Item("At the right end", () => SetLampMonitor(LampMonitorPlace.Right), S.LampMonitor == LampMonitorPlace.Right, null));
            }
            // Words spelled out in lamps, scrolling or flashing.
            items.Add(new Separator());
            items.Add(new TextBlock { Text = "Text", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 4) });
            items.Add(Item("No text", () => SetLampText(LampTextEffect.None), S.LampTextMode == LampTextEffect.None, null));
            items.Add(Item("Scroll the text", () => SetLampText(LampTextEffect.Scroll), S.LampTextMode == LampTextEffect.Scroll, null));
            items.Add(Item("Flash the text", () => SetLampText(LampTextEffect.Flash), S.LampTextMode == LampTextEffect.Flash, null));
            var sides = Item("Keep blinking beside the text", () => { S.LampTextSides = !S.LampTextSides; app.Save(); ApplyLampText(); }, S.LampTextSides, null);
            sides.ToolTip = "The lamps on either side of the text (and reminders) keep blinking while it flashes or scrolls";
            items.Add(sides);
            var typeIt = Item(S.LampText.Length > 0 ? "Change the text..." : "Type the text...", () => EditLampText(), false, "");
            if (S.LampText.Length > 0) typeIt.ToolTip = "Now: " + S.LampText;
            items.Add(typeIt);

            // Reminders: each flashes from its date and time on, until it's dismissed.
            items.Add(new Separator());
            items.Add(new TextBlock { Text = "Reminders", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 4) });
            var due = DueReminders();
            foreach (var r in due)
            {
                var one = r;
                items.Add(Item("Dismiss \u201c" + Shorten(r.Text.Trim(), 30) + "\u201d", () => DismissReminder(one), false, "\ue73e"));
            }
            if (due.Count > 1) items.Add(Item("Dismiss all " + due.Count, DismissDueReminders, false, null));
            var coming = ComingReminders();
            foreach (var r in coming.Take(3))
                items.Add(new TextBlock { Text = "\u201c" + Shorten(r.Text.Trim(), 30) + "\u201d, " + ReminderWhen(r.At), FontSize = 11, Opacity = 0.75,
                                          IsHitTestVisible = false, Margin = new Thickness(0, 0, 0, 3) });
            if (coming.Count > 3) items.Add(new TextBlock { Text = "and " + (coming.Count - 3) + " more", FontSize = 11, Opacity = 0.75, IsHitTestVisible = false, Margin = new Thickness(0, 0, 0, 3) });
            items.Add(Item(S.Reminders.Count > 0 ? "Add or delete reminders..." : "Add a reminder...", () => app.OpenReminderSettings(), false, "\ue787"));
            items.Add(new Separator());
            items.Add(Item("Lamp strip settings...", () => app.OpenSettings(1), false, ""));
            return items;
        }

        public const string RandomLampsText = "Randomize (changes on its own now and then)";

        MenuItem LampChoice(string header, LampActivity a)
        {
            return Item(header, () => SetLampActivity(a), S.LampMode == a, null);
        }

        /// <summary>Saves the lamps' activity; the strip on screen changes right away.</summary>
        void SetLampActivity(LampActivity a)
        {
            if (S.LampMode == a) return;
            if (a == LampActivity.Random) LampPanel.NewRandomTurn(S.LampMode);   // changes right away, so you can see it took
            S.LampMode = a;
            app.Save();
            foreach (var p in LampPanel.OnScreen) p.Activity = a;
        }

        /// <summary>Scrolls or flashes the strip's text, or goes back to blinking; asks for the text if there isn't any yet.</summary>
        void SetLampText(LampTextEffect effect)
        {
            if (effect != LampTextEffect.None && S.LampText.Length == 0)
            {
                string t = AskLampText();
                if (string.IsNullOrEmpty(t)) return;
                S.LampText = t;
            }
            S.LampTextMode = effect;
            app.Save();
            ApplyLampText();
        }

        /// <summary>Asks for new text for the strip; with none showing yet, it starts scrolling.</summary>
        void EditLampText()
        {
            string t = AskLampText();
            if (t == null) return;
            S.LampText = t;
            if (t.Length > 0 && S.LampTextMode == LampTextEffect.None) S.LampTextMode = LampTextEffect.Scroll;
            app.Save();
            ApplyLampText();
        }

        /// <summary>The text to spell on the strip (null if cancelled).</summary>
        string AskLampText()
        {
            string t = InputDialog.Ask(this, app, "Lamp strip text", "Text to spell out in lamps (letters, numbers and common punctuation):", S.LampText);
            return t == null ? null : t.Trim();
        }

        // ---------- reminders ----------

        System.Windows.Threading.DispatcherTimer reminderTimer;

        /// <summary>Reminders whose time has come, soonest first: they flash on the strips until they're dismissed.</summary>
        List<LampReminder> DueReminders()
        {
            var now = DateTime.Now;
            return S.Reminders.Where(r => r.At <= now).OrderBy(r => r.At).ToList();
        }

        /// <summary>Reminders still to come, soonest first.</summary>
        List<LampReminder> ComingReminders()
        {
            var now = DateTime.Now;
            return S.Reminders.Where(r => r.At > now).OrderBy(r => r.At).ToList();
        }

        /// <summary>
        /// What the strips spell: the reminders that are due, flashing one after another; otherwise the strip's own text,
        /// if it has some. With reminders still to come, it looks again when the next is due (and at least every hour,
        /// in case the clock changes).
        /// </summary>
        void ApplyLampText()
        {
            var due = DueReminders();
            if (due.Count > 0) LampPanel.ShowText(string.Join("\n", due.Select(r => r.Text.Trim())), LampTextEffect.Flash, S.LampTextSides);
            else LampPanel.ShowText(S.LampText, S.LampTextMode, S.LampTextSides);
            if (reminderTimer == null)
            {
                reminderTimer = new System.Windows.Threading.DispatcherTimer();
                reminderTimer.Tick += delegate { reminderTimer.Stop(); ApplyLampText(); };
            }
            reminderTimer.Stop();
            var next = ComingReminders().FirstOrDefault();
            if (next != null)
            {
                var wait = next.At - DateTime.Now + TimeSpan.FromSeconds(0.5);
                reminderTimer.Interval = wait > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait;
                reminderTimer.Start();
            }
        }

        /// <summary>Dismisses a reminder (it's done, so it leaves the list); the strip goes back to what it showed.</summary>
        void DismissReminder(LampReminder r)
        {
            S.Reminders.Remove(r);
            app.Save();
            ApplyLampText();
        }

        void DismissDueReminders()
        {
            foreach (var r in DueReminders()) S.Reminders.Remove(r);
            app.Save();
            ApplyLampText();
        }

        /// <summary>"on Tue, Dec 1 at 9:00 AM" (with the year when it isn't this year).</summary>
        static string ReminderWhen(DateTime at)
        {
            var c = System.Globalization.CultureInfo.CurrentCulture;
            return "on " + at.ToString(at.Year == DateTime.Today.Year ? "ddd, MMM d" : "ddd, MMM d, yyyy", c) + " at " + at.ToString("t", c);
        }

        static string Shorten(string s, int max) { return s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "\u2026"; }

        /// <summary>Puts the monitor at an end of the strip (or takes it away) and saves it.</summary>
        void SetLampMonitor(LampMonitorPlace place)
        {
            if (S.LampMonitor == place) return;
            S.LampMonitor = place;
            app.Save();
            CloseLampWindow();
            UpdateLampWindow();
        }

        static string DescribeMenu(IEnumerable<object> items)
        {
            return string.Join(" | ", items.Select(o =>
                o is Separator ? "---" : o is TextBlock ? "(" + ((TextBlock)o).Text + ")"
                : o is MenuItem ? (((MenuItem)o).IsChecked ? "[x] " : "") + (string)((MenuItem)o).Header : "?"));
        }

        /// <summary>
        /// Test copies only: "lamptest:" steps two seconds apart, logged to lamp-test.txt. menu: the right-click menu;
        /// pick:&lt;start of a choice&gt;: clicks it; lamps: what each strip on screen is doing; rightclick: right-clicks
        /// each strip (the menu is described, not opened) and checks a real click on it would reach it.
        /// </summary>
        void TestLamps(string script)
        {
            var steps = new Queue<string>(script.Split(';'));
            string log = System.IO.Path.Combine(LauncherData.DataFolder, "lamp-test.txt");
            Action<string> write = line => { try { System.IO.File.AppendAllText(log, line + "\r\n"); } catch (Exception) { } };
            Func<LampPanel, string> where = p =>
            {
                var w = Window.GetWindow(p);
                return (p.InMonitor ? "the monitor, in " : "") + (w is LampWindow ? "its own window" : w is DockWindow ? "the detached quick launch strip" : w == this ? "the launcher" : "?")
                    + (p.Fills ? ", behind icons" : "");
            };
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += delegate
            {
                if (steps.Count == 0) { timer.Stop(); return; }
                string step = steps.Dequeue();
                string arg = step.Contains(":") ? step.Substring(step.IndexOf(':') + 1) : "";
                try
                {
                    if (step == "menu") write("menu: " + DescribeMenu(LampMenuItems()));
                    else if (step.StartsWith("pick:"))
                    {
                        var mi = LampMenuItems().OfType<MenuItem>().FirstOrDefault(m => ((string)m.Header).StartsWith(arg, StringComparison.OrdinalIgnoreCase));
                        write("pick " + arg + ": " + (mi == null ? "NO SUCH ITEM" : "clicked"));
                        if (mi != null) mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    }
                    else if (step.StartsWith("settext:"))
                    {
                        S.LampText = arg.Trim();
                        app.Save();
                        ApplyLampText();
                        write("text set to: " + S.LampText);
                    }
                    else if (step.StartsWith("reminder:"))
                    {
                        // reminder:<seconds from now>:<text> adds a reminder
                        int colon = arg.IndexOf(':');
                        var r = new LampReminder { At = DateTime.Now.AddSeconds(double.Parse(arg.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture)), Text = arg.Substring(colon + 1) };
                        S.Reminders.Add(r);
                        app.Save();
                        ApplyLampText();
                        write("reminder added " + ReminderWhen(r.At) + ": " + r.Text);
                    }
                    else if (step == "reminderstate")
                        write("reminders: " + (S.Reminders.Count == 0 ? "none" : string.Join("; ", S.Reminders.OrderBy(r => r.At).Select(r =>
                            r.Text + " (" + (r.At <= DateTime.Now ? "flashing" : ReminderWhen(r.At)) + ")"))));
                    else if (step.StartsWith("font:"))
                    {
                        var cols = LampFont.Columns(arg);
                        write("font: " + arg);
                        for (int r = 0; r < LampFont.Height; r++) write("   |" + new string(cols.Select(c => ((c >> r) & 1) != 0 ? '#' : '.').ToArray()));
                    }
                    else if (step == "text")
                        foreach (var p in LampPanel.OnScreen.Where(p => !p.InMonitor))
                        {
                            var rows = p.LitRows();
                            int last = rows.Length == 0 ? -1 : rows.Max(r => r.LastIndexOf('#'));
                            write("lit lamps on the strip in " + where(p) + " (" + (DueReminders().Count > 0 ? "reminders, flashing" : S.LampTextMode.ToString())
                                + (S.LampTextSides ? ", blinking beside it; the text's columns: " + p.TextStretch : "") + "):");
                            foreach (var r in rows) write("   |" + (last < 0 ? "" : r.Substring(0, Math.Min(r.Length, last + 2))));
                        }
                    else if (step == "lamps")
                        write("saved: " + S.LampMode + "; strips: " + string.Join("; ", LampPanel.OnScreen.Select(p =>
                            where(p) + ", " + p.Activity + (p.Activity == LampActivity.Random ? " now " + p.Current : ""))));
                    else if (step == "rightclick")
                    {
                        var panels = LampPanel.OnScreen;
                        if (panels.Count == 0) write("right-click: no lamp strip on screen");
                        foreach (var p in panels)
                        {
                            string got = null;
                            lampMenuProbe = d => got = d;
                            var e = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right) { RoutedEvent = Mouse.MouseUpEvent };
                            p.RaiseEvent(e);
                            lampMenuProbe = null;
                            // Would a real click reach it? Hit-test its left end, middle and right end from its window.
                            var w = Window.GetWindow(p);
                            // (The monitor's lamps are behind its glass: a click there lands on the monitor, which has the menu.)
                            var monitor = p.InMonitor ? VisualTreeHelper.GetParent(p) as Visual : null;
                            var reach = new[] { 6.0, p.ActualWidth / 2, p.ActualWidth - 6 }.Select(x =>
                            {
                                var hit = w == null ? null : VisualTreeHelper.HitTest(w, p.TranslatePoint(new Point(x, p.ActualHeight / 2), w));
                                bool ok = hit != null && (hit.VisualHit == p || (monitor != null && hit.VisualHit is Visual && ((Visual)hit.VisualHit).IsDescendantOf(monitor)));
                                return ok ? "yes" : "no (" + (hit == null ? "nothing" : hit.VisualHit.GetType().Name) + ")";
                            });
                            write("right-click on the strip in " + where(p) + ": " + (got == null ? "NO MENU" : got)
                                + "; clicks reach it at left/middle/right: " + string.Join("/", reach));
                        }
                    }
                }
                catch (Exception ex) { write(step + ": ERROR " + ex.Message); }
            };
            timer.Start();
        }
    }

    /// <summary>
    /// Borderless window that holds the lamp strip above or below the launcher. It touches the launcher, so the side
    /// against it is square and has no edge line: the two read as one piece.
    /// </summary>
    internal class LampWindow : Window
    {
        public LampPanel Lamps { get; private set; }
        public LampMonitor Monitor { get; private set; }   // null when there's none
        readonly Border strip;

        /// <summary>How tall the strip itself is (the window is taller when a monitor stands on it).</summary>
        public double StripHeight { get { return strip.ActualHeight; } }

        public LampWindow(LampActivity mode, Color edge, double corner, bool above, LampMonitorPlace monitorAt)
        {
            Title = "Lamp strip";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            SizeToContent = SizeToContent.Height;
            double r = Math.Max(0, corner - 1);
            var lamps = new LampPanel(mode) { Corners = above ? new CornerRadius(r, r, 0, 0) : new CornerRadius(0, 0, r, r) };
            Lamps = lamps;
            strip = new Border
            {
                CornerRadius = above ? new CornerRadius(corner, corner, 0, 0) : new CornerRadius(0, 0, corner, corner),
                BorderThickness = above ? new Thickness(1, 1, 1, 0) : new Thickness(1, 0, 1, 1),
                BorderBrush = new SolidColorBrush(edge),
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x0D)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = lamps
            };
            if (monitorAt == LampMonitorPlace.None) { Content = strip; return; }

            // The monitor stands on the strip's end, rising above it; the strip runs on behind it a little, so the
            // strip's rounded corner there doesn't show. The lamps stop short of it, leaving a plain panel (about a
            // module wide), flat right up to the case's edge, and the sill runs on into the case's base.
            bool right = monitorAt == LampMonitorPlace.Right;
            double tuck = corner + 2, inside = LampMonitor.CaseWidth - tuck, space = 22;
            strip.Margin = right ? new Thickness(0, 0, inside, 0) : new Thickness(inside, 0, 0, 0);
            if (right) lamps.ReserveRight = tuck + space; else lamps.ReserveLeft = tuck + space;
            strip.Child = lamps;
            // Below the launcher the strip has a 1-wide edge line under its sill, so the base is 1 taller there.
            double baseHeight = LampPanel.PanelHeight - LampPanel.SillTopY + (above ? 0 : 1);
            Monitor = new LampMonitor(mode, baseHeight)
            {
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            var both = new Grid();
            both.Children.Add(strip);
            both.Children.Add(Monitor);
            Content = both;
        }
    }
}
