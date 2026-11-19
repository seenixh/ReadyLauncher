using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>Adding all installed apps at once, adding new apps automatically, and sorting by company / category.</summary>
    internal partial class LauncherWindow
    {
        public const string NewAppsFolder = "New apps";

        public void OpenAddAllApps(Window owner)
        {
            CloseFolder();
            ClearSelection();
            var result = AddAppsDialog.Ask(owner ?? this, app, S, Data.AllItems().ToList(), Data.Usage);
            if (result == null) return;
            if (result.Sections) AddAppSections(result.Groups);
            else if (result.Items.Count > 0) AddAppItems(result.Items);
        }

        // ---------- labeled sections ----------

        const double SectionLabelHeight = 36, SectionGap = 18;

        /// <summary>
        /// Lay apps out in labeled groups, no folders. What is already on the launcher (icons, lines and text, in
        /// their current arrangement) becomes the first group, "Existing", at the top.
        /// </summary>
        void AddAppSections(List<KeyValuePair<string, List<LauncherItem>>> groups)
        {
            if (!FreeLayout) S.Arrange = ArrangeMode.Free;   // sections are placed by position
            int cols = Columns();
            double y = Pad;
            if (Data.Items.Count > 0)
            {
                double top = Data.Items.Min(i => i.Y);
                if (Data.Decorations.Count > 0) top = Math.Min(top, Data.Decorations.Min(d => d.Y));
                double bottom = Math.Max(Data.Items.Max(i => i.Y) + CellH, DecorationsBottom());
                var oldDecor = Data.Decorations.ToList();
                AddSectionLabel("Existing", y);
                y += SectionLabelHeight;
                double dy = y - top;   // move the whole existing arrangement up (or down) to sit under its label
                foreach (var it in Data.Items) it.Y += dy;
                foreach (var d in oldDecor) d.Y += dy;
                y = bottom + dy + SectionGap;
            }
            else if (Data.Decorations.Count > 0) y = DecorationsBottom() + SectionGap;

            foreach (var g in groups)
            {
                if (g.Value.Count == 0) continue;
                AddSectionLabel(g.Key, y);
                y += SectionLabelHeight;
                for (int i = 0; i < g.Value.Count; i++)
                {
                    g.Value[i].X = Pad + (i % cols) * CellW;
                    g.Value[i].Y = y + (i / cols) * CellH;
                    Data.Items.Add(g.Value[i]);
                }
                y += Math.Ceiling(g.Value.Count / (double)cols) * CellH + SectionGap;
            }
            app.Save();
            RenderItems();
            Reveal(Data.Items.Count > 0 ? Pad : 0, y);
        }

        void AddSectionLabel(string name, double y)
        {
            Data.Decorations.Add(new Decoration { Kind = DecorKind.Text, Text = name, Font = "Segoe UI", FontSize = 18, Bold = true, X = Pad + 6, Y = y });
        }

        /// <summary>
        /// Make sure icons just added can be seen: scroll to them, and switch scrolling on if it was off and
        /// they don't all fit in the window.
        /// </summary>
        void Reveal(double top, double bottom)
        {
            UpdateLayout();
            double viewH = scroll.ViewportHeight > 0 ? scroll.ViewportHeight : scroll.ActualHeight;
            if (!S.ScrollEnabled && viewH > 0 && bottom > viewH + 2)
            {
                S.ScrollEnabled = true;
                scroll.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
                app.Save();
                UpdateCanvasExtent();
                const string why = "Not all of the new icons fit in the launcher window, so scrolling has been switched on to let you reach them. "
                    + "You can switch it off again in Settings > Appearance, or make the window bigger.";
                if (!IsVisible) app.Notify(why);   // added in the background (a newly installed app)
                else
                {
                    app.ModalOpen = true;
                    try { MessageBox.Show(this, why, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information); }
                    finally { app.ModalOpen = false; }
                }
            }
            UpdateLayout();
            if (top < scroll.VerticalOffset || top > scroll.VerticalOffset + viewH - CellH)
                scroll.ScrollToVerticalOffset(Math.Max(0, top - Pad));
        }

        /// <summary>
        /// Empty grid cells, top to bottom, that no icon, line or text covers, so new icons fill the gaps
        /// you can see before going further down.
        /// </summary>
        List<Point> FreeCells(int count)
        {
            int cols = Columns();
            var taken = Data.Items.Select(i => new Rect(i.X, i.Y, CellW, CellH)).ToList();
            foreach (var v in decorViews.Values)
                taken.Add(new Rect(System.Windows.Controls.Canvas.GetLeft(v), System.Windows.Controls.Canvas.GetTop(v), v.DesiredSize.Width, v.DesiredSize.Height));
            var cells = new List<Point>();
            for (int r = 0; cells.Count < count && r < 10000; r++)
                for (int c = 0; c < cols && cells.Count < count; c++)
                {
                    var p = CellPos(c, r);
                    var cell = new Rect(p.X + 6, p.Y + 6, CellW - 12, CellH - 12);
                    if (taken.Any(t => t.IntersectsWith(cell))) continue;
                    cells.Add(p);
                    taken.Add(new Rect(p.X, p.Y, CellW, CellH));
                }
            return cells;
        }

        static bool SameApp(LauncherItem a, LauncherItem b)
        {
            return string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) || string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Folders join an existing folder of the same name; everything else goes after the current icons.</summary>
        void AddAppItems(List<LauncherItem> items)
        {
            var place = new List<LauncherItem>();
            foreach (var it in items)
            {
                var same = it.IsFolder ? Data.Items.FirstOrDefault(f => f.IsFolder && string.Equals(f.Name, it.Name, StringComparison.OrdinalIgnoreCase)) : null;
                if (same == null) { place.Add(it); continue; }
                foreach (var c in it.Children) if (!same.Children.Any(x => SameApp(x, c))) same.Children.Add(c);
            }
            AppendItems(place);
        }

        /// <summary>Put new root icons in the first empty spots (from the top), in the order given, and show them.</summary>
        void AppendItems(IList<LauncherItem> items)
        {
            if (items.Count == 0) return;
            if (S.Arrange == ArrangeMode.Auto) Data.Items.AddRange(items);
            else
            {
                var cells = FreeCells(items.Count);
                for (int i = 0; i < items.Count && i < cells.Count; i++) { items[i].X = cells[i].X; items[i].Y = cells[i].Y; }
                Data.Items.AddRange(items);
                if (S.Arrange == ArrangeMode.Grid) LayoutGrid(null);
            }
            app.Save();
            RenderItems();
            Reveal(items.Min(i => i.Y), items.Max(i => i.Y) + CellH);
        }

        // ---------- sorting existing icons by company / category ----------

        /// <summary>Order by a group key, with "unknown" groups last, then by name.</summary>
        static int CompareGroups(string ga, bool lastA, string gb, bool lastB, LauncherItem a, LauncherItem b)
        {
            if (lastA != lastB) return lastA ? 1 : -1;
            int c = string.Compare(ga, gb, StringComparison.CurrentCultureIgnoreCase);
            return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        static int CompareCompany(LauncherItem a, LauncherItem b)
        {
            string ca = AppCatalog.Describe(a).Item1, cb = AppCatalog.Describe(b).Item1;
            return CompareGroups(ca, ca.Length == 0, cb, cb.Length == 0, a, b);
        }

        static int CompareCategory(LauncherItem a, LauncherItem b)
        {
            string ca = AppCatalog.Describe(a).Item2, cb = AppCatalog.Describe(b).Item2;
            return CompareGroups(ca, ca == AppCatalog.Other, cb, cb == AppCatalog.Other, a, b);
        }

        // ---------- adding newly installed apps automatically ----------

        FileSystemWatcher[] startMenuWatchers;
        DispatcherTimer appCheckDelay, storePoll;
        bool appCheckRunning, appCheckAgain;

        /// <summary>Start or stop watching for new apps to match the setting.</summary>
        public void UpdateAppWatching()
        {
            if (!S.AutoAddNewApps)
            {
                if (startMenuWatchers != null) foreach (var w in startMenuWatchers) w.Dispose();
                startMenuWatchers = null;
                if (storePoll != null) storePoll.Stop();
                return;
            }
            if (startMenuWatchers == null)
            {
                var list = new List<FileSystemWatcher>();
                foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                                             Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
                {
                    try
                    {
                        if (!Directory.Exists(root)) continue;
                        var w = new FileSystemWatcher(root, "*.lnk") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName };
                        FileSystemEventHandler changed = (s, e) => Dispatcher.BeginInvoke(new Action(ScheduleAppCheck));
                        w.Created += changed;
                        w.Renamed += (s, e) => Dispatcher.BeginInvoke(new Action(ScheduleAppCheck));
                        w.EnableRaisingEvents = true;
                        list.Add(w);
                    }
                    catch (Exception) { }
                }
                startMenuWatchers = list.ToArray();
            }
            if (storePoll == null)
            {
                // Store apps don't add Start menu shortcuts, so look for them every few minutes.
                storePoll = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
                storePoll.Tick += delegate { ScheduleAppCheck(); };
            }
            storePoll.Start();
            ScheduleAppCheck();   // also catches apps installed while the launcher wasn't running
        }

        /// <summary>Installers often create several shortcuts in a row; wait for them to finish.</summary>
        void ScheduleAppCheck()
        {
            if (appCheckDelay == null)
            {
                appCheckDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                appCheckDelay.Tick += delegate { appCheckDelay.Stop(); RunAppCheck(); };
            }
            appCheckDelay.Stop();
            appCheckDelay.Start();
        }

        void RunAppCheck()
        {
            if (!S.AutoAddNewApps) return;
            if (appCheckRunning) { appCheckAgain = true; return; }
            appCheckRunning = true;
            var seen = new HashSet<string>(Data.SeenApps, StringComparer.OrdinalIgnoreCase);
            bool firstLook = seen.Count == 0;   // just switched on: remember what's installed now, add nothing
            AppCatalog.ScanAsync(key => !seen.Contains(key), false, entries => Dispatcher.BeginInvoke(new Action(() =>
            {
                appCheckRunning = false;
                var fresh = entries.Where(e => !seen.Contains(e.Key)).ToList();
                Data.SeenApps = entries.Select(e => e.Key).ToList();   // forget uninstalled apps, so a reinstall counts as new
                if (!firstLook)
                {
                    var all = Data.AllItems().ToList();
                    var toAdd = fresh.Where(e => !e.Junk && !all.Any(i => string.Equals(i.Path, e.LaunchPath, StringComparison.OrdinalIgnoreCase)
                                                                     || (!i.IsFolder && string.Equals(i.Name, e.Name, StringComparison.OrdinalIgnoreCase)))).ToList();
                    if (toAdd.Count > 0) AddNewlyInstalled(toAdd);
                }
                app.Save();
                if (appCheckAgain) { appCheckAgain = false; ScheduleAppCheck(); }
            })));
        }

        void AddNewlyInstalled(List<AppEntry> apps)
        {
            var items = apps.Select(e => new LauncherItem { Kind = ItemKind.App, Name = e.Name, Path = e.LaunchPath }).ToList();
            if (S.AutoAddToFolder)
            {
                var folder = Data.Items.FirstOrDefault(f => f.IsFolder && string.Equals(f.Name, NewAppsFolder, StringComparison.OrdinalIgnoreCase));
                if (folder == null)
                {
                    folder = new LauncherItem { Kind = ItemKind.Folder, Name = NewAppsFolder };
                    folder.Children.AddRange(items);
                    AppendItems(new[] { folder });
                }
                else
                {
                    folder.Children.AddRange(items);
                    app.Save();
                    if (openFolder == folder) FillFolder(); else RenderItems();
                }
            }
            else AppendItems(items);

            string names = apps.Count == 1 ? apps[0].Name : apps.Count + " new apps";
            app.Notify("Added " + names + " to the launcher" + (S.AutoAddToFolder ? " (in the \"" + NewAppsFolder + "\" folder)." : "."));
        }
    }
}
