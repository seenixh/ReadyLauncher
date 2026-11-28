using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The lamp strip's right-click menu. It has everything Settings has for the strip, in groups (the strip, its text, its
    /// reminders); beside each setting is what it's set to now. Choosing one of several closes the menu; ticking something
    /// on or off leaves it open.
    /// </summary>
    internal partial class LauncherWindow
    {
        List<object> LampMenuItems()
        {
            var items = new List<object>();

            // Reminders flashing now come first: dismissing them is what the click is most likely for.
            var due = DueReminders();
            if (due.Count > 0)
            {
                items.Add(Heading(due.Count == 1 ? "Reminder flashing now" : "Reminders flashing now"));
                foreach (var r in due)
                {
                    var one = r;
                    items.Add(Item("Dismiss " + Quoted(r.Text, 30), () => DismissReminder(one), false, ""));
                }
                if (due.Count > 1) items.Add(Item("Dismiss all " + due.Count, DismissDueReminders, false, null));
                items.Add(new Separator());
            }

            items.Add(Heading("Strip"));
            items.Add(LampPlaceMenu());
            items.Add(LampHeightMenu());
            items.Add(LampLooksMenu());
            items.Add(LampMonitorMenu());

            items.Add(new Separator());
            items.Add(Heading("Text"));
            items.Add(LampTextMenu());
            var type = Item(S.LampText.Length > 0 ? "Change the text..." : "Type the text...", EditLampText, false, "");
            if (S.LampText.Length > 0)
            {
                type.InputGestureText = Quoted(S.LampText, 22);
                type.ToolTip = "Now: " + S.LampText;
            }
            items.Add(type);
            items.Add(LampSpeedMenu());
            items.Add(LampTailsMenu());
            MenuItem sides = null;
            sides = Item("Keep blinking beside the text", () =>
            {
                S.LampTextSides = !S.LampTextSides;
                app.Save();
                ApplyLampText();
                sides.IsChecked = S.LampTextSides;
            }, S.LampTextSides, null);
            sides.StaysOpenOnClick = true;
            sides.ToolTip = "The lamps on either side of the text (and reminders) keep blinking instead of going dark";
            items.Add(sides);

            items.Add(new Separator());
            items.Add(Heading("Magic 8 Ball"));
            items.AddRange(EightBallItems());

            items.Add(new Separator());
            items.Add(Heading("Random numbers"));
            // The saved patterns' buttons; picking numbers; the pattern; and the numbers picked. Clicking a button picks
            // numbers with its pattern and the menu stays open, so what these show is brought up to date.
            MenuItem pick = null, pattern = null, picked = null;
            Action numbersChanged = null;
            if (S.LampNumberPatterns.Count > 0) items.Add(PatternButtons(() => numbersChanged()));
            pick = Item("Pick random numbers", PickLampNumbers, false, "\ue8b1");
            items.Add(pick);
            pattern = LampPatternMenu();
            items.Add(pattern);
            picked = SubMenu("Picked numbers", "\ue81c", "");
            items.Add(picked);
            numbersChanged = () =>
            {
                pick.InputGestureText = PatternName(S.LampNumbers);
                pick.ToolTip = "New random numbers, picked with the pattern " + S.LampNumbers + ", show on the strip's lamps at once";
                pattern.InputGestureText = Shorten(S.LampNumbers, 26);
                FillPicks(picked);
            };
            numbersChanged();
            items.Add(new Separator());
            items.Add(Heading("Reminders"));
            var add = Item("Add a reminder...", AddReminder, false, "");
            add.ToolTip = "Choose a date, a time and some text: from then on, the strip flashes it until you dismiss it";
            items.Add(add);
            items.Add(RemindersMenu());

            items.Add(new Separator());
            items.Add(Item("Lamp strip in Settings...", () => app.OpenSettings(SettingsWindow.LampStripTab), false, ""));
            return items;
        }

        // ---------- the strip ----------

        MenuItem LampPlaceMenu()
        {
            var menu = SubMenu("Position", "", PlaceText(S.LampPanel));
            Action<LampPanelPlace, string> place = (p, text) =>
            {
                var mi = Choice(text, S.LampPanel == p, () => SetLampPlace(p));
                if ((p == LampPanelPlace.UnderStrip || p == LampPanelPlace.StripBackground) && S.Dock == DockSource.Hidden && S.LampPanel != p)
                    TurnOff(mi, "The quick launch strip isn't showing: choose what it shows in Settings, Quick launch strip");
                Into(menu, mi);
            };
            Into(menu, Heading("Inside the launcher"));
            place(LampPanelPlace.Top, "Along the top (buttons below it)");
            place(LampPanelPlace.Bottom, "Along the bottom");
            Into(menu, Heading("Outside the launcher"));
            place(LampPanelPlace.AboveLauncher, "Above the launcher");
            place(LampPanelPlace.BelowLauncher, "Below the launcher");
            Into(menu, Heading("On the quick launch strip"));
            place(LampPanelPlace.UnderStrip, "Across the bottom of the strip");
            place(LampPanelPlace.StripBackground, "Behind the strip's icons");
            Into(menu, new Separator());
            var off = Item("No lamp strip (turn it back on in Settings)", () => SetLampPlace(LampPanelPlace.Off), false, "");
            off.ToolTip = "Takes the strip away. To bring it back: Settings, Lamp strip, Position.";
            Into(menu, off);
            return menu;
        }

        static string PlaceText(LampPanelPlace p)
        {
            switch (p)
            {
                case LampPanelPlace.Top: return "Inside, along the top";
                case LampPanelPlace.Bottom: return "Inside, along the bottom";
                case LampPanelPlace.AboveLauncher: return "Above the launcher";
                case LampPanelPlace.BelowLauncher: return "Below the launcher";
                case LampPanelPlace.UnderStrip: return "Under quick launch";
                case LampPanelPlace.StripBackground: return "Behind quick launch";
                default: return "Off";
            }
        }

        /// <summary>Moves the strip somewhere else (or takes it away), right away.</summary>
        void SetLampPlace(LampPanelPlace place)
        {
            if (S.LampPanel == place) return;
            S.LampPanel = place;
            app.Save();
            ApplySettings();
            app.RaiseSettings();
        }

        MenuItem LampHeightMenu()
        {
            var menu = SubMenu("Height", "", S.LampRows + " lamps");
            if (S.LampPanel == LampPanelPlace.StripBackground) menu.ToolTip = "Behind the quick launch strip's icons the lamps fill the space there is, so this only changes the text";
            for (int k = LampFont.MinRows; k <= LampFont.MaxRows; k++)
            {
                int rows = k;
                Into(menu, Choice(HeightText(rows), S.LampRows == rows, () => SetLampRows(rows)));
            }
            return menu;
        }

        /// <summary>The lamp colors there are to choose from, in the order Settings lists them.</summary>
        public static readonly LampColors[] ColorChoices = { LampColors.RedAmber, LampColors.Picked };
        public static readonly LampHue[] Hues = (LampHue[])Enum.GetValues(typeof(LampHue));

        public static string ColorsText(LampColors c)
        {
            return c == LampColors.Picked ? "Your own colors" : "Red and amber (the usual)";
        }

        /// <summary>
        /// A dot of a color that does something of its own when clicked (rather than being a click on the menu entry or
        /// checkbox it's in).
        /// </summary>
        internal static FrameworkElement ClickableDot(Color c, string tip, out System.Windows.Shapes.Ellipse dot, Action click)
        {
            var edge = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
            var ring = new SolidColorBrush(Color.FromArgb(200, 128, 128, 128));
            var d = new System.Windows.Shapes.Ellipse { Width = 13, Height = 13, Fill = new SolidColorBrush(c), Stroke = edge, StrokeThickness = 1 };
            dot = d;
            // Lined up with the other entries' dots, with a little more to click on around it.
            var host = new Border { Child = d, Background = Brushes.Transparent, Padding = new Thickness(2), Margin = new Thickness(-2, -2, 6, -2),
                                    VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = tip, Tag = "dot" };
            host.MouseEnter += delegate { d.Stroke = ring; d.StrokeThickness = 2; };
            host.MouseLeave += delegate { d.Stroke = edge; d.StrokeThickness = 1; };
            OwnClick(host, click);
            return host;
        }

        /// <summary>A small X at the end of a menu entry that deletes what the entry is.</summary>
        static FrameworkElement DeleteMark(Action click)
        {
            var x = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(4), Background = Brushes.Transparent, Margin = new Thickness(18, -2, -6, -2),
                Cursor = Cursors.Hand, ToolTip = "Delete this color", Tag = "delete",
                Child = new TextBlock { Text = "\ue711", FontFamily = TileView.IconFont, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            var hover = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128));
            x.MouseEnter += delegate { x.Background = hover; };
            x.MouseLeave += delegate { x.Background = Brushes.Transparent; };
            OwnClick(x, click);
            return x;
        }

        /// <summary>Clicking it does that, and only that: the press and the release go no further.</summary>
        static void OwnClick(UIElement e, Action click)
        {
            e.MouseLeftButtonDown += (s, a) => { a.Handled = true; };
            e.MouseLeftButtonUp += (s, a) => { a.Handled = true; click(); };
        }

        /// <summary>The whole menu something in a menu is in (null: none, as in test copies).</summary>
        static ContextMenu RootMenu(DependencyObject d)
        {
            for (; d != null; d = LogicalTreeHelper.GetParent(d))
            {
                var menu = d as ContextMenu;
                if (menu != null) return menu;
            }
            return null;
        }

        /// <summary>Closes the menu the entry is in, then does something (that opens a window of its own).</summary>
        void AfterMenu(DependencyObject entry, Action then)
        {
            var root = RootMenu(entry);
            if (root != null) root.IsOpen = false;
            Dispatcher.BeginInvoke(then, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>Asks, then deletes a color from the wheel.</summary>
        void DeleteLampCustom(LampCustomColor c)
        {
            bool yes;
            app.ModalOpen = true;
            try { yes = DeleteDialog.Ask(this, S.SettingsTheme, c.Color, LampPalette.Swatch(c.Color, S.LampPastel)); }
            finally { app.ModalOpen = false; }
            if (yes) RemoveLampCustom(c);
        }

        /// <summary>A menu entry's words with a dot of color before them.</summary>
        static DataTemplate DotTemplate(Color c)
        {
            var fill = new SolidColorBrush(c);
            fill.Freeze();
            var edge = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
            edge.Freeze();
            var dot = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
            dot.SetValue(FrameworkElement.WidthProperty, 13.0);
            dot.SetValue(FrameworkElement.HeightProperty, 13.0);
            dot.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            dot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            dot.SetValue(System.Windows.Shapes.Shape.FillProperty, fill);
            dot.SetValue(System.Windows.Shapes.Shape.StrokeProperty, edge);
            var words = new FrameworkElementFactory(typeof(TextBlock));
            words.SetBinding(TextBlock.TextProperty, new Binding());
            var row = new FrameworkElementFactory(typeof(StackPanel));
            row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            row.AppendChild(dot);
            row.AppendChild(words);
            var t = new DataTemplate { VisualTree = row };
            t.Seal();
            return t;
        }

        public const string RandomLampsText = "Randomize (changes on its own now and then)";
        public const string CustomLampsText = "Custom (set with the sliders below)";
        internal const string SpeedTip = "How quick: 1 is the slowest, 20 the quickest; 10 is as quick as Computing";
        internal const string LampsTip = "How many of them are lit at a time";

        /// <summary>The activity now, beside its name in the menu: a custom one that's saved goes by its name.</summary>
        string ActivityNow()
        {
            if (S.LampMode == LampActivity.Random) return "Randomize";
            if (S.LampMode != LampActivity.Custom) return S.LampMode.ToString();
            if (DefaultActivityInUse) return "Default";
            var saved = S.LampActivities.FirstOrDefault(a => a.SameAs(S.LampCustomActivity));
            return saved != null ? Shorten(saved.Name, 22) : "Custom";
        }

        System.Windows.Threading.DispatcherTimer saveSoon;

        /// <summary>
        /// One of the custom activity's sliders moved: the lamps change at once (to the custom activity, if they weren't on
        /// it), and it's saved once the slider has rested a moment.
        /// </summary>
        void CustomLevelChanged()
        {
            if (S.LampMode != LampActivity.Custom)
            {
                S.LampMode = LampActivity.Custom;
                foreach (var p in LampPanel.OnScreen) p.Activity = LampActivity.Custom;
            }
            if (saveSoon == null)
            {
                saveSoon = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                saveSoon.Tick += delegate { saveSoon.Stop(); app.Save(); };
            }
            saveSoon.Stop();
            saveSoon.Start();
        }

        /// <summary>Saves the custom activity's settings under a name (one already called that gets these instead).</summary>
        void SaveLampActivity()
        {
            var levels = S.LampCustomActivity;
            var had = S.LampActivities.FirstOrDefault(a => a.SameAs(levels));
            string name = InputDialog.Ask(this, app, "Save the lamp activity", "A name for this lamp activity:\n\n" + levels.Summary(),
                had != null ? had.Name : LampActivityLevels.NextName(S.LampActivities));
            if (name == null) return;
            S.LampActivities = LampActivityLevels.Save(S.LampActivities, name, levels);
            app.Save();
            showLine = "activity:" + name.Trim();
            SetLampActivity(LampActivity.Custom);
        }

        /// <summary>Asks, then deletes a saved custom activity.</summary>
        void DeleteLampActivity(LampActivityLevels a)
        {
            bool yes;
            app.ModalOpen = true;
            try { yes = DeleteDialog.AskActivity(this, S.SettingsTheme, a.Name, a.Summary()); }
            finally { app.ModalOpen = false; }
            if (!yes) return;
            S.LampActivities.Remove(a);
            app.Save();
        }

        /// <summary>
        /// A slider in the menu, with its value and what it sets after it. It doesn't light up when pointed at (it isn't
        /// something to click), and the menu stays open while it's moved.
        /// </summary>
        static MenuItem SliderEntry(string tag, int value, int min, int max, string unit, string word, string tip, Action<int> changed)
        {
            var slider = new Slider
            {
                Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), IsSnapToTickEnabled = true, TickFrequency = 1,
                SmallChange = 1, LargeChange = max - min >= 50 ? 10 : 2, Width = 170, VerticalAlignment = VerticalAlignment.Center
            };
            var number = new TextBlock { Width = 38, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            number.Text = (int)slider.Value + unit;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Tag = slider, ToolTip = tip, Background = Brushes.Transparent };
            row.Children.Add(slider);
            row.Children.Add(number);
            row.Children.Add(new TextBlock { Text = word, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center });
            slider.ValueChanged += delegate
            {
                number.Text = (int)slider.Value + unit;
                changed((int)slider.Value);
            };
            return new MenuItem { Header = row, Tag = tag, StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(40, 1, 12, 1) };
        }

        static ControlTemplate plainEntry;

        /// <summary>A menu entry that's only what it shows: no shading when pointed at, no tick, no arrow.</summary>
        static ControlTemplate PlainEntry
        {
            get
            {
                if (plainEntry != null) return plainEntry;
                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
                var content = new FrameworkElementFactory(typeof(ContentPresenter));
                content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(HeaderedItemsControl.HeaderProperty));
                border.AppendChild(content);
                plainEntry = new ControlTemplate(typeof(MenuItem)) { VisualTree = border };
                plainEntry.Seal();
                return plainEntry;
            }
        }

        /// <summary>
        /// <paramref name="sub"/>'s submenu keeps clear of the lamps (the strip's, and the monitor with its cabinet), so you
        /// can see them change as you choose: above them, or below them if there isn't room above. With
        /// <paramref name="high"/>, it also opens high: its top level with the top of the whole menu rather than with its entry.
        /// </summary>
        void OpenClear(MenuItem sub, bool high)
        {
            System.Windows.Controls.Primitives.Popup hooked = null;
            Action place = () =>
            {
                var popup = sub.Template == null ? null : sub.Template.FindName("PART_Popup", sub) as System.Windows.Controls.Primitives.Popup;
                var anchor = popup == null ? null : VisualTreeHelper.GetParent(popup) as FrameworkElement;
                ContextMenu root = null;
                for (DependencyObject d = sub; d != null && root == null; d = LogicalTreeHelper.GetParent(d)) root = d as ContextMenu;
                var child = popup == null ? null : popup.Child as FrameworkElement;
                if (anchor == null || root == null || child == null || PresentationSource.FromVisual(anchor) == null || PresentationSource.FromVisual(root) == null) return;
                if (hooked != popup)
                {
                    hooked = popup;
                    popup.Opened += delegate { Dispatcher.BeginInvoke(new Action(() => { if (sub.IsSubmenuOpen) PlaceClear(popup, anchor, root, child, high); }), System.Windows.Threading.DispatcherPriority.Loaded); };
                }
                PlaceClear(popup, anchor, root, child, high);
            };
            sub.SubmenuOpened += (s, e) => { if (e.OriginalSource == sub) place(); };
        }

        void PlaceClear(System.Windows.Controls.Primitives.Popup popup, FrameworkElement anchor, ContextMenu root, FrameworkElement child, bool high)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = child.ActualHeight > 0 ? new Size(child.ActualWidth, child.ActualHeight) : child.DesiredSize;
            Rect a = ScreenRect(anchor), m = ScreenRect(root);
            var usual = new Rect(a.Right + popup.HorizontalOffset, a.Top, size.Width, size.Height);
            popup.VerticalOffset = ClearSubmenuOffset(high ? Math.Min(0, m.Top - usual.Top) : UsualSubmenuOffset, usual, LampsArea(), WorkAreaAt(a.TopLeft));
        }

        /// <summary>How far down a submenu usually opens from its entry's top (as the menus' look has it).</summary>
        const double UsualSubmenuOffset = -5;

        /// <summary>
        /// How far to move a submenu from its entry (<paramref name="usual"/>: beside it, its top level with the entry's),
        /// starting from <paramref name="start"/>, so it doesn't cover <paramref name="keepClear"/>: up until it's just
        /// above it, or down until it's just below it if there isn't room above in <paramref name="work"/> (the screen
        /// less the taskbar). With room for neither, as high as it goes; the popup itself keeps it on the screen.
        /// </summary>
        internal static double ClearSubmenuOffset(double start, Rect usual, Rect? keepClear, Rect work)
        {
            var placed = usual;
            placed.Offset(0, start);
            if (!keepClear.HasValue) return start;
            var k = keepClear.Value;
            if (placed.Bottom <= k.Top || placed.Top >= k.Bottom) return start;   // clear of them already
            if (k.Top - work.Top >= placed.Height || work.Bottom - k.Bottom < placed.Height) return start - (placed.Bottom - k.Top);
            return start + (k.Bottom - placed.Top);
        }

        /// <summary>
        /// Where a menu of <paramref name="size"/> opened at <paramref name="at"/> goes to leave <paramref name="keepClear"/>
        /// in sight: just above it, or just below it if there isn't room above; null (where it was opened) if it fits
        /// neither way, or there's nothing to keep clear of. Its left edge stays where it was opened, if the screen allows.
        /// </summary>
        internal static Point? ClearMenuPlace(Size size, Point at, Rect? keepClear, Rect work)
        {
            if (!keepClear.HasValue) return null;
            var k = keepClear.Value;
            double x = Math.Max(work.Left, Math.Min(at.X, work.Right - size.Width));
            if (k.Top - work.Top >= size.Height) return new Point(x, k.Top - size.Height);
            if (work.Bottom - k.Bottom >= size.Height) return new Point(x, k.Bottom);
            return null;
        }

        /// <summary>
        /// Where the lamps are on the screen, in device-independent pixels: the strip, and the monitor with its cabinet
        /// (all of the window they're in, outside the launcher), and lamps in the launcher or the quick launch strip.
        /// Null when none are showing.
        /// </summary>
        Rect? LampsArea()
        {
            Rect? area = null;
            Action<FrameworkElement> add = e =>
            {
                if (e == null || !e.IsVisible || PresentationSource.FromVisual(e) == null) return;
                var r = ScreenRect(e);
                area = area.HasValue ? Rect.Union(area.Value, r) : r;
            };
            if (lampWindow != null && lampWindow.IsVisible) add(lampWindow.Content as FrameworkElement);
            foreach (var p in LampPanel.OnScreen) add(p);
            return area;
        }

        /// <summary>The work area (the screen less the taskbar) of the screen a point is on, in device-independent pixels.</summary>
        Rect WorkAreaAt(Point dip)
        {
            var src = PresentationSource.FromVisual(this);
            if (src == null || src.CompositionTarget == null) return SystemParameters.WorkArea;
            var px = src.CompositionTarget.TransformToDevice.Transform(dip);
            var wa = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)px.X, (int)px.Y)).WorkingArea;
            var fromDevice = src.CompositionTarget.TransformFromDevice;
            return new Rect(fromDevice.Transform(new Point(wa.Left, wa.Top)), fromDevice.Transform(new Point(wa.Right, wa.Bottom)));
        }

        /// <summary>A point in an element, on the screen in device-independent pixels (null when it isn't on screen).</summary>
        static Point? ScreenPoint(UIElement el, Point p)
        {
            try
            {
                var src = PresentationSource.FromVisual(el);
                if (src == null || src.CompositionTarget == null) return null;
                return src.CompositionTarget.TransformFromDevice.Transform(el.PointToScreen(p));
            }
            catch (Exception) { return null; }
        }

        /// <summary>Where an element is on the screen, in device-independent pixels.</summary>
        static Rect ScreenRect(FrameworkElement e)
        {
            var fromDevice = PresentationSource.FromVisual(e).CompositionTarget.TransformFromDevice;
            return new Rect(fromDevice.Transform(e.PointToScreen(new Point(0, 0))), fromDevice.Transform(e.PointToScreen(new Point(e.ActualWidth, e.ActualHeight))));
        }

        /// <summary>
        /// How far to move a submenu from its usual place (<paramref name="usual"/>: beside its entry, its top level with the
        /// entry's): up level with the top of the menu, and further up if it would still cover <paramref name="keepClear"/>.
        /// The popup itself keeps it on the screen.
        /// </summary>
        internal static double HighSubmenuOffset(double menuTop, Rect usual, Rect? keepClear)
        {
            double offset = Math.Min(0, menuTop - usual.Top);
            var placed = usual;
            placed.Offset(0, offset);
            if (keepClear.HasValue)
            {
                var k = keepClear.Value;
                if (placed.Left < k.Right && placed.Right > k.Left && placed.Bottom > k.Top && placed.Top < k.Bottom) offset -= placed.Bottom - k.Top;
            }
            return offset;
        }

        MenuItem LampMonitorMenu()
        {
            var menu = SubMenu("Monitor", "", (S.LampMonitor == LampMonitorPlace.Left ? "Left end" : S.LampMonitor == LampMonitorPlace.Right ? "Right end" : "None")
                + (S.LampEightBall && S.LampMonitor != LampMonitorPlace.None ? ", 8 ball" : ""));
            Into(menu, Choice("No monitor", S.LampMonitor == LampMonitorPlace.None, () => SetLampMonitor(LampMonitorPlace.None)));
            Into(menu, Choice("At the left end of the strip", S.LampMonitor == LampMonitorPlace.Left, () => SetLampMonitor(LampMonitorPlace.Left)));
            Into(menu, Choice("At the right end of the strip", S.LampMonitor == LampMonitorPlace.Right, () => SetLampMonitor(LampMonitorPlace.Right)));
            if (!LampAbove && !LampBelow) TurnOff(menu, "The monitor stands on a strip outside the launcher: above it or below it (see Position)");
            return menu;
        }

        /// <summary>
        /// The Magic 8 Ball in the monitor, on or off (on, a monitor goes at the right end if there's none; off, that monitor
        /// goes again), and Instant Response: its answer shows at once and stays until the ball is clicked again.
        /// </summary>
        List<object> EightBallItems()
        {
            bool outside = LampAbove || LampBelow, ball = S.LampEightBall && S.LampMonitor != LampMonitorPlace.None;
            var eight = Item("Magic 8 Ball in the monitor", () => SetEightBall(!ball), ball, null);
            eight.ToolTip = "Ask a yes-or-no question and click the ball: it shakes and spins, and its answer shows on the strip"
                + (ball ? (S.LampMonitorForBall ? ". Unticked, the monitor goes too, as it was before." : "")
                        : S.LampMonitor == LampMonitorPlace.None ? ". The monitor goes at the right end of the strip (unticked, it goes again)." : "");
            if (!outside) TurnOff(eight, "The ball sits in the monitor, which stands on a strip outside the launcher: above it or below it (see Position)");
            MenuItem instant = null;
            instant = Item("Instant Response", () =>
            {
                SetEightBallInstant(!S.LampEightBallInstant);
                instant.IsChecked = S.LampEightBallInstant;
            }, S.LampEightBallInstant, null);
            instant.StaysOpenOnClick = true;
            instant.ToolTip = "When the ball stops spinning, its answer is on the strip at once and stays there until you click the ball again (for a new answer)";
            if (!ball || !outside) TurnOff(instant, "For the Magic 8 Ball: tick it first. " + instant.ToolTip);
            return new List<object> { eight, instant };
        }

        // ---------- its text ----------

        MenuItem LampTextMenu()
        {
            var t = S.LampTextMode;
            var menu = SubMenu("Show text", "", t == LampTextEffect.Scroll ? "Scrolling" : t == LampTextEffect.Flash ? "Flashing" : t == LampTextEffect.Still ? "Still"
                : t == LampTextEffect.Emerge ? "Fading in and out" : "Off");
            Into(menu, Choice("No text (the lamps just blink)", t == LampTextEffect.None, () => SetLampText(LampTextEffect.None)));
            Into(menu, Choice("Scroll the text", t == LampTextEffect.Scroll, () => SetLampText(LampTextEffect.Scroll)));
            Into(menu, Choice("Flash the text", t == LampTextEffect.Flash, () => SetLampText(LampTextEffect.Flash)));
            Into(menu, Choice("Keep the text still", t == LampTextEffect.Still, () => SetLampText(LampTextEffect.Still)));
            var emerge = Choice("Fade the text in out of the blinking, and back", t == LampTextEffect.Emerge, () => SetLampText(LampTextEffect.Emerge));
            emerge.ToolTip = "The blinking lamps slowly settle into the text, then slowly go back to blinking, over and over";
            Into(menu, emerge);
            return menu;
        }

        static readonly int[] SpeedChoices = { 25, 50, 75, 100, 125, 150, 200, 250, 300 };

        MenuItem LampSpeedMenu()
        {
            var menu = SubMenu("Scroll speed", "", S.LampScrollSpeed + "%");
            foreach (int k in SpeedChoices.Concat(new[] { S.LampScrollSpeed }).Distinct().OrderBy(k => k))
            {
                int speed = k;
                string text = speed + "%" + (speed == 100 ? " (the usual)" : speed == AppSettings.MinScrollSpeed ? " (slowest)" : speed == AppSettings.MaxScrollSpeed ? " (fastest)" : "");
                Into(menu, Choice(text, S.LampScrollSpeed == speed, () => SetLampSpeed(speed)));
            }
            Into(menu, new Separator());
            Into(menu, Item("Another speed...", AskLampSpeed, false, ""));
            if (S.LampTextMode != LampTextEffect.Scroll) TurnOff(menu, "For text that scrolls (see Show text)");
            return menu;
        }

        void SetLampSpeed(int speed)
        {
            S.LampScrollSpeed = Math.Max(AppSettings.MinScrollSpeed, Math.Min(AppSettings.MaxScrollSpeed, speed));
            app.Save();
            ApplyLampText();
        }

        /// <summary>Asks for any speed the Settings slider has; something that isn't one of them is asked for again.</summary>
        void AskLampSpeed()
        {
            string prompt = "How fast the text scrolls, from " + AppSettings.MinScrollSpeed + " to " + AppSettings.MaxScrollSpeed + " percent (100 is the usual speed):";
            string typed = S.LampScrollSpeed.ToString(CultureInfo.CurrentCulture);
            while ((typed = InputDialog.Ask(this, app, "Text scroll speed", prompt, typed)) != null)
            {
                int speed = SpeedTyped(typed);
                if (speed > 0) { SetLampSpeed(speed); return; }
            }
        }

        /// <summary>The speed typed ("150" or "150 %"), or 0 when it isn't one the strip can scroll at.</summary>
        static int SpeedTyped(string typed)
        {
            int speed;
            return int.TryParse(typed.Replace("%", "").Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out speed)
                && speed >= AppSettings.MinScrollSpeed && speed <= AppSettings.MaxScrollSpeed ? speed : 0;
        }

        MenuItem LampTailsMenu()
        {
            int most = LampFont.TailRowsFor(S.LampRows);
            var menu = SubMenu("Room for tails (g, j, p, q, y)", "", S.LampTails == 0 ? "None" : S.LampTails == 1 ? "1 lamp" : S.LampTails + " lamps");
            menu.ToolTip = "How many of the strip's lamps are kept below the line for tails. More for tails makes the letters smaller.";
            for (int k = 0; k <= most; k++)
            {
                int rows = k;
                Into(menu, Choice(TailText(S.LampRows, rows), S.LampTails == rows, () => SetLampTails(rows)));
            }
            if (most == 0) TurnOff(menu, "For a strip 7 or more lamps high (see Height)");
            return menu;
        }

        // ---------- random numbers ----------

        /// <summary>
        /// The pattern random numbers are picked with: typing a new one, and saving the one in use (saved ones are buttons
        /// under the Random numbers heading).
        /// </summary>
        MenuItem LampPatternMenu()
        {
            var menu = SubMenu("Number pattern", "\ue8ef", Shorten(S.LampNumbers, 26));
            var type = Item("Type a pattern...", TypeLampNumbers, false, "\ue70f");
            type.ToolTip = "Groups separated by commas, each like xxx/0-999: an x for each digit, then the lowest and highest number";
            Into(menu, type);
            var save = Item("Save this pattern...", SaveLampNumbers, false, "\ue74e");
            save.ToolTip = "Keep " + S.LampNumbers + " under a name: it gets a button under Random numbers, to pick numbers with it in one click";
            Into(menu, save);
            return menu;
        }

        /// <summary>
        /// The saved number patterns as buttons, under the Random numbers heading. Clicking one picks numbers with its
        /// pattern, and they show on the strip at once; the menu stays open, to click it again or another one (a click
        /// outside the menu closes it). Pointing at one shows its pattern and how many times it's been clicked;
        /// right-clicking one offers to delete it (you're asked first). The one in use has an outline.
        /// </summary>
        MenuItem PatternButtons(Action changed)
        {
            Color back = SettingsTheme.MenuColor("Border");
            Color ink = (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) / 255 > 0.55 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White;
            Brush plain = new SolidColorBrush(back), hover = new SolidColorBrush(Mix(back, ink, 0.14)), down = new SolidColorBrush(Mix(back, ink, 0.26));
            Brush ring = new SolidColorBrush(SettingsTheme.MenuColor("Accent")), words = new SolidColorBrush(ink);
            var row = new WrapPanel { MaxWidth = 330, Background = Brushes.Transparent };
            Action outline = () =>
            {
                foreach (Border b in row.Children)
                {
                    bool inUse = ((LampNumberPattern)b.Tag).Pattern == S.LampNumbers;
                    b.BorderThickness = new Thickness(inUse ? 1.5 : 0);
                    b.Padding = inUse ? new Thickness(8.5, 1.5, 8.5, 2.5) : new Thickness(10, 3, 10, 4);
                }
            };
            foreach (var p in S.LampNumberPatterns)
            {
                var one = p;
                var chip = new Border
                {
                    Child = new TextBlock { Text = Shorten(one.Name, 24), Foreground = words },
                    Background = plain, BorderBrush = ring, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 2, 6, 2),
                    Cursor = Cursors.Hand, Tag = one, ToolTip = PatternTip(one)
                };
                chip.MouseEnter += delegate { chip.Background = hover; };
                chip.MouseLeave += delegate { chip.Background = plain; };
                chip.MouseLeftButtonDown += (s, e) => { e.Handled = true; chip.Background = down; };
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    chip.Background = chip.IsMouseOver ? hover : plain;
                    PickWithPattern(one);
                    chip.ToolTip = PatternTip(one);
                    outline();
                    changed();
                };
                chip.MouseRightButtonDown += (s, e) => { e.Handled = true; };
                chip.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowPatternButtonMenu(chip, one); };
                row.Children.Add(chip);
            }
            outline();
            return new MenuItem { Header = row, Tag = "Saved patterns", StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(32, 1, 12, 3) };
        }

        /// <summary>What pointing at a saved pattern's button shows: its pattern, and how many times it's been clicked.</summary>
        static string PatternTip(LampNumberPattern p)
        {
            return "Pattern: " + p.Pattern + "\n"
                + (p.Clicks == 0 ? "Not clicked yet" : "Clicked " + p.Clicks.ToString("N0", CultureInfo.CurrentCulture) + (p.Clicks == 1 ? " time" : " times"))
                + "\nRight-click it to delete it";
        }

        /// <summary>A saved pattern's button was clicked: numbers are picked with its pattern (which is the one in use from now on).</summary>
        void PickWithPattern(LampNumberPattern p)
        {
            p.Clicks++;
            S.LampNumbers = NumberPattern.Tidy(p.Pattern);
            PickLampNumbers();   // (and saves)
        }

        Action<string> patternMenuProbe;   // test copies: describe a pattern button's right-click menu instead of opening it

        /// <summary>A saved pattern's button's right-click menu: Delete (you're asked first).</summary>
        List<object> PatternButtonMenuItems(FrameworkElement chip, LampNumberPattern p)
        {
            var delete = Item("Delete", () => AfterMenu(chip, () => DeleteLampPattern(p)), false, "\ue74d");
            delete.ToolTip = "Take " + p.Name + " off your saved patterns (you're asked first)";
            return new List<object> { Heading(Shorten(p.Name, 30)), delete };
        }

        void ShowPatternButtonMenu(FrameworkElement chip, LampNumberPattern p)
        {
            var items = PatternButtonMenuItems(chip, p);
            if (patternMenuProbe != null) { patternMenuProbe(DescribeMenu(items)); return; }
            var menu = BuildMenu(items);
            menu.PlacementTarget = chip;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.Closed += delegate
            {
                // Closed without deleting: the strip's menu, still open, takes the mouse back, so a click outside it closes it as usual.
                var root = RootMenu(chip);
                if (root != null && root.IsOpen && Mouse.Captured == null) Mouse.Capture(root, CaptureMode.SubTree);
            };
            menu.IsOpen = true;
        }

        /// <summary>
        /// The numbers picked, newest first, each with the pattern it was picked with and the date and time, in a list that
        /// scrolls; and clearing the list. Off while there are none.
        /// </summary>
        void FillPicks(MenuItem menu)
        {
            menu.Items.Clear();
            var picks = S.LampNumberPicks;
            menu.InputGestureText = picks.Count == 0 ? "None" : Shorten(picks[0].Numbers, 26);
            menu.IsEnabled = picks.Count > 0;
            menu.ToolTip = picks.Count == 0 ? "None picked yet: the numbers you pick are listed here, with the date and time" : null;
            ToolTipService.SetShowOnDisabled(menu, true);
            if (picks.Count == 0) return;
            Into(menu, Heading("Newest first" + (picks.Count > 1 ? " (" + picks.Count + ")" : "")));
            Into(menu, PickList(picks, PatternName));
            Into(menu, new Separator());
            MenuItem clear = null;
            clear = Item("Clear the list...", () => AfterMenu(clear, ClearPicks), false, "\ue74d");
            clear.ToolTip = "Take every pick off this list (you're asked first)";
            Into(menu, clear);
        }

        /// <summary>The picks, a row each: the numbers, the pattern's name and when; it scrolls when they don't all fit.</summary>
        static MenuItem PickList(IList<LampNumberPick> picks, Func<string, string> patternName)
        {
            var grid = new Grid();
            for (int c = 0; c < 3; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int r = 0; r < picks.Count; r++)
            {
                var k = picks[r];
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cells = new[]
                {
                    new TextBlock { Text = k.Numbers, FontWeight = FontWeights.SemiBold, MaxWidth = 240, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = k.Numbers, Margin = new Thickness(0, 2, 20, 2) },
                    new TextBlock { Text = patternName(k.Pattern), Opacity = 0.7, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = "Picked with " + k.Pattern, Margin = new Thickness(0, 2, 20, 2) },
                    new TextBlock { Text = PickedWhen(k.When), Opacity = 0.7, Margin = new Thickness(0, 2, 0, 2) }
                };
                for (int c = 0; c < 3; c++)
                {
                    System.Windows.Documents.Typography.SetNumeralAlignment(cells[c], FontNumeralAlignment.Tabular);
                    Grid.SetRow(cells[c], r);
                    Grid.SetColumn(cells[c], c);
                    grid.Children.Add(cells[c]);
                }
            }
            var scroll = new ScrollViewer
            {
                Content = grid, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false, Padding = new Thickness(0, 0, 6, 0)
            };
            try { scroll.Resources.MergedDictionaries.Add(SettingsTheme.ScrollBarStyles()); } catch (Exception) { }   // slim, in the menu's colors
            return new MenuItem { Header = scroll, Tag = "Pick list", StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(32, 1, 8, 1) };
        }

        /// <summary>When numbers were picked: the date and the time, to the second.</summary>
        internal static string PickedWhen(DateTime t)
        {
            return t.ToString("d", CultureInfo.CurrentCulture) + "  " + t.ToString("T", CultureInfo.CurrentCulture);
        }

        /// <summary>Asks, then clears the list of picked numbers.</summary>
        void ClearPicks()
        {
            bool yes;
            app.ModalOpen = true;
            try { yes = DeleteDialog.AskPicks(this, S.SettingsTheme, S.LampNumberPicks.Count); }
            finally { app.ModalOpen = false; }
            if (!yes) return;
            S.LampNumberPicks.Clear();
            app.Save();
        }

        /// <summary>A pattern's name if it's saved, or the pattern itself (shortened).</summary>
        string PatternName(string pattern)
        {
            var saved = S.LampNumberPatterns.FirstOrDefault(p => p.Pattern == pattern);
            return saved != null ? Shorten(saved.Name, 26) : Shorten(pattern, 26);
        }

        /// <summary>
        /// New random numbers, with the pattern in use, show on the strip's lamps at once (and stay a while); they go on
        /// the list of picked numbers, with the date and time.
        /// </summary>
        void PickLampNumbers()
        {
            string problem;
            var p = NumberPattern.Parse(S.LampNumbers, out problem) ?? NumberPattern.Parse(NumberPattern.Usual, out problem);
            string numbers = p.Pick();
            LampPanel.ShowAnswer(numbers, 12, true);
            S.LampNumberPicks.Insert(0, new LampNumberPick { When = DateTime.Now, Numbers = numbers, Pattern = S.LampNumbers });
            if (S.LampNumberPicks.Count > LampNumberPick.MaxKept) S.LampNumberPicks.RemoveRange(LampNumberPick.MaxKept, S.LampNumberPicks.Count - LampNumberPick.MaxKept);
            app.Save();
        }

        void SetLampNumbers(string pattern)
        {
            S.LampNumbers = NumberPattern.Tidy(pattern);
            app.Save();
        }

        /// <summary>Asks for a new pattern, again while it isn't one the strip can use; then picks numbers with it.</summary>
        internal const string NumberPrompt = "Groups of numbers, separated by commas. Write each group as an x for each digit, a slash, and the lowest "
            + "and highest number, like xxx/0-999 (smaller numbers get zeros in front).\n\nFor example, xx/0-99,xxx/0-999,xxxx/0-9999 picks numbers "
            + "like 24 432 8476, and x/1-6,x/1-6 rolls two dice.";   // (the example numbers kept on one line)

        void TypeLampNumbers()
        {
            string typed = S.LampNumbers, problem = null;
            while ((typed = InputDialog.Ask(this, app, "Random number pattern", NumberPrompt, typed, problem)) != null)
            {
                if (NumberPattern.Parse(typed, out problem) == null) continue;
                SetLampNumbers(typed);
                PickLampNumbers();
                return;
            }
        }

        /// <summary>Saves the pattern in use under a name (one already called that gets this pattern instead).</summary>
        void SaveLampNumbers()
        {
            var had = S.LampNumberPatterns.FirstOrDefault(p => p.Pattern == S.LampNumbers);
            string name = InputDialog.Ask(this, app, "Save the number pattern", "A name for " + S.LampNumbers + " (leave it empty to call it by the pattern itself):", had != null ? had.Name : "");
            if (name == null) return;
            S.LampNumberPatterns = SavePattern(S.LampNumberPatterns, name, S.LampNumbers);
            app.Save();
        }

        /// <summary>
        /// The saved patterns with this one saved under <paramref name="name"/> (the pattern itself when there's none): it
        /// takes the place of one already saved with that name or that pattern (a pattern is saved once, so saving it
        /// again renames it).
        /// </summary>
        internal static List<LampNumberPattern> SavePattern(List<LampNumberPattern> saved, string name, string pattern)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = pattern;
            Func<LampNumberPattern, bool> same = p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase) || p.Pattern == pattern;
            var list = saved.Where(p => !same(p)).Select(p => new LampNumberPattern { Name = p.Name, Pattern = p.Pattern, Clicks = p.Clicks }).ToList();
            int at = saved.FindIndex(p => same(p));
            if (at >= 0) at -= saved.Take(at).Count(p => same(p));
            int clicks = saved.Where(p => p.Pattern == pattern).Select(p => p.Clicks).DefaultIfEmpty(0).Max();   // (renamed, it keeps its clicks)
            list.Insert(at < 0 ? list.Count : at, new LampNumberPattern { Name = name, Pattern = pattern, Clicks = clicks });
            return TidyPatterns(list);
        }

        /// <summary>Saved patterns that are patterns, each name once, no more than 30.</summary>
        internal static List<LampNumberPattern> TidyPatterns(IEnumerable<LampNumberPattern> saved)
        {
            var list = new List<LampNumberPattern>();
            foreach (var p in saved ?? new LampNumberPattern[0])
            {
                if (p == null || !NumberPattern.IsPattern(p.Pattern)) continue;
                string pattern = NumberPattern.Tidy(p.Pattern), name = string.IsNullOrWhiteSpace(p.Name) ? pattern : p.Name.Trim();
                if (list.Any(o => string.Equals(o.Name, name, StringComparison.CurrentCultureIgnoreCase))) continue;
                list.Add(new LampNumberPattern { Name = name, Pattern = pattern, Clicks = Math.Max(0, p.Clicks) });
            }
            return list.Take(30).ToList();
        }

        /// <summary>Asks, then deletes a saved pattern.</summary>
        void DeleteLampPattern(LampNumberPattern p)
        {
            bool yes;
            app.ModalOpen = true;
            try { yes = DeleteDialog.AskPattern(this, S.SettingsTheme, p.Name, p.Pattern); }
            finally { app.ModalOpen = false; }
            if (!yes) return;
            S.LampNumberPatterns.RemoveAll(o => o.Name == p.Name && o.Pattern == p.Pattern);
            app.Save();
        }

        // ---------- its reminders ----------

        const int RemindersListed = 12;   // in the menu; Settings lists them all

        MenuItem RemindersMenu()
        {
            var all = S.Reminders.OrderBy(r => r.At).ToList();
            var menu = SubMenu("Your reminders", "", all.Count == 0 ? "None" : all.Count.ToString(CultureInfo.CurrentCulture));
            if (all.Count == 0) { TurnOff(menu, "You have none yet"); return menu; }
            Into(menu, Heading("Click one to change or delete it"));
            foreach (var r in all.Take(RemindersListed))
            {
                var one = r;
                var mi = Item(Quoted(r.Text, 30), () => EditReminder(one));
                mi.InputGestureText = r.At <= DateTime.Now ? "flashing now" : ReminderWhen(r.At);
                mi.ToolTip = r.Text.Trim();
                Into(menu, mi);
            }
            if (all.Count > RemindersListed) Into(menu, Item("All " + all.Count + " in Settings...", app.OpenReminderSettings));
            return menu;
        }

        void AddReminder()
        {
            var r = new LampReminder { At = ReminderDialog.UsualTime, Text = "" };
            if (ReminderDialog.Ask(this, app, r, true) != ReminderDialog.Answer.Keep) return;
            S.Reminders.Add(r);
            app.Save();
            ApplyLampText();
        }

        void EditReminder(LampReminder r)
        {
            var answer = ReminderDialog.Ask(this, app, r, false);
            if (answer == ReminderDialog.Answer.Cancel) return;
            if (answer == ReminderDialog.Answer.Delete) S.Reminders.Remove(r);
            app.Save();
            ApplyLampText();
        }

        // ---------- building it ----------

        static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, FontSize = 11, Opacity = 0.6, IsHitTestVisible = false, Margin = new Thickness(0, 2, 0, 3) };
        }

        static string Quoted(string text, int most) { return "“" + Shorten(text.Trim(), most) + "”"; }

        /// <summary>An entry that opens a submenu, with what it's set to now beside its name.</summary>
        static MenuItem SubMenu(string header, string glyph, string now)
        {
            return new MenuItem { Header = header, InputGestureText = now, Icon = new TextBlock { Text = glyph, FontFamily = TileView.IconFont, FontSize = 14 } };
        }

        static void Into(MenuItem menu, object entry) { menu.Items.Add(SettingsTheme.MenuEntry(entry)); }

        static MenuItem Choice(string header, bool chosen, Action pick) { return Item(header, pick, chosen, null); }

        /// <summary>An entry that can't be used as things are, saying why when you point at it.</summary>
        static void TurnOff(MenuItem mi, string why)
        {
            mi.IsEnabled = false;
            mi.ToolTip = why;
            ToolTipService.SetShowOnDisabled(mi, true);
        }

        // ---------- test copies only ----------

        static string DescribeMenu(IEnumerable<object> items)
        {
            return string.Join(" | ", items.Select(o =>
            {
                var mi = o as MenuItem;
                var heading = o as TextBlock ?? (mi == null ? null : mi.Header as TextBlock);
                if (heading != null) return "(" + (heading.Text.Length > 0 ? heading.Text : heading.Tag as string) + ")";
                if (mi == null) return o is Separator ? "---" : "?";
                var chips = mi.Header as WrapPanel;
                if (chips != null)
                    return "[buttons: " + string.Join(", ", chips.Children.OfType<Border>().Select(b => ((LampNumberPattern)b.Tag).Name + " x" + ((LampNumberPattern)b.Tag).Clicks
                        + (b.BorderThickness.Left > 0 ? " (in use)" : ""))) + "]";
                var list = mi.Header as ScrollViewer;
                if (list != null && list.Content is StackPanel)
                {
                    var lines = ((StackPanel)list.Content).Children.OfType<Border>().ToList();
                    return "[" + mi.Tag + " list, " + lines.Count + (lines.Count > ListLinesShown ? " (scrolls, " + ListLinesShown + " at a time)" : "") + ": "
                        + string.Join(", ", lines.Select(LineWords)) + "]";
                }
                if (list != null)
                {
                    var g = (Grid)list.Content;
                    return "[list of " + g.RowDefinitions.Count + ": " + string.Join(" / ", g.Children.OfType<TextBlock>().Where(t => Grid.GetRow(t) < 3).GroupBy(t => Grid.GetRow(t))
                        .Select(r => string.Join(", ", r.Select(t => t.Text)))) + (g.RowDefinitions.Count > 3 ? " / ..." : "") + "]";
                }
                var button = mi.Header as Border;
                if (button != null && button.Tag is string) return button.Tag + (button.Opacity < 1 ? " (off)" : "");
                var chipRow = mi.Header as StackPanel;
                if (chipRow != null && chipRow.Tag as string == "chips") return "[" + string.Join(", ", chipRow.Children.OfType<Border>().Select(ChipWords)) + "]";
                var row = mi.Header as FrameworkElement;
                if (row != null && row.Tag is Slider) return HeaderText(mi) + " [" + ((Slider)row.Tag).Value + "]";
                return (mi.IsChecked ? "[x] " : "") + HeaderText(mi) + (mi.Header is DockPanel ? " (dot, X)" : "") + (mi.InputGestureText.Length > 0 ? " = " + mi.InputGestureText : "") + (mi.IsEnabled ? "" : " (off)")
                    + (mi.HasItems ? " > {" + DescribeMenu(mi.Items.Cast<object>()) + "}" : "");
            }));
        }

        /// <summary>An entry's words (a custom color's entry has them in its Tag).</summary>
        static string HeaderText(MenuItem mi) { return mi.Header as string ?? mi.Tag as string; }

        /// <summary>The entry whose words start with that: "Computing", or "Height&gt;9 lamps" to say in which submenu.</summary>
        MenuItem FindInLampMenu(string words)
        {
            int arrow = words.LastIndexOf('>');   // "Activity & Colors>Default>Computing": Computing, in Default
            string parent = arrow < 0 ? null : words.Substring(0, arrow), start = words.Substring(arrow + 1);
            if (parent != null && parent.IndexOf('>') >= 0) parent = parent.Substring(parent.LastIndexOf('>') + 1);
            Func<MenuItem, string, bool> starts = (m, w) => HeaderText(m) != null && HeaderText(m).StartsWith(w, StringComparison.OrdinalIgnoreCase);
            Func<IEnumerable<MenuItem>, IEnumerable<MenuItem>> flat = null;
            flat = list => list.SelectMany(m => new[] { m }.Concat(flat(m.Items.OfType<MenuItem>())));   // submenus in submenus too
            var all = flat(LampMenuItems().OfType<MenuItem>()).ToList();
            var among = parent == null ? all : all.Where(m => starts(m, parent)).SelectMany(m => m.Items.OfType<MenuItem>());
            return among.FirstOrDefault(m => starts(m, start));
        }

        /// <summary>A saved pattern's button in the menu, by its name.</summary>
        static Border PatternButtonIn(IEnumerable<object> items, string name)
        {
            var row = items.OfType<MenuItem>().Select(m => m.Header as WrapPanel).FirstOrDefault(w => w != null);
            return row == null ? null : row.Children.OfType<Border>().FirstOrDefault(b => string.Equals(((LampNumberPattern)b.Tag).Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The Random numbers part of a description of the menu.</summary>
        static string NumbersPart(string menu)
        {
            int from = menu.IndexOf("(Random numbers)"), to = menu.IndexOf("(Reminders)");
            return from < 0 ? menu : to > from ? menu.Substring(from, to - from) : menu.Substring(from);
        }

        /// <summary>The menu and each of its submenus, drawn off screen (none is opened) in a light and a dark theme.</summary>
        void SaveLampMenuPictures(string file)
        {
            var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            wait.Tick += delegate
            {
                wait.Stop();
                var keep = SettingsTheme.MenuMode;
                var rows = new List<List<System.Windows.Media.Imaging.BitmapSource>>();
                foreach (var mode in new[] { SettingsThemeMode.Light, SettingsThemeMode.Dark })
                {
                    SettingsTheme.MenuMode = mode;
                    var row = new List<System.Windows.Media.Imaging.BitmapSource> { MenuPicture(BuildMenu(LampMenuItems())) };
                    foreach (var sub in LampMenuItems().OfType<MenuItem>().Where(m => m.HasItems))
                    {
                        var entries = sub.Items.Cast<object>().ToList();
                        var inner = entries.OfType<MenuItem>().Where(m => m.HasItems).ToList();   // a submenu's own submenus, after it
                        sub.Items.Clear();
                        row.Add(MenuPicture(BuildMenu(entries)));
                        foreach (var deeper in inner)
                        {
                            var more = deeper.Items.Cast<object>().ToList();
                            deeper.Items.Clear();
                            row.Add(MenuPicture(BuildMenu(more)));
                        }
                    }
                    rows.Add(row);
                }
                SettingsTheme.MenuMode = keep;
                double width = rows.Max(r => r.Sum(b => b.Width + 14)) + 14, y = 14, height = rows.Sum(r => r.Max(b => b.Height) + 14) + 14;
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x5c, 0x66, 0x70)), null, new Rect(0, 0, width, height));
                    foreach (var r in rows)
                    {
                        double x = 14;
                        foreach (var b in r) { dc.DrawImage(b, new Rect(x, y, b.Width, b.Height)); x += b.Width + 14; }
                        y += r.Max(b => b.Height) + 14;
                    }
                }
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = System.IO.File.Create(file)) enc.Save(fs);
            };
            wait.Start();
        }
    }
}
