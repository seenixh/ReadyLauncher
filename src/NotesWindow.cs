using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>
    /// A notepad that sits beside the launcher and opens and closes with it: tabs of notes with bold, italic, underline,
    /// strikethrough, text sizes, colors, highlights, bullets, numbering and checkboxes (right-click the text).
    /// </summary>
    internal class NotesWindow : Window
    {
        readonly TextBlock header, saveGlyph;
        readonly WrapPanel tabStrip;
        readonly Border frame, addTab;
        readonly List<string> names = new List<string>();
        // One editor per tab, shown one at a time: switching tabs never swaps a document inside a live editor
        // (which can trip up its text cursor), and each tab keeps its own scroll position and undo.
        readonly List<RichTextBox> editors = new List<RichTextBox>();
        readonly Grid editorHost = new Grid();
        readonly Border textArea;   // the panel behind the text: the notes background color goes here
        RichTextBox box { get { return editors[Math.Max(0, current)]; } }
        readonly double fontSize;
        Color chromeColor;   // title, tabs and buttons (the color chosen in Settings, or readable on the launcher color)
        Color bodyColor;     // the notes text when the text area has no color of its own (readable on the launcher color)
        Color? areaColor;    // the notes background from Settings, or null for a light tint of the window
        Color windowColor;   // the window's own color (under that light tint)
        /// <summary>The open tab's own text color (readable on its text area).</summary>
        Color fgColor { get { return TextFor(current); } }

        /// <summary>What a tab keeps besides its text: its own color, and the web addresses made plain in it.</summary>
        class TabState
        {
            public Color? Color;
            public List<string> NoLinks = new List<string>();
        }
        int current = -1;
        int renaming = -1;
        TextBox renameBox;
        string lastCopied;
        DispatcherTimer linkTimer;   // turns web addresses into links shortly after typing stops
        bool linking;                // a link update is changing the text right now
        bool linkPasted;             // something was just pasted: link it straight away
        TextPointer testMenuAt;      // test copies only: where the right-click menu acts (instead of the mouse)

        public event Action TextEdited;
        public event Action EscapePressed;
        public event Action SaveRequested;
        /// <summary>Shows the color wheel (current color in, chosen color or null out).</summary>
        public Func<string, string> PickColor;
        /// <summary>Asks a yes / no question.</summary>
        public Func<string, bool> Confirm;
        /// <summary>Something changed since the notes were last saved.</summary>
        public bool Dirty;

        static readonly Color[] TextColors =
        {
            Color.FromRgb(0xE7, 0x48, 0x56), Color.FromRgb(0xF7, 0x63, 0x0C), Color.FromRgb(0xFF, 0xB9, 0x00), Color.FromRgb(0x16, 0xC6, 0x0C),
            Color.FromRgb(0x00, 0xB7, 0xC3), Color.FromRgb(0x3A, 0x96, 0xDD), Color.FromRgb(0xB1, 0x46, 0xC2), Color.FromRgb(0xE3, 0x00, 0x8C),
            Color.FromRgb(0x8E, 0x8E, 0x8E), Colors.White, Colors.Black
        };
        static readonly string[] TextColorNames = { "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink", "Gray", "White", "Black" };

        // See-through, so the text stays readable on light and dark notes.
        static readonly Color[] HighlightColors =
        {
            Color.FromArgb(0x99, 0xFF, 0xE0, 0x00), Color.FromArgb(0x80, 0x22, 0xD3, 0x4C), Color.FromArgb(0x80, 0x3A, 0xA0, 0xFF),
            Color.FromArgb(0x80, 0xFF, 0x4F, 0xA8), Color.FromArgb(0x80, 0xFF, 0x8C, 0x00), Color.FromArgb(0x80, 0xA0, 0x60, 0xFF)
        };
        static readonly string[] HighlightNames = { "Yellow", "Green", "Blue", "Pink", "Orange", "Purple" };

        static readonly Color[] BackColors =
        {
            Color.FromRgb(0x1B, 0x1B, 0x1B), Color.FromRgb(0x3A, 0x3A, 0x3A), Color.FromRgb(0xE6, 0xE6, 0xE6), Colors.White,
            Color.FromRgb(0x1F, 0x3A, 0x5F), Color.FromRgb(0x2B, 0x57, 0x9A), Color.FromRgb(0x0F, 0x5E, 0x5E), Color.FromRgb(0x2E, 0x5E, 0x2E),
            Color.FromRgb(0x4B, 0x2E, 0x6B), Color.FromRgb(0xFF, 0xF4, 0xA3), Color.FromRgb(0xF8, 0xD7, 0xE3)
        };
        static readonly string[] BackColorNames = { "Black", "Dark gray", "Light gray", "White", "Navy", "Blue", "Teal", "Green", "Purple", "Sticky-note yellow", "Pink" };

        public NotesWindow(Brush background, Color foreground, Color body, Brush border, Color? notesBackground, List<NoteTab> noteTabs, int selected, double fontSize)
        {
            Title = "Notes";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            this.fontSize = Math.Max(8, fontSize);
            windowColor = ColorOf(background);
            chromeColor = foreground;
            bodyColor = body;
            areaColor = notesBackground;
            var fg = new SolidColorBrush(foreground);

            header = new TextBlock
            {
                Text = "Notes", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = fg,
                Margin = new Thickness(4, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
            };
            saveGlyph = new TextBlock
            {
                Text = "", FontFamily = TileView.IconFont, FontSize = 14, Foreground = fg,   // the save icon
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            var save = HoverButton(saveGlyph, "Save all the notes to a text file", 30, 26);
            save.MouseLeftButtonUp += delegate { if (SaveRequested != null) SaveRequested(); };
            DockPanel.SetDock(save, Dock.Right);
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            top.Children.Add(save);
            top.Children.Add(header);
            DockPanel.SetDock(top, Dock.Top);

            tabStrip = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(tabStrip, Dock.Top);
            addTab = HoverButton(new TextBlock
            {
                Text = "", FontFamily = TileView.IconFont, FontSize = 11, Foreground = fg,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }, "New tab", 26, 24);
            addTab.Margin = new Thickness(0, 0, 0, 4);
            addTab.MouseLeftButtonUp += delegate { AddTab(); };

            foreach (var t in noteTabs ?? new List<NoteTab>())
            {
                Color own;
                var state = new TabState { NoLinks = new List<string>(t.NoLinks ?? new List<string>()) };
                if (NoteDocs.TryColor(t.Color, out own)) state.Color = own;
                names.Add(t.Name);
                AddEditor(NoteDocs.Build(t, this.fontSize, TextColorOn(bodyColor, state.Color ?? areaColor), Edited), state);
            }
            if (names.Count == 0) { names.Add("Notes"); AddEditor(NoteDocs.Build(new NoteTab(), this.fontSize, TextFor(-1), Edited), new TabState()); }

            textArea = new Border { CornerRadius = new CornerRadius(6), Child = editorHost };
            BuildFindBar();
            BuildUndoBar();
            var dock = new DockPanel();
            dock.Children.Add(top);
            dock.Children.Add(tabStrip);
            dock.Children.Add(undoBar);
            dock.Children.Add(findBar);
            dock.Children.Add(textArea);
            frame = new Border
            {
                CornerRadius = new CornerRadius(12), Background = background, BorderBrush = border, BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 10), Child = dock
            };
            var root = new Grid();
            root.Children.Add(frame);
            root.Children.Add(MakeGrip(false));
            root.Children.Add(MakeGrip(true));
            Content = root;
            SelectTab(Math.Max(0, Math.Min(selected, names.Count - 1)));
            Dirty = false;

            PreviewKeyDown += (s, e) =>
            {
                var mods = Keyboard.Modifiers;
                if (e.Key == Key.F && mods == ModifierKeys.Control) { e.Handled = true; OpenFind(); return; }
                if (e.Key == Key.F3) { e.Handled = true; FindNext(mods == ModifierKeys.Shift ? -1 : 1); return; }
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                if (renaming >= 0) { CancelRename(); return; }
                if (findBar.Visibility == Visibility.Visible) { CloseFind(); return; }
                if (EscapePressed != null) EscapePressed();
            };
        }

        static TabState StateOf(RichTextBox editor) { return (TabState)editor.Tag; }

        /// <summary>A tab's text-area color: its own, else the one from Settings (null = a light tint).</summary>
        Color? AreaOf(int tab)
        {
            if (tab >= 0 && tab < editors.Count && StateOf(editors[tab]).Color.HasValue) return StateOf(editors[tab]).Color;
            return areaColor;
        }

        /// <summary>The default text color for a tab: dark on light text areas, white on dark ones.</summary>
        Color TextFor(int tab) { return TextColorOn(bodyColor, AreaOf(tab)); }

        /// <summary>The open tab's text area: its color, or a light tint over the window.</summary>
        Brush EditorBack()
        {
            var area = AreaOf(current);
            if (area.HasValue) return new SolidColorBrush(Color.FromRgb(area.Value.R, area.Value.G, area.Value.B));
            return new SolidColorBrush(Color.FromArgb(0x1E, bodyColor.R, bodyColor.G, bodyColor.B));
        }

        /// <summary>Text and link colors for one tab, after its color (or the Settings color) changed.</summary>
        void ColorEditor(int tab)
        {
            var e = editors[tab];
            var text = new SolidColorBrush(TextFor(tab));
            e.Foreground = text;
            e.CaretBrush = text;
            e.Document.Foreground = text;
            e.Document.Resources[typeof(Hyperlink)] = NoteDocs.LinkStyle(TextFor(tab));   // links stay readable on the color
            ColorChecks(tab);
        }

        /// <summary>Text that reads well on the text area: dark on light colors, white on dark ones.</summary>
        static Color TextColorOn(Color plain, Color? area)
        {
            if (!area.HasValue) return plain;
            var c = area.Value;
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
            return lum > 0.5 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White;
        }

        /// <summary>
        /// An editor for one tab (hidden until its tab is chosen), placed at the end of the list. Its Tag holds the tab's
        /// own color and the web addresses made plain text in it.
        /// </summary>
        RichTextBox AddEditor(FlowDocument doc, TabState state)
        {
            var fg = new SolidColorBrush(TextColorOn(bodyColor, state.Color ?? areaColor));
            var e = new RichTextBox
            {
                Document = doc, AcceptsReturn = true, AcceptsTab = true, IsDocumentEnabled = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Foreground = fg, CaretBrush = fg, BorderThickness = new Thickness(0), Padding = new Thickness(6, 6, 6, 6),
                FontSize = fontSize, SelectionBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF)),
                ContextMenu = SettingsTheme.ThemeMenu(new ContextMenu()), Background = Brushes.Transparent, Visibility = Visibility.Collapsed,
                IsInactiveSelectionHighlightEnabled = true   // a found word stays highlighted while typing in the find box
            };
            e.SpellCheck.IsEnabled = true;
            e.Tag = state;
            e.TextChanged += (s, a) =>
            {
                Edited();
                findStale = true;
                // Undo / redo are left alone, so undoing a new link doesn't just make it again.
                if (!linking && a.UndoAction != UndoAction.Undo && a.UndoAction != UndoAction.Redo) QueueLinks();
            };
            e.SelectionChanged += delegate { if (!linking) QueueLinks(); };   // e.g. clicking away from an address just typed
            WatchLinkClicks(e);
            e.ContextMenuOpening += delegate { FillEditorMenu(); };
            e.PreviewKeyDown += OnEditorKey;
            DataObject.AddCopyingHandler(e, (s, a) => { lastCopied = e.Selection.Text; });
            DataObject.AddPastingHandler(e, OnPaste);
            editors.Add(e);
            editorHost.Children.Add(e);
            ColorChecks(editors.Count - 1);
            return e;
        }

        static Color ColorOf(Brush b)
        {
            var s = b as SolidColorBrush;
            return s != null ? Color.FromRgb(s.Color.R, s.Color.G, s.Color.B) : Colors.Black;
        }

        /// <summary>The color a tab's text area shows: its own, the one from Settings, or the light tint over the window.</summary>
        Color AreaShown(int tab)
        {
            var a = AreaOf(tab);
            if (a.HasValue) return a.Value;
            double t = 0x1E / 255.0;
            Func<byte, byte, byte> mix = (w, b) => (byte)Math.Round(w + (b - w) * t);
            return Color.FromRgb(mix(windowColor.R, bodyColor.R), mix(windowColor.G, bodyColor.G), mix(windowColor.B, bodyColor.B));
        }

        /// <summary>A tab's checkboxes in colors that suit its text area (never the bright white of a plain checkbox).</summary>
        void ColorChecks(int tab)
        {
            if (tab < 0 || tab >= editors.Count) return;
            editors[tab].Document.Resources[typeof(CheckBox)] = NoteDocs.CheckStyle(AreaShown(tab), TextFor(tab), chromeColor);
        }

        Border HoverButton(UIElement content, string tip, double w, double h)
        {
            var b = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = tip, Child = content };
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(0x30, chromeColor.R, chromeColor.G, chromeColor.B)); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            return b;
        }

        void Edited()
        {
            Dirty = true;
            if (TextEdited != null) TextEdited();
        }

        // ---------- what the launcher saves ----------

        public List<NoteTab> Tabs()
        {
            var list = new List<NoteTab>();
            for (int i = 0; i < names.Count; i++)
            {
                var state = StateOf(editors[i]);
                list.Add(new NoteTab
                {
                    Name = names[i], Paras = NoteDocs.Read(editors[i].Document, fontSize, TextFor(i)),
                    NoLinks = new List<string>(state.NoLinks),
                    Color = state.Color.HasValue ? Color.FromRgb(state.Color.Value.R, state.Color.Value.G, state.Color.Value.B).ToString() : ""
                });
            }
            return list;
        }

        static List<string> PlainUrls(RichTextBox editor) { return StateOf(editor).NoLinks; }

        // ---------- links ----------

        void QueueLinks()
        {
            if (linkTimer == null)
            {
                linkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                linkTimer.Tick += delegate { linkTimer.Stop(); UpdateLinks(); };
            }
            linkTimer.Stop();
            linkTimer.Start();
        }

        /// <summary>Web addresses in the open tab become links; the one still being typed waits until the cursor moves on.</summary>
        void UpdateLinks()
        {
            if (current < 0) return;
            var editor = box;
            bool now = linkPasted || !editor.IsKeyboardFocusWithin;
            linkPasted = false;
            linking = true;
            editor.BeginChange();
            try
            {
                foreach (var p in NoteDocs.Paragraphs(editor.Document).ToList())
                    NoteDocs.Linkify(p, PlainUrls(editor), now ? null : editor.CaretPosition);
            }
            finally
            {
                editor.EndChange();
                linking = false;
            }
        }

        /// <summary>
        /// A click on a link opens it (with or without Ctrl). Dragging across a link still selects text, and Shift+click
        /// still extends a selection, so links can be edited; right-click offers "Remove link" too.
        /// </summary>
        void WatchLinkClicks(RichTextBox editor)
        {
            Hyperlink pressed = null;
            Point pressedAt = new Point();
            editor.PreviewMouseLeftButtonDown += (s, a) =>
            {
                pressedAt = a.GetPosition(editor);
                pressed = (Keyboard.Modifiers & ModifierKeys.Shift) == 0 ? LinkUnder(editor, pressedAt) : null;
            };
            editor.PreviewMouseLeftButtonUp += (s, a) =>
            {
                var link = pressed;
                pressed = null;
                if (link != null) ClickedAt(editor, link, pressedAt, a.GetPosition(editor));
            };
            // A hand over links, so it's clear they can be clicked (the editor otherwise always shows the text cursor).
            editor.AddHandler(Mouse.QueryCursorEvent, new QueryCursorEventHandler((s, a) =>
            {
                if (LinkUnder(editor, a.GetPosition(editor)) == null) return;
                a.Cursor = Cursors.Hand;
                a.Handled = true;
            }), true);
        }

        /// <summary>Opens the link when the button came up where it went down, on the same link (a click, not a drag).</summary>
        void ClickedAt(RichTextBox editor, Hyperlink link, Point down, Point up)
        {
            if (Math.Abs(up.X - down.X) > 4 || Math.Abs(up.Y - down.Y) > 4) return;
            if (LinkUnder(editor, up) != link) return;
            OpenLink(link);
        }

        /// <summary>The link under a point in an editor (only right over its text, not beside it), or null.</summary>
        static Hyperlink LinkUnder(RichTextBox editor, Point p)
        {
            try { return NoteDocs.LinkAt(editor.GetPositionFromPoint(p, false)); }
            catch (Exception) { return null; }
        }

        static void OpenLink(Hyperlink link)
        {
            string url = NoteDocs.UrlFor(NoteDocs.LinkText(link));
            if (url == null) return;
            if (Program.TestMode)
            {
                // Test copies write down what they would open instead of starting a browser.
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(LauncherData.DataFolder, "opened.txt"), url + "\r\n"); } catch (Exception) { }
                return;
            }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { }
        }

        /// <summary>Turns a link into plain text, and remembers to keep that address plain in this tab.</summary>
        void MakePlain(Hyperlink link)
        {
            string text = NoteDocs.LinkText(link).Trim();
            var plain = PlainUrls(box);
            if (!plain.Any(u => string.Equals(u, text, StringComparison.OrdinalIgnoreCase))) plain.Add(text);
            linking = true;
            try { NoteDocs.Unwrap(link); }
            finally { linking = false; }
            Edited();
        }

        /// <summary>Lets a web address that was made plain text be a link again.</summary>
        void MakeLink(string text)
        {
            PlainUrls(box).RemoveAll(u => string.Equals(u, text, StringComparison.OrdinalIgnoreCase));
            linkPasted = true;   // link it now, even with the cursor on it
            UpdateLinks();
            Edited();
        }

        public int SelectedTab { get { return Math.Max(0, current); } }

        /// <summary>
        /// New colors: the window (launcher color), its title and tabs, and the text area behind the notes (null = a light
        /// tint). Text left in the default color follows the text area.
        /// </summary>
        public void ApplyColors(Brush background, Color foreground, Color body, Color? notesBackground)
        {
            chromeColor = foreground;
            bodyColor = body;
            areaColor = notesBackground;
            windowColor = ColorOf(background);
            var chrome = new SolidColorBrush(foreground);
            frame.Background = background;
            header.Foreground = chrome;
            saveGlyph.Foreground = chrome;
            ((TextBlock)addTab.Child).Foreground = chrome;
            textArea.Background = EditorBack();
            for (int i = 0; i < editors.Count; i++) ColorEditor(i);
            RefreshTabs();
        }

        /// <summary>Gives one tab its own text-area color (null = back to the one from Settings).</summary>
        void SetTabColor(int tab, Color? color)
        {
            if (tab < 0 || tab >= editors.Count) return;
            StateOf(editors[tab]).Color = color.HasValue ? Color.FromRgb(color.Value.R, color.Value.G, color.Value.B) : (Color?)null;
            ColorEditor(tab);
            if (tab == current) textArea.Background = EditorBack();
            RefreshTabs();
            Edited();
        }

        /// <summary>Briefly show "saved" next to the title.</summary>
        public void ShowSaved(string fileName)
        {
            header.Text = "Notes  –  saved to " + fileName;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            t.Tick += delegate { t.Stop(); header.Text = "Notes"; };
            t.Start();
        }

        // ---------- tabs ----------

        void SelectTab(int i)
        {
            if (i < 0 || i >= editors.Count) return;
            if (i != current)
            {
                bool first = current < 0;
                bool typing = !first && current < editors.Count && editors[current].IsKeyboardFocusWithin;
                for (int k = 0; k < editors.Count; k++) editors[k].Visibility = k == i ? Visibility.Visible : Visibility.Collapsed;
                current = i;
                if (typing) editors[i].Focus();   // carry on typing in the tab just opened
                if (!first) Edited();   // remember which tab is open
            }
            if (textArea != null) textArea.Background = EditorBack();   // each tab can have its own color
            RefreshTabs();
        }

        void RefreshTabs()
        {
            tabStrip.Children.Clear();
            for (int i = 0; i < names.Count; i++)
                tabStrip.Children.Add(i == renaming ? (UIElement)MakeRenameBox(i) : MakeTab(i));
            tabStrip.Children.Add(addTab);
        }

        UIElement MakeTab(int index)
        {
            bool on = index == current;
            var text = new TextBlock
            {
                Text = names[index], FontSize = 12.5, Foreground = new SolidColorBrush(chromeColor), MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal, Opacity = on ? 1 : 0.8
            };
            var idle = on ? new SolidColorBrush(Color.FromArgb(0x38, chromeColor.R, chromeColor.G, chromeColor.B)) : Brushes.Transparent;
            UIElement content = text;
            var own = StateOf(editors[index]).Color;
            if (own.HasValue)
            {
                // A tab with its own color shows it as a dot before its name.
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 9, Height = 9, Fill = new SolidColorBrush(own.Value), Margin = new Thickness(0, 1, 6, 0), VerticalAlignment = VerticalAlignment.Center,
                    Stroke = new SolidColorBrush(Color.FromArgb(0x90, chromeColor.R, chromeColor.G, chromeColor.B)), StrokeThickness = 1
                });
                row.Children.Add(text);
                content = row;
            }
            var tab = new Border
            {
                CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 3, 9, 4), Margin = new Thickness(0, 0, 4, 4), Cursor = Cursors.Hand,
                Background = idle, Child = content, ToolTip = names[index] + "  (double-click to rename, right-click for more)"
            };
            System.Windows.Automation.AutomationProperties.SetName(tab, "Tab " + names[index]);
            tab.MouseEnter += delegate { if (!on) tab.Background = new SolidColorBrush(Color.FromArgb(0x1E, chromeColor.R, chromeColor.G, chromeColor.B)); };
            tab.MouseLeave += delegate { tab.Background = idle; };
            tab.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (e.ClickCount == 2) StartRename(index);
                else SelectTab(index);
            };
            tab.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabMenu(index, tab); };
            return tab;
        }

        UIElement MakeRenameBox(int index)
        {
            renameBox = new TextBox
            {
                Text = names[index], FontSize = 12.5, MinWidth = 70, MaxWidth = 180, Padding = new Thickness(4, 1, 4, 2),
                Margin = new Thickness(0, 0, 4, 4), MaxLength = 60, VerticalContentAlignment = VerticalAlignment.Center
            };
            renameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; CommitRename(); } };
            renameBox.LostKeyboardFocus += delegate { CommitRename(); };
            return renameBox;
        }

        public void StartRename(int index)
        {
            if (index < 0 || index >= names.Count) return;
            renaming = index;
            RefreshTabs();
            Activate();
            var b = renameBox;
            Dispatcher.BeginInvoke(new Action(() => { b.Focus(); b.SelectAll(); }), DispatcherPriority.Input);
        }

        void CommitRename()
        {
            if (renaming < 0) return;
            int i = renaming;
            string name = renameBox == null ? "" : renameBox.Text.Trim();
            renaming = -1;
            if (name.Length > 0 && name != names[i]) { names[i] = name; Edited(); }
            RefreshTabs();
        }

        void CancelRename()
        {
            renaming = -1;
            RefreshTabs();
            box.Focus();
        }

        public void AddTab()
        {
            string name;
            int n = names.Count + 1;
            do { name = "Note " + n++; } while (names.Contains(name));
            names.Add(name);
            AddEditor(NoteDocs.Build(new NoteTab(), fontSize, TextColorOn(bodyColor, areaColor), Edited), new TabState());
            SelectTab(names.Count - 1);
            Edited();
            StartRename(names.Count - 1);   // type a name straight away (Enter keeps it)
        }

        void ShowTabMenu(int index, FrameworkElement anchor)
        {
            var menu = SettingsTheme.ThemeMenu(new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom });
            menu.Items.Add(MenuAction("Rename", () => StartRename(index)));
            menu.Items.Add(MenuAction("Move left", () => MoveTab(index, -1), index > 0));
            menu.Items.Add(MenuAction("Move right", () => MoveTab(index, 1), index < names.Count - 1));
            menu.Items.Add(ColorMenu("Color", index));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuAction("New tab", AddTab));
            menu.Items.Add(MenuAction("Delete this tab", () => DeleteTab(index), names.Count > 1));
            menu.IsOpen = true;
        }

        void MoveTab(int index, int by)
        {
            int to = index + by;
            if (to < 0 || to >= names.Count) return;
            var n = names[index]; names[index] = names[to]; names[to] = n;
            var ed = editors[index]; editors[index] = editors[to]; editors[to] = ed;
            if (current == index) current = to;
            else if (current == to) current = index;
            RefreshTabs();
            Edited();
        }

        void DeleteTab(int index)
        {
            if (names.Count <= 1 || index < 0 || index >= names.Count) return;
            bool hasText = NoteDocs.Paragraphs(editors[index].Document).Any(p => NoteDocs.Text(p).Trim().Length > 0 || NoteDocs.CheckOf(p) != null);
            if (hasText && Confirm != null && !Confirm("Delete the tab \"" + names[index] + "\" and the notes in it?")) return;
            int show = current > index ? current - 1 : current == index ? Math.Min(index, names.Count - 2) : current;
            deleted = Tuple.Create(index, names[index], editors[index]);   // kept for a little while, for Undo
            editorHost.Children.Remove(editors[index]);
            names.RemoveAt(index);
            editors.RemoveAt(index);
            current = -1;   // the tab on screen may be gone: show the one that takes its place
            for (int k = 0; k < editors.Count; k++) editors[k].Visibility = k == show ? Visibility.Visible : Visibility.Collapsed;
            current = show;
            textArea.Background = EditorBack();
            findStale = true;
            RefreshTabs();
            ShowUndo(deleted.Item2);
            Edited();
        }

        // ---------- undoing a deleted tab ----------

        Tuple<int, string, RichTextBox> deleted;   // where it was, its name, its editor (with its notes)
        Border undoBar;
        TextBlock undoText;
        DispatcherTimer undoTimer;

        void BuildUndoBar()
        {
            undoText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var undo = new TextBlock
            {
                Text = "Undo", FontWeight = FontWeights.SemiBold, TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0)
            };
            undo.MouseLeftButtonUp += delegate { UndoDelete(); };
            System.Windows.Automation.AutomationProperties.SetName(undo, "Undo delete");
            DockPanel.SetDock(undo, Dock.Right);
            var row = new DockPanel();
            row.Children.Add(undo);
            row.Children.Add(undoText);
            undoBar = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 0, 6), Child = row, Visibility = Visibility.Collapsed };
            DockPanel.SetDock(undoBar, Dock.Top);
        }

        void ShowUndo(string name)
        {
            undoText.Text = "Deleted the tab \"" + name + "\".";
            var c = chromeColor;
            undoText.Foreground = new SolidColorBrush(c);
            ((TextBlock)((DockPanel)undoBar.Child).Children[0]).Foreground = new SolidColorBrush(c);
            undoBar.Background = new SolidColorBrush(Color.FromArgb(0x2A, c.R, c.G, c.B));
            undoBar.Visibility = Visibility.Visible;
            if (undoTimer == null)
            {
                undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                undoTimer.Tick += delegate { undoTimer.Stop(); undoBar.Visibility = Visibility.Collapsed; deleted = null; };
            }
            undoTimer.Stop();
            undoTimer.Start();
        }

        /// <summary>Puts the last deleted tab back where it was, notes and all.</summary>
        public void UndoDelete()
        {
            if (deleted == null) return;
            int at = Math.Min(deleted.Item1, names.Count);
            names.Insert(at, deleted.Item2);
            editors.Insert(at, deleted.Item3);
            editorHost.Children.Add(deleted.Item3);
            deleted.Item3.Visibility = Visibility.Collapsed;
            if (current >= at) current++;
            deleted = null;
            if (undoTimer != null) undoTimer.Stop();
            undoBar.Visibility = Visibility.Collapsed;
            findStale = true;
            SelectTab(at);
            Edited();
        }

        // ---------- the editor's right-click menu ----------

        static MenuItem MenuAction(string header, Action action) { return MenuAction(header, action, true); }

        static MenuItem MenuAction(object header, Action action, bool enabled)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += delegate { action(); };
            return mi;
        }

        static MenuItem Toggle(TextBlock header, string keys, bool on, Action action)
        {
            var mi = MenuAction(header, action, true);
            mi.IsChecked = on;
            if (keys != null) mi.InputGestureText = keys;
            return mi;
        }

        static MenuItem ColorItem(string name, Color c, bool on, Action action)
        {
            var mi = MenuAction(name, action, true);
            mi.Icon = new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(c),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, 0x80, 0x80, 0x80)), BorderThickness = new Thickness(1)
            };
            if (on) mi.FontWeight = FontWeights.SemiBold;
            return mi;
        }

        void FillEditorMenu()
        {
            var menu = box.ContextMenu;
            menu.Items.Clear();
            var sel = box.Selection;

            // Spelling suggestions for a misspelled word under the mouse (the menu this replaces had them).
            SpellingError error = null;
            TextPointer at = null;
            try
            {
                box.UpdateLayout();   // e.g. right after switching tabs
                at = testMenuAt ?? box.GetPositionFromPoint(Mouse.GetPosition(box), true) ?? box.CaretPosition;
                error = at == null ? null : box.GetSpellingError(at);
            }
            catch (Exception) { error = null; at = box.CaretPosition; }   // the menu still works, just without suggestions

            // A link under the mouse: open, copy, or make it plain text. A web address made plain: make it a link again.
            var link = NoteDocs.LinkAt(at);
            if (link != null)
            {
                string url = NoteDocs.UrlFor(NoteDocs.LinkText(link)) ?? NoteDocs.LinkText(link);
                var open = MenuAction("Open link", () => OpenLink(link));
                open.InputGestureText = "Click";
                open.FontWeight = FontWeights.SemiBold;
                menu.Items.Add(open);
                menu.Items.Add(MenuAction("Copy link", () => { try { Clipboard.SetText(url); } catch (Exception) { } }));
                menu.Items.Add(MenuAction("Remove link (plain text)", () => MakePlain(link)));
                menu.Items.Add(new Separator());
            }
            else
            {
                string plainUrl = NoteDocs.PlainUrlAt(at);
                if (plainUrl != null && PlainUrls(box).Any(u => string.Equals(u, plainUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    menu.Items.Add(MenuAction("Make it a link", () => MakeLink(plainUrl)));
                    menu.Items.Add(new Separator());
                }
            }
            if (error != null)
            {
                int n = 0;
                foreach (string suggestion in error.Suggestions)
                {
                    if (n++ >= 5) break;
                    string word = suggestion;
                    var mi = MenuAction(word, () => { error.Correct(word); Edited(); });
                    mi.FontWeight = FontWeights.SemiBold;
                    menu.Items.Add(mi);
                }
                if (n == 0) menu.Items.Add(MenuAction("(no spelling suggestions)", () => { }, false));
                menu.Items.Add(MenuAction("Ignore all", () => error.IgnoreAll()));
                menu.Items.Add(new Separator());
            }

            object weight = sel.GetPropertyValue(TextElement.FontWeightProperty);
            object style = sel.GetPropertyValue(TextElement.FontStyleProperty);
            bool under, strike;
            NoteDocs.Flags(sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection, out under, out strike);
            menu.Items.Add(Toggle(new TextBlock { Text = "Bold", FontWeight = FontWeights.Bold }, "Ctrl+B",
                weight is FontWeight && ((FontWeight)weight).ToOpenTypeWeight() >= 600, () => EditingCommands.ToggleBold.Execute(null, box)));
            menu.Items.Add(Toggle(new TextBlock { Text = "Italic", FontStyle = FontStyles.Italic }, "Ctrl+I",
                style is FontStyle && (FontStyle)style != FontStyles.Normal, () => EditingCommands.ToggleItalic.Execute(null, box)));
            menu.Items.Add(Toggle(new TextBlock { Text = "Underline", TextDecorations = TextDecorations.Underline }, "Ctrl+U",
                under, () => ToggleDecoration(TextDecorationLocation.Underline)));
            menu.Items.Add(Toggle(new TextBlock { Text = "Strikethrough", TextDecorations = TextDecorations.Strikethrough }, "Ctrl+Shift+X",
                strike, () => ToggleDecoration(TextDecorationLocation.Strikethrough)));
            menu.Items.Add(new Separator());
            menu.Items.Add(SizeMenu(sel));
            menu.Items.Add(TextColorMenu(sel));
            menu.Items.Add(HighlightMenu(sel));
            menu.Items.Add(StyleMenu());
            menu.Items.Add(new Separator());

            var para = box.CaretPosition.Paragraph;
            var list = para == null ? null : ListOf(para);
            menu.Items.Add(Toggle(new TextBlock { Text = "Bullets" }, "Ctrl+Shift+L", list != null && !NoteDocs.IsNumbered(list.MarkerStyle),
                () => ToggleList(false)));
            menu.Items.Add(Toggle(new TextBlock { Text = "Numbering" }, "Ctrl+Shift+N", list != null && NoteDocs.IsNumbered(list.MarkerStyle),
                () => ToggleList(true)));
            menu.Items.Add(Toggle(new TextBlock { Text = "Checkbox" }, "Ctrl+Shift+C", para != null && NoteDocs.CheckOf(para) != null, ToggleChecks));
            menu.Items.Add(new Separator());
            menu.Items.Add(ColorMenu("Notes background (this tab)", current));
            var stamp = MenuAction("Insert date and time", InsertDateTime);
            stamp.InputGestureText = "F5";
            menu.Items.Add(stamp);
            menu.Items.Add(MenuAction("Find...", OpenFind));
            ((MenuItem)menu.Items[menu.Items.Count - 1]).InputGestureText = "Ctrl+F";
            menu.Items.Add(new Separator());
            menu.Items.Add(new MenuItem { Header = "Cut", Command = ApplicationCommands.Cut, CommandTarget = box, InputGestureText = "Ctrl+X" });
            menu.Items.Add(new MenuItem { Header = "Copy", Command = ApplicationCommands.Copy, CommandTarget = box, InputGestureText = "Ctrl+C" });
            menu.Items.Add(new MenuItem { Header = "Paste", Command = ApplicationCommands.Paste, CommandTarget = box, InputGestureText = "Ctrl+V" });
            menu.Items.Add(new MenuItem { Header = "Select all", Command = ApplicationCommands.SelectAll, CommandTarget = box, InputGestureText = "Ctrl+A" });
        }

        MenuItem SizeMenu(TextSelection sel)
        {
            object v = sel.GetPropertyValue(TextElement.FontSizeProperty);
            double now = v is double ? (double)v : -1;
            var m = new MenuItem { Header = "Text size" };
            var normal = MenuAction("Normal (" + fontSize.ToString("0.#") + ")", () => ApplyFormat(TextElement.FontSizeProperty, fontSize));
            normal.IsChecked = Math.Abs(now - fontSize) < 0.05;
            m.Items.Add(normal);
            m.Items.Add(new MenuItem { Header = "Bigger", Command = EditingCommands.IncreaseFontSize, CommandTarget = box, InputGestureText = "Ctrl+]" });
            m.Items.Add(new MenuItem { Header = "Smaller", Command = EditingCommands.DecreaseFontSize, CommandTarget = box, InputGestureText = "Ctrl+[" });
            m.Items.Add(new Separator());
            foreach (double size in new double[] { 10, 12, 14, 16, 18, 20, 24, 28, 32, 40, 48 })
            {
                double s = size;
                var mi = MenuAction(s.ToString("0"), () => ApplyFormat(TextElement.FontSizeProperty, s));
                mi.IsChecked = Math.Abs(now - s) < 0.05 && !normal.IsChecked;
                m.Items.Add(mi);
            }
            return m;
        }

        MenuItem TextColorMenu(TextSelection sel)
        {
            var brush = sel.GetPropertyValue(TextElement.ForegroundProperty) as SolidColorBrush;
            Color? now = brush == null ? (Color?)null : brush.Color;
            var m = new MenuItem { Header = "Text color" };
            m.Items.Add(ColorItem("Default", fgColor, now == fgColor, ClearTextColor));
            m.Items.Add(new Separator());
            for (int i = 0; i < TextColors.Length; i++)
            {
                var c = TextColors[i];
                m.Items.Add(ColorItem(TextColorNames[i], c, now == c, () => ApplyFormat(TextElement.ForegroundProperty, new SolidColorBrush(c))));
            }
            m.Items.Add(new Separator());
            m.Items.Add(MenuAction("More colors...", () =>
            {
                string picked = PickColor == null ? null : PickColor((now ?? fgColor).ToString());
                Color c;
                if (picked != null && NoteDocs.TryColor(picked, out c)) ApplyFormat(TextElement.ForegroundProperty, new SolidColorBrush(c));
            }));
            return m;
        }

        MenuItem HighlightMenu(TextSelection sel)
        {
            var brush = sel.GetPropertyValue(TextElement.BackgroundProperty) as SolidColorBrush;
            Color? now = brush == null || brush.Color.A == 0 ? (Color?)null : brush.Color;
            var m = new MenuItem { Header = "Highlight" };
            m.Items.Add(ColorItem("None", Colors.Transparent, now == null, () => ApplyFormat(TextElement.BackgroundProperty, Brushes.Transparent)));
            m.Items.Add(new Separator());
            for (int i = 0; i < HighlightColors.Length; i++)
            {
                var c = HighlightColors[i];
                m.Items.Add(ColorItem(HighlightNames[i], c, now == c, () => ApplyFormat(TextElement.BackgroundProperty, new SolidColorBrush(c))));
            }
            m.Items.Add(new Separator());
            m.Items.Add(MenuAction("More colors...", () =>
            {
                string picked = PickColor == null ? null : PickColor(now.HasValue ? Color.FromRgb(now.Value.R, now.Value.G, now.Value.B).ToString() : "#FFE000");
                Color c;
                if (picked != null && NoteDocs.TryColor(picked, out c))
                    ApplyFormat(TextElement.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x99, c.R, c.G, c.B)));
            }));
            return m;
        }

        /// <summary>
        /// The text-area color of one tab. "Same as the other tabs" goes back to the notes background from Settings
        /// (a light tint unless one is set there).
        /// </summary>
        MenuItem ColorMenu(string header, int tab)
        {
            var own = tab >= 0 && tab < editors.Count ? StateOf(editors[tab]).Color : null;
            var m = new MenuItem { Header = header };
            var same = MenuAction("Same as the other tabs", () => SetTabColor(tab, null));
            same.IsChecked = !own.HasValue;
            m.Items.Add(same);
            m.Items.Add(new Separator());
            for (int i = 0; i < BackColors.Length; i++)
            {
                var c = BackColors[i];
                m.Items.Add(ColorItem(BackColorNames[i], c, own == c, () => SetTabColor(tab, c)));
            }
            m.Items.Add(new Separator());
            m.Items.Add(MenuAction("More colors...", () =>
            {
                var start = AreaOf(tab);   // start the wheel at the tab's color
                string picked = PickColor == null ? null : PickColor(start.HasValue ? Color.FromRgb(start.Value.R, start.Value.G, start.Value.B).ToString() : "#FFF4A3");
                Color c;
                if (picked != null && NoteDocs.TryColor(picked, out c)) SetTabColor(tab, c);
            }));
            return m;
        }

        // ---------- headings, date and time ----------

        static readonly string[] StyleNames = { "Normal text", "Title", "Heading", "Subheading" };
        static readonly double[] StyleScale = { 1.0, 1.8, 1.45, 1.2 };

        /// <summary>A paragraph style for the lines in the selection: bigger and bold for titles and headings.</summary>
        void ApplyStyle(int style)
        {
            double size = Math.Round(fontSize * StyleScale[style]);
            var weight = style == 0 ? FontWeights.Normal : FontWeights.Bold;
            foreach (var p in SelectedParagraphs())
            {
                var check = NoteDocs.CheckOf(p);
                var range = new TextRange(check != null ? check.ElementEnd : p.ContentStart, p.ContentEnd);
                if (range.IsEmpty) continue;
                range.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                range.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
            }
            if (box.Selection.IsEmpty)   // what's typed next gets the style too
            {
                box.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
                box.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, weight);
            }
            Edited();
        }

        MenuItem StyleMenu()
        {
            var m = new MenuItem { Header = "Style" };
            string[] keys = { "Ctrl+0", "Ctrl+1", "Ctrl+2", "Ctrl+3" };
            for (int i = 1; i <= 3; i++)
            {
                int style = i;
                var sample = new TextBlock { Text = StyleNames[i], FontWeight = FontWeights.Bold, FontSize = 12 * (1 + (StyleScale[i] - 1) * 0.5) };
                m.Items.Add(Toggle(sample, keys[i], false, () => ApplyStyle(style)));
            }
            m.Items.Add(new Separator());
            m.Items.Add(Toggle(new TextBlock { Text = StyleNames[0] }, keys[0], false, () => ApplyStyle(0)));
            return m;
        }

        /// <summary>Types the time and date at the cursor, like Windows Notepad's F5 (e.g. 3:45 PM 11/17/2026).</summary>
        void InsertDateTime()
        {
            var now = DateTime.Now;
            box.Selection.Text = now.ToShortTimeString() + " " + now.ToShortDateString();
            box.Selection.Select(box.Selection.End, box.Selection.End);
            Edited();
        }

        void ApplyFormat(DependencyProperty property, object value)
        {
            box.Selection.ApplyPropertyValue(property, value);
            Edited();
        }

        /// <summary>Back to the notes' own text color, so the text follows the background if it changes later.</summary>
        void ClearTextColor()
        {
            var sel = box.Selection;
            if (sel.IsEmpty) { ApplyFormat(TextElement.ForegroundProperty, new SolidColorBrush(fgColor)); return; }
            // Mark the selection with a color nobody picks, then take the color off everything that has it.
            var mark = Color.FromArgb(0xFE, 0x01, 0x02, 0x03);
            sel.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(mark));
            foreach (var p in NoteDocs.Paragraphs(box.Document))
                foreach (var e in Elements(p))
                {
                    var b = e.ReadLocalValue(TextElement.ForegroundProperty) as SolidColorBrush;
                    if (b != null && b.Color == mark) e.ClearValue(TextElement.ForegroundProperty);
                }
            Edited();
        }

        static IEnumerable<TextElement> Elements(Paragraph p)
        {
            var found = new List<TextElement> { p };
            AddInlines(p.Inlines, found);
            return found;
        }

        static void AddInlines(InlineCollection inlines, List<TextElement> found)
        {
            foreach (var i in inlines)
            {
                found.Add(i);
                var span = i as Span;
                if (span != null) AddInlines(span.Inlines, found);
            }
        }

        /// <summary>Underline or strikethrough on or off for the selection, leaving the other one as it is.</summary>
        void ToggleDecoration(TextDecorationLocation which)
        {
            bool isUnder = which == TextDecorationLocation.Underline;
            var sel = box.Selection;
            if (sel.IsEmpty)
            {
                bool u, s;
                NoteDocs.Flags(sel.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection, out u, out s);
                if (isUnder) u = !u; else s = !s;
                ApplyFormat(Inline.TextDecorationsProperty, NoteDocs.Decorations(u, s));
                return;
            }
            var pieces = new List<Tuple<TextPointer, TextPointer, bool, bool>>();
            foreach (var p in NoteDocs.Paragraphs(box.Document))
                foreach (var run in NoteDocs.Runs(p))
                {
                    var start = run.ContentStart.CompareTo(sel.Start) > 0 ? run.ContentStart : sel.Start;
                    var end = run.ContentEnd.CompareTo(sel.End) < 0 ? run.ContentEnd : sel.End;
                    if (start.CompareTo(end) >= 0) continue;
                    bool u, s;
                    NoteDocs.DecorationsOf(run, out u, out s);
                    pieces.Add(Tuple.Create(start, end, u, s));
                }
            if (pieces.Count == 0) return;
            bool all = pieces.All(x => isUnder ? x.Item3 : x.Item4);
            foreach (var x in pieces)
            {
                bool u = isUnder ? !all : x.Item3, s = isUnder ? x.Item4 : !all;
                new TextRange(x.Item1, x.Item2).ApplyPropertyValue(Inline.TextDecorationsProperty, NoteDocs.Decorations(u, s));
            }
            Edited();
        }

        static List ListOf(Paragraph p)
        {
            var item = p.Parent as ListItem;
            return item == null ? null : item.List;
        }

        List<Paragraph> SelectedParagraphs()
        {
            var sel = box.Selection;
            return NoteDocs.Paragraphs(box.Document)
                .Where(p => p.ContentEnd.CompareTo(sel.Start) >= 0 && p.ContentStart.CompareTo(sel.End) <= 0).ToList();
        }

        /// <summary>Checkboxes on (or off, if every chosen line has one) for the lines in the selection.</summary>
        void ToggleChecks()
        {
            var paras = SelectedParagraphs();
            if (paras.Count == 0) return;
            bool all = paras.All(p => NoteDocs.CheckOf(p) != null);
            foreach (var p in paras)
            {
                if (all) RemoveCheck(p);
                else if (NoteDocs.CheckOf(p) == null) AddCheck(p);
            }
            Edited();
        }

        void AddCheck(Paragraph p)
        {
            bool caretAtStart = box.CaretPosition.CompareTo(p.ContentStart) <= 0 || box.CaretPosition.GetOffsetToPosition(p.ContentStart) == 0;
            var c = NoteDocs.MakeCheck(false, fontSize, Edited);
            if (p.Inlines.FirstInline != null) p.Inlines.InsertBefore(p.Inlines.FirstInline, c);
            else p.Inlines.Add(c);
            if (caretAtStart || NoteDocs.Text(p).Length == 0) box.CaretPosition = c.ElementEnd;   // type after the box, not before it
        }

        static void RemoveCheck(Paragraph p)
        {
            var c = NoteDocs.CheckOf(p);
            if (c == null) return;
            bool wasDone = ((CheckBox)c.Child).IsChecked == true;
            var owner = c.Parent as Paragraph;
            var span = c.Parent as Span;
            if (owner != null) owner.Inlines.Remove(c);
            else if (span != null) span.Inlines.Remove(c);
            if (wasDone) NoteDocs.SetStrike(p, false);
        }

        /// <summary>Bullets or numbering on/off for the selected lines; a list that ends up next to one of its kind joins it.</summary>
        void ToggleList(bool numbered)
        {
            if (numbered) EditingCommands.ToggleNumbering.Execute(null, box);
            else EditingCommands.ToggleBullets.Execute(null, box);
            // Keep the selection where it was while lists join (positions are kept per paragraph, which don't change).
            var sel = box.Selection;
            Paragraph startP = sel.Start.Paragraph, endP = sel.End.Paragraph;
            int startOff = startP == null ? 0 : startP.ContentStart.GetOffsetToPosition(sel.Start);
            int endOff = endP == null ? 0 : endP.ContentStart.GetOffsetToPosition(sel.End);
            if (NoteDocs.JoinLists(box.Document) && startP != null && endP != null)
            {
                var s = startP.ContentStart.GetPositionAtOffset(startOff) ?? startP.ContentEnd;
                var e = endP.ContentStart.GetPositionAtOffset(endOff) ?? endP.ContentEnd;
                box.Selection.Select(s, e);
            }
            Edited();
        }

        // ---------- find (Ctrl+F) across all tabs ----------

        Border findBar;
        TextBox findBox;
        TextBlock findCount;
        readonly List<Tuple<int, TextPointer, TextPointer>> found = new List<Tuple<int, TextPointer, TextPointer>>();   // tab, start, end
        int foundAt = -1;
        bool findStale = true;   // the notes changed since the last search

        void BuildFindBar()
        {
            findBox = new TextBox { MinWidth = 60, Padding = new Thickness(4, 2, 4, 2), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Find in all tabs (Enter: next, Shift+Enter: previous)" };
            System.Windows.Automation.AutomationProperties.SetName(findBox, "Find");
            findBox.TextChanged += delegate { findStale = true; Find(0); };
            findBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; FindNext(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1); }
            };
            findCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0), FontSize = 11.5 };
            var prev = FindButton("", "Previous (Shift+Enter)", () => FindNext(-1));
            var next = FindButton("", "Next (Enter)", () => FindNext(1));
            var close = FindButton("", "Close (Esc)", CloseFind);
            var row = new DockPanel();
            foreach (var b in new[] { close, next, prev }) { DockPanel.SetDock(b, Dock.Right); row.Children.Add(b); }
            DockPanel.SetDock(findCount, Dock.Right);
            row.Children.Add(findCount);
            row.Children.Add(findBox);
            findBar = new Border { Margin = new Thickness(0, 0, 0, 6), Child = row, Visibility = Visibility.Collapsed };
            DockPanel.SetDock(findBar, Dock.Top);
        }

        Border FindButton(string glyph, string tip, Action click)
        {
            var b = HoverButton(new TextBlock
            {
                Text = glyph, FontFamily = TileView.IconFont, FontSize = 11, Foreground = new SolidColorBrush(chromeColor),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }, tip, 26, 24);
            System.Windows.Automation.AutomationProperties.SetName(b, tip);
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        public void OpenFind()
        {
            findCount.Foreground = new SolidColorBrush(chromeColor);
            foreach (var b in ((DockPanel)findBar.Child).Children.OfType<Border>()) ((TextBlock)b.Child).Foreground = new SolidColorBrush(chromeColor);
            findBar.Visibility = Visibility.Visible;
            string picked = box.Selection.Text;
            if (picked.Length > 0 && picked.Length < 80 && picked.IndexOf('\n') < 0) findBox.Text = picked.Trim();   // look for the selected words
            Activate();
            Dispatcher.BeginInvoke(new Action(() => { findBox.Focus(); findBox.SelectAll(); }), DispatcherPriority.Input);
            findStale = true;
            Find(0);
        }

        void CloseFind()
        {
            findBar.Visibility = Visibility.Collapsed;
            box.Focus();   // back to the text, with the last match still selected
        }

        void FindNext(int step)
        {
            if (findBar.Visibility != Visibility.Visible) { OpenFind(); return; }
            Find(step);
        }

        /// <summary>
        /// Looks for the find box's text in every tab (ignoring case). Step 0 = the first match from where the cursor is,
        /// for while you type; 1 / -1 = the next / previous match, moving to other tabs and around the end as needed.
        /// </summary>
        void Find(int step)
        {
            string q = findBox.Text;
            if (findStale)
            {
                found.Clear();
                if (q.Length > 0)
                    for (int t = 0; t < editors.Count; t++)
                        foreach (var p in NoteDocs.Paragraphs(editors[t].Document))
                            foreach (var m in NoteDocs.FindText(p, q)) found.Add(Tuple.Create(t, m.Item1, m.Item2));
                findStale = false;
                foundAt = -1;
                if (step != 0) step = 0;   // after a change, start from the cursor again
            }
            findBox.Background = q.Length > 0 && found.Count == 0 ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0xD6)) : SystemColors.WindowBrush;
            if (found.Count == 0)
            {
                foundAt = -1;
                findCount.Text = q.Length == 0 ? "" : "No results";
                return;
            }
            if (step == 0 || foundAt < 0)
            {
                // The first match at or after the cursor in this tab, else the first in a later tab, else the very first.
                var from = box.Selection.Start;
                foundAt = found.FindIndex(f => f.Item1 > current || (f.Item1 == current && f.Item2.CompareTo(from) >= 0));
                if (foundAt < 0) foundAt = 0;
            }
            else foundAt = (foundAt + step + found.Count) % found.Count;
            ShowFound(found[foundAt]);
            findCount.Text = (foundAt + 1) + " of " + found.Count;
        }

        void ShowFound(Tuple<int, TextPointer, TextPointer> f)
        {
            SelectTab(f.Item1);
            var editor = editors[f.Item1];
            editor.Selection.Select(f.Item2, f.Item3);
            editor.UpdateLayout();
            try
            {
                // Scroll so the match sits in the upper part of the text area.
                var r = f.Item2.GetCharacterRect(LogicalDirection.Forward);
                if (r.Top < 0 || r.Bottom > editor.ViewportHeight) editor.ScrollToVerticalOffset(editor.VerticalOffset + r.Top - editor.ViewportHeight / 3);
            }
            catch (Exception) { }
        }

        // ---------- resizing by dragging the outer edge ----------

        /// <summary>Raised while the outer edge is dragged: the new width (in device-independent pixels).</summary>
        public event Action<double> WidthDragged;
        public event Action WidthDragEnded;
        readonly Border[] grips = new Border[2];
        bool resizeOnLeft;

        /// <summary>Which edge can be dragged: the one away from the launcher (left when the notes are on the left).</summary>
        public bool ResizeOnLeft
        {
            get { return resizeOnLeft; }
            set
            {
                resizeOnLeft = value;
                if (grips[0] != null) grips[0].Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (grips[1] != null) grips[1].Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        Border MakeGrip(bool right)
        {
            var grip = new Border
            {
                Width = 7, Background = Brushes.Transparent, Cursor = Cursors.SizeWE, ToolTip = "Drag to make the notes wider or narrower",
                HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 12, 0, 12), Visibility = right ? Visibility.Visible : Visibility.Collapsed
            };
            grips[right ? 1 : 0] = grip;
            double startX = 0, startWidth = 0;
            grip.MouseLeftButtonDown += (s, e) =>
            {
                startX = PointToScreen(e.GetPosition(this)).X;
                startWidth = ActualWidth;
                grip.CaptureMouse();
                e.Handled = true;
            };
            grip.MouseMove += (s, e) =>
            {
                if (!grip.IsMouseCaptured) return;
                var source = PresentationSource.FromVisual(this);
                double scale = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M11 : 1;
                double dx = (PointToScreen(e.GetPosition(this)).X - startX) / scale;
                if (WidthDragged != null) WidthDragged(right ? startWidth + dx : startWidth - dx);
            };
            grip.MouseLeftButtonUp += (s, e) =>
            {
                if (!grip.IsMouseCaptured) return;
                grip.ReleaseMouseCapture();
                if (WidthDragEnded != null) WidthDragEnded();
            };
            return grip;
        }

        void OnEditorKey(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.None && e.Key == Key.F5) { e.Handled = true; InsertDateTime(); return; }
            if (mods == ModifierKeys.Control && (e.Key == Key.D0 || e.Key == Key.NumPad0)) { e.Handled = true; ApplyStyle(0); return; }
            if (mods == ModifierKeys.Control && (e.Key == Key.D1 || e.Key == Key.NumPad1)) { e.Handled = true; ApplyStyle(1); return; }
            if (mods == ModifierKeys.Control && (e.Key == Key.D2 || e.Key == Key.NumPad2)) { e.Handled = true; ApplyStyle(2); return; }
            if (mods == ModifierKeys.Control && (e.Key == Key.D3 || e.Key == Key.NumPad3)) { e.Handled = true; ApplyStyle(3); return; }
            if (mods == ModifierKeys.Control && e.Key == Key.U) { e.Handled = true; ToggleDecoration(TextDecorationLocation.Underline); return; }
            if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.X) { e.Handled = true; ToggleDecoration(TextDecorationLocation.Strikethrough); return; }
            if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.L) { e.Handled = true; ToggleList(false); return; }
            if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N) { e.Handled = true; ToggleList(true); return; }
            if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.C) { e.Handled = true; ToggleChecks(); return; }
            if (e.Key != Key.Enter || mods != ModifierKeys.None) return;

            // Enter on a checkbox line starts another checkbox line; Enter on an empty one ends the checklist.
            var p = box.CaretPosition.Paragraph;
            if (p == null || NoteDocs.CheckOf(p) == null) return;
            if (NoteDocs.Text(p).Trim().Length == 0) { e.Handled = true; RemoveCheck(p); Edited(); return; }
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var next = box.CaretPosition.Paragraph;
                if (next == null || next == p || NoteDocs.CheckOf(next) != null) return;
                AddCheck(next);
                NoteDocs.SetStrike(next, false);
                bool u, s;
                NoteDocs.Flags(box.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection, out u, out s);
                if (s) box.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, NoteDocs.Decorations(u, false));   // new line isn't done yet
                Edited();
            }), DispatcherPriority.Input);
        }

        /// <summary>Text pasted from other apps comes in plain, in the notes' own style; copies from these notes keep their formatting.</summary>
        void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var data = e.DataObject;
            string text = data.GetDataPresent(DataFormats.UnicodeText) ? data.GetData(DataFormats.UnicodeText) as string : null;
            if (text != null && lastCopied != null && text == lastCopied && data.GetDataPresent(DataFormats.Xaml)) return;
            if (text == null) { e.CancelCommand(); return; }
            var plain = new DataObject();
            plain.SetData(DataFormats.UnicodeText, text);
            e.DataObject = plain;
            e.FormatToApply = DataFormats.UnicodeText;
            linkPasted = true;   // pasted web addresses become links straight away
        }

        // ---------- test copies ----------

        /// <summary>Test copies only: select the text of tab <paramref name="tab"/>'s paragraph <paramref name="para"/>.</summary>
        public void TestSelect(int tab, int para)
        {
            SelectTab(tab);
            var p = NoteDocs.Paragraphs(box.Document).ElementAtOrDefault(para);
            if (p == null) return;
            var start = NoteDocs.CheckOf(p) != null ? NoteDocs.CheckOf(p).ElementEnd : p.ContentStart;
            box.Selection.Select(start, p.ContentEnd);
        }

        static void TestReport(string line)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(LauncherData.DataFolder, "test.txt"), line + "\r\n"); } catch (Exception) { }
        }

        public void TestCommand(string what)
        {
            if (what.StartsWith("find-"))
            {
                OpenFind();
                findBox.Text = what.Substring(5);
                TestReport("find \"" + findBox.Text + "\": " + findCount.Text + ", now on tab " + names[current] + ", selected \"" + box.Selection.Text + "\"");
                return;
            }
            switch (what)
            {
                case "findnext":
                    FindNext(1);
                    TestReport("next: " + findCount.Text + ", now on tab " + names[current] + ", selected \"" + box.Selection.Text + "\"");
                    return;
                case "indent": EditingCommands.IncreaseIndentation.Execute(null, box); break;
                case "title": ApplyStyle(1); break;
                case "heading": ApplyStyle(2); break;
                case "f5": box.Selection.Select(box.Selection.End, box.Selection.End); InsertDateTime(); break;
                case "tabcolor": SetTabColor(current, Color.FromRgb(0xFF, 0xF4, 0xA3)); break;
                case "undodel":
                    UndoDelete();
                    TestReport("after undo: tabs " + string.Join(", ", names) + "; open " + names[current]);
                    break;
                case "widen":
                    if (WidthDragged != null) WidthDragged(ActualWidth + 80);
                    if (WidthDragEnded != null) WidthDragEnded();
                    break;
                case "bold": EditingCommands.ToggleBold.Execute(null, box); break;
                case "italic": EditingCommands.ToggleItalic.Execute(null, box); break;
                case "underline": ToggleDecoration(TextDecorationLocation.Underline); break;
                case "strike": ToggleDecoration(TextDecorationLocation.Strikethrough); break;
                case "bullets": ToggleList(false); break;
                case "numbering": ToggleList(true); break;
                case "check": ToggleChecks(); break;
                case "tick":
                    var p = box.CaretPosition.Paragraph;
                    var c = p == null ? null : NoteDocs.CheckOf(p);
                    if (c != null) ((CheckBox)c.Child).IsChecked = !(((CheckBox)c.Child).IsChecked == true);
                    break;
                case "size24": ApplyFormat(TextElement.FontSizeProperty, 24.0); break;
                case "red": ApplyFormat(TextElement.ForegroundProperty, new SolidColorBrush(TextColors[0])); break;
                case "highlight": ApplyFormat(TextElement.BackgroundProperty, new SolidColorBrush(HighlightColors[0])); break;
                case "newtab": AddTab(); CommitRename(); break;
                case "enter":
                    // Press Enter at the end of the line, the way the keyboard does (key handler first, then the editor).
                    box.Selection.Select(box.Selection.End, box.Selection.End);
                    var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(box), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                    box.RaiseEvent(key);
                    if (!key.Handled) box.CaretPosition = box.CaretPosition.InsertParagraphBreak();   // what the editor does on Enter
                    break;
                case "type": box.Selection.Text = "Buy stamps"; break;
                case "typeurl":
                    box.Selection.Text = "Docs at https://example.com/page?a=1, see www.test.org and youtube.com. Mail bob@gmail.com (or (https://x.org/a_(b)))";
                    UpdateLinks();
                    break;
                case "unlink":
                    var first = NoteDocs.Paragraphs(box.Document).SelectMany(NoteDocs.Links).FirstOrDefault();
                    if (first != null) MakePlain(first);
                    break;
                case "relink": if (PlainUrls(box).Count > 0) MakeLink(PlainUrls(box)[0]); break;
                case "clicklink":
                case "draglink":
                case "clickplain":
                    // A click (or a drag) at the spot where the first link, or the start of the first line, is drawn.
                    var target = NoteDocs.Paragraphs(box.Document).SelectMany(NoteDocs.Links).FirstOrDefault();
                    if (target == null) break;
                    box.UpdateLayout();
                    var spot = what == "clickplain" ? NoteDocs.Paragraphs(box.Document).First().ContentStart.GetPositionAtOffset(2)
                                                    : target.ContentStart.GetPositionAtOffset(3);
                    var rect = spot.GetCharacterRect(LogicalDirection.Forward);
                    var pt = new Point(rect.X + 1, rect.Y + rect.Height / 2);
                    var under = LinkUnder(box, pt);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(LauncherData.DataFolder, "opened.txt"),
                        what + ": link under the pointer: " + (under == null ? "none" : NoteDocs.LinkText(under)) + "\r\n");
                    if (under != null) ClickedAt(box, under, pt, what == "draglink" ? new Point(pt.X + 40, pt.Y) : pt);
                    break;
                case "linkmenu":
                    var onLink = NoteDocs.Paragraphs(box.Document).SelectMany(NoteDocs.Links).FirstOrDefault();
                    if (onLink == null) break;
                    testMenuAt = onLink.ContentStart.GetPositionAtOffset(2);
                    FillEditorMenu();
                    testMenuAt = null;
                    box.ContextMenu.PlacementTarget = box;
                    box.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Center;
                    box.ContextMenu.IsOpen = true;
                    break;
                case "linkreport":
                    var links = NoteDocs.Paragraphs(box.Document).SelectMany(NoteDocs.Links)
                        .Select(l => NoteDocs.LinkText(l) + " -> " + (NoteDocs.UrlFor(NoteDocs.LinkText(l)) ?? "?")).ToList();
                    System.IO.File.AppendAllText(System.IO.Path.Combine(LauncherData.DataFolder, "links.txt"),
                        "links: " + string.Join("  |  ", links) + "\r\nkept plain: " + string.Join(", ", PlainUrls(box)) + "\r\n\r\n");
                    break;
                case "moveleft": MoveTab(current, -1); break;
                case "deltab": DeleteTab(current); break;
                case "menu": FillEditorMenu(); box.ContextMenu.PlacementTarget = box; box.ContextMenu.IsOpen = true; break;
            }
            Edited();
        }
    }
}
