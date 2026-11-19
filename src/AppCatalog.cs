using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.Win32;

namespace ReadyLauncher
{
    /// <summary>One installed app, from a Start menu shortcut (desktop app) or the Microsoft Store.</summary>
    internal class AppEntry
    {
        public string Key;            // stable id (lower-case launch path)
        public string Name;
        public string LaunchPath;     // the Start menu .lnk, or shell:AppsFolder\<AUMID> for Store apps
        public string Target = "";    // the program a shortcut points to (desktop apps)
        public string Company = "";
        public string Category = "";
        public string Folder = "";    // Start menu folder, e.g. "Windows Tools"
        public bool IsStore;
        public DateTime Installed;
        public bool Junk;             // uninstallers, help files, web pages...
        public bool Detailed;
    }

    /// <summary>Finds installed apps and works out their publisher and a best-guess category.</summary>
    internal static class AppCatalog
    {
        public const string Other = "Other";

        /// <summary>Scan on a background STA thread; <paramref name="done"/> runs on that thread.</summary>
        public static void ScanAsync(Func<string, bool> wantDetails, bool preloadIcons, Action<List<AppEntry>> done)
        {
            var t = new Thread(() =>
            {
                List<AppEntry> list;
                try { list = Scan(wantDetails, preloadIcons); }
                catch (Exception) { list = new List<AppEntry>(); }
                done(list);
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        public static List<AppEntry> Scan(Func<string, bool> wantDetails, bool preloadIcons)
        {
            var result = new List<AppEntry>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string self = Process.GetCurrentProcess().MainModule.FileName;

            // Desktop apps: every shortcut in the Start menu (yours and everyone's).
            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                                         Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) })
            {
                var files = new List<FileInfo>();
                ShellHelper.CollectLinks(root, files, 0);
                foreach (var f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f.Name);
                    if (!names.Add(name)) continue;   // the same app in both Start menus
                    string rel = f.DirectoryName.Length > root.Length ? f.DirectoryName.Substring(root.Length).Trim('\\') : "";
                    var e = new AppEntry
                    {
                        Name = name, LaunchPath = f.FullName, Key = f.FullName.ToLowerInvariant(),
                        Folder = rel, Installed = f.CreationTime, Junk = ShellHelper.IsJunk(name)
                    };
                    if (wantDetails == null || wantDetails(e.Key)) Detail(e, self);
                    result.Add(e);
                }
            }

            // Microsoft Store apps.
            var packages = PackageInfo();
            foreach (var pair in StoreApps())
            {
                string aumid = pair.Key, name = pair.Value;
                if (string.IsNullOrWhiteSpace(name) || !names.Add(name)) continue;
                string path = @"shell:AppsFolder\" + aumid;
                var e = new AppEntry { Name = name, LaunchPath = path, Key = path.ToLowerInvariant(), IsStore = true, Junk = ShellHelper.IsJunk(name) };
                string family = aumid.Substring(0, aumid.IndexOf('!'));
                Tuple<string, DateTime> info;
                if (packages.TryGetValue(family, out info)) { e.Company = info.Item1; e.Installed = info.Item2; }
                if (string.IsNullOrEmpty(e.Company) && family.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)) e.Company = "Microsoft";
                e.Category = Categorize(e.Name, e.Company, "");
                e.Detailed = true;
                result.Add(e);
            }

