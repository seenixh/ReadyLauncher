using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace ReadyLauncher
{
    /// <summary>A background picture carried inside a backup, so it can be restored on another PC.</summary>
    public class BackupPicture
    {
        public string Path;
        public byte[] Bytes;
    }

    /// <summary>Everything the launcher knows (icons, folders, lines, text, notes, settings) plus its pictures.</summary>
    [XmlRoot("ReadyLauncherBackup")]
    public class LauncherBackup
    {
        public int Version = 1;
        public DateTime Exported;
        public LauncherData Data;
        public List<BackupPicture> Pictures = new List<BackupPicture>();
    }

    internal static class Backup
    {
        const long MaxPictureBytes = 40L * 1024 * 1024;
        static XmlSerializer oldNameReader;   // backups saved before the rename (root element "WinLauncherBackup")

        public static void Export(LauncherData data, string file)
        {
            var b = new LauncherBackup { Exported = DateTime.Now, Data = data };
            var s = data.Settings;
            var files = new List<string> { s.BackgroundImage, s.DockBackgroundImage };
            // Custom icons that are the user's own pictures/.ico files (Windows' icon libraries exist on every PC).
            foreach (var it in data.AllItems())
                if (IconCache.IsPicture(it.IconPath) || (it.IconPath ?? "").EndsWith(".ico", StringComparison.OrdinalIgnoreCase)) files.Add(it.IconPath);
            foreach (var p in files)
            {
                if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) continue;
                if (b.Pictures.Any(x => string.Equals(x.Path, p, StringComparison.OrdinalIgnoreCase))) continue;
                if (new FileInfo(p).Length > MaxPictureBytes) continue;
                b.Pictures.Add(new BackupPicture { Path = p, Bytes = File.ReadAllBytes(p) });
            }
            string tmp = file + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(LauncherBackup)).Serialize(fs, b);
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        /// <summary>Read an exported backup, or a plain launcher.xml copied from another PC.</summary>
        public static LauncherBackup Read(string file)
        {
            LauncherBackup b = null;
            using (var r = XmlReader.Create(file))
            {
                var bs = new XmlSerializer(typeof(LauncherBackup));
                var ds = new XmlSerializer(typeof(LauncherData));
                if (oldNameReader == null) oldNameReader = new XmlSerializer(typeof(LauncherBackup), new XmlRootAttribute("WinLauncherBackup"));
                if (bs.CanDeserialize(r)) b = (LauncherBackup)bs.Deserialize(r);
                else if (oldNameReader.CanDeserialize(r)) b = (LauncherBackup)oldNameReader.Deserialize(r);
                else if (ds.CanDeserialize(r)) b = new LauncherBackup { Data = (LauncherData)ds.Deserialize(r) };
            }
            if (b == null || b.Data == null) throw new InvalidDataException("This file isn't a launcher backup.");
            if (b.Pictures == null) b.Pictures = new List<BackupPicture>();
            b.Data.Normalize();
            return b;
        }

        /// <summary>
        /// Pictures that no longer exist where they were (e.g. on a different PC) are written into the
        /// launcher's own folder, and the settings are pointed at the restored copies.
        /// </summary>
        public static void RestorePictures(LauncherBackup b)
        {
            var s = b.Data.Settings;
            foreach (var p in b.Pictures)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Path) || p.Bytes == null || p.Bytes.Length == 0) continue;
                if (File.Exists(p.Path)) continue;
                string dir = Path.Combine(LauncherData.DataFolder, "Pictures");
                Directory.CreateDirectory(dir);
                string name = Path.GetFileName(p.Path);
                if (string.IsNullOrWhiteSpace(name)) name = "picture.png";
                string target = Path.Combine(dir, name);
                for (int n = 2; File.Exists(target) && new FileInfo(target).Length != p.Bytes.Length; n++)
                    target = Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + " (" + n + ")" + Path.GetExtension(name));
                if (!File.Exists(target)) File.WriteAllBytes(target, p.Bytes);
                if (string.Equals(s.BackgroundImage, p.Path, StringComparison.OrdinalIgnoreCase)) s.BackgroundImage = target;
                if (string.Equals(s.DockBackgroundImage, p.Path, StringComparison.OrdinalIgnoreCase)) s.DockBackgroundImage = target;
                foreach (var it in b.Data.AllItems())
                    if (string.Equals(it.IconPath, p.Path, StringComparison.OrdinalIgnoreCase)) it.IconPath = target;
            }
        }

        /// <summary>"12 icons, 2 folders, 3 lines, 1 text label and notes" for the import question.</summary>
        public static string Describe(LauncherData d)
        {
            int folders = d.Items.Count(i => i.IsFolder);
            int icons = d.AllItems().Count(i => !i.IsFolder);
            int lines = d.Decorations.Count(x => x.Kind == DecorKind.Line);
            int texts = d.Decorations.Count(x => x.Kind == DecorKind.Text);
            var parts = new List<string> { Count(icons, "icon") };
            if (folders > 0) parts.Add(Count(folders, "folder"));
            if (lines > 0) parts.Add(Count(lines, "line"));
            if (texts > 0) parts.Add(Count(texts, "text label"));
            if (!string.IsNullOrWhiteSpace(d.NotesText)) parts.Add("notes");
            string list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
            return list + ", plus all settings";
        }

        static string Count(int n, string noun) { return n + " " + noun + (n == 1 ? "" : "s"); }
    }
}
