using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ReadyLauncher
{
    /// <summary>Settings > Appearance > Theme: choosing a theme, saving your look as one, and sharing themes as files.</summary>
    internal partial class SettingsWindow
    {
        readonly List<string> themeIds = new List<string>();   // "" for None, then each theme's id, in list order
        bool themeListBusy;
        Button deleteThemeButton, exportThemeButton;

        string SelectedThemeId { get { int k = themeBox.SelectedIndex; return k >= 0 && k < themeIds.Count ? themeIds[k] : ""; } }

        /// <summary>The Theme row: the list, then its buttons and a word on what a theme does.</summary>
        void AddThemeRows(Grid g, AppSettings s)
        {
            themeBox = new ComboBox { Padding = new Thickness(6, 4, 6, 4) };
            bool known = Themes.Find(s.Theme) != null || Themes.FindMine(s, s.Theme) != null;
            if (known && s.OwnLook != null) ownLook = Themes.Copy(s.OwnLook);
            FillThemeList(known ? s.Theme : "");
            themeBox.SelectionChanged += delegate { ChooseTheme(); ShowThemeButtons(); };
            AddRow(g, "Theme", themeBox);

            var save = new Button { Content = "Save as theme...", Padding = new Thickness(10, 2, 10, 2), ToolTip = "Keep the look you see now as a theme of your own: its pictures, colors, see-through settings, animation and lamp strip" };
            deleteThemeButton = new Button { Content = "Delete", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Delete the theme chosen above (only themes you saved or imported) and go back to your own look" };
            exportThemeButton = new Button { Content = "Export...", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Save the theme chosen above to a file you can share; anyone with Ready Launcher can add it with Import" };
            var import = new Button { Content = "Import...", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Add a theme someone shared with you (a .rltheme file)" };
            save.Click += delegate { SaveAsTheme(); };
            deleteThemeButton.Click += delegate { DeleteTheme(); };
            exportThemeButton.Click += delegate { ExportTheme(); };
            import.Click += delegate { ImportTheme(); };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(save);
            buttons.Children.Add(deleteThemeButton);
            buttons.Children.Add(exportThemeButton);
            buttons.Children.Add(import);
            AddRow(g, "", buttons);
            ShowThemeButtons();
        }

        /// <summary>
        /// Test copies only, and Settings opened after the theme in use was deleted: nothing to show for it, so your own
        /// look is put back, ready to keep with OK.
        /// </summary>
        void RestoreIfThemeGone(AppSettings s)
        {
            bool gone = s.Theme.Length > 0 && Themes.Find(s.Theme) == null && Themes.FindMine(s, s.Theme) == null;
            if (gone && s.OwnLook != null) PutLook(Themes.Copy(s.OwnLook));
        }

        void FillThemeList(string selectId)
        {
            var s = app.Data.Settings;
            themeListBusy = true;
            themeBox.Items.Clear();
            themeIds.Clear();
            themeBox.Items.Add("None (your own look)");
            themeIds.Add("");
            foreach (var t in Themes.All) { themeBox.Items.Add(t.Name); themeIds.Add(t.Id); }
            foreach (var u in s.MyThemes) { themeBox.Items.Add(u.Name); themeIds.Add(u.Id); }
            themeBox.SelectedIndex = Math.Max(0, themeIds.IndexOf(selectId ?? ""));
            themeListBusy = false;
            ShowThemeButtons();
        }

        void ShowThemeButtons()
        {
            if (deleteThemeButton == null) return;
            string id = SelectedThemeId;
            deleteThemeButton.IsEnabled = Themes.FindMine(app.Data.Settings, id) != null;
            exportThemeButton.IsEnabled = id.Length > 0;
        }

        /// <summary>Choose a theme in the list and show its look, as picking it would (your own look is kept to go back to).</summary>
        void ShowTheme(string id, SavedLook look)
        {
            if (ownLook == null) ownLook = LookFromControls();
            FillThemeList(id);
            PutLook(look);
        }

        MessageBoxResult Say(string text, string title, MessageBoxButton buttons)
        {
            app.ModalOpen = true;   // the launcher stays open behind the message
            try { return MessageBox.Show(this, text, title, buttons, buttons == MessageBoxButton.YesNo ? MessageBoxImage.Question : MessageBoxImage.Information); }
            finally { app.ModalOpen = false; }
        }

        void SaveAsTheme()
        {
            var s = app.Data.Settings;
            var mine = Themes.FindMine(s, SelectedThemeId);
            string name = InputDialog.Ask(this, app, "Save as theme",
                "A name for this look. The theme keeps its pictures, colors, see-through settings, animation and lamp strip:", mine != null ? mine.Name : "My theme");
            if (name == null) return;
            name = name.Trim();
            if (name.Length == 0) return;
            if (name.Length > 60) name = name.Substring(0, 60).Trim();
            foreach (var t in Themes.All)
                if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) || name.StartsWith("None", StringComparison.OrdinalIgnoreCase) && name.Contains("own look"))
                {
                    Say("\"" + name + "\" is already the name of a built-in theme. Please choose another name.", "Save as theme", MessageBoxButton.OK);
                    return;
                }
            UserTheme existing = null;
            foreach (var u in s.MyThemes) if (string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase)) existing = u;
            if (existing != null && Say("You already have a theme named \"" + existing.Name + "\". Replace it with the look you see now?", "Save as theme", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            var look = LookFromControls();
            string id = existing != null ? existing.Id : Themes.NewId();
            try
            {
                // The theme gets its own copies of the pictures, so it still works if the originals move.
                look.BackgroundImage = Themes.KeepPicture(look.BackgroundImage, id, "background");
                look.DockBackgroundImage = Themes.KeepPicture(look.DockBackgroundImage, id, "strip");
            }
            catch (Exception ex)
            {
                Say("The theme's pictures couldn't be copied: " + ex.Message, "Save as theme", MessageBoxButton.OK);
                return;
            }
            if (existing == null) { existing = new UserTheme { Id = id }; s.MyThemes.Add(existing); }
            existing.Name = name;
            existing.Look = look;
            app.Save();
            ShowTheme(id, Themes.Copy(look));
        }

        void DeleteTheme()
        {
            var s = app.Data.Settings;
            var u = Themes.FindMine(s, SelectedThemeId);
            if (u == null) return;
            if (Say("Delete your theme \"" + u.Name + "\"? Your own look comes back (press OK or Apply to keep it).", "Delete theme", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            s.MyThemes.Remove(u);
            if (ownLook != null) PutLook(ownLook);   // as if None was chosen
            ownLook = null;
            var used = new List<string> { imageRow.Box.Text, dockImageRow.Box.Text, s.BackgroundImage, s.DockBackgroundImage };
            if (s.OwnLook != null) { used.Add(s.OwnLook.BackgroundImage); used.Add(s.OwnLook.DockBackgroundImage); }
            foreach (var o in s.MyThemes)
                if (o.Look != null) { used.Add(o.Look.BackgroundImage); used.Add(o.Look.DockBackgroundImage); }
            Themes.DeletePictures(u, used);
            app.Save();
            FillThemeList("");
        }

        void ExportTheme()
        {
            var s = app.Data.Settings;
            string id = SelectedThemeId;
            if (id.Length == 0) return;
            string name = (string)themeBox.SelectedItem;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export theme", Filter = ThemeFiles.Filter, DefaultExt = ThemeFiles.Extension, FileName = ThemeFiles.FileNameFor(name),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            bool? ok;
            app.ModalOpen = true;
            try { ok = dlg.ShowDialog(this); } finally { app.ModalOpen = false; }
            if (ok != true) return;
            try
            {
                ThemeFiles.Export(name, Themes.LookForId(s, id), dlg.FileName);
                Say("Saved \"" + name + "\" to " + dlg.FileName + ".\n\nAnyone with Ready Launcher can add it with Import.", "Export theme", MessageBoxButton.OK);
            }
            catch (Exception ex) { Say("The theme couldn't be exported: " + ex.Message, "Export theme", MessageBoxButton.OK); }
        }

        void ImportTheme()
        {
            var s = app.Data.Settings;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import theme", Filter = ThemeFiles.ImportFilter + "|All files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            bool? ok;
            app.ModalOpen = true;
            try { ok = dlg.ShowDialog(this); } finally { app.ModalOpen = false; }
            if (ok != true) return;
            UserTheme u;
            try { u = ThemeFiles.Import(dlg.FileName, s); }
            catch (Exception ex)
            {
                Say("That theme couldn't be added: " + ex.Message, "Import theme", MessageBoxButton.OK);
                return;
            }
            app.Save();
            ShowTheme(u.Id, Themes.Copy(u.Look));
            Say("Added \"" + u.Name + "\" to your themes. It's showing here now: press OK or Apply to use it.", "Import theme", MessageBoxButton.OK);
        }
    }
}
