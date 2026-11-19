using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>What to add: folders / loose icons, or named groups to lay out as labeled sections.</summary>
    internal class AddAppsResult
    {
        public bool Sections;
        public List<LauncherItem> Items = new List<LauncherItem>();
        public List<KeyValuePair<string, List<LauncherItem>>> Groups = new List<KeyValuePair<string, List<LauncherItem>>>();
    }

    /// <summary>Find every installed app and choose how to group and order them before adding them all.</summary>
    internal class AddAppsDialog : Window
    {
        static readonly string[] GroupNames = { "Don't group", "Category", "Company (publisher)", "First letter",
            "Desktop or Microsoft Store app", "When it was installed" };
        static readonly string[] OrderNames = { "Name (A to Z)", "Name (Z to A)", "Company", "Category", "Newest first", "Oldest first", "Most used" };
        static readonly string[] StyleNames = { "Folders", "Labeled groups on the launcher (no folders)" };

        readonly HashSet<string> existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, int> uses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        readonly TextBlock status, styleHint;
        readonly ComboBox group, order, style;
        readonly CheckBox skipExisting, skipJunk;
        readonly Button add;
        List<AppEntry> apps;
        public AddAppsResult Result;

        public static AddAppsResult Ask(Window owner, LauncherApp app, AppSettings s, IEnumerable<LauncherItem> existing, List<UsageEntry> usage)
        {
            var dlg = new AddAppsDialog(s, existing, usage);
            SettingsTheme.Use(dlg, s.SettingsTheme);   // the Settings colors (no bright white checkboxes)
            if (owner != null && owner.IsVisible) dlg.Owner = owner; else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            bool wasModal = app.ModalOpen;
            app.ModalOpen = true;
            try
            {
                if (dlg.ShowDialog() != true) return null;
                s.AddAppsGroup = dlg.group.SelectedIndex;   // remember the choices for next time
                s.AddAppsOrder = dlg.order.SelectedIndex;
                s.AddAppsSections = dlg.style.SelectedIndex == 1;
                return dlg.Result;
            }
            finally { app.ModalOpen = wasModal; }
        }

        AddAppsDialog(AppSettings s, IEnumerable<LauncherItem> existing, List<UsageEntry> usage)
        {
            Title = "Add all installed apps";
            Width = 600;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");

            foreach (var it in existing)
            {
                if (!string.IsNullOrEmpty(it.Path)) existingPaths.Add(it.Path);
                if (!string.IsNullOrEmpty(it.Name) && !it.IsFolder) existingNames.Add(it.Name);
            }
            var usageCopy = usage.ToList();

            var g = SettingsWindow.Page();
            g.MinHeight = 0;
            status = new TextBlock { Text = "Looking for installed apps...", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) };
            Grid.SetColumnSpan(status, 2);
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.Children.Add(status);

            group = SettingsWindow.Combo(s.AddAppsGroup, GroupNames);
            SettingsWindow.AddRow(g, "Group them by", group);
            style = SettingsWindow.Combo(s.AddAppsSections ? 1 : 0, StyleNames);
            SettingsWindow.AddRow(g, "Show the groups as", style);
            styleHint = new TextBlock { FontSize = 11.5, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) };
            SettingsWindow.AddRow(g, "", styleHint);
            order = SettingsWindow.Combo(s.AddAppsOrder, OrderNames);
            SettingsWindow.AddRow(g, "Order them by", order);
            group.SelectionChanged += delegate { ShowStyleHint(); };
            style.SelectionChanged += delegate { ShowStyleHint(); };
            ShowStyleHint();
            skipExisting = SettingsWindow.Check("Leave out apps that are already in the launcher", true);
            skipJunk = SettingsWindow.Check("Leave out uninstallers, help files and web pages", true);
            var checks = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            checks.Children.Add(skipExisting);
            checks.Children.Add(skipJunk);
            SettingsWindow.AddRow(g, "", checks);
            SettingsWindow.AddRow(g, "", new TextBlock
            {
                Text = "Categories are a best guess from each app's name, Start menu folder and publisher.",
                FontSize = 11.5, Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0)
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 4, 12, 12) };
            add = new Button { Content = "Add apps", MinWidth = 110, IsDefault = true, IsEnabled = false, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 4, 10, 4) };
            add.Click += delegate { Result = Build(); DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true, Padding = new Thickness(0, 4, 0, 4) };
            buttons.Children.Add(add);
            buttons.Children.Add(cancel);

            var root = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);
            root.Children.Add(g);
            Content = root;

            skipExisting.Click += delegate { UpdateCount(); };
            skipJunk.Click += delegate { UpdateCount(); };

            Loaded += delegate
            {
                AppCatalog.ScanAsync(null, true, list =>
                {
                    // Windows' own launch counts, for "Most used".
                    List<LauncherItem> frequent;
                    try { frequent = ShellHelper.MostFrequent(1000, usageCopy); } catch (Exception) { frequent = new List<LauncherItem>(); }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        foreach (var f in frequent) if (!uses.ContainsKey(f.Name)) uses[f.Name] = f.LaunchCount;
                        apps = list;
                        UpdateCount();
                    }));
                });
            };
        }

        void ShowStyleHint()
        {
            bool sections = style.SelectedIndex == 1;
            style.IsEnabled = group.SelectedIndex != 0 || sections;
            styleHint.Text = sections
                ? "Each group gets a heading on the launcher with its icons underneath. What's already on your launcher becomes the first group, "
                  + "\"Existing\", at the top (its icons, lines and text keep their arrangement). Icons are then placed freely rather than on the grid."
                : group.SelectedIndex == 0
                    ? "Every app is added as an icon in the first empty spots."
                    : "Each group becomes a folder. A group with only one app is added as a plain icon, and a folder with the same name as one you already have is added to.";
        }

        bool AlreadyThere(AppEntry e) { return existingPaths.Contains(e.LaunchPath) || existingNames.Contains(e.Name); }

        List<AppEntry> Chosen()
        {
            return apps.Where(e => !(skipJunk.IsChecked == true && e.Junk) && !(skipExisting.IsChecked == true && AlreadyThere(e))).ToList();
        }

        void UpdateCount()
        {
            if (apps == null) return;
            int store = apps.Count(e => e.IsStore && !e.Junk), desktop = apps.Count(e => !e.IsStore && !e.Junk);
            int there = apps.Count(e => !e.Junk && AlreadyThere(e)), junk = apps.Count(e => e.Junk);
            int n = Chosen().Count;
            status.Text = "Found " + (store + desktop) + " apps on this PC: " + desktop + " desktop apps and " + store + " from the Microsoft Store"
                + (there > 0 ? ". " + there + " are already in your launcher" : "")
                + (junk > 0 ? ". " + junk + " uninstallers, help files and web pages were found too" : "") + ".";
            add.Content = n == 1 ? "Add 1 app" : "Add " + n + " apps";
            add.IsEnabled = n > 0;
        }

        int Uses(AppEntry e) { int n; return uses.TryGetValue(e.Name, out n) ? n : 0; }

        IEnumerable<AppEntry> Sorted(IEnumerable<AppEntry> list)
        {
            var byName = StringComparer.CurrentCultureIgnoreCase;
            switch (order.SelectedIndex)
            {
                case 1: return list.OrderByDescending(e => e.Name, byName);
                case 2: return list.OrderBy(e => e.Company.Length == 0 ? 1 : 0).ThenBy(e => e.Company, byName).ThenBy(e => e.Name, byName);
                case 3: return list.OrderBy(e => e.Category == AppCatalog.Other ? 1 : 0).ThenBy(e => e.Category, byName).ThenBy(e => e.Name, byName);
                case 4: return list.OrderByDescending(e => e.Installed).ThenBy(e => e.Name, byName);
                case 5: return list.OrderBy(e => e.Installed).ThenBy(e => e.Name, byName);
                case 6: return list.OrderByDescending(Uses).ThenBy(e => e.Name, byName);
                default: return list.OrderBy(e => e.Name, byName);
            }
        }

        /// <summary>Folder name for an app, plus a sort key that puts the groups in a sensible order.</summary>
        string GroupOf(AppEntry e, out string sortKey)
        {
            string name;
            switch (group.SelectedIndex)
            {
                // Sort keys start with 0 (normal) or 1 ("other" groups, which go last).
                case 1: name = e.Category; sortKey = (name == AppCatalog.Other ? "1|" : "0|") + name; return name;
                case 2: name = e.Company.Length > 0 ? e.Company : "Other publishers"; sortKey = (e.Company.Length > 0 ? "0|" : "1|") + name; return name;
                case 3:
                    char c = string.IsNullOrEmpty(e.Name) ? '#' : char.ToUpperInvariant(e.Name[0]);
                    name = char.IsLetter(c) ? c.ToString() : "#";
                    sortKey = (name == "#" ? "1|" : "0|") + name;
                    return name;
                case 4: name = e.IsStore ? "Microsoft Store apps" : "Desktop apps"; sortKey = "0|" + name; return name;
                case 5:
                    var now = DateTime.Now;
                    int rank;
                    if (e.Installed >= now.AddDays(-31)) { name = "Installed this month"; rank = 0; }
                    else if (e.Installed.Year == now.Year) { name = "Installed earlier this year"; rank = 1; }
                    else if (e.Installed.Year == now.Year - 1) { name = "Installed last year"; rank = 2; }
                    else { name = "Installed before " + (now.Year - 1); rank = 3; }
                    sortKey = rank.ToString();
                    return name;
                default: sortKey = ""; return null;
            }
        }

        static LauncherItem ToItem(AppEntry e)
        {
            return new LauncherItem { Kind = ItemKind.App, Name = e.Name, Path = e.LaunchPath };
        }

        AddAppsResult Build()
        {
            var chosen = Chosen();
            var result = new AddAppsResult { Sections = style.SelectedIndex == 1 };
            if (group.SelectedIndex == 0)
            {
                var all = Sorted(chosen).Select(ToItem).ToList();
                if (result.Sections) result.Groups.Add(new KeyValuePair<string, List<LauncherItem>>("Installed apps", all));
                else result.Items = all;
                return result;
            }

            var groups = new Dictionary<string, List<AppEntry>>(StringComparer.OrdinalIgnoreCase);
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in chosen)
            {
                string sortKey, name = GroupOf(e, out sortKey);
                List<AppEntry> list;
                if (!groups.TryGetValue(name, out list)) { groups[name] = list = new List<AppEntry>(); keys[name] = sortKey; }
                list.Add(e);
            }
            var loose = new List<AppEntry>();
            foreach (var name in groups.Keys.OrderBy(k => keys[k], StringComparer.CurrentCultureIgnoreCase))
            {
                var members = Sorted(groups[name]).ToList();
                if (result.Sections) { result.Groups.Add(new KeyValuePair<string, List<LauncherItem>>(name, members.Select(ToItem).ToList())); continue; }
                if (members.Count == 1) { loose.Add(members[0]); continue; }
                var folder = new LauncherItem { Kind = ItemKind.Folder, Name = name };
                folder.Children.AddRange(members.Select(ToItem));
                result.Items.Add(folder);
            }
            result.Items.AddRange(Sorted(loose).Select(ToItem));
            return result;
        }
    }
}
