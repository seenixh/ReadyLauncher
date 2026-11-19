using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ReadyLauncher
{
    /// <summary>Giving a shortcut or folder a different icon: one of Windows' icons, or any picture.</summary>
    internal partial class LauncherWindow
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int PickIconDlg(IntPtr owner, StringBuilder path, int pathSize, ref int index);

        MenuItem IconMenu(LauncherItem it)
        {
            var menu = new MenuItem { Header = "Change icon", Icon = new TextBlock { Text = "", FontFamily = TileView.IconFont, FontSize = 14 } };
            menu.Items.Add(Item("Choose a Windows icon...", () => PickWindowsIcon(it), false, ""));
            menu.Items.Add(Item("Choose a picture...", () => PickIconPicture(it), false, ""));
            var reset = Item("Use the original icon", () => SetIcon(it, "", 0), false, "");
            reset.IsEnabled = !string.IsNullOrEmpty(it.IconPath);
            menu.Items.Add(new Separator());
            menu.Items.Add(reset);
            return menu;
        }

        /// <summary>Windows' own "Change Icon" dialog, starting in the app's file or in the big Windows icon library.</summary>
        void PickWindowsIcon(LauncherItem it)
        {
            string start = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\imageres.dll");
            int index = 0;
            if (!string.IsNullOrEmpty(it.IconPath) && !IconCache.IsPicture(it.IconPath)) { start = it.IconPath; index = it.IconIndex; }
            else if (it.Path != null && it.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(it.Path)) start = it.Path;

            var sb = new StringBuilder(start, 1024);
            int ok;
            app.ModalOpen = true;
            try { ok = PickIconDlg(new WindowInteropHelper(this).Handle, sb, sb.Capacity, ref index); }
            finally { app.ModalOpen = false; }
            if (ok == 0) return;
            SetIcon(it, Environment.ExpandEnvironmentVariables(sb.ToString()), index);
        }

        void PickIconPicture(LauncherItem it)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a picture for \"" + it.Name + "\"",
                Filter = "Pictures and icons|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.tif;*.tiff;*.jfif;*.webp|All files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };
            bool? ok;
            app.ModalOpen = true;
            try { ok = dlg.ShowDialog(this); }
            finally { app.ModalOpen = false; }
            if (ok == true) SetIcon(it, dlg.FileName, 0);
        }

        void SetIcon(LauncherItem it, string path, int index)
        {
            it.IconPath = path ?? "";
            it.IconIndex = index;
            app.Save();
            if (openFolder != null) FillFolder(); else RenderItems();
        }
    }
}
