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
                lampWindow = new LampWindow(S.LampMode, S.LampRows, edge, Corner, LampAbove, S.LampMonitor);
                Satellite(lampWindow);
                lampWindow.Lamps.Palette = LampPalette.From(S);
                if (lampWindow.Monitor != null) lampWindow.Monitor.Lamps.Palette = LampPalette.From(S);
                HookLampStrip(lampWindow.Lamps, false);
                if (lampWindow.Monitor != null) HookLampMenu(lampWindow.Monitor);
                TidyBallAnswer();
                if (lampWindow.Monitor != null && S.LampEightBall)
                {
                    var ball = lampWindow.Monitor.AddBall();
                    ball.Asked += AnswerEightBall;
                    ball.Shaken += BallShaken;
                    if (BallAnswerKept) ball.FaceAnswer();   // its answer is still on the strip
                }
                lampWindow.Topmost = !inBackground;   // behind other windows too while the launcher is sent to the background
                lampWindow.SizeChanged += delegate { PlaceSatellites(); };
            }
            if (!IsVisible) { lampWindow.Hide(); return; }
            if (lampWindow.Owner == null) lampWindow.Owner = this;
            lampWindow.Show();
            PlaceSatellites();
        }

        // ---------- the Magic 8 Ball ----------

        static readonly string[] EightBallAnswers =
        {
            "It is certain", "It is decidedly so", "Without a doubt", "Yes definitely", "You may rely on it",
            "As I see it, yes", "Most likely", "Outlook good", "Yes", "Signs point to yes",
            "Reply hazy, try again", "Ask again later", "Better not tell you now", "Cannot predict now", "Concentrate and ask again",
            "Don't count on it", "My reply is no", "My sources say no", "Outlook not so good", "Very doubtful"
        };
        static readonly Random eightBallPick = new Random();
        int lastAnswer = -1, testAnswer = -1;
        bool answerHooked;
        string ballAnswer;   // the ball's answer last put on the strip

        /// <summary>
        /// The ball has stopped spinning: one of its answers (not the last one again), in capitals, comes out of the strip's
        /// blinking; with Instant Response, it's there at once and stays until the ball is clicked again.
        /// </summary>
        void AnswerEightBall()
        {
            int k = testAnswer >= 0 ? testAnswer : eightBallPick.Next(EightBallAnswers.Length - 1);
            if (testAnswer < 0 && k >= lastAnswer && lastAnswer >= 0) k++;   // never the same twice in a row
            testAnswer = -1;
            lastAnswer = k;
            if (!answerHooked)
            {
                answerHooked = true;
                LampPanel.AnswerOver += delegate { if (lampWindow != null && lampWindow.Monitor != null && lampWindow.Monitor.Ball != null) lampWindow.Monitor.Ball.TurnBack(); };
            }
            ballAnswer = EightBallAnswers[k].ToUpperInvariant();
            if (S.LampEightBallInstant) LampPanel.KeepAnswer(ballAnswer);
            else LampPanel.ShowAnswer(ballAnswer);
        }

        /// <summary>Whether the ball's answer is on the strip, to stay until the ball is clicked again.</summary>
        bool BallAnswerKept { get { return ballAnswer != null && LampPanel.AnswerKept && LampPanel.AnswerNow == ballAnswer; } }

        /// <summary>The ball was clicked again: an answer it left on the strip goes (a new one comes when it stops spinning).</summary>
        void BallShaken()
        {
            if (BallAnswerKept) LampPanel.ClearAnswer();
        }

        /// <summary>
        /// The ball's answer comes off the strip when the ball's gone (the strip goes back to what it was showing), and one
        /// that stays when Instant Response is turned off.
        /// </summary>
        void TidyBallAnswer()
        {
            if (ballAnswer == null || LampPanel.AnswerNow != ballAnswer) return;
            bool ball = S.LampEightBall && S.LampMonitor != LampMonitorPlace.None && (LampAbove || LampBelow);
            if (!ball || (LampPanel.AnswerKept && !S.LampEightBallInstant)) LampPanel.ClearAnswer();
        }

        /// <summary>
        /// Puts the Magic 8 Ball in the monitor or takes it away. There has to be a monitor for it: if there's none, one goes
        /// at the right end, and it goes again when the ball does, so the strip is back as it was before.
        /// </summary>
        void SetEightBall(bool on)
        {
            if (on && !S.LampEightBall) S.LampMonitorForBall = S.LampMonitor == LampMonitorPlace.None;
            if (on && S.LampMonitor == LampMonitorPlace.None) S.LampMonitor = LampMonitorPlace.Right;
            if (!on && S.LampEightBall && S.LampMonitorForBall) S.LampMonitor = LampMonitorPlace.None;
            if (!on) S.LampMonitorForBall = false;
            S.LampEightBall = on;
            app.Save();
            CloseLampWindow();
            UpdateLampWindow();   // (an answer of the ball's comes off the strip)
        }

        /// <summary>Instant Response on or off (off: an answer of the ball's that's staying on the strip goes).</summary>
        void SetEightBallInstant(bool on)
        {
            S.LampEightBallInstant = on;
            app.Save();
            TidyBallAnswer();
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

        // ---------- right-clicking the strip (its menu is in LauncherWindow.LampMenu.cs) ----------

        Action<string> lampMenuProbe;   // test copies: describe the menu instead of opening it

        /// <summary>
        /// Right-clicking a lamp strip shows its menu. Where it's part of the launcher or the quick launch strip,
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

        /// <summary>Ticks or clears one of your own colors, and uses them; the last one stays ticked.</summary>
        void PickLampHue(LampHue h)
        {
            bool had = S.LampPicked.Contains(h), own = S.LampColors == LampColors.Picked;
            S.LampColors = LampColors.Picked;
            if (!had) S.LampPicked.Add(h);
            else if (own && LampColorsTicked > 1) S.LampPicked.Remove(h);
            S.LampPicked = LampPalette.Tidy(S.LampPicked);
            ApplyLampColors();
        }

        /// <summary>How many of your own colors are ticked, ones from the wheel too.</summary>
        int LampColorsTicked { get { return S.LampPicked.Count + S.LampCustom.Count(c => c.On); } }

        /// <summary>The same for a color from the wheel.</summary>
        void TickLampCustom(LampCustomColor c)
        {
            bool own = S.LampColors == LampColors.Picked;
            S.LampColors = LampColors.Picked;
            if (!c.On) c.On = true;
            else if (own && LampColorsTicked > 1) c.On = false;
            ApplyLampColors();
        }

        /// <summary>The color wheel: a new color, or another in place of one you have; it's ticked and used right away.</summary>
        void PickLampCustom(LampCustomColor change)
        {
            string start = change != null ? change.Color : S.LampCustom.Count > 0 ? S.LampCustom[S.LampCustom.Count - 1].Color
                : LampPalette.Hex(LampPalette.Swatch(S.LampPicked.Count > 0 ? S.LampPicked[0] : LampHue.Amber, false).ToString()), picked;
            app.ModalOpen = true;
            try { picked = LampPalette.Hex(ColorPickerDialog.Pick(this, start)); }
            finally { app.ModalOpen = false; }
            if (picked == null) return;
            if (change != null) { change.Color = picked; change.On = true; }
            else if (S.LampCustom.All(c => c.Color != picked)) S.LampCustom.Add(new LampCustomColor { Color = picked, On = true });
            foreach (var c in S.LampCustom) if (c.Color == picked) c.On = true;   // one you had already
            S.LampCustom = LampPalette.Tidy(S.LampCustom);
            S.LampColors = LampColors.Picked;
            ApplyLampColors();
        }

        /// <summary>Takes a color from the wheel off the list.</summary>
        void RemoveLampCustom(LampCustomColor c)
        {
            S.LampCustom.Remove(c);
            if (S.LampColors == LampColors.Picked && LampColorsTicked == 0) S.LampPicked.Add(LampHue.Blue);   // something to start from
            ApplyLampColors();
        }

        /// <summary>The lamps' colors, the monitor's too; they change right away.</summary>
        void ApplyLampColors()
        {
            app.Save();
            var palette = LampPalette.From(S);
            foreach (var p in LampPanel.OnScreen) p.Palette = palette;
        }

        /// <summary>Makes the strip this many lamps high; it changes right away.</summary>
        void SetLampRows(int rows)
        {
            S.LampRows = rows;
            app.Save();
            foreach (var p in LampPanel.OnScreen) if (!p.InMonitor) p.RowsHigh = rows;
            ApplyLampText();
            PlaceSatellites();   // with a monitor on it, the strip's window stays the same size, so it wouldn't move by itself
        }

        /// <summary>"8 lamps high (text in upper and lowercase)".</summary>
        public static string HeightText(int rows)
        {
            return rows + " lamps high (text in " + (rows <= LampFont.MinRows ? "capitals" : "upper and lowercase") + ")";
        }

        /// <summary>"2 lamps (capitals 7 high)" for a strip so many lamps high, or no room for tails.</summary>
        public static string TailText(int stripRows, int rows)
        {
            return (rows == 0 ? "None" : rows + (rows == 1 ? " lamp" : " lamps")) + " (capitals " + (stripRows - rows) + " high)";
        }

        /// <summary>How many of the rows are kept for tails (0: none); the text changes right away.</summary>
        void SetLampTails(int rows)
        {
            S.LampTextTails = rows > 0;
            if (rows > 0) S.LampTailRows = rows;
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
            if (due.Count > 0) LampPanel.ShowText(string.Join("\n", due.Select(r => r.Text.Trim())), LampTextEffect.Flash, S.LampTextSides, S.LampScrollSpeed, S.LampTails);
            else LampPanel.ShowText(S.LampText, S.LampTextMode, S.LampTextSides, S.LampScrollSpeed, S.LampTails);
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
            if (place == LampMonitorPlace.None) S.LampEightBall = S.LampMonitorForBall = false;   // it has nowhere to be
            app.Save();
            CloseLampWindow();
            UpdateLampWindow();
        }

        /// <summary>
        /// Test copies only: "lamptest:" steps two seconds apart, logged to lamp-test.txt. menu: the right-click menu;
        /// pick:&lt;start of a choice&gt;: clicks it; lamps: what each strip on screen is doing; rightclick: right-clicks
        /// each strip (the menu is described, not opened) and checks a real click on it would reach it.
        /// </summary>
        void TestLamps(string script)
        {
            var steps = new Queue<string>(script.Split(';'));
            var clock = System.Diagnostics.Stopwatch.StartNew();
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
                        var mi = FindInLampMenu(arg);
                        bool usable = mi != null && mi.IsEnabled && (!(mi.Parent is MenuItem) || ((MenuItem)mi.Parent).IsEnabled);
                        write("pick " + arg + ": " + (mi == null ? "NO SUCH ITEM" : !usable ? "off" : "clicked" + (mi.StaysOpenOnClick ? " (menu stays open)" : "")));
                        if (usable) mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    }
                    else if (step.StartsWith("settext:"))
                    {
                        S.LampText = arg.Trim();
                        app.Save();
                        ApplyLampText();
                        write("text set to: " + S.LampText);
                    }
                    else if (step.StartsWith("speed:"))
                    {
                        S.LampScrollSpeed = int.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                        app.Save();
                        ApplyLampText();
                        write("scroll speed set to " + S.LampScrollSpeed + "%");
                    }
                    else if (step.StartsWith("speedtyped:"))
                        write("speed typed '" + arg + "': " + (SpeedTyped(arg) == 0 ? "asked for again" : SpeedTyped(arg) + "%"));
                    else if (step == "place")
                    {
                        // where the strips are, and whether each stands where it should beside the launcher
                        var at = LampPanel.OnScreen.Where(p => !p.InMonitor).Select(p => where(p)
                            + (lampWindow != null && Window.GetWindow(p) == lampWindow ? ", " + (LampAbove ? "its bottom " + Math.Round(lampWindow.Top + lampWindow.ActualHeight - (Top + Shadow), 2) + " from the launcher's top"
                                : "its bottom " + Math.Round(lampWindow.Top + lampWindow.ActualHeight - (Top + ActualHeight - Shadow), 2) + " below the launcher's bottom, " + Math.Round(lampWindow.StripHeight, 2) + " high") : "")
                            + ", right-click " + (p.IsHitTestVisible ? "on" : "off")).ToList();
                        write("saved place: " + S.LampPanel + "; strips: " + (at.Count == 0 ? "none" : string.Join("; ", at)) + "; monitors: " + LampPanel.OnScreen.Count(p => p.InMonitor));
                    }
                    else if (step.StartsWith("typed:")) foreach (var a in arg.Split('~')) InputDialog.TestAnswers.Enqueue(a);   // what the next typing windows are answered with
                    else if (step.StartsWith("numbersnow"))
                        write("numbers: " + (LampPanel.AnswerNow ?? "none") + "; pattern " + S.LampNumbers + "; saved: " + string.Join(" | ", S.LampNumberPatterns.Select(p => p.Name + " = " + p.Pattern))
                              + (InputDialog.TestAsked != null ? "; last asked: " + InputDialog.TestAsked.Replace("\n", " / ") : ""));
                    else if (step.StartsWith("parse:"))
                    {
                        string problem;
                        var p = NumberPattern.Parse(arg, out problem);
                        write("parse '" + arg + "': " + (p == null ? "NOT A PATTERN: " + problem : p + " -> " + p.Pick() + " / " + p.Pick() + " / " + p.Pick()));
                    }
                    else if (step.StartsWith("numberstats:"))
                    {
                        string problem;
                        var p = NumberPattern.Parse(arg, out problem);
                        write("20,000 picks with " + arg + ": " + (p == null ? problem : p.Stats(20000)));
                    }
                    else if (step.StartsWith("inputshot:"))
                    {
                        // The typing windows for number patterns: as it opens, after a pattern that isn't one, and naming a long one.
                        string problem;
                        NumberPattern.Parse("xx/0-999", out problem);
                        InputDialog.SavePicture("Random number pattern", NumberPrompt, S.LampNumbers, null, arg + "-type.png");
                        InputDialog.SavePicture("Random number pattern", NumberPrompt, "xx/0-999", problem, arg + "-problem.png");
                        string lotto = "xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49";
                        InputDialog.SavePicture("Save the number pattern", "A name for " + lotto + " (leave it empty to call it by the pattern itself):", "", null, arg + "-save.png");
                        InputDialog.SavePicture("Rename", "Name:", "Notepad", null, arg + "-short.png");
                    }
                    else if (step.StartsWith("patterndialog:")) DeleteDialog.SavePatternPicture("Lottery", "xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49,xx/1-49", SettingsThemeMode.Light, arg);
                    else if (step.StartsWith("eightanswer:")) testAnswer = int.Parse(arg);   // the next answer, by its number (0 to 19)
                    else if (step == "eightball" || step == "ball")
                    {
                        var ball = lampWindow == null || lampWindow.Monitor == null ? null : lampWindow.Monitor.Ball;
                        if (ball == null) write(step + ": no ball");
                        else
                        {
                            if (step == "eightball")
                            {
                                // a press and release on the ball, as a click would be
                                var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                                var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
                                ball.RaiseEvent(down);
                                ball.RaiseEvent(up);
                            }
                            // Where a real click would go (the way the mouse finds what it's over).
                            var w = Window.GetWindow(ball);
                            Func<Point, string> reaches = pt =>
                            {
                                var hit = w.InputHitTest(ball.TranslatePoint(pt, w)) as DependencyObject;
                                return hit == ball ? "the ball" : hit == null ? "nothing" : hit.GetType().Name;
                            };
                            write(step + ": the ball is " + ball.State + "; answer " + (LampPanel.AnswerNow ?? "none") + "; a click on its middle reaches " + reaches(new Point(ball.ActualWidth / 2, ball.ActualHeight / 2))
                                  + ", on its edge " + reaches(new Point(ball.ActualWidth / 2 + 16, ball.ActualHeight / 2)) + ", in the window's corner " + reaches(new Point(4, 4)));
                        }
                    }
                    else if (step == "menufade") SettingsTheme.TestCloseAtOnce(write);
                    else if (step == "settingsok") app.TestSettingsOk(write);
                    else if (step == "ballnow")
                    {
                        // the ball, its answer on the strip, and the monitor
                        var ball = lampWindow == null || lampWindow.Monitor == null ? null : lampWindow.Monitor.Ball;
                        var strip = LampPanel.OnScreen.FirstOrDefault(p => !p.InMonitor);
                        write("ballnow: " + (ball == null ? "no ball" : "the ball is " + ball.State) + "; on the strip: " + (LampPanel.AnswerNow ?? "its own text") + (LampPanel.AnswerKept ? " (stays)" : "")
                              + "; lit: " + (strip == null ? "no strip" : strip.ColorReport()) + "; monitor " + S.LampMonitor + (S.LampMonitorForBall ? " (for the ball)" : "")
                              + "; 8 ball " + S.LampEightBall + ", instant " + S.LampEightBallInstant);
                    }
                    else if (step == "boardlit")
                        write("board: " + (lampWindow != null && lampWindow.Monitor != null ? lampWindow.Monitor.Board.LitCount + " of 78 lit; big lamps in the window: " + lampWindow.Monitor.Lamps.LampsHere : "no monitor"));
                    else if (step.StartsWith("monitorframes:"))
                    {
                        // Pictures the monitor (three times the size) every 0.15 seconds, ten times.
                        var monitor = lampWindow == null ? null : lampWindow.Monitor;
                        if (monitor == null) write("monitorframes: no monitor");
                        else
                        {
                            System.IO.Directory.CreateDirectory(arg);
                            int shot = 0;
                            var frames = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.15) };
                            frames.Tick += delegate
                            {
                                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(monitor.ActualWidth * 3), (int)(monitor.ActualHeight * 3), 288, 288, PixelFormats.Pbgra32);
                                var dv = new DrawingVisual();
                                using (var dc = dv.RenderOpen()) dc.DrawRectangle(new VisualBrush(monitor), null, new Rect(0, 0, monitor.ActualWidth, monitor.ActualHeight));
                                rtb.Render(dv);
                                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                                using (var fs = System.IO.File.Create(System.IO.Path.Combine(arg, string.Format("m{0:00}.png", shot)))) enc.Save(fs);
                                if (++shot >= 10) frames.Stop();
                            };
                            frames.Start();
                            write("monitorframes: the board is " + (monitor.Board.IsVisible ? "showing, " + monitor.Board.LitCount + " lit" : "hidden") + (monitor.Ball != null ? ", with the 8 ball" : ""));
                        }
                    }
                    else if (step.StartsWith("ballframes:"))
                    {
                        // Clicks the ball and pictures the monitor (three times the size) every quarter second for 3.5 seconds.
                        var ball = lampWindow == null || lampWindow.Monitor == null ? null : lampWindow.Monitor.Ball;
                        if (ball == null) write("ballframes: no ball");
                        else
                        {
                            System.IO.Directory.CreateDirectory(arg);
                            var monitor = lampWindow.Monitor;
                            ball.Shake();
                            int shot = 0;
                            var frames = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(0.25) };
                            frames.Tick += delegate
                            {
                                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(monitor.ActualWidth * 3), (int)(monitor.ActualHeight * 3), 288, 288, PixelFormats.Pbgra32);
                                var dv = new DrawingVisual();
                                using (var dc = dv.RenderOpen()) dc.DrawRectangle(new VisualBrush(monitor), null, new Rect(0, 0, monitor.ActualWidth, monitor.ActualHeight));
                                rtb.Render(dv);
                                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                                using (var fs = System.IO.File.Create(System.IO.Path.Combine(arg, string.Format("b{0:00}.png", shot)))) enc.Save(fs);
                                if (++shot > 14) frames.Stop();
                            };
                            frames.Start();
                            write("ballframes: clicked; the ball is " + ball.State);
                        }
                    }
                    else if (step.StartsWith("button:") || step.StartsWith("buttonright:") || step.StartsWith("buttontip:"))
                    {
                        // A saved pattern's button in the menu (the menu itself isn't opened): a click on it, as the mouse sends
                        // it, and the menu's Random numbers part as it is then; a right-click (its menu is described, not
                        // opened); or what pointing at it shows.
                        var items = LampMenuItems();
                        var chip = PatternButtonIn(items, arg);
                        if (chip == null) write(step + ": NOT FOUND");
                        else if (step.StartsWith("buttontip:")) write(step + ": " + ((string)chip.ToolTip).Replace("\n", " / "));
                        else
                        {
                            bool right = step.StartsWith("buttonright:");
                            string got = null;
                            patternMenuProbe = d => got = d;
                            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, right ? MouseButton.Right : MouseButton.Left) { RoutedEvent = right ? UIElement.MouseRightButtonDownEvent : UIElement.MouseLeftButtonDownEvent };
                            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, right ? MouseButton.Right : MouseButton.Left) { RoutedEvent = right ? UIElement.MouseRightButtonUpEvent : UIElement.MouseLeftButtonUpEvent };
                            chip.RaiseEvent(down);
                            chip.RaiseEvent(up);
                            patternMenuProbe = null;
                            write(step + ": press " + (down.Handled ? "kept" : "PASSED ON") + ", release " + (up.Handled ? "kept" : "PASSED ON")
                                + (right ? "; its menu: " + (got ?? "NONE") : "; on the strip: " + (LampPanel.AnswerNow ?? "none") + "; menu now: " + NumbersPart(DescribeMenu(items))));
                        }
                    }
                    else if (step.StartsWith("buttondelete:"))
                    {
                        // Delete in a saved pattern's right-click menu (neither menu is opened); deleteanswer: answers the question.
                        var chip = PatternButtonIn(LampMenuItems(), arg);
                        var del = chip == null ? null : PatternButtonMenuItems(chip, (LampNumberPattern)chip.Tag).OfType<MenuItem>().FirstOrDefault(m => HeaderText(m) == "Delete");
                        DeleteDialog.TestAsked = null;
                        if (del == null) write(step + ": NOT FOUND");
                        else { del.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); write(step + ": clicked Delete"); }
                    }
                    else if (step == "picks")
                        write("picks: " + S.LampNumberPicks.Count + (S.LampNumberPicks.Count > 0 ? ", newest " + string.Join(" / ", S.LampNumberPicks.Take(3).Select(k => k.Numbers + " (" + k.Pattern + ", " + PickedWhen(k.When) + ")")) : "")
                              + "; pattern " + S.LampNumbers + "; saved: " + string.Join(" | ", S.LampNumberPatterns.Select(p => p.Name + " = " + p.Pattern + " x" + p.Clicks))
                              + "; last asked: " + (DeleteDialog.TestAsked ?? "nothing"));
                    else if (step == "pickwatch")
                    {
                        // How quickly picked numbers show: the strip's text a moment after Pick random numbers, while they stay, and as they go.
                        var strip = LampPanel.OnScreen.FirstOrDefault(p => !p.InMonitor);
                        var mi = FindInLampMenu("Pick random numbers");
                        mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        var started = System.Diagnostics.Stopwatch.StartNew();
                        foreach (double at in new[] { 0.1, 0.4, 2, 11.5, 13.5, 16.5 })
                        {
                            var once = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(at) };
                            once.Tick += delegate
                            {
                                once.Stop();
                                write("  " + started.Elapsed.TotalSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s: " + (strip == null ? "no strip" : strip.TextProgress));
                            };
                            once.Start();
                        }
                        write("pickwatch: picked " + (LampPanel.AnswerNow ?? "nothing"));
                    }
                    else if (step.StartsWith("picksdialog:")) DeleteDialog.SavePicksPicture(12, SettingsThemeMode.Light, arg);
                    else if (step.StartsWith("wheel:")) ColorPickerDialog.TestPick = arg;   // what the color wheel gives from now on
                    else if (step.StartsWith("deleteanswer:")) DeleteDialog.TestAnswer = arg == "yes";   // the answer to "Delete this custom color?"
                    else if (step.StartsWith("activityx:"))
                    {
                        // A press and release on a saved activity's X in the menu (the menu itself isn't opened).
                        var mi = FindInLampMenu("Activity>" + arg);
                        var part = mi == null ? null : ((DockPanel)mi.Header).Children.OfType<FrameworkElement>().FirstOrDefault(e => (string)e.Tag == "delete");
                        if (part == null) write(step + ": NOT FOUND");
                        else
                        {
                            DeleteDialog.TestAsked = null;
                            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
                            part.RaiseEvent(down);
                            part.RaiseEvent(up);
                            write(step + ": press " + (down.Handled ? "kept" : "PASSED ON") + ", release " + (up.Handled ? "kept" : "PASSED ON"));
                        }
                    }
                    else if (step.StartsWith("colordot:") || step.StartsWith("colorx:"))
                    {
                        // A press and release on a custom color's dot or X in the menu (the menu itself isn't opened).
                        var mi = FindInLampMenu("Colors>" + arg);
                        var part = mi == null ? null : ((DockPanel)mi.Header).Children.OfType<FrameworkElement>().FirstOrDefault(e => (string)e.Tag == (step.StartsWith("colordot:") ? "dot" : "delete"));
                        if (part == null) write(step + ": NOT FOUND");
                        else
                        {
                            DeleteDialog.TestAsked = null;
                            var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                            var up = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent };
                            part.RaiseEvent(down);
                            part.RaiseEvent(up);
                            write(step + ": press " + (down.Handled ? "kept" : "PASSED ON") + ", release " + (up.Handled ? "kept" : "PASSED ON") + "; tip: " + part.ToolTip);
                            var asked = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                            asked.Tick += delegate { asked.Stop(); if (DeleteDialog.TestAsked != null) write("   asked about deleting " + DeleteDialog.TestAsked); };
                            asked.Start();
                        }
                    }
                    else if (step.StartsWith("deleteshot:"))
                    {
                        DeleteDialog.SavePicture("#40FFC0", LampPalette.Swatch("#40FFC0", false), SettingsThemeMode.Light, arg + "-light.png");
                        DeleteDialog.SavePicture("#FF2080", LampPalette.Swatch("#FF2080", false), SettingsThemeMode.Dark, arg + "-dark.png");
                    }
                    else if (step.StartsWith("stripshot:"))
                        foreach (var p in LampPanel.OnScreen.Where(p => !p.InMonitor)) { p.SavePicture(arg); write(System.IO.Path.GetFileName(arg) + ": " + p.TextProgress); }
                    else if (step.StartsWith("reminderdialog:"))
                    {
                        // reminderdialog:<new or which reminder>:<month/day/year/time>:<text> (the dialog is never shown)
                        var part = arg.Split(new[] { ':' }, 3);
                        int which;
                        bool isNew = !int.TryParse(part[0], out which);
                        var r = isNew ? new LampReminder { At = ReminderDialog.UsualTime, Text = "" } : S.Reminders.OrderBy(x => x.At).ElementAt(which);
                        write("dialog: " + ReminderDialog.Test(r, isNew, part[1], part[2]));
                        if (isNew && r.Text.Length > 0) S.Reminders.Add(r);
                        app.Save();
                        ApplyLampText();
                    }
                    else if (step.StartsWith("dialogclick:"))
                    {
                        // dialogclick:<button>:<text to type, or nothing>: for the dialog the next step opens
                        int colon = arg.IndexOf(':');
                        ReminderDialog.TestClick = new[] { colon < 0 ? null : arg.Substring(colon + 1), colon < 0 ? arg : arg.Substring(0, colon) };
                    }
                    else if (step.StartsWith("remindershot:"))
                    {
                        ReminderDialog.SavePicture(new LampReminder { At = ReminderDialog.UsualTime, Text = "" }, true, SettingsThemeMode.Light, arg + "-new.png");
                        ReminderDialog.SavePicture(new LampReminder { At = DateTime.Today.AddDays(40).AddHours(14.25), Text = "Pay the rent" }, false, SettingsThemeMode.Dark, arg + "-old.png");
                    }
                    else if (step == "colors")
                        foreach (var p in LampPanel.OnScreen) write("lit lamps on " + where(p) + " (" + p.Palette + "): " + p.ColorReport());
                    else if (step == "progress")
                        foreach (var p in LampPanel.OnScreen.Where(p => !p.InMonitor))
                            write("at " + clock.Elapsed.TotalSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s, the strip in " + where(p) + ": " + p.TextProgress);
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
                        var font = LampFont.For(S.LampRows, S.LampTails);
                        var cols = font.Columns(arg);
                        write("font: " + arg + " (" + cols.Count + " columns)");
                        for (int r = 0; r < font.Height; r++) write("   |" + new string(cols.Select(c => ((c >> r) & 1) != 0 ? '#' : '.').ToArray()));
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
                    else if (step.StartsWith("pace:"))
                    {
                        // pace:<seconds>: how many lamps go on or off a second on each strip, and on the monitor's LED board
                        double seconds = double.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                        var strips = LampPanel.OnScreen.Where(p => !p.InMonitor).ToList();
                        var before = strips.Select(p => p.LitRows()).ToList();
                        var changes = new long[strips.Count];
                        var lit = new double[strips.Count];
                        int samples = 0;
                        var board = lampWindow != null && lampWindow.Monitor != null ? lampWindow.Monitor.Board : null;
                        long boardFrom = board == null ? 0 : board.Changes;
                        double boardLit = 0, started = clock.Elapsed.TotalSeconds;
                        var sampler = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                        sampler.Tick += delegate
                        {
                            samples++;
                            for (int k = 0; k < strips.Count; k++)
                            {
                                var now = strips[k].LitRows();
                                int on = 0, all = 0;
                                for (int r = 0; r < now.Length && r < before[k].Length; r++)
                                    for (int c = 0; c < now[r].Length && c < before[k][r].Length; c++)
                                    {
                                        if (now[r][c] != before[k][r][c]) changes[k]++;
                                        if (now[r][c] == '#') on++;
                                        all++;
                                    }
                                lit[k] += all == 0 ? 0 : on / (double)all;
                                before[k] = now;
                            }
                            if (board != null) boardLit += board.LitCount;
                            double took = clock.Elapsed.TotalSeconds - started;
                            if (took < seconds) return;
                            sampler.Stop();
                            var inv = System.Globalization.CultureInfo.InvariantCulture;
                            for (int k = 0; k < strips.Count; k++)
                                write("pace on the strip in " + where(strips[k]) + " (" + strips[k].Current + "): " + (changes[k] / took).ToString("0.0", inv)
                                    + " lamps change a second, " + (100 * lit[k] / samples).ToString("0", inv) + "% lit");
                            if (board != null)
                                write("pace on the LED board: " + ((board.Changes - boardFrom) / took).ToString("0.00", inv) + " LEDs change a second, "
                                    + (boardLit / samples).ToString("0.0", inv) + " of 78 lit");
                        };
                        sampler.Start();
                    }
                    else if (step.StartsWith("custom:"))
                    {
                        // custom:<slider>=<value>: moves one of the custom activity's sliders in the menu (it isn't opened)
                        int eq = arg.IndexOf('=');
                        var mi = FindInLampMenu("Activity>" + arg.Substring(0, eq));
                        var row = mi == null ? null : mi.Header as FrameworkElement;
                        if (row == null || !(row.Tag is Slider)) write(step + ": NO SUCH SLIDER");
                        else
                        {
                            ((Slider)row.Tag).Value = double.Parse(arg.Substring(eq + 1), System.Globalization.CultureInfo.InvariantCulture);
                            write(step + ": slider now " + ((Slider)row.Tag).Value + "; in use: " + S.LampMode + ", " + S.LampCustomActivity.Summary()
                                  + " Strips: " + string.Join(", ", LampPanel.OnScreen.Select(p => p.Current.ToString())));
                        }
                    }
                    else if (step == "activities")
                        write("activity: " + S.LampMode + " (" + ActivityNow() + "); custom: " + S.LampCustomActivity.Summary() + " Saved: "
                              + (S.LampActivities.Count == 0 ? "none" : string.Join(" | ", S.LampActivities.Select(a => a.Name + " = " + a.Summary())))
                              + (InputDialog.TestAsked != null ? " Last asked: " + InputDialog.TestAsked.Replace("\n", " / ") : "")
                              + (DeleteDialog.TestAsked != null ? " Asked to delete: " + DeleteDialog.TestAsked : ""));
                    else if (step.StartsWith("highmenu:"))
                    {
                        // highmenu:<menu top>,<left>,<top>,<width>,<height>[,<monitor left>,<top>,<width>,<height>]: where the submenu goes
                        var n = arg.Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        var usual = new Rect(n[1], n[2], n[3], n[4]);
                        Rect? clear = n.Length >= 9 ? new Rect(n[5], n[6], n[7], n[8]) : (Rect?)null;
                        double off = HighSubmenuOffset(n[0], usual, clear);
                        write("submenu at " + n[2] + " to " + (n[2] + n[4]) + ", menu top " + n[0] + (clear.HasValue ? ", monitor " + clear.Value.Top + " to " + clear.Value.Bottom
                              + " (x " + clear.Value.Left + " to " + clear.Value.Right + ")" : "") + ": moved " + off + ", now " + (n[2] + off) + " to " + (n[2] + off + n[4]));
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

        public LampWindow(LampActivity mode, int rows, Color edge, double corner, bool above, LampMonitorPlace monitorAt)
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
            var lamps = new LampPanel(mode, rows) { Corners = above ? new CornerRadius(r, r, 0, 0) : new CornerRadius(0, 0, r, r) };
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
            double baseHeight = LampPanel.SillHeight + (above ? 0 : 1);
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
