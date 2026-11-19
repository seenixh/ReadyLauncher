using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>Folders: merge-on-drop, the open-folder panel with its editable name, ungroup/dissolve.</summary>
    internal partial class LauncherWindow
    {
        Grid overlay;
        Border folderPanel;
        TextBox folderName;
        WrapPanel folderItems;
        LauncherItem openFolder;

        bool FolderOverlayShown { get { return overlay.Visibility == Visibility.Visible && overlay.Opacity > 0; } }

        UIElement BuildOverlay()
        {
            overlay = new Grid { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)) };
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == overlay) { CloseFolder(); e.Handled = true; }
            };

            folderName = new TextBox
            {
                FontSize = 20, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 14), MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "Folder name - click to rename"
            };
            folderName.TextChanged += delegate { if (openFolder != null) openFolder.Name = folderName.Text; };
            folderName.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { folderPanel.Focus(); e.Handled = true; }
            };

            folderItems = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var stack = new StackPanel();
            stack.Children.Add(folderName);
            stack.Children.Add(folderItems);

            folderPanel = new Border
            {
                CornerRadius = new CornerRadius(18), Padding = new Thickness(20, 16, 20, 16),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 280, Child = stack, AllowDrop = true, Focusable = true,
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 4, Opacity = 0.45 }
            };
            folderPanel.DragOver += (s, e) => { e.Effects = LinkEffect(e); e.Handled = true; };
            folderPanel.Drop += (s, e) =>
            {
                if (openFolder == null) return;
                foreach (var it in ShellHelper.ItemsFromData(e.Data)) openFolder.Children.Add(it);
                FillFolder();
                app.Save();
                e.Handled = true;
            };
            overlay.Children.Add(folderPanel);
            return overlay;
        }

        void StyleOverlay()
        {
            folderPanel.Background = new SolidColorBrush(Color.FromArgb(0xF5, (byte)(bg.R * 0.88), (byte)(bg.G * 0.88), (byte)(bg.B * 0.88)));
            folderName.Foreground = fg;
            folderName.CaretBrush = fg;
            folderName.BorderBrush = new SolidColorBrush(Color.FromArgb(0x80, fgColor.R, fgColor.G, fgColor.B));
        }

        void OpenFolder(LauncherItem folder, bool rename)
        {
            openFolder = folder;
            folderName.Text = folder.Name;
            FillFolder();
            overlay.Opacity = 1;
            overlay.IsHitTestVisible = true;
            overlay.Visibility = Visibility.Visible;
            if (rename)
                Dispatcher.BeginInvoke(new Action(() => { folderName.Focus(); folderName.SelectAll(); }), DispatcherPriority.Input);
        }

        void FillFolder()
        {
            folderItems.Children.Clear();
            if (openFolder == null) return;
            double cellH = IconSize + 52;
            folderItems.ItemWidth = CellW;
            folderItems.ItemHeight = cellH;
            folderPanel.MaxWidth = CellW * 5 + 44;
            foreach (var c in openFolder.Children)
            {
                var t = new TileView(c, IconSize, true, new SolidColorBrush(IconTextColor), CellW);
                AttachTileHandlers(t, DragOrigin.Folder);
                folderItems.Children.Add(t);
            }
        }

        void CloseFolder()
        {
            if (openFolder == null) return;
            if (string.IsNullOrWhiteSpace(openFolder.Name)) openFolder.Name = "Folder";
            openFolder = null;
            overlay.Visibility = Visibility.Collapsed;
            overlay.Opacity = 1;
            overlay.IsHitTestVisible = true;
            app.Save();
            RenderItems();
        }

        /// <summary>
        /// Drop <paramref name="source"/> on <paramref name="target"/>: adds it to the target folder, or turns
        /// the two icons into a new folder (opened with its name selected so it can be typed straight away).
        /// </summary>
        LauncherItem MergeIntoFolder(LauncherItem source, LauncherItem target, bool openIfNew)
        {
            if (source == target) return target;
            LauncherItem folder = target;
            bool created = false;
            if (!target.IsFolder)
            {
                folder = new LauncherItem { Kind = ItemKind.Folder, Name = "New folder", X = target.X, Y = target.Y };
                int idx = Data.Items.IndexOf(target);
                if (idx >= 0) Data.Items[idx] = folder; else Data.Items.Add(folder);
                folder.Children.Add(target);
                created = true;
            }
            Data.Items.Remove(source);
            if (source.IsFolder) folder.Children.AddRange(source.Children);
            else folder.Children.Add(source);
            app.Save();
            RenderItems();
            if (openIfNew && created) OpenFolder(folder, true);
            return folder;
        }

        /// <summary>Take a child out of a folder; a folder left with one icon turns back into that icon.</summary>
        void RemoveFromFolder(LauncherItem folder, LauncherItem child, Point? canvasPoint, bool placeOnCanvas)
        {
            folder.Children.Remove(child);
            int idx = Data.Items.IndexOf(folder);
            if (folder.Children.Count == 1 && idx >= 0)
            {
                var last = folder.Children[0];
                folder.Children.Clear();
                last.X = folder.X; last.Y = folder.Y;
                Data.Items[idx] = last;
            }
            else if (folder.Children.Count == 0 && idx >= 0) Data.Items.RemoveAt(idx);

            if (placeOnCanvas) AddItems(new[] { child }, canvasPoint);
            else { app.Save(); RenderItems(); }
        }

        void Ungroup(LauncherItem folder)
        {
            CloseFolder();
            int idx = Data.Items.IndexOf(folder);
            var kids = folder.Children.ToList();
            folder.Children.Clear();
            Data.Items.Remove(folder);
            if (kids.Count > 0)
            {
                kids[0].X = folder.X; kids[0].Y = folder.Y;
                Data.Items.Insert(Math.Max(0, Math.Min(idx, Data.Items.Count)), kids[0]);
            }
            if (kids.Count > 1) AddItems(kids.Skip(1).ToList(), null);
            else { app.Save(); RenderItems(); }
        }

        void ReorderInFolder(LauncherItem item, Point windowPoint)
        {
            if (openFolder == null) return;
            for (int i = 0; i < folderItems.Children.Count; i++)
            {
                var t = folderItems.Children[i] as TileView;
                if (t == null || t.Item == item || !IsOver(t, windowPoint)) continue;
                openFolder.Children.Remove(item);
                openFolder.Children.Insert(Math.Min(i, openFolder.Children.Count), item);
                app.Save();
                break;
            }
            FillFolder();
        }
    }
}
