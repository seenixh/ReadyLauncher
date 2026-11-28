using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The strip menu's Activity &amp; Colors: how busy the lamps are and what colors they are, in one submenu; custom
    /// activities and colors saved under a name; and both picked at random, right away or each time the launcher opens.
    /// </summary>
    internal partial class LauncherWindow
    {
        static readonly Random lampDice = new Random();
        LampActivityLevels randomActivity;   // the custom activity last picked at random (what "Add random activity" saves)

        static readonly LampActivityParts[] ActivityParts = { LampActivityParts.Strip, LampActivityParts.Monitor, LampActivityParts.Red };
        static readonly LampRandomColors[] RandomColorKindsAll = { LampRandomColors.CustomSingle, LampRandomColors.CustomMultiple, LampRandomColors.RandomSingle, LampRandomColors.RandomMultiple };

        // ---------- the menu ----------

        /// <summary>
        /// Activity &amp; Colors. Activity: the ready-made ones (in a submenu), or your own, with ticks for the lamps it
        /// changes (the strip's, the monitor's and its mini lamps), two sliders for them, saved ones, and one picked at
        /// random. Colors: red and amber, your own (the ready-made ones in a submenu, your custom ones here), pastel, saved
        /// ones, and colors picked at random. The menu stays open for all of it, and the lamps change as you go. It opens
        /// high, so it doesn't cover the monitor.
        /// </summary>
        MenuItem LampLooksMenu()
        {
            var menu = SubMenu("Activity & Colors", "", ActivityNow());
            menu.Tag = HighSubmenu;   // it opens high, clear of the lamps
            FillLooks(menu);
            return menu;
        }

        /// <summary>
        /// What's in Activity &amp; Colors. (While it's open, nothing in it is taken out or made again: taking out what
        /// was just clicked would close the menu. Something new for a list goes in beside what's there.)
        /// </summary>
        void FillLooks(MenuItem menu)
        {
            menu.Items.Clear();
            var refresh = new List<Action>();
            Action marks = () =>
            {
                foreach (var r in refresh) r();
                menu.InputGestureText = ActivityNow();
            };
            AddActivityPart(menu, refresh, marks);
            Into(menu, new Separator());
            AddColorsPart(menu, refresh, marks);
            marks();
        }

        /// <summary>
        /// After a window of its own closed the menu (the color wheel, a name to type, or "Delete?"): the strip's menu again,
        /// where it was, with Activity &amp; Colors open, to carry on where you were. Test copies only say so.
        /// </summary>
        void ReopenLooks()
        {
            if (Program.TestMode) { if (testNote != null) testNote("  (the strip's menu opens again where it was, with Activity & Colors open)"); return; }
            if (!IsVisible) return;
            var items = LampMenuItems();
            var looks = items.OfType<MenuItem>().FirstOrDefault(m => m.Tag as string == HighSubmenu);
            var menu = OpenLampMenu(items, lastLampMenuAt);
            if (looks != null)
                Dispatcher.BeginInvoke(new Action(() => { if (menu.IsOpen) looks.IsSubmenuOpen = true; }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>What a submenu of the strip's menu that opens high (level with the top of the menu) has as its Tag.</summary>
        const string HighSubmenu = "high";

        /// <summary>A part of a menu's heading, with its icon: a little bigger than the other headings.</summary>
        static TextBlock SectionHeading(string glyph, string text)
        {
            var tb = new TextBlock { FontSize = 13, Opacity = 0.8, IsHitTestVisible = false, Margin = new Thickness(0, 3, 0, 3), Tag = text };
            tb.Inlines.Add(new System.Windows.Documents.Run(glyph) { FontFamily = TileView.IconFont, FontSize = 13, BaselineAlignment = BaselineAlignment.Center });
            tb.Inlines.Add(new System.Windows.Documents.Run("  " + text));
            return tb;
        }

        void AddActivityPart(MenuItem menu, List<Action> refresh, Action marks)
        {
            Into(menu, SectionHeading("", "Activity"));

            // Default: every lamp at speed 5, with a quarter of them lit. Your own: set with the sliders.
            Action fill = null;
            var standard = Item("Default", () =>
            {
                var c = S.LampCustomActivity;
                c.StripSpeed = c.MonitorSpeed = c.LedSpeed = DefaultActivity.StripSpeed;
                c.StripLamps = c.MonitorLamps = c.LedLamps = DefaultActivity.StripLamps;
                S.LampRandomActivity = false;   // it stays until you choose otherwise
                SetLampActivity(LampActivity.Custom);
                app.Save();
                fill();
                marks();
            }, false, null);
            standard.StaysOpenOnClick = true;
            standard.ToolTip = "All the lamps (the strip's, the monitor's and its mini lamps) at speed " + DefaultActivity.StripSpeed + ", with "
                + DefaultActivity.StripLamps + "% of them lit. Choosing it switches Random activity off.";
            refresh.Add(() => standard.IsChecked = DefaultActivityInUse);
            Into(menu, standard);

            var custom = Item(CustomLampsText, () => { SetLampActivity(LampActivity.Custom); marks(); }, false, null);
            custom.StaysOpenOnClick = true;
            custom.ToolTip = "Your own activity: how quick the lamps are, and how many of them are lit. Tick the lamps the sliders change; the lamps change as you move them.";
            refresh.Add(() => custom.IsChecked = S.LampMode == LampActivity.Custom && !DefaultActivityInUse);
            Into(menu, custom);

            Slider speed = null, lamps = null;
            bool filling = false;
            fill = () =>   // the sliders show the first ticked lamps' settings
            {
                filling = true;
                speed.Value = PartLevel(true);
                lamps.Value = PartLevel(false);
                filling = false;
            };
            var parts = ChipRow();
            foreach (var p in ActivityParts)
            {
                var one = p;
                parts.Children.Add(Toggle(PartName(one), PartTip(one), () => (S.LampActivityParts & one) != 0, null, () =>
                {
                    var now = S.LampActivityParts ^ one;
                    if ((now & LampActivityParts.All) == 0) return;   // at least one stays ticked
                    S.LampActivityParts = now;
                    app.Save();
                    fill();
                    marks();
                }, refresh, 0));
            }
            Into(menu, ChipEntry(parts, "Lamps the sliders change"));
            Action<bool, int> set = (isSpeed, v) =>
            {
                if (filling) return;
                var c = S.LampCustomActivity;
                var on = S.LampActivityParts;
                if ((on & LampActivityParts.Strip) != 0) { if (isSpeed) c.StripSpeed = v; else c.StripLamps = v; }
                if ((on & LampActivityParts.Monitor) != 0) { if (isSpeed) c.MonitorSpeed = v; else c.MonitorLamps = v; }
                if ((on & LampActivityParts.Red) != 0) { if (isSpeed) c.LedSpeed = v; else c.LedLamps = v; }
                CustomLevelChanged();
                marks();
            };
            var speedEntry = SliderEntry("Speed", PartLevel(true), LampActivityLevels.MinSpeed, LampActivityLevels.MaxSpeed, "", "Speed", SpeedTip, v => set(true, v));
            var lampsEntry = SliderEntry("Lamps", PartLevel(false), 0, 100, "%", "Lamps", LampsTip, v => set(false, v));
            speed = (Slider)((FrameworkElement)speedEntry.Header).Tag;
            lamps = (Slider)((FrameworkElement)lampsEntry.Header).Tag;
            Into(menu, speedEntry);
            Into(menu, lampsEntry);

            // Saved ones: clicking one uses it (the menu stays open); its X deletes it.
            Into(menu, new Separator());
            var savedHeading = Heading(S.LampActivities.Count > 0 ? "Saved activities: click one to use it" : "Saved activities");
            Into(menu, savedHeading);
            StackPanel savedLines;
            var savedList = ScrollList("activity", out savedLines);
            Func<LampActivityLevels, Border> savedRow = one =>
            {
                Border row = null;
                var delete = DeleteMark(() => AfterMenu(row, () => { DeleteLampActivity(one); ReopenLooks(); }));
                ((FrameworkElement)delete).ToolTip = "Delete this saved activity";
                row = ListLine(one.Name, new TextBlock { Text = Shorten(one.Name, 34), VerticalAlignment = VerticalAlignment.Center }, one.Summary(),
                    () => S.LampMode == LampActivity.Custom && one.SameAs(S.LampCustomActivity), () =>
                    {
                        var c = S.LampCustomActivity;
                        c.StripSpeed = one.StripSpeed; c.StripLamps = one.StripLamps;
                        c.MonitorSpeed = one.MonitorSpeed; c.MonitorLamps = one.MonitorLamps;
                        c.LedSpeed = one.LedSpeed; c.LedLamps = one.LedLamps;
                        S.LampRandomActivity = false;   // a saved one stays until you choose otherwise
                        SetLampActivity(LampActivity.Custom);
                        app.Save();
                        fill();
                        marks();
                    }, delete, refresh);
                return row;
            };
            foreach (var a in S.LampActivities) savedLines.Children.Add(savedRow(a));
            Into(menu, savedList);
            MenuItem save = null;
            save = Item("Save this custom activity...", () => AfterMenu(save, () => { SaveLampActivity(); ReopenLooks(); }), false, "");
            save.ToolTip = "Keep your own activity's settings under a name, to use again later";
            Into(menu, save);

            // At random: now, or each time the launcher opens.
            var random = ChipRow();
            random.Children.Add(Toggle("Random activity",
                "Each time the launcher opens, the lamps ticked above get a speed and a number lit, picked at random. Choosing a ready-made or saved activity switches this off.",
                () => S.LampRandomActivity, null, () =>
                {
                    S.LampRandomActivity = !S.LampRandomActivity;
                    app.Save();
                    marks();
                }, refresh, 0));
            random.Children.Add(ChipButton("", "Randomize now", "Picks a speed and a number lit at random for the lamps ticked above, right now", null, () =>
            {
                RandomizeLampActivity(true);
                fill();
                marks();
            }, refresh));
            Into(menu, ChipEntry(random, "Random activity"));
            Into(menu, MenuButton("", "Add random activity to saved activities", "Keeps the activity picked at random in your saved activities, as Random 1, Random 2...",
                RandomActivityUnsaveable, () =>
                {
                    var added = SaveRandomActivity();
                    if (added != null) AddLine(savedLines, savedRow(added));   // at the end of the list, in sight
                    savedHeading.Text = "Saved activities: click one to use it";
                    marks();
                }, refresh));
        }

        void AddColorsPart(MenuItem menu, List<Action> refresh, Action marks)
        {
            Into(menu, SectionHeading("", "Colors"));
            var usual = Item("Default (red & amber)", () =>
            {
                FixedLampColors();
                S.LampColors = LampColors.RedAmber;
                S.LampStripColors.Clear();   // every lamp back to the usual, the strip's, the monitor's and its mini lamps too
                S.LampMonitorColors.Clear();
                S.LampMiniColors.Clear();
                S.LampLeftColors.Clear();
                S.LampMiddleColors.Clear();
                S.LampRightColors.Clear();
                ApplyLampColors();
                marks();
            }, false, null);
            usual.StaysOpenOnClick = true;
            usual.ToolTip = "The lamps' usual colors, red and amber: back to them whenever you like (all of them, the monitor's lamps and mini lamps too)";
            refresh.Add(() => usual.IsChecked = S.LampColorList.Count == 0 && S.LampColors == LampColors.RedAmber);
            Into(menu, usual);
            Into(menu, new Separator());

            // Your custom colors, from the color wheel: tick one or more to use them.
            if (S.LampCustom.Count > 0) Into(menu, Heading("Your custom colors: tick one or more to use them"));
            StackPanel customLines;
            var customList = ScrollList("custom", out customLines);
            foreach (var c in S.LampCustom)   // from the color wheel: click to tick, the dot to change it, the X to delete it
            {
                var one = c;
                Border row = null;
                System.Windows.Shapes.Ellipse dot;
                var change = ClickableDot(LampPalette.Swatch(one.Color, S.LampPastel), "Change this color with the color wheel", out dot, () => AfterMenu(row, () => { PickLampCustom(one); ReopenLooks(); }));
                var delete = DeleteMark(() => AfterMenu(row, () => { DeleteLampCustom(one); ReopenLooks(); }));
                var shows = new StackPanel { Orientation = Orientation.Horizontal };
                shows.Children.Add(change);
                shows.Children.Add(new TextBlock { Text = one.Color, VerticalAlignment = VerticalAlignment.Center });
                row = ListLine(one.Color, shows, "One of your custom colors: click to tick or untick it (the lamps use the ones ticked), its dot to change it, its X to delete it."
                    + " Right-click it to use it for the lamp strip, the monitor's lamps or its mini lamps on their own.",
                    () => OwnColorsInUse && one.On, () => { TickCustomColor(one); marks(); }, delete, refresh,
                    line => ShowPartColorsMenu(line, one.Color, new List<string> { one.Color }, marks));
                row.DataContext = new List<string> { one.Color };   // (test copies: its colors)
                refresh.Add(() => dot.Fill = new SolidColorBrush(LampPalette.Swatch(one.Color, S.LampPastel)));
                customLines.Children.Add(row);
            }
            Into(menu, customList);
            MenuItem add = null;
            add = Item("Add a custom color...", () => AfterMenu(add, () => { PickLampCustom(null); ReopenLooks(); }), false, "");
            add.ToolTip = "Choose any color from a color wheel, for your custom colors";
            Into(menu, add);
            var pastel = Item("Pastel (softer colors)", () => { S.LampPastel = !S.LampPastel; ApplyLampColors(); marks(); });
            pastel.StaysOpenOnClick = true;
            pastel.ToolTip = "Softer, paler lamps, whatever their colors";
            refresh.Add(() => pastel.IsChecked = S.LampPastel);
            Into(menu, pastel);

            // Saved ones: clicking one uses it (the menu stays open); its X deletes it.
            Into(menu, new Separator());
            var savedHeading = Heading(S.LampColorSets.Count > 0 ? "Saved colors: click one to use them" : "Saved colors");
            Into(menu, savedHeading);
            StackPanel savedLines;
            var savedList = ScrollList("colors", out savedLines);
            Func<LampColorSet, Border> savedRow = one =>
            {
                var colors = one.List();
                Border row = null;
                var delete = DeleteMark(() => AfterMenu(row, () => { DeleteLampColorSet(one); ReopenLooks(); }));
                ((FrameworkElement)delete).ToolTip = "Delete these saved colors";
                var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                var shows = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(dots, Dock.Right);
                shows.Children.Add(dots);
                shows.Children.Add(new TextBlock { Text = Shorten(one.Name, 30), VerticalAlignment = VerticalAlignment.Center });
                row = ListLine(one.Name, shows, ColorWords(colors) + ". Right-click to use these for the lamp strip, the monitor's lamps or its mini lamps on their own.",
                    () => S.LampColorList.Count > 0 && !S.LampColorsAtRandom && SameColors(colors, S.LampColorList),
                    () => { UseLampColorSet(one); marks(); }, delete, refresh, line => ShowPartColorsMenu(line, "\u201c" + Shorten(one.Name, 24) + "\u201d", colors, marks));
                row.DataContext = colors;   // (test copies: its colors)
                refresh.Add(() =>
                {
                    dots.Children.Clear();
                    foreach (var e in colors.Take(6))
                        dots.Children.Add(new System.Windows.Shapes.Ellipse
                        {
                            Width = 10, Height = 10, Margin = new Thickness(0, 0, 3, 0), Fill = new SolidColorBrush(LampPalette.EntrySwatch(e, S.LampPastel)),
                            Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), StrokeThickness = 1
                        });
                    if (colors.Count > 6) dots.Children.Add(new TextBlock { Text = "+" + (colors.Count - 6), FontSize = 10, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
                });
                return row;
            };
            foreach (var set in S.LampColorSets) savedLines.Children.Add(savedRow(set));
            Into(menu, savedList);
            MenuItem save = null;
            save = Item("Save these colors...", () => AfterMenu(save, () => { SaveLampColors(null); ReopenLooks(); }), false, "");
            refresh.Add(() => Allow(save, ColorsUnsaveable(), "Keep the colors the lamps have now under a name, to use again later"));
            Into(menu, save);

            // At random: now, or each time the launcher opens; with more than one kind ticked, one of them each time.
            Into(menu, Heading("Random colors: tick one kind or more"));
            var rows = new[] { ChipRow(), ChipRow() };
            for (int k = 0; k < RandomColorKindsAll.Length; k++)
            {
                var kind = RandomColorKindsAll[k];
                rows[k / 2].Children.Add(Toggle(RandomKindName(kind), RandomKindTip(kind), () => (S.LampRandomColors & kind) != 0,
                    () => (S.LampRandomColors & kind) != 0 || RandomKindPossible(kind) ? null : RandomKindWhyNot(kind), () =>
                    {
                        S.LampRandomColors ^= kind;
                        if (S.LampRandomColors == LampRandomColors.None && S.LampColorsAtRandom)
                        {
                            // None ticked now: back to the colors there were before the random ones.
                            S.LampColorList.Clear();
                            S.LampColorsAtRandom = false;
                            ApplyLampColors();
                        }
                        else app.Save();
                        marks();
                    }, refresh, 150));
            }
            Into(menu, ChipEntry(rows[0], "Random colors"));
            Into(menu, ChipEntry(rows[1], "Random colors 2"));
            var now = ChipRow();
            now.Children.Add(ChipButton("", "Randomize now", "Picks new colors at random right now, of a kind ticked above",
                () => RandomColorKinds().Count > 0 ? null : "Tick a kind of random colors above first", () =>
                {
                    RandomizeLampColors(true);
                    marks();
                }, refresh));
            Into(menu, ChipEntry(now, "Randomize colors"));
            Into(menu, MenuButton("", "Add random colors to saved colors", "Keeps the colors picked at random in your saved colors, as Random 1, Random 2...",
                RandomColorsUnsaveable, () =>
                {
                    var added = SaveRandomColors();
                    if (added != null) AddLine(savedLines, savedRow(added));   // at the end of the list, in sight
                    savedHeading.Text = "Saved colors: click one to use them";
                    marks();
                }, refresh));
        }

        /// <summary>An entry usable or not as things are now, saying why not (or what it does) when pointed at.</summary>
        static void Allow(MenuItem mi, string whyNot, string tip)
        {
            mi.IsEnabled = whyNot == null;
            mi.ToolTip = whyNot ?? tip;
            ToolTipService.SetShowOnDisabled(mi, true);
        }

        Action<string> partMenuProbe;   // test copies: describe a color's right-click menu instead of opening it

        System.Windows.Controls.Primitives.Popup partPopup;   // a color's "Use for" panel, while it's open
        Panel partPopupIn;                                    // the line it belongs to (its panel)

        /// <summary>
        /// Right-clicking a custom color or saved colors in Activity &amp; Colors: a small panel, at the mouse, of which lamps
        /// use those colors of their own, the strip's, the monitor's, or its mini lamps. Tick one or more; untick one and
        /// those lamps go back to the colors the others have (the mini lamps: red). It belongs to the strip's menu, the way
        /// its submenus do, so the menu stays open with it (a menu of its own took the mouse from the strip's menu, which
        /// closed it). It goes on a click anywhere else in the menu, or when the menu closes.
        /// </summary>
        void ShowPartColorsMenu(FrameworkElement line, string what, List<string> colors, Action changed)
        {
            if (partMenuProbe != null) { partMenuProbe(DescribeMenu(PartColorsItems(what, colors, changed))); return; }
            ClosePartPopup();
            var dock = ((Border)line).Child as DockPanel;
            if (dock == null) return;
            var refresh = new List<Action>();
            var lines = new StackPanel();
            lines.Children.Add(new TextBlock { Text = "Use " + what + " for", FontSize = 11, Opacity = 0.6, Margin = new Thickness(8, 2, 10, 3) });
            Action after = () =>
            {
                foreach (var r in refresh) r();
                changed();
            };
            foreach (var p in ActivityParts)
            {
                var part = p;
                var shows = new StackPanel { Orientation = Orientation.Horizontal };
                shows.Children.Add(new TextBlock { Text = PartName(part), VerticalAlignment = VerticalAlignment.Center });
                if (part == LampActivityParts.Strip)   // and its thirds: up to two of them
                    for (int k = 0; k < 3; k++) shows.Children.Add(ThirdChip(k, colors, refresh, after));
                lines.Children.Add(ListLine(PartName(part), shows, PartColorsTip(part), () => SameColors(PartColors(part), colors), () =>
                {
                    TogglePartColors(part, colors);
                    after();
                }, null, refresh));
            }
            var panel = new Border
            {
                Child = lines, Background = new SolidColorBrush(SettingsTheme.MenuColor("Control")), BorderBrush = new SolidColorBrush(SettingsTheme.MenuColor("Border")),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Margin = new Thickness(0, 0, 8, 8),
                MinWidth = 180, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Direction = 270, Opacity = 0.35 }
            };
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                Child = panel, AllowsTransparency = true, StaysOpen = true, Focusable = false, HorizontalOffset = 2,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint, PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None
            };
            if (Program.TestMode)   // test copies: far off screen, where nobody sees it
            {
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                popup.HorizontalOffset = popup.VerticalOffset = -30000;
            }
            // In the line itself (taking no room there), so it's part of the menu: the menu keeps the mouse for it.
            DockPanel.SetDock(popup, Dock.Left);
            dock.Children.Insert(0, popup);
            partPopup = popup;
            partPopupIn = dock;
            var root = RootMenu(line);
            if (root != null)
            {
                RoutedEventHandler gone = null;
                MouseButtonEventHandler elsewhere = null;
                gone = delegate { ClosePartPopup(); root.Closed -= gone; };
                elsewhere = (s, e) =>
                {
                    if (partPopup != popup) { root.RemoveHandler(UIElement.PreviewMouseDownEvent, elsewhere); return; }
                    for (var d = e.OriginalSource as DependencyObject; d != null; d = VisualTreeHelper.GetParent(d) ?? LogicalTreeHelper.GetParent(d))
                        if (d == panel) return;   // in it: its lines do the rest
                    ClosePartPopup();
                };
                root.Closed += gone;
                root.AddHandler(UIElement.PreviewMouseDownEvent, elsewhere, true);
            }
            line.IsVisibleChanged += (s, e) => { if (!line.IsVisible && partPopup == popup) ClosePartPopup(); };   // Activity & Colors closed
            popup.IsOpen = true;
        }

        void ClosePartPopup()
        {
            if (partPopup == null) return;
            partPopup.IsOpen = false;
            if (partPopupIn != null) partPopupIn.Children.Remove(partPopup);
            partPopup = null;
            partPopupIn = null;
        }

        static readonly string[] ThirdNames = { "left", "middle", "right" };

        /// <summary>
        /// One third of the strip, in a color's "Use for" panel: lit up while that third has these colors of its own; a
        /// click gives it them, or takes them away. Up to two thirds can have one color (all three: tick Lamp strip).
        /// </summary>
        FrameworkElement ThirdChip(int third, List<string> colors, List<Action> refresh, Action after)
        {
            Brush accent = new SolidColorBrush(SettingsTheme.MenuColor("Accent")), onInk = new SolidColorBrush(SettingsTheme.MenuColor("AccentText"));
            Brush ink = new SolidColorBrush(SettingsTheme.MenuColor("Text")), hover = new SolidColorBrush(Color.FromArgb(70, 128, 128, 128));
            var words = new TextBlock { Text = ThirdNames[third], Margin = new Thickness(7, 1, 7, 2), VerticalAlignment = VerticalAlignment.Center };
            var chip = new Border { Child = words, CornerRadius = new CornerRadius(4), Margin = new Thickness(third == 0 ? 14 : 3, -1, 0, -1), Tag = ThirdNames[third] };
            ToolTipService.SetShowOnDisabled(chip, true);
            bool usable = true, on = false;
            Action look = () =>
            {
                on = SameColors(ThirdColors(third), colors);
                string no = ThirdWhyNot(third, colors);
                usable = no == null;
                chip.Background = on ? accent : Brushes.Transparent;
                words.Foreground = on ? onInk : ink;
                words.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
                chip.Opacity = usable ? 1 : 0.45;
                chip.Cursor = usable ? Cursors.Hand : Cursors.Arrow;
                chip.ToolTip = no ?? (on ? "The strip's " + ThirdNames[third] + " third has these colors: click to give it back the strip's"
                    : "Only the " + ThirdNames[third] + " third of the strip gets these colors (up to two thirds; for all of the strip, tick Lamp strip)");
            };
            chip.MouseEnter += delegate { if (usable && !on) chip.Background = hover; };
            chip.MouseLeave += delegate { look(); };
            OwnClick(chip, () =>
            {
                if (!usable) return;
                ToggleThird(third, colors);
                after();
            });
            refresh.Add(look);
            look();
            return chip;
        }

        /// <summary>A third of the strip's own colors (none: the strip's).</summary>
        List<string> ThirdColors(int third) { return third == 0 ? S.LampLeftColors : third == 1 ? S.LampMiddleColors : S.LampRightColors; }

        /// <summary>Why a third can't be given these colors now: two thirds have them already (null: it can).</summary>
        string ThirdWhyNot(int third, List<string> colors)
        {
            if (SameColors(ThirdColors(third), colors)) return null;
            return Enumerable.Range(0, 3).Count(k => SameColors(ThirdColors(k), colors)) >= 2
                ? "Two thirds of the strip have these colors already, the most for one color: for all of it, tick Lamp strip" : null;
        }

        /// <summary>A third of the strip gets these colors of its own, or (if it had them) the strip's again; false: not allowed.</summary>
        bool ToggleThird(int third, List<string> colors)
        {
            if (ThirdWhyNot(third, colors) != null) return false;
            var tidy = SameColors(ThirdColors(third), colors) ? new List<string>() : LampPalette.TidyList(colors);
            if (third == 0) S.LampLeftColors = tidy;
            else if (third == 1) S.LampMiddleColors = tidy;
            else S.LampRightColors = tidy;
            ApplyLampColors();
            return true;
        }

        /// <summary>Test copies only: the same choices, as menu entries (to describe and click them).</summary>
        List<object> PartColorsItems(string what, List<string> colors, Action changed)
        {
            var items = new List<object> { Heading("Use " + what + " for") };
            foreach (var p in ActivityParts)
            {
                var part = p;
                MenuItem mi = null;
                mi = Item(PartName(part), () =>
                {
                    mi.IsChecked = TogglePartColors(part, colors);
                    changed();
                }, SameColors(PartColors(part), colors), null);
                mi.StaysOpenOnClick = true;
                mi.ToolTip = PartColorsTip(part);
                items.Add(mi);
            }
            for (int k = 0; k < 3; k++)   // (in the panel, they're beside Lamp strip)
            {
                int third = k;
                MenuItem mi = null;
                mi = Item("Strip " + ThirdNames[third], () =>
                {
                    ToggleThird(third, colors);
                    mi.IsChecked = SameColors(ThirdColors(third), colors);
                    changed();
                }, SameColors(ThirdColors(third), colors), null);
                mi.StaysOpenOnClick = true;
                if (ThirdWhyNot(third, colors) != null) TurnOff(mi, ThirdWhyNot(third, colors));
                items.Add(mi);
            }
            return items;
        }

        /// <summary>Those lamps get these colors of their own, or (if they had them) go back to the usual; true: they have them now.</summary>
        bool TogglePartColors(LampActivityParts part, List<string> colors)
        {
            bool had = SameColors(PartColors(part), colors);
            SetPartColors(part, had ? new List<string>() : colors);
            return !had;
        }

        string PartColorsTip(LampActivityParts part)
        {
            string which = part == LampActivityParts.Strip ? "the strip's lamps" : part == LampActivityParts.Monitor ? "the monitor's lamps" : "the monitor's mini lamps";
            return "Ticked, " + which + " have these colors, whatever the others have. Unticked, they go back to "
                + (part == LampActivityParts.Red ? "red." : "the colors the rest of the lamps have.")
                + (part != LampActivityParts.Strip && S.LampMonitor == LampMonitorPlace.None ? " (There's no monitor now: choose one in Monitor, in the strip's menu.)" : "");
        }

        /// <summary>The colors of their own that lamps have (none: the same as the others, or for the mini lamps, red).</summary>
        List<string> PartColors(LampActivityParts part)
        {
            return part == LampActivityParts.Strip ? S.LampStripColors : part == LampActivityParts.Monitor ? S.LampMonitorColors : S.LampMiniColors;
        }

        void SetPartColors(LampActivityParts part, List<string> colors)
        {
            var tidy = LampPalette.TidyList(colors);
            if (part == LampActivityParts.Strip) S.LampStripColors = tidy;
            else if (part == LampActivityParts.Monitor) S.LampMonitorColors = tidy;
            else S.LampMiniColors = tidy;
            ApplyLampColors();
        }

        /// <summary>How many lines of a list in the menu show at a time; more scroll.</summary>
        const int ListLinesShown = 4;

        string showLine;   // "activity:Random 2": a line to scroll into sight as the menu opens (one just saved)

        /// <summary>
        /// A list in the menu (saved activities, custom colors, saved colors): its lines one under another, and once there
        /// are more than four, four at a time, with a scroll bar. As the menu opens, one just saved (or else the one in use) is
        /// scrolled into sight. <paramref name="kind"/> is what's in it, for that ("activity", "custom" or "colors").
        /// </summary>
        MenuItem ScrollList(string kind, out StackPanel lines)
        {
            var panel = new StackPanel { Tag = kind };
            lines = panel;
            var scroll = new ScrollViewer
            {
                Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false, MaxHeight = ListLinesShown * 27
            };
            try { scroll.Resources.MergedDictionaries.Add(SettingsTheme.ScrollBarStyles()); } catch (Exception) { }   // slim, in the menu's colors
            panel.SizeChanged += delegate   // four lines high, as they are
            {
                var first = panel.Children.OfType<FrameworkElement>().FirstOrDefault();
                if (first != null && first.ActualHeight > 0) scroll.MaxHeight = ListLinesShown * first.ActualHeight + 1;
            };
            scroll.Loaded += delegate
            {
                var lineNow = panel.Children.OfType<Border>();
                var want = lineNow.FirstOrDefault(b => showLine == kind + ":" + b.Tag) ?? lineNow.FirstOrDefault(LineTicked);
                if (want != null && showLine == kind + ":" + want.Tag) showLine = null;
                if (want != null) Dispatcher.BeginInvoke(new Action(want.BringIntoView), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            return new MenuItem { Header = scroll, Tag = kind, StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(0) };
        }

        /// <summary>A new line at the end of a list in the menu, scrolled into sight.</summary>
        void AddLine(StackPanel lines, Border line)
        {
            lines.Children.Add(line);
            Dispatcher.BeginInvoke(new Action(line.BringIntoView), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// A line in a list in the menu, looking like any line of the menu: a tick (in the icon's place) while it's in use,
        /// what it shows, and an X to delete it at the end; shaded when pointed at. It never takes the keyboard (so the menu
        /// stays open); clicking it does <paramref name="click"/>, and its dot or X do their own things.
        /// </summary>
        static Border ListLine(string name, UIElement shows, string tip, Func<bool> ticked, Action click, FrameworkElement delete, List<Action> refresh,
                               Action<FrameworkElement> rightClick = null)
        {
            var hover = new SolidColorBrush(SettingsTheme.MenuColor("Selection"));
            var tick = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 1 6 L 4.5 9.5 L 11 2.5"), Stroke = new SolidColorBrush(SettingsTheme.MenuColor("Accent")), StrokeThickness = 2,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(1, 0, 0, 0)
            };
            var tickPlace = new Border { Width = 24, Child = tick };   // (the menu's icon column: its words start right after it)
            var line = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(tickPlace, Dock.Left);
            line.Children.Add(tickPlace);
            if (delete != null) { DockPanel.SetDock(delete, Dock.Right); line.Children.Add(delete); }
            line.Children.Add(shows);
            var row = new Border { Child = line, Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5, 10, 5), Tag = name, ToolTip = tip, Cursor = Cursors.Hand };
            row.MouseEnter += delegate { row.Background = hover; };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            OwnClick(row, click);
            if (rightClick != null)
            {
                row.MouseRightButtonDown += (s, e) => { e.Handled = true; };
                row.MouseRightButtonUp += (s, e) => { e.Handled = true; rightClick(row); };
            }
            refresh.Add(() => tick.Visibility = ticked() ? Visibility.Visible : Visibility.Hidden);
            tick.Visibility = ticked() ? Visibility.Visible : Visibility.Hidden;
            return row;
        }

        /// <summary>Whether a line in a list shows its tick (it's the one in use).</summary>
        static bool LineTicked(Border line)
        {
            var dock = line.Child as DockPanel;
            var place = dock == null ? null : dock.Children.OfType<Border>().FirstOrDefault();
            return place != null && place.Child != null && place.Child.Visibility == Visibility.Visible;
        }

        /// <summary>Test copies only: a line in a list, in words: "[x] Random 1 (X)".</summary>
        static string LineWords(Border line)
        {
            var dock = line.Child as DockPanel;
            bool x = dock != null && dock.Children.OfType<Border>().Any(c => c.Tag as string == "delete");
            return (LineTicked(line) ? "[x] " : "") + line.Tag + (x ? " (X)" : "");
        }

        /// <summary>
        /// A line in a menu that looks like any other (an icon, then its words; shaded when pointed at) but is a button of
        /// its own: it never takes the keyboard, so clicking it, or it going grey afterwards, can't close the menu, which
        /// stays open. <paramref name="whyNot"/> says why it can't be clicked as things are (null: it can).
        /// </summary>
        static MenuItem MenuButton(string glyph, string text, string tip, Func<string> whyNot, Action click, List<Action> refresh)
        {
            var hover = new SolidColorBrush(SettingsTheme.MenuColor("Selection"));
            var line = new DockPanel();
            var icon = new TextBlock { Text = glyph, FontFamily = TileView.IconFont, FontSize = 14, Width = 24, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(icon, Dock.Left);
            line.Children.Add(icon);
            line.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var row = new Border { Child = line, Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 5, 10, 5), Tag = text };
            ToolTipService.SetShowOnDisabled(row, true);
            bool usable = true;
            Action look = () =>
            {
                string no = whyNot == null ? null : whyNot();
                usable = no == null;
                row.Opacity = usable ? 1 : 0.45;
                row.Cursor = usable ? Cursors.Hand : Cursors.Arrow;
                row.ToolTip = no ?? tip;
                if (!usable) row.Background = Brushes.Transparent;
            };
            Action press = () => { if (usable) click(); };
            row.MouseEnter += delegate { if (usable) row.Background = hover; };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            OwnClick(row, press);
            refresh.Add(look);
            look();
            var mi = new MenuItem { Header = row, Tag = text, StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(0) };
            mi.Click += delegate { press(); };   // (test copies click it this way; a real click is the row's own)
            return mi;
        }

        // ---------- small things to click, side by side ----------

        /// <summary>A row of small things to click, side by side in one menu entry.</summary>
        static StackPanel ChipRow() { return new StackPanel { Orientation = Orientation.Horizontal, Tag = "chips", Background = Brushes.Transparent }; }

        /// <summary>A menu entry that's only a row of them: the menu stays open as they're clicked.</summary>
        static MenuItem ChipEntry(Panel row, string tag)
        {
            return new MenuItem { Header = row, Tag = tag, StaysOpenOnClick = true, Focusable = false, Template = PlainEntry, Padding = new Thickness(34, 1, 12, 1) };
        }

        /// <summary>
        /// Something to tick in a row: a box with a tick in it while it's on, and its words; it lights up when pointed at.
        /// <paramref name="whyNot"/> says why it can't be clicked as things are (null: it can).
        /// </summary>
        static FrameworkElement Toggle(string text, string tip, Func<bool> on, Func<string> whyNot, Action click, List<Action> refresh, double width)
        {
            Color ink = SettingsTheme.MenuColor("Text"), accent = SettingsTheme.MenuColor("Accent");
            var hover = new SolidColorBrush(SettingsTheme.MenuColor("Selection"));
            var tick = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3 7 L 5.8 9.8 L 11 4"), Stroke = new SolidColorBrush(SettingsTheme.MenuColor("AccentText")), StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
            };
            var box = new Border { Width = 15, Height = 15, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), Child = tick, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            var inside = new StackPanel { Orientation = Orientation.Horizontal };
            inside.Children.Add(box);
            inside.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var chip = new Border { Child = inside, Background = Brushes.Transparent, CornerRadius = new CornerRadius(4), Padding = new Thickness(5, 3, 8, 3), Margin = new Thickness(0, 1, 4, 1), Tag = text };
            if (width > 0) chip.MinWidth = width;
            ToolTipService.SetShowOnDisabled(chip, true);
            bool usable = true;
            Action look = () =>
            {
                bool isOn = on();
                string no = whyNot == null ? null : whyNot();
                usable = no == null;
                tick.Visibility = isOn ? Visibility.Visible : Visibility.Hidden;
                box.Background = isOn ? new SolidColorBrush(accent) : Brushes.Transparent;
                box.BorderBrush = new SolidColorBrush(isOn ? accent : Color.FromArgb(150, ink.R, ink.G, ink.B));
                chip.Opacity = usable ? 1 : 0.45;
                chip.Cursor = usable ? Cursors.Hand : Cursors.Arrow;
                chip.ToolTip = no ?? tip;
            };
            chip.MouseEnter += delegate { if (usable) chip.Background = hover; };
            chip.MouseLeave += delegate { chip.Background = Brushes.Transparent; };
            chip.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; if (usable) click(); };
            refresh.Add(look);
            look();
            return chip;
        }

        /// <summary>A button in a row (like the saved number patterns' ones), with an icon before its words.</summary>
        static FrameworkElement ChipButton(string glyph, string text, string tip, Func<string> whyNot, Action click, List<Action> refresh)
        {
            Color back = SettingsTheme.MenuColor("Border");
            Color ink = (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) / 255 > 0.55 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White;
            Brush plain = new SolidColorBrush(back), hover = new SolidColorBrush(Mix(back, ink, 0.14)), down = new SolidColorBrush(Mix(back, ink, 0.26));
            var inside = new StackPanel { Orientation = Orientation.Horizontal };
            inside.Children.Add(new TextBlock { Text = glyph, FontFamily = TileView.IconFont, FontSize = 12, Foreground = new SolidColorBrush(ink), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 0) });
            inside.Children.Add(new TextBlock { Text = text, Foreground = new SolidColorBrush(ink), VerticalAlignment = VerticalAlignment.Center });
            var chip = new Border { Child = inside, Background = plain, CornerRadius = new CornerRadius(5), Padding = new Thickness(10, 3, 10, 4), Margin = new Thickness(6, 2, 6, 2), Tag = text };
            ToolTipService.SetShowOnDisabled(chip, true);
            bool usable = true;
            Action look = () =>
            {
                string no = whyNot == null ? null : whyNot();
                usable = no == null;
                chip.Opacity = usable ? 1 : 0.45;
                chip.Cursor = usable ? Cursors.Hand : Cursors.Arrow;
                chip.ToolTip = no ?? tip;
            };
            chip.MouseEnter += delegate { if (usable) chip.Background = hover; };
            chip.MouseLeave += delegate { chip.Background = plain; };
            chip.MouseLeftButtonDown += (s, e) => { e.Handled = true; if (usable) chip.Background = down; };
            chip.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                chip.Background = chip.IsMouseOver ? hover : plain;
                if (usable) click();
            };
            refresh.Add(look);
            look();
            return chip;
        }

        /// <summary>Test copies only: a tick or button in a row, in words: "[x] Lamp strip", "Randomize now (off)".</summary>
        static string ChipWords(Border b)
        {
            var inside = b.Child as Panel;
            var box = inside == null ? null : inside.Children.OfType<Border>().FirstOrDefault();
            string tick = box == null ? "" : box.Child != null && box.Child.Visibility == Visibility.Visible ? "[x] " : "[ ] ";
            return tick + b.Tag + (b.Opacity < 1 ? " (off)" : "");
        }

        // ---------- activity ----------

        static string PartName(LampActivityParts p)
        {
            return p == LampActivityParts.Strip ? "Lamp strip" : p == LampActivityParts.Monitor ? "Monitor lamps" : "Monitor mini lamps";
        }

        static string PartTip(LampActivityParts p)
        {
            string which = p == LampActivityParts.Strip ? "the strip's lamps" : p == LampActivityParts.Monitor ? "the monitor's lamps" : "the monitor's mini lamps";
            return "The sliders below, and picking an activity at random, change " + which + " while this is ticked";
        }

        /// <summary>A speed (or how many lit) of the first lamps ticked: what the menu's sliders show.</summary>
        int PartLevel(bool speed)
        {
            var c = S.LampCustomActivity;
            var on = S.LampActivityParts;
            if ((on & LampActivityParts.Strip) != 0) return speed ? c.StripSpeed : c.StripLamps;
            if ((on & LampActivityParts.Monitor) != 0) return speed ? c.MonitorSpeed : c.MonitorLamps;
            return speed ? c.LedSpeed : c.LedLamps;
        }

        /// <summary>The menu's Default activity: every lamp at speed 5, with a quarter of them lit.</summary>
        static readonly LampActivityLevels DefaultActivity = new LampActivityLevels
        {
            StripSpeed = 5, StripLamps = 25, MonitorSpeed = 5, MonitorLamps = 25, LedSpeed = 5, LedLamps = 25
        };

        bool DefaultActivityInUse { get { return S.LampMode == LampActivity.Custom && DefaultActivity.SameAs(S.LampCustomActivity); } }

        /// <summary>
        /// A speed and a number lit, at random, for each of the lamps ticked (the strip's, the monitor's, its mini lamps); the
        /// lamps change to your own activity with them at once. False when there's nothing to change.
        /// </summary>
        bool RandomizeLampActivity(bool save)
        {
            var on = S.LampActivityParts;
            if ((on & LampActivityParts.All) == 0) return false;
            var c = S.LampCustomActivity;
            Func<int> speed = () => lampDice.Next(LampActivityLevels.MinSpeed, LampActivityLevels.MaxSpeed + 1);
            if ((on & LampActivityParts.Strip) != 0) { c.StripSpeed = speed(); c.StripLamps = lampDice.Next(10, 91); }
            if ((on & LampActivityParts.Monitor) != 0) { c.MonitorSpeed = speed(); c.MonitorLamps = lampDice.Next(10, 91); }
            if ((on & LampActivityParts.Red) != 0) { c.LedSpeed = speed(); c.LedLamps = lampDice.Next(3, 41); }   // the mini lamps: never too busy
            randomActivity = c.Copy("");
            if (S.LampMode != LampActivity.Custom)
            {
                S.LampMode = LampActivity.Custom;
                foreach (var p in LampPanel.OnScreen) p.Activity = LampActivity.Custom;
            }
            if (save) app.Save();
            return true;
        }

        /// <summary>Why the activity picked at random can't be added to the saved ones now (null: it can).</summary>
        string RandomActivityUnsaveable()
        {
            if (randomActivity == null || S.LampMode != LampActivity.Custom || !randomActivity.SameAs(S.LampCustomActivity))
                return "Click Randomize now first (or let Random activity pick one when the launcher opens): this saves the activity picked at random";
            var had = S.LampActivities.FirstOrDefault(a => a.SameAs(S.LampCustomActivity));
            if (had != null) return "It's saved already, as " + Shorten(had.Name, 30);
            return null;
        }

        /// <summary>The activity picked at random, saved as "Random 1" (or the next number free); the one saved, or null.</summary>
        LampActivityLevels SaveRandomActivity()
        {
            if (RandomActivityUnsaveable() != null) return null;
            string name = FreeName("Random", S.LampActivities.Select(a => a.Name));
            S.LampActivities = LampActivityLevels.Save(S.LampActivities, name, S.LampCustomActivity);
            app.Save();
            return S.LampActivities.FirstOrDefault(a => a.Name == name);
        }

        /// <summary>"Random 1", or the next number not used yet.</summary>
        static string FreeName(string word, IEnumerable<string> taken)
        {
            var names = new HashSet<string>(taken, StringComparer.CurrentCultureIgnoreCase);
            for (int n = 1; ; n++) if (!names.Contains(word + " " + n)) return word + " " + n;
        }

        // ---------- colors ----------

        /// <summary>Your custom colors are in use (or ready-made ones ticked in Settings, with them).</summary>
        bool OwnColorsInUse { get { return S.LampColors == LampColors.Picked && S.LampColorList.Count == 0; } }

        /// <summary>
        /// Ticks or clears one of your custom colors in the strip's menu, and the lamps use your custom colors: the ones
        /// ticked, and no others (ready-made ones ticked in Settings go). While they weren't in use, this one alone is
        /// ticked. The last one ticked stays ticked.
        /// </summary>
        void TickCustomColor(LampCustomColor c)
        {
            bool inUse = OwnColorsInUse;
            FixedLampColors();
            S.LampColors = LampColors.Picked;
            S.LampPicked.Clear();
            if (!inUse) foreach (var o in S.LampCustom) o.On = o == c;
            else if (!c.On) c.On = true;
            else if (S.LampCustom.Count(o => o.On) > 1) c.On = false;
            ApplyLampColors();
        }

        /// <summary>Colors in words: "Blue, #FFA1DC".</summary>
        static string ColorWords(IEnumerable<string> colors) { return string.Join(", ", colors); }

        /// <summary>The same colors, in whatever order.</summary>
        static bool SameColors(ICollection<string> a, ICollection<string> b)
        {
            return a.Count == b.Count && !a.Except(b).Any();
        }

        /// <summary>
        /// Red and amber, or your own colors, from now on: saved or random colors in use go, and so does picking colors at
        /// random each time the launcher opens.
        /// </summary>
        void FixedLampColors()
        {
            S.LampColorList.Clear();
            S.LampColorsAtRandom = false;
            S.LampRandomColors = LampRandomColors.None;
        }

        /// <summary>The colors the lamps have now, to save (null: red and amber, which is always there).</summary>
        List<string> LampColorsInUse()
        {
            if (S.LampColorList.Count > 0) return S.LampColorList.ToList();
            if (S.LampColors != LampColors.Picked) return null;
            return LampPalette.TidyList(S.LampPicked.Select(h => h.ToString()).Concat(S.LampCustom.Where(c => c.On).Select(c => c.Color)));
        }

        /// <summary>Why the colors in use can't be saved now (null: they can).</summary>
        string ColorsUnsaveable()
        {
            var colors = LampColorsInUse();
            if (colors == null || colors.Count == 0) return "Default (red & amber) is always at the top: tick custom colors, or pick random ones, to save them";
            return null;
        }

        /// <summary>Saves the colors in use under a name (asked for, unless given); ones called that, or the same colors, get these instead.</summary>
        void SaveLampColors(string name)
        {
            if (ColorsUnsaveable() != null) return;
            var colors = LampColorsInUse();
            var had = S.LampColorSets.FirstOrDefault(o => SameColors(o.List(), colors));
            if (name == null)
                name = InputDialog.Ask(this, app, "Save the lamp colors", "A name for these lamp colors:\n\n" + ColorWords(colors),
                    had != null ? had.Name : FreeName("Colors", S.LampColorSets.Select(o => o.Name)));
            if (name == null) return;
            S.LampColorSets = LampColorSet.Save(S.LampColorSets, name, colors);
            app.Save();
            showLine = "colors:" + name.Trim();
        }

        /// <summary>Saved colors: the lamps have them now, until you choose others (picking colors at random goes off).</summary>
        void UseLampColorSet(LampColorSet set)
        {
            var colors = set.List();
            if (colors.Count == 0) return;
            S.LampColorList = colors;
            S.LampColorsAtRandom = false;
            S.LampRandomColors = LampRandomColors.None;
            ApplyLampColors();
        }

        /// <summary>Asks, then deletes saved colors (the lamps keep the colors they have now).</summary>
        void DeleteLampColorSet(LampColorSet set)
        {
            bool yes;
            app.ModalOpen = true;
            try { yes = DeleteDialog.AskColorSet(this, S.SettingsTheme, set.Name, ColorWords(set.List())); }
            finally { app.ModalOpen = false; }
            if (!yes) return;
            S.LampColorSets.Remove(set);
            app.Save();
        }

        static string RandomKindName(LampRandomColors k)
        {
            switch (k)
            {
                case LampRandomColors.CustomSingle: return "Custom (single)";
                case LampRandomColors.CustomMultiple: return "Custom (multiple)";
                case LampRandomColors.RandomSingle: return "Random (single)";
                default: return "Random (multiple)";
            }
        }

        static string RandomKindTip(LampRandomColors k)
        {
            switch (k)
            {
                case LampRandomColors.CustomSingle: return "A single custom color is randomly chosen every time the launcher is opened.";
                case LampRandomColors.CustomMultiple: return "Multiple custom colors are randomly chosen every time the launcher is opened.";
                case LampRandomColors.RandomSingle: return "A single color is randomly chosen every time the launcher is opened.";
                default: return "Multiple colors are randomly chosen every time the launcher is opened.";
            }
        }

        /// <summary>Whether colors of that kind can be picked: your custom ones need one custom color, or two for several.</summary>
        bool RandomKindPossible(LampRandomColors k)
        {
            if (k == LampRandomColors.CustomSingle) return S.LampCustom.Count >= 1;
            if (k == LampRandomColors.CustomMultiple) return S.LampCustom.Count >= 2;
            return true;
        }

        static string RandomKindWhyNot(LampRandomColors k)
        {
            return k == LampRandomColors.CustomSingle ? "Add a custom color first (Add a custom color... above): this picks one of your custom colors"
                : "Add two or more custom colors first (Add a custom color... above): this picks some of your custom colors";
        }

        /// <summary>The kinds of random colors ticked that can be picked as things are.</summary>
        List<LampRandomColors> RandomColorKinds()
        {
            return RandomColorKindsAll.Where(k => (S.LampRandomColors & k) != 0 && RandomKindPossible(k)).ToList();
        }

        /// <summary>
        /// New colors at random, of one of the kinds ticked (that picked at random too, when there are several), and not the
        /// same as the ones there are now if that can be helped; the lamps change at once. False when none can be picked.
        /// </summary>
        bool RandomizeLampColors(bool save)
        {
            var kinds = RandomColorKinds();
            if (kinds.Count == 0) return false;
            List<string> pick = null;
            for (int tries = 0; tries < 8; tries++)
            {
                pick = PickRandomColors(kinds[lampDice.Next(kinds.Count)]);
                if (!SameColors(pick, S.LampColorList)) break;
            }
            S.LampColorList = pick;
            S.LampColorsAtRandom = true;
            if (save) ApplyLampColors();
            else UseLampPalette();
            return true;
        }

        List<string> PickRandomColors(LampRandomColors kind)
        {
            var pool = S.LampCustom.Select(c => c.Color).ToList();
            switch (kind)
            {
                case LampRandomColors.CustomSingle: return new List<string> { pool[lampDice.Next(pool.Count)] };
                case LampRandomColors.CustomMultiple: return pool.OrderBy(c => lampDice.Next()).Take(lampDice.Next(2, Math.Min(pool.Count, 6) + 1)).ToList();   // 2 to 6 of them
                case LampRandomColors.RandomSingle: return LampPalette.RandomColors(1, lampDice);
                default: return LampPalette.RandomColors(lampDice.Next(2, 9), lampDice);   // 2 to 8 of them
            }
        }

        /// <summary>Why the colors picked at random can't be added to the saved ones now (null: they can).</summary>
        string RandomColorsUnsaveable()
        {
            if (!S.LampColorsAtRandom || S.LampColorList.Count == 0)
                return "Click Randomize now first (or let random colors be picked when the launcher opens): this saves the colors picked at random";
            var had = S.LampColorSets.FirstOrDefault(o => SameColors(o.List(), S.LampColorList));
            if (had != null) return "They're saved already, as " + Shorten(had.Name, 30);
            return null;
        }

        /// <summary>The colors picked at random, saved as "Random 1" (or the next number free); the ones saved, or null.</summary>
        LampColorSet SaveRandomColors()
        {
            if (RandomColorsUnsaveable() != null) return null;
            string name = FreeName("Random", S.LampColorSets.Select(o => o.Name));
            S.LampColorSets = LampColorSet.Save(S.LampColorSets, name, S.LampColorList);
            app.Save();
            return S.LampColorSets.FirstOrDefault(o => o.Name == name);
        }

        // ---------- each time the launcher opens ----------

        /// <summary>
        /// The launcher is opening: a custom activity and colors picked at random, when those are switched on (and there's
        /// a lamp strip to show them).
        /// </summary>
        void RandomizeOnOpen()
        {
            if (S.LampPanel == LampPanelPlace.Off) return;
            bool changed = false;
            if (S.LampRandomActivity) changed |= RandomizeLampActivity(false);
            if (S.LampRandomColors != LampRandomColors.None) changed |= RandomizeLampColors(false);
            if (changed) app.Save();
        }

        /// <summary>Test copies only: the colors and activity settings in words.</summary>
        string LooksNow()
        {
            return "activity " + S.LampMode + " (" + S.LampCustomActivity.Summary() + "), parts " + S.LampActivityParts + ", random activity " + S.LampRandomActivity
                + " | colors " + S.LampColors + (S.LampColorList.Count > 0 ? ", using " + ColorWords(S.LampColorList) + (S.LampColorsAtRandom ? " (random)" : " (saved)") : "")
                + ", random colors " + S.LampRandomColors + ", palette " + LampPalette.From(S)
                + " | own colors: strip " + (S.LampStripColors.Count > 0 ? ColorWords(S.LampStripColors) : "-") + ", monitor " + (S.LampMonitorColors.Count > 0 ? ColorWords(S.LampMonitorColors) : "-")
                + ", mini " + (S.LampMiniColors.Count > 0 ? ColorWords(S.LampMiniColors) : "-")
                + ", thirds " + string.Join(" / ", Enumerable.Range(0, 3).Select(k => ThirdColors(k).Count > 0 ? ColorWords(ThirdColors(k)) : "-"))
                + " | saved colors: " + string.Join(" / ", S.LampColorSets.Select(o => o.Name + " = " + o.Colors))
                + " | saved activities: " + string.Join(" / ", S.LampActivities.Select(a => a.Name));
        }
    }
}
