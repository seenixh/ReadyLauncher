using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ReadyLauncher
{
    /// <summary>Launching, naming, and turning drag-and-drop data into launcher items.</summary>
    internal static class ShellHelper
    {
        public static bool IsShellPath(string path)
        {
            return path != null && (path.StartsWith("::") || path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsUrl(string s)
        {
            Uri u;
            return Uri.TryCreate(s, UriKind.Absolute, out u) && (u.Scheme == "http" || u.Scheme == "https");
        }

        public static void Launch(string path, string args, bool asAdmin)
        {
            var psi = new ProcessStartInfo();
            psi.UseShellExecute = true;
            if (IsShellPath(path))
            {
                psi.FileName = "explorer.exe";
                psi.Arguments = path.StartsWith("::") ? "shell:" + path : path;
            }
            else
            {
                psi.FileName = path;
                psi.Arguments = args ?? "";
                if (File.Exists(path)) psi.WorkingDirectory = System.IO.Path.GetDirectoryName(path);
            }
            if (asAdmin) psi.Verb = "runas";
            Process.Start(psi);
        }

        public static void OpenLocation(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        public static string DisplayName(string path)
        {
            try
            {
                if (IsShellPath(path)) return ShellDisplayName(path) ?? path;
                if (Directory.Exists(path)) return new DirectoryInfo(path).Name;
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".exe")
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription) && vi.FileDescription.Length < 40)
                        return vi.FileDescription.Trim();
                }
                return System.IO.Path.GetFileNameWithoutExtension(path);
            }
            catch (Exception) { return System.IO.Path.GetFileNameWithoutExtension(path); }
        }

        static string ShellDisplayName(string parsingName)
        {
            IShellItem item = null;
            try
            {
                item = Native.CreateItem(parsingName);
                string name;
                item.GetDisplayName(Native.SIGDN_NORMALDISPLAY, out name);
                return name;
            }
            catch (Exception) { return null; }
            finally { Native.Release(item); }
        }

        public static LauncherItem ItemForPath(string path)
        {
            var it = new LauncherItem();
            it.Kind = ItemKind.App;
            it.Path = path;
            it.Name = DisplayName(path);
            return it;
        }

        public static LauncherItem ItemForUrl(string url, string title)
        {
            var it = new LauncherItem();
            it.Kind = ItemKind.Link;
            it.Path = url;
            it.Name = string.IsNullOrWhiteSpace(title) ? new Uri(url).Host.Replace("www.", "") : title;
            return it;
        }

        // ---------- drag & drop ----------

        public static bool CanAccept(IDataObject data)
        {
            return data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent("Shell IDList Array")
                || data.GetDataPresent("UniformResourceLocatorW") || data.GetDataPresent(DataFormats.UnicodeText);
        }

        public static List<LauncherItem> ItemsFromData(IDataObject data)
        {
            var list = new List<LauncherItem>();
            try
            {
                var files = data.GetData(DataFormats.FileDrop) as string[];
                // Apps dragged out of the Start menu's folders arrive named by their app ID alone ("AcrobatReader"),
                // not as files; the shell's own list of what was dragged describes them better, when it's there.
                if (files != null && files.Any(f => !File.Exists(f) && !Directory.Exists(f)))
                    list.AddRange(FromIdList(data));
                if (files != null && list.Count == 0)
                    foreach (var f in files)
                    {
                        // The Start menu can hand over names like "{Program Files ID}\Adobe\...\Photoshop.exe" here,
                        // which aren't real paths (and show a generic icon): turn them into ones that are.
                        string path = File.Exists(f) || Directory.Exists(f) ? f : FixShellPath(f);
                        var it = ItemForPath(path);
                        list.Add(it);
                    }
                if (list.Count == 0) list.AddRange(FromIdList(data));
                if (list.Count == 0)
                {
                    string url = ReadUnicode(data, "UniformResourceLocatorW");
                    if (url == null) url = data.GetData(DataFormats.UnicodeText) as string;
                    if (url != null) url = url.Trim().Split('\n')[0].Trim();
                    if (url != null && IsUrl(url)) list.Add(ItemForUrl(url, LinkTitle(data)));
                }
            }
            catch (Exception) { }
            return list;
        }

        // Start menu / "All apps" drags (including Store apps) arrive as a CIDA of shell IDs.
        static IEnumerable<LauncherItem> FromIdList(IDataObject data)
        {
            var result = new List<LauncherItem>();
            var ms = data.GetData("Shell IDList Array") as MemoryStream;
            if (ms == null) return result;
            byte[] b = ms.ToArray();
            int count = BitConverter.ToInt32(b, 0);
            var h = GCHandle.Alloc(b, GCHandleType.Pinned);
            try
            {
                IntPtr basePtr = h.AddrOfPinnedObject();
                IntPtr parent = basePtr + BitConverter.ToInt32(b, 4);
                for (int i = 0; i < count; i++)
                {
                    IntPtr full = Native.ILCombine(parent, basePtr + BitConverter.ToInt32(b, 8 + 4 * i));
                    IShellItem item = null;
                    try
                    {
                        Native.SHCreateItemFromIDList(full, typeof(IShellItem).GUID, out item);
                        string name;
                        item.GetDisplayName(Native.SIGDN_NORMALDISPLAY, out name);
                        var it = ItemForPath(BestPath(item));
                        if (!string.IsNullOrEmpty(name)) it.Name = name;
                        result.Add(it);
                    }
                    catch (Exception) { }
                    finally { Native.Release(item); Native.ILFree(full); }
                }
            }
            finally { h.Free(); }
            return result;
        }

        const uint SIGDN_FILESYSPATH = 0x80058000;
        const string AppsFolderPrefix = "::{4234D49B-0245-4DF3-B780-3893943456E1}\\";

        /// <summary>
        /// A path for a dragged-in shell item that the launcher can open and draw the real icon for. Items dragged from
        /// the Start menu aren't plain files: they're named inside Windows' "Apps" folder, or relative to a known folder
        /// like Program Files ("{GUID}\VideoLAN\VLC\vlc.exe"), and those names don't give an icon when read back later.
        /// </summary>
        static string BestPath(IShellItem item)
        {
            string fs = null, parsing = null;
            try { item.GetDisplayName(SIGDN_FILESYSPATH, out fs); } catch (Exception) { fs = null; }
            if (!string.IsNullOrEmpty(fs) && (File.Exists(fs) || Directory.Exists(fs))) return fs;   // a real file or folder
            item.GetDisplayName(Native.SIGDN_DESKTOPABSOLUTEPARSING, out parsing);
            return FixShellPath(parsing);
        }

        /// <summary>
        /// Turns a Start menu-style shell name into the first of: the real file, or shell:AppsFolder\... (what "Add all
        /// installed apps" uses) for which Windows can give an icon. Anything else comes back unchanged.
        /// </summary>
        public static string FixShellPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string id = path.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase) ? path.Substring(AppsFolderPrefix.Length) : null;
            if (id == null && IsAppId(path)) id = path;   // a Store app, named by its app ID alone
            if (id == null && IsBareName(path))
            {
                // A desktop app named by its own app ID alone ("AcrobatReader"), as the Start menu's folders hand
                // them over - but only if Windows knows an app by that ID.
                if (ShellDisplayName(@"shell:AppsFolder\" + path) == null) return path;
                id = path;
            }
            var tries = new List<string>();
            if (id != null && (id.Length > 3 && id[1] == ':' && File.Exists(id))) tries.Add(id);   // the id is the program's own path
            string known = FromKnownFolder(id ?? path);
            if (known != null) tries.Add(known);
            if (id != null && id.IndexOfAny(new[] { '\\', '!' }) < 0)
            {
                string link = StartMenuLink(id);   // its Start menu shortcut: a real file, with the app's own icon
                if (link != null) tries.Add(link);
            }
            if (id != null) tries.Add(@"shell:AppsFolder\" + id);
            if (tries.Count == 0) return path;
            foreach (var t in tries) if (IconCache.Get(t, 32) != null) return t;   // the first one Windows shows an icon for
            return tries[0];
        }

        /// <summary>
        /// A plain name that isn't a file, folder, address or Store app ID - possibly a desktop app's own app ID,
        /// like "AcrobatReader" for Adobe Acrobat.
        /// </summary>
        public static bool IsBareName(string s)
        {
            return !string.IsNullOrWhiteSpace(s) && s.IndexOfAny(new[] { '\\', '/', ':', '!' }) < 0 && !File.Exists(s) && !Directory.Exists(s);
        }

        /// <summary>The Start menu shortcut for a desktop app with this app ID (found by the app's name), or null.</summary>
        static string StartMenuLink(string id)
        {
            string name = ShellDisplayName(@"shell:AppsFolder\" + id);
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                                         Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
            {
                var links = new List<FileInfo>();
                CollectLinks(root, links, 0);
                foreach (var f in links)
                    if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(f.Name), name, StringComparison.OrdinalIgnoreCase))
                        return f.FullName;
            }
            return null;
        }

        /// <summary>"{known folder GUID}\rest" → the real path, if that file or folder exists.</summary>
        static string FromKnownFolder(string name)
        {
            if (name == null || name.Length < 40 || name[0] != '{' || name[37] != '}' || name[38] != '\\') return null;
            Guid g;
            if (!Guid.TryParse(name.Substring(1, 36), out g)) return null;
            string root = Native.KnownFolderPath(g);
            if (root == null) return null;
            string full = System.IO.Path.Combine(root, name.Substring(39));
            return File.Exists(full) || Directory.Exists(full) ? full : null;
        }

        /// <summary>True for paths saved by older versions from Start menu drags (which often show a generic icon).</summary>
        public static bool NeedsFixing(string path)
        {
            return path != null && (path.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase) || IsAppId(path)
                || (path.Length > 39 && path[0] == '{' && path[37] == '}' && path[38] == '\\'));
        }

        /// <summary>A Store app's ID, like "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App" (not a file, folder or address).</summary>
        static bool IsAppId(string s)
        {
            return s != null && s.IndexOf('!') > 0 && s.IndexOfAny(new[] { '\\', '/', ':' }) < 0;
        }

        static string ReadUnicode(IDataObject data, string format)
        {
            if (!data.GetDataPresent(format)) return null;
            var ms = data.GetData(format) as MemoryStream;
            if (ms == null) return null;
            return Encoding.Unicode.GetString(ms.ToArray()).TrimEnd('\0');
        }

        // Browsers put the page title in a virtual "Title.url" file descriptor.
        static string LinkTitle(IDataObject data)
        {
            try
            {
                var moz = ReadUnicode(data, "text/x-moz-url");
                if (moz != null && moz.Contains("\n")) return moz.Split('\n')[1].Trim();
                if (!data.GetDataPresent("FileGroupDescriptorW")) return null;
                var ms = data.GetData("FileGroupDescriptorW") as MemoryStream;
                byte[] b = ms.ToArray();
                if (b.Length < 76 + 2) return null;
                string name = Encoding.Unicode.GetString(b, 76, Math.Min(520, b.Length - 76));
                int z = name.IndexOf('\0');
                if (z >= 0) name = name.Substring(0, z);
                if (name.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
                return name;
            }
            catch (Exception) { return null; }
        }

        // ---------- dock sources ----------

        static readonly string[] junkWords = { "uninstall", "readme", "read me", "help", "documentation", "website",
            "release notes", "manual", "license", "setup", "installer", "update" };

        internal static bool IsJunk(string name)
        {
            string n = name.ToLowerInvariant();
            return junkWords.Any(w => n.Contains(w));
        }

        /// <summary>Newest Start-menu shortcuts, i.e. the most recently installed desktop apps.</summary>
        public static List<LauncherItem> RecentlyInstalled(int take)
        {
            var files = new List<FileInfo>();
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                                         Environment.GetFolderPath(Environment.SpecialFolder.Programs) })
                CollectLinks(root, files, 0);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<LauncherItem>();
            foreach (var f in files.OrderByDescending(f => f.CreationTime))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(f.Name);
                if (IsJunk(name) || !seen.Add(name)) continue;
                var it = new LauncherItem { Kind = ItemKind.App, Path = f.FullName, Name = name, DateAdded = f.CreationTime };
                result.Add(it);
                if (result.Count >= take) break;
            }
            return result;
        }

        internal static void CollectLinks(string dir, List<FileInfo> into, int depth)
        {
            if (string.IsNullOrEmpty(dir) || depth > 4) return;
            try
            {
                var di = new DirectoryInfo(dir);
                if (!di.Exists) return;
                into.AddRange(di.GetFiles("*.lnk"));
                foreach (var sub in di.GetDirectories()) CollectLinks(sub.FullName, into, depth + 1);
            }
            catch (Exception) { }
        }

        const string UserAssistKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

        /// <summary>
        /// Most-used apps: Windows' own launch counts (UserAssist, which records Start/taskbar/Explorer launches)
        /// plus launches made from this launcher.
        /// </summary>
        public static List<LauncherItem> MostFrequent(int take, List<UsageEntry> ownUsage)
        {
            var scores = new Dictionary<string, Tuple<string, string, int>>(StringComparer.OrdinalIgnoreCase); // name -> (path,name,count)
            Action<string, string, int> add = (path, name, count) =>
            {
                if (string.IsNullOrEmpty(name) || count <= 0 || IsJunk(name)) return;
                Tuple<string, string, int> cur;
                if (scores.TryGetValue(name, out cur)) scores[name] = Tuple.Create(cur.Item1, cur.Item2, cur.Item3 + count);
                else scores[name] = Tuple.Create(path, name, count);
            };

            string self = Process.GetCurrentProcess().MainModule.FileName;
            try
            {
                using (var ua = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UserAssistKey))
                {
                    if (ua != null)
                        foreach (var guid in ua.GetSubKeyNames())
                            using (var countKey = ua.OpenSubKey(guid + @"\Count"))
                            {
                                if (countKey == null) continue;
                                foreach (var raw in countKey.GetValueNames())
                                {
                                    var bytes = countKey.GetValue(raw) as byte[];
                                    if (bytes == null || bytes.Length < 16) continue;
                                    int count = BitConverter.ToInt32(bytes, 4);
                                    string path = ResolveUserAssist(Rot13(raw));
                                    if (path == null || string.Equals(path, self, StringComparison.OrdinalIgnoreCase)) continue;
                                    string name = DisplayName(path);
                                    add(path, name, count);
                                }
                            }
                }
            }
            catch (Exception) { }

            foreach (var u in ownUsage)
                if (File.Exists(u.Path) || IsShellPath(u.Path))
                    add(u.Path, u.Name ?? DisplayName(u.Path), u.Count);

            return scores.Values.OrderByDescending(t => t.Item3).Take(take)
                .Select(t => new LauncherItem { Kind = ItemKind.App, Path = t.Item1, Name = t.Item2, LaunchCount = t.Item3 })
                .ToList();
        }

        static string ResolveUserAssist(string name)
        {
            if (name.StartsWith("UEME_", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.StartsWith("{") && name.Length > 39 && name[37] == '}' && name[38] == '\\')
            {
                Guid g;
                if (!Guid.TryParse(name.Substring(1, 36), out g)) return null;
                string root = Native.KnownFolderPath(g);
                if (root == null) return null;
                name = System.IO.Path.Combine(root, name.Substring(39));
            }
            if (name.Contains("\\"))
            {
                string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
                if ((ext != ".exe" && ext != ".lnk") || !File.Exists(name)) return null;
                if (name.IndexOf(@"\Temp\", StringComparison.OrdinalIgnoreCase) >= 0) return null;
                return name;
            }
            // Otherwise it's an AppUserModelID (Store app, or a pinned desktop app).
            string shellPath = @"shell:AppsFolder\" + name;
            return ShellDisplayName(shellPath) != null ? shellPath : null;
        }

        static string Rot13(string s)
        {
            var c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++)
            {
                char ch = c[i];
                if (ch >= 'a' && ch <= 'z') c[i] = (char)('a' + (ch - 'a' + 13) % 26);
                else if (ch >= 'A' && ch <= 'Z') c[i] = (char)('A' + (ch - 'A' + 13) % 26);
            }
            return new string(c);
        }
    }
}
