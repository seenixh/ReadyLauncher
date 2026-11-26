using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Header bar, right-click menus, add/rename/remove, arrange and sort.</summary>
    internal partial class LauncherWindow
    {
        FrameworkElement BuildHeader()
        {
            var bar = new DockPanel { Height = 42, Background = Brushes.Transparent, LastChildFill = true };
            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == bar && e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(buttons, Dock.Right);
            buttons.Children.Add(GlyphButton("", "Add a shortcut or link", ShowAddMenu));
            buttons.Children.Add(GlyphButton("", "Arrange / sort icons", b => ShowMenu(b, ArrangeMenuItems())));
            buttons.Children.Add(GlyphButton("", "Notes window", b => ShowMenu(b, NotesMenuItems())));
            sensorButton = null;
            if (S.Sensors.Count > 0) buttons.Children.Add(SensorButton());   // only when there are sensors to show
            buttons.Children.Add(GlyphButton("", "Settings", b => app.OpenSettings()));
            buttons.Children.Add(BackgroundButton());   // send to background / keep on top again
            buttons.Children.Add(GlyphButton("", "Hide launcher (Esc)", b => HideLauncher()));
            bar.Children.Add(buttons);

            // The lock, on the left: keeps icons, lines and text where they are.
            lockButton = GlyphButton("", "", b => ToggleLock());
            lockButton.Margin = new Thickness(10, 0, 2, 0);
            System.Windows.Automation.AutomationProperties.SetName(lockButton, "Lock or unlock the layout");
            DockPanel.SetDock(lockButton, Dock.Left);
            bar.Children.Add(lockButton);
            ShowLockState();
            // The rest of the bar stays empty (the window is dragged by it), so it lets clicks through to the bar.
            bar.Children.Add(new Border { IsHitTestVisible = false });
            return bar;
        }

        Border lockButton;

        void ShowLockState()
        {
            if (lockButton == null) return;
            ((TextBlock)lockButton.Child).Text = S.LayoutLocked ? "" : "";   // closed / open padlock
            lockButton.ToolTip = S.LayoutLocked
                ? "Locked: icons, lines, text and sensors stay where they are. Click to unlock and move them again."
                : "Lock the layout: keep icons, lines, text and sensors where they are (click again to unlock)";
        }

        public void ToggleLock()
        {
            S.LayoutLocked = !S.LayoutLocked;
            app.Save();
            ShowLockState();
            if (sensorWindow != null) sensorWindow.SetLocked(S.LayoutLocked);
        }

        Border GlyphButton(string glyph, string tip, Action<FrameworkElement> click)
        {
            var c = ButtonsColor;   // chosen in Settings → Appearance, or readable on the background
            var b = new Border
            {
                Width = 30, Height = 32, CornerRadius = new CornerRadius(6), Background = Brushes.Transparent,
                ToolTip = tip, Cursor = Cursors.Hand, Margin = new Thickness(1, 0, 1, 0),   // close together
                Child = new TextBlock
                {
                    Text = glyph, FontFamily = TileView.IconFont, FontSize = 15, Foreground = new SolidColorBrush(c),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            var hover = new SolidColorBrush(Color.FromArgb(0x30, c.R, c.G, c.B));
            b.MouseEnter += delegate { b.Background = hover; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; click(b); };
            return b;
        }

        static void ShowMenu(FrameworkElement anchor, IEnumerable<object> items)
        {
            var menu = BuildMenu(items);
            if (anchor != null) { menu.PlacementTarget = anchor; menu.Placement = PlacementMode.Bottom; }
            menu.IsOpen = true;
        }

        /// <summary>A right-click menu in the theme's colors, its headings as plain labels.</summary>
        static ContextMenu BuildMenu(IEnumerable<object> items)
        {
            var menu = SettingsTheme.ThemeMenu(new ContextMenu());
            foreach (var i in items) menu.Items.Add(SettingsTheme.MenuEntry(i));
            return menu;
        }

        static MenuItem Item(string header, Action action, bool isChecked, string glyph)
        {
            var mi = new MenuItem { Header = header, IsChecked = isChecked };
            if (glyph != null && !isChecked) mi.Icon = new TextBlock { Text = glyph, FontFamily = TileView.IconFont, FontSize = 14 };
            mi.Click += delegate { action(); };
            return mi;
        }

        static MenuItem Item(string header, Action action) { return Item(header, action, false, null); }

        // ---------- menus ----------

        void ShowAddMenu(FrameworkElement anchor)
        {
            ShowMenu(anchor, AddMenuItems(null));
        }

        /// <param name="at">Where on the icon area a new line or text goes (the right-clicked spot), or null.</param>
        List<object> AddMenuItems(Point? at)
        {
            return new List<object>
            {
                Item("Browse for an app or file...", BrowseForApps, false, ""),
                Item("Add a folder location...", BrowseForFolder, false, ""),
                Item("Add a web link...", AddWebLink, false, ""),
                Item("Add all installed apps...", () => OpenAddAllApps(null), false, ""),
                Item("Add a keyboard shortcut...", AddKeyShortcut, false,""),
                new Separator(),
                Item("Add a horizontal line...", () => AddLine(false, at), false, ""),
                Item("Add a vertical line...", () => AddLine(true, at), false, ""),
                Item("Add text...", () => AddText(at), false, "")
            };
        }

        void AddKeyShortcut()
        {
            string name = null;
            ModifierKeys mods = ModifierKeys.None;
            Key key = Key.None;
            if (!KeyShortcutDialog.Edit(this, app, ref name, ref mods, ref key)) return;
            AddItems(new[] { new LauncherItem { Kind = ItemKind.Keys, Name = name, KeyMods = mods, KeyKey = key } }, null);
        }

        void EditKeyShortcut(LauncherItem it)
        {
            string name = it.Name;
            ModifierKeys mods = it.KeyMods;
            Key key = it.KeyKey;
            if (!KeyShortcutDialog.Edit(this, app, ref name, ref mods, ref key)) return;
            it.Name = name;
            it.KeyMods = mods;
            it.KeyKey = key;
            app.Save();
            if (openFolder != null) FillFolder(); else RenderItems();
        }


        List<object> NotesMenuItems()
        {
            return new List<object>
            {
                Item("Notes on the left", () => SetNotesPlacement(NotesPlacement.Left), S.NotesPos == NotesPlacement.Left, null),
                Item("Notes on the right", () => SetNotesPlacement(NotesPlacement.Right), S.NotesPos == NotesPlacement.Right, null),
                Item("No notes window", () => SetNotesPlacement(NotesPlacement.Off), S.NotesPos == NotesPlacement.Off, null),
                new Separator(),
                Item("Notes settings...", () => app.OpenSettings(SettingsWindow.NotesTab), false, "\uE713")
            };
        }
        List<object> ArrangeMenuItems()
        {
            var items = AllArrangeItems();
            if (!S.LayoutLocked) return items;
            // Locked: arranging and sorting would move things, so they wait until it's unlocked.
            foreach (var mi in items.OfType<MenuItem>()) mi.IsEnabled = false;
            items.Insert(0, Item("Unlock to rearrange (the padlock at the top left)", ToggleLock, false, ""));
            items.Insert(1, new Separator());
            return items;
        }

        List<object> AllArrangeItems()
        {
            return new List<object>
            {
                Item("Free placement (drag anywhere)", () => SetArrange(ArrangeMode.Free), S.Arrange == ArrangeMode.Free, null),
                Item("Free placement, align to nearby icons", () => SetArrange(ArrangeMode.FreeAlign), S.Arrange == ArrangeMode.FreeAlign, null),
                Item("Align icons to grid", () => SetArrange(ArrangeMode.Grid), S.Arrange == ArrangeMode.Grid, null),
                Item("Auto arrange icons", () => SetArrange(ArrangeMode.Auto), S.Arrange == ArrangeMode.Auto, null),
                new Separator(),
                Item("Sort by name", () => SortItems((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase)), false, ""),
                Item("Sort by date added", () => SortItems((a, b) => a.DateAdded.CompareTo(b.DateAdded)), false, ""),
                Item("Sort by company", () => SortItems(CompareCompany), false, ""),
                Item("Sort by category", () => SortItems(CompareCategory), false, ""),
                Item("Sort by most used",() => SortItems((a, b) => Uses(b).CompareTo(Uses(a))), false, "")
            };
        }

        static int Uses(LauncherItem i) { return i.IsFolder ? i.Children.Sum(c => c.LaunchCount) : i.LaunchCount; }

        void ShowCanvasMenu(Point at)
        {
            ShowMenu(null, CanvasMenuItems(at));
        }

        /// <summary>The menu for an empty spot on the icon area.</summary>
        List<object> CanvasMenuItems(Point at)
        {
            var items = AddMenuItems(at);
            items.Add(new Separator());
            if (SelectionCount > 0) items.AddRange(SelectionMenuItems());
            else
            {
                var all = Item("Select all (Ctrl+A)", SelectAll, false, "");
                all.IsEnabled = Data.Items.Count + Data.Decorations.Count > 0;
                items.Add(all);
                items.Add(new TextBlock { Text = "Tip: drag on an empty spot to select several icons", FontSize = 11, Opacity = 0.6, IsHitTestVisible = false });
            }
            items.Add(new Separator());
            items.Add(inBackground ? Item("Keep on top again", KeepOnTop) : Item("Send to background (stay open behind other windows)", SendToBackground));
            items.Add(new Separator());
            var arrange = new MenuItem { Header = "Arrange icons" };
            foreach (var i in ArrangeMenuItems()) arrange.Items.Add(SettingsTheme.MenuEntry(i));
            items.Add(arrange);
            var notes = new MenuItem { Header = "Notes window" };
            foreach (var i in NotesMenuItems()) notes.Items.Add(SettingsTheme.MenuEntry(i));
            items.Add(notes);
            items.Add(new Separator());
            items.Add(Item("Settings...", () => app.OpenSettings(), false, ""));
            return items;
        }

        /// <summary>
        /// Test copies only: the empty-spot menu, an icon's menu and the lamp strip's menu, drawn off screen (no menu
        /// is opened) in each Settings theme, side by side, saved as one picture.
        /// </summary>
        void SaveMenuPictures(string file)
        {
            var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            wait.Tick += delegate
            {
                wait.Stop();
                var keep = SettingsTheme.MenuMode;
                var modes = new[] { SettingsThemeMode.Light, SettingsThemeMode.LightGrey, SettingsThemeMode.Grey, SettingsThemeMode.DarkGrey, SettingsThemeMode.Dark, SettingsThemeMode.Christmas };
                var tile = tiles.Values.FirstOrDefault(v => v.Item.Kind != ItemKind.Folder);
                var columns = new List<List<System.Windows.Media.Imaging.BitmapSource>>();
                foreach (var mode in modes)
                {
                    SettingsTheme.MenuMode = mode;
                    var col = new List<System.Windows.Media.Imaging.BitmapSource>();
                    col.Add(MenuPicture(BuildMenu(CanvasMenuItems(new Point(20, 20)))));
                    if (tile != null) col.Add(MenuPicture(BuildMenu(ItemMenuItems(tile, DragOrigin.Canvas))));
                    col.Add(MenuPicture(BuildMenu(LampMenuItems())));
                    columns.Add(col);
                }
                SettingsTheme.MenuMode = keep;
                double colW = columns.Max(c => c.Max(b => b.Width)) + 16, height = columns.Max(c => c.Sum(b => b.Height + 12)) + 40;
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2c, 0x33, 0x2a)), null, new Rect(0, 0, colW * columns.Count, height));
                    for (int k = 0; k < columns.Count; k++)
                    {
                        var label = new FormattedText(modes[k].ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI"), 14, Brushes.White, 1.0);
                        dc.DrawText(label, new Point(k * colW + 10, 10));
                        double y = 36;
                        foreach (var b in columns[k]) { dc.DrawImage(b, new Rect(k * colW + 8, y, b.Width, b.Height)); y += b.Height + 12; }
                    }
                }
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(colW * columns.Count), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = System.IO.File.Create(file)) enc.Save(fs);
            };
            wait.Start();
        }

        /// <summary>Test copies only: a menu laid out and drawn on its own, never opened.</summary>
        static System.Windows.Media.Imaging.BitmapSource MenuPicture(ContextMenu menu)
        {
            menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            menu.Arrange(new Rect(menu.DesiredSize));
            menu.UpdateLayout();
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            rtb.Render(menu);
            rtb.Freeze();
            return rtb;
        }

        void ShowItemMenu(TileView t, DragOrigin origin)
        {
            ShowMenu(null, ItemMenuItems(t, origin));
        }

        List<object> ItemMenuItems(TileView t, DragOrigin origin)
        {
            var it = t.Item;
            var items = new List<object>();
            if (it.IsFolder)
            {
                items.Add(Item("Open", () => OpenFolder(it, false), false, ""));
                items.Add(Item("Rename", () => OpenFolder(it, true), false, ""));
                items.Add(Item("Ungroup", () => Ungroup(it), false, ""));
                items.Add(new Separator());
                items.Add(IconMenu(it));
                items.Add(new Separator());
                items.Add(Item("Remove folder from launcher", () => RemoveItem(it), false, ""));
                return items;
            }
            if (it.Kind == ItemKind.Keys)
            {
                items.Add(Item("Press " + AppSettings.HotkeyText(it.KeyMods, it.KeyKey), () => LaunchItem(it, false), false, ""));
                if (origin != DragOrigin.Dock) items.Add(Item("Change keys...", () => EditKeyShortcut(it), false, ""));
            }
            else items.Add(Item("Open", () => LaunchItem(it, false), false, ""));
            if (it.Kind == ItemKind.App && !ShellHelper.IsShellPath(it.Path))
            {
                items.Add(Item("Run as administrator", () => LaunchItem(it, true), false, ""));
                items.Add(Item("Open file location", () => ShellHelper.OpenLocation(it.Path), false, ""));
            }
            if (origin != DragOrigin.Dock)
            {
                items.Add(new Separator());
                items.Add(Item("Create a text link...", () => { CloseFolder(); AddTextLinkFor(it); }, false, ""));
                items.Add(IconMenu(it));
            }
            if (origin == DragOrigin.Dock)
            {
                items.Add(new Separator());
                items.Add(Item("Add to launcher", () => AddItems(new[] { it.CloneShortcut() }, null), false, ""));
                var hide = Item("Remove from quick launch", () => HideFromDock(it), false, "");
                hide.ToolTip = "Stop showing this app in " + DockListName(S.Dock) + " (it still shows in the other list). To bring it back, right-click the round button on the strip.";
                items.Add(hide);
            }
            else
            {
                items.Add(new Separator());
                items.Add(Item("Rename...", () => RenameItem(it), false, ""));
                if (origin == DragOrigin.Folder)
                    items.Add(Item("Move out of folder", () => { var f = openFolder; CloseFolder(); RemoveFromFolder(f, it, null, true); }, false, ""));
                items.Add(Item("Remove from launcher", () => RemoveItem(it), false, ""));
            }
            return items;
        }

        // ---------- commands ----------

        public void SetArrange(ArrangeMode mode)
        {
            if (mode == ArrangeMode.Auto && S.Arrange != ArrangeMode.Auto)
            {
                // Keep the current visual order (row by row) when switching to auto arrange.
                var ordered = Data.Items.OrderBy(i => Math.Round(i.Y / CellH)).ThenBy(i => i.X).ToList();
                Data.Items.Clear();
                Data.Items.AddRange(ordered);
            }
            S.Arrange = mode;
            app.Save();
            RenderItems();
        }

        void SortItems(Comparison<LauncherItem> cmp)
        {
            var sorted = Data.Items.ToList();
            sorted.Sort(cmp);
            Data.Items.Clear();
            Data.Items.AddRange(sorted);
            LayoutAuto();
            app.Save();
            RenderItems();
        }

        void BrowseForApps()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose apps or files to add to the launcher",
                Multiselect = true,
                DereferenceLinks = false,
                Filter = "Apps and shortcuts|*.exe;*.lnk;*.url;*.appref-ms;*.bat;*.cmd;*.msc|All files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };
            bool? ok;
            app.ModalOpen = true;
            try { ok = dlg.ShowDialog(this); }
            finally { app.ModalOpen = false; }
            if (ok == true) AddItems(dlg.FileNames.Select(f => ShellHelper.ItemForPath(f)).ToList(), null);
        }

        void BrowseForFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose a folder to add to the launcher" })
            {
                System.Windows.Forms.DialogResult result;
                app.ModalOpen = true;
                try { result = dlg.ShowDialog(new Win32Owner(new WindowInteropHelper(this).Handle)); }
                finally { app.ModalOpen = false; }
                if (result == System.Windows.Forms.DialogResult.OK)
                    AddItems(new[] { ShellHelper.ItemForPath(dlg.SelectedPath) }, null);
            }
        }

        void AddWebLink()
        {
            string url = InputDialog.Ask(this, app, "Add a web link", "Web address (URL):", "https://");
            if (string.IsNullOrWhiteSpace(url)) return;
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            if (!ShellHelper.IsUrl(url))
            {
                MessageBox.Show(this, "That doesn't look like a web address.", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var item = ShellHelper.ItemForUrl(url, null);
            string name = InputDialog.Ask(this, app, "Add a web link", "Name to show under the icon:", item.Name);
            if (!string.IsNullOrWhiteSpace(name)) item.Name = name.Trim();
            AddItems(new[] { item }, null);
        }

        void RenameItem(LauncherItem it)
        {
            string name = InputDialog.Ask(this, app, "Rename", "Name:", it.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            it.Name = name.Trim();
            app.Save();
            if (openFolder != null) FillFolder(); else RenderItems();
        }

        void RemoveItem(LauncherItem it)
        {
            if (openFolder != null && openFolder.Children.Contains(it))
            {
                var f = openFolder;
                f.Children.Remove(it);
                if (f.Children.Count == 0) { CloseFolder(); Data.Items.Remove(f); RenderItems(); }
                else FillFolder();
                app.Save();
                return;
            }
            if (it.IsFolder && it.Children.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    "Remove the folder \"" + it.Name + "\" and the " + it.Children.Count + " shortcut(s) inside it from the launcher?\n\n(The apps and files themselves are not touched.)",
                    Program.AppName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK) return;
            }
            Data.Items.Remove(it);
            app.Save();
            RenderItems();
        }
    }

    internal class Win32Owner : System.Windows.Forms.IWin32Window
    {
        readonly IntPtr handle;
        public Win32Owner(IntPtr handle) { this.handle = handle; }
        public IntPtr Handle { get { return handle; } }
    }
}