            if (preloadIcons)
                foreach (var e in result) if (!e.Junk) IconCache.Get(e.LaunchPath, 64);
            return result;
        }

        static readonly string[] docTypes = { ".chm", ".pdf", ".txt", ".htm", ".html", ".url", ".rtf", ".doc", ".docx", ".hlp", ".md", ".xml", ".log" };

        /// <summary>Read where a shortcut points, who made that program, and guess its category.</summary>
        public static void Detail(AppEntry e, string self)
        {
            if (e.Detailed) return;
            e.Detailed = true;
            string target, args;
            ShortcutTarget(e.LaunchPath, out target, out args);
            e.Target = target ?? "";
            string ext = Path.GetExtension(e.Target).ToLowerInvariant();
            if (Array.IndexOf(docTypes, ext) >= 0) e.Junk = true;                    // a document, not an app
            if (e.Target.Length > 0 && string.Equals(e.Target, self, StringComparison.OrdinalIgnoreCase)) e.Junk = true;
            e.Company = CompanyOf(e.Target);
            if (e.Company.Length == 0 && e.Name.StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)) e.Company = "Microsoft";
            if (e.Company.Length == 0 && officeApps.IsMatch(e.Name)) e.Company = "Microsoft";
            e.Category = Categorize(e.Name, e.Company, e.Folder);
        }

        static readonly Regex officeApps = new Regex(@"^(Word|Excel|PowerPoint|Outlook|OneNote|Access|Publisher|OneDrive|Teams|Visio|Project)\b", RegexOptions.IgnoreCase);

        static string CompanyOf(string exe)
        {
            try
            {
                if (string.IsNullOrEmpty(exe) || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(exe)) return "";
                return CleanCompany(FileVersionInfo.GetVersionInfo(exe).CompanyName);
            }
            catch (Exception) { return ""; }
        }

        static readonly Regex legalSuffix = new Regex(@"[,\s]+(Inc\.?|Incorporated|Corporation|Corp\.?|LLC|L\.L\.C\.|Ltd\.?|Limited|GmbH|AG|S\.A\.?|B\.V\.?|Co\.|Company|Pty|Pte|SE|Oy|AB|ApS|s\.r\.o\.)$", RegexOptions.IgnoreCase);

        /// <summary>"Mozilla Corporation" -> "Mozilla", "Google LLC" -> "Google".</summary>
        public static string CleanCompany(string c)
        {
            if (string.IsNullOrWhiteSpace(c)) return "";
            c = c.Trim().Trim('.').Trim();
            for (int i = 0; i < 3; i++) c = legalSuffix.Replace(c, "").Trim().TrimEnd(',').Trim();
            if (c.StartsWith("The ", StringComparison.OrdinalIgnoreCase) && c.Length > 8) c = c.Substring(4);
            return c.Length > 40 ? c.Substring(0, 40).Trim() : c;
        }

        // ---------- categories (a best guess from the name, Start menu folder and publisher) ----------

        static readonly Tuple<string, Regex>[] byName = Rules(
            "Internet & browsers", @"browser|chrome|chromium|firefox|edge|opera|brave|vivaldi|tor browser|waterfox|filezilla|qbittorrent|utorrent|vpn",
            "Communication", @"teams|zoom|discord|slack|skype|whatsapp|telegram|signal|mail|outlook|thunderbird|messenger|webex|phone link|meet",
            "Games", @"steam|epic games|games?|xbox|ea app|origin|ubisoft|uplay|battle\.net|gog galaxy|minecraft|roblox|solitaire|riot client|league of legends|valorant|fortnite",
            "Development", @"visual studio|vs ?code|code|terminal|powershell|command prompt|git|github|python|idle|node\.?js|java|jdk|android studio|intellij|pycharm|webstorm|rider|clion|sql|docker|postman|wsl|ubuntu|debian|kali|developer|notepad\+\+|sublime|vim|emacs|cmake",
            "Graphics & design", @"paint|photoshop|illustrator|lightroom|gimp|inkscape|krita|blender|figma|affinity|snipping|snip|canva|3d|cad|draw|sketch|paint\.net|irfanview|xnview|darktable",
            "Photos, music & video", @"media player|vlc|spotify|music|video|movies|tv|itunes|audacity|obs|netflix|prime video|youtube|clipchamp|photos?|camera|sound recorder|plex|kodi|winamp|foobar|handbrake|premiere|davinci|resolve|potplayer|mpc",
            "Office & productivity", @"word|excel|powerpoint|onenote|access|publisher|office|libreoffice|openoffice|writer|calc|impress|acrobat|reader|pdf|notepad|wordpad|to ?do|sticky notes|calendar|evernote|notion|obsidian|calculator|onedrive|dropbox|google drive|visio|project",
            "System & utilities", @"settings|control panel|task manager|registry|regedit|disk|cleanup|defrag|device manager|event viewer|services|system|recovery|backup|defender|security|antivirus|firewall|resource monitor|performance|file explorer|explorer|character map|remote desktop|magnifier|narrator|on-screen keyboard|7-zip|winrar|zip|ccleaner|driver|utility|tools|monitor|manager|print|scanner|clock|weather|maps|feedback|tips|get help|quick assist");

        static readonly Tuple<string, Regex>[] byFolder = Rules(
            "System & utilities", @"windows tools|administrative tools|system tools|accessibility|accessories|maintenance|windows powershell|startup",
            "Office & productivity", @"microsoft office|office tools|libreoffice",
            "Development", @"visual studio|python|git|java|node\.?js|jetbrains|android",
            "Games", @"games|steam|epic|ubisoft|ea\b|riot");

        static readonly Tuple<string, Regex>[] byCompany = Rules(
            "Games", @"valve|epic games|electronic arts|ubisoft|riot|blizzard|activision|mojang|rockstar|bethesda|cd projekt|square enix|bandai|sega|2k|unity technologies",
            "Graphics & design", @"adobe|autodesk|corel|serif|blender|figma|canva",
            "Development", @"jetbrains|github|python software|oracle|docker|postman|sublime|git development",
            "Photos, music & video", @"spotify|videolan|plex|apple|audacity|obs project|netflix",
            "Communication", @"zoom|slack|discord|whatsapp|telegram|signal|cisco",
            "Internet & browsers", @"mozilla|google|opera|brave|vivaldi",
            "System & utilities", @"nvidia|intel|advanced micro devices|amd|realtek|logitech|corsair|razer|asus|asustek|dell|hp|hewlett|lenovo|acer|micro-star|msi|samsung|western digital|seagate|piriform|igor pavlov|win\.rar|malwarebytes|avast|avg|norton|mcafee|kaspersky|eset|bitdefender");

        static Tuple<string, Regex>[] Rules(params string[] pairs)
        {
            var list = new List<Tuple<string, Regex>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                list.Add(Tuple.Create(pairs[i], new Regex(@"\b(" + pairs[i + 1] + @")\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)));
            return list.ToArray();
        }

        public static string Categorize(string name, string company, string folder)
        {
            foreach (var r in byName) if (r.Item2.IsMatch(name ?? "")) return r.Item1;
            foreach (var r in byFolder) if (r.Item2.IsMatch(folder ?? "")) return r.Item1;
            foreach (var r in byCompany) if (r.Item2.IsMatch(company ?? "")) return r.Item1;
            return Other;
        }

        // ---------- describing icons already on the launcher (for "sort by company / category") ----------

        static readonly Dictionary<string, Tuple<string, string>> described = new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, Tuple<string, DateTime>> packageCache;

        /// <summary>(company, category) for a launcher icon.</summary>
        public static Tuple<string, string> Describe(LauncherItem it)
        {
            if (it.IsFolder) return Tuple.Create("", it.Name ?? "");
            if (it.Kind == ItemKind.Link) return Tuple.Create("", "Web links");
            if (it.Kind == ItemKind.Keys) return Tuple.Create("", "Key shortcuts");
            string path = it.Path ?? "";
            Tuple<string, string> d;
            if (described.TryGetValue(path, out d)) return d;
            string company = "";
            try
            {
                if (path.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
                {
                    string aumid = path.Substring(17);
                    int bang = aumid.IndexOf('!');
                    if (bang > 0)
                    {
                        if (packageCache == null) packageCache = PackageInfo();
                        Tuple<string, DateTime> info;
                        if (packageCache.TryGetValue(aumid.Substring(0, bang), out info)) company = info.Item1;
                    }
                    if (company.Length == 0 && aumid.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) company = "Microsoft";
                }
                else if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string target, args;
                    ShortcutTarget(path, out target, out args);
                    company = CompanyOf(target);
                }
                else company = CompanyOf(path);
            }
            catch (Exception) { }
            if (company.Length == 0 && (it.Name ?? "").StartsWith("Microsoft ", StringComparison.OrdinalIgnoreCase)) company = "Microsoft";
            d = Tuple.Create(company, Categorize(it.Name, company, ""));
            described[path] = d;
            return d;
        }

        // ---------- shortcuts ----------

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLinkObject { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
            void GetIDList(out IntPtr pidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        }

        public static void ShortcutTarget(string lnk, out string target, out string args)
        {
            target = ""; args = "";
            object obj = null;
            try
            {
                obj = new ShellLinkObject();
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)obj).Load(lnk, 0);
                var link = (IShellLinkW)obj;
                var sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                target = sb.ToString();
                sb.Clear();
                link.GetArguments(sb, sb.Capacity);
                args = sb.ToString();
            }
            catch (Exception) { }
            finally { Native.Release(obj); }
        }

        // ---------- Microsoft Store apps ----------

        [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IEnumShellItems
        {
            [PreserveSig] int Next(uint count, [MarshalAs(UnmanagedType.Interface)] out IShellItem item, out uint fetched);
            void Skip(uint count);
            void Reset();
            void Clone(out IEnumShellItems copy);
        }

        static readonly Guid BHID_EnumItems = new Guid("94f60519-2850-4924-aa5a-d15e84868039");
        const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;

        /// <summary>Test copies only: the id and name of every item in Windows' "Apps" folder (what the Start menu lists).</summary>
        internal static List<KeyValuePair<string, string>> AppsFolderItems()
        {
            var list = new List<KeyValuePair<string, string>>();
            IShellItem folder = null;
            IEnumShellItems items = null;
            try
            {
                folder = Native.CreateItem("shell:AppsFolder");
                IntPtr p;
                folder.BindToHandler(IntPtr.Zero, BHID_EnumItems, typeof(IEnumShellItems).GUID, out p);
                items = (IEnumShellItems)Marshal.GetObjectForIUnknown(p);
                Marshal.Release(p);
                IShellItem child;
                uint fetched;
                while (items.Next(1, out child, out fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        string id, name;
                        child.GetDisplayName(SIGDN_PARENTRELATIVEPARSING, out id);
                        child.GetDisplayName(Native.SIGDN_NORMALDISPLAY, out name);
                        list.Add(new KeyValuePair<string, string>(id, name));
                    }
                    catch (Exception) { }
                    finally { Native.Release(child); }
                }
            }
            catch (Exception) { }
            finally { Native.Release(items); Native.Release(folder); }
            return list;
        }

        /// <summary>AUMID -> name for every Store (packaged) app in the "All apps" list.</summary>
        static List<KeyValuePair<string, string>> StoreApps()
        {
            var list = new List<KeyValuePair<string, string>>();
            IShellItem folder = null;
            IEnumShellItems items = null;
            try
            {
                folder = Native.CreateItem("shell:AppsFolder");
                IntPtr p;
                folder.BindToHandler(IntPtr.Zero, BHID_EnumItems, typeof(IEnumShellItems).GUID, out p);
                items = (IEnumShellItems)Marshal.GetObjectForIUnknown(p);
                Marshal.Release(p);
                IShellItem child;
                uint fetched;
                while (items.Next(1, out child, out fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        string id, name;
                        child.GetDisplayName(SIGDN_PARENTRELATIVEPARSING, out id);
                        child.GetDisplayName(Native.SIGDN_NORMALDISPLAY, out name);
                        int bang = id == null ? -1 : id.IndexOf('!');
                        // Packaged apps look like "Publisher.App_hash!AppId"; others duplicate Start menu shortcuts.
                        if (bang > 0 && id.Substring(0, bang).Contains("_") && !id.Contains("\\"))
                            list.Add(new KeyValuePair<string, string>(id, name));
                    }
                    catch (Exception) { }
                    finally { Native.Release(child); }
                }
            }
            catch (Exception) { }
            finally { Native.Release(items); Native.Release(folder); }
            return list;
        }

        const string PackagesKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        /// <summary>Package family name -> (publisher, install date), from the registry and each app's manifest.</summary>
        static Dictionary<string, Tuple<string, DateTime>> PackageInfo()
        {
            var map = new Dictionary<string, Tuple<string, DateTime>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(PackagesKey))
                {
                    if (key == null) return map;
                    foreach (var full in key.GetSubKeyNames())
                    {
                        var parts = full.Split('_');
                        if (parts.Length < 2) continue;
                        string family = parts[0] + "_" + parts[parts.Length - 1];
                        string root;
                        using (var k = key.OpenSubKey(full)) root = k == null ? null : k.GetValue("PackageRootFolder") as string;
                        if (string.IsNullOrEmpty(root)) continue;
                        DateTime installed = DateTime.MinValue;
                        try { installed = Directory.GetCreationTime(root); } catch (Exception) { }
                        Tuple<string, DateTime> cur;
                        if (map.TryGetValue(family, out cur) && cur.Item2 >= installed) continue;   // keep the newest version
                        map[family] = Tuple.Create(ManifestPublisher(Path.Combine(root, "AppxManifest.xml")), installed);
                    }
                }
            }
            catch (Exception) { }
            return map;
        }

        static string ManifestPublisher(string manifest)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(manifest);
                var shown = doc.SelectSingleNode("//*[local-name()='Properties']/*[local-name()='PublisherDisplayName']");
                string name = shown == null ? "" : shown.InnerText.Trim();
                if (name.Length > 0 && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)) return CleanCompany(name);
                var id = doc.SelectSingleNode("//*[local-name()='Identity']/@Publisher");
                if (id != null)
                {
                    var m = Regex.Match(id.Value, @"CN=([^,]+)");
                    if (m.Success && !Regex.IsMatch(m.Groups[1].Value, @"^[0-9A-F-]{20,}$", RegexOptions.IgnoreCase)) return CleanCompany(m.Groups[1].Value.Trim('"'));
                }
            }
            catch (Exception) { }
            return "";
        }
    }
}
