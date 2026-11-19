using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>A ready-made look: a background picture and matching colors for every window and button.</summary>
    internal class LookTheme
    {
        public string Id, Name;
        public SavedLook Look;                 // the colors (its BackgroundImage is filled in by MakePicture)
        public Func<string, string> MakePicture;   // folder -> the background picture, made the first time it's needed
    }

    internal static class Themes
    {
        public static readonly LookTheme[] All = { ChristmasLights() };

        public static LookTheme Find(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }

        /// <summary>Where themes keep their pictures: next to the launcher's own data.</summary>
        public static string PictureFolder { get { return Path.Combine(LauncherData.DataFolder, "themes"); } }

        /// <summary>A theme's look, with its picture made if it isn't there yet.</summary>
        public static SavedLook LookFor(LookTheme t)
        {
            var l = Copy(t.Look);
            l.BackgroundImage = t.MakePicture(PictureFolder);
            return l;
        }

        /// <summary>At startup: a theme picture that has gone missing (say, after moving to another PC) is made again.</summary>
        public static void EnsurePictures(AppSettings s)
        {
            var t = Find(s.Theme);
            if (t == null || string.IsNullOrEmpty(s.BackgroundImage) || File.Exists(s.BackgroundImage)) return;
            if (!string.Equals(Path.GetDirectoryName(s.BackgroundImage), PictureFolder, StringComparison.OrdinalIgnoreCase)) return;
            try { s.BackgroundImage = t.MakePicture(PictureFolder); } catch (Exception) { }
        }

        public static SavedLook Copy(SavedLook a)
        {
            return new SavedLook
            {
                BackgroundColor = a.BackgroundColor, BackgroundImage = a.BackgroundImage, BackgroundImageOff = a.BackgroundImageOff,
                BackgroundImageTransparency = a.BackgroundImageTransparency,
                ButtonsColor = a.ButtonsColor, VolumeColor = a.VolumeColor, IconTextColor = a.IconTextColor, NotesChromeColor = a.NotesChromeColor,
                SameColorForAll = a.SameColorForAll, AllPartsColor = a.AllPartsColor,
                NotesBackColor = a.NotesBackColor, DockBackgroundColor = a.DockBackgroundColor, DockBackgroundImage = a.DockBackgroundImage,
                DockImageOff = a.DockImageOff, DockImageTransparency = a.DockImageTransparency,
                SensorTextColor = a.SensorTextColor, SensorBackColor = a.SensorBackColor, SettingsTheme = a.SettingsTheme,
                Full = a.Full, WindowTransparency = a.WindowTransparency, DockTransparency = a.DockTransparency,
                Animation = a.Animation, AnimationSpeed = a.AnimationSpeed,
                AnimateNotes = a.AnimateNotes, AnimateSensors = a.AnimateSensors, AnimateDock = a.AnimateDock, AnimateVolume = a.AnimateVolume,
                LampPanel = a.LampPanel, LampMode = a.LampMode
            };
        }

        // ---------- your own themes ----------

        public static UserTheme FindMine(AppSettings s, string id)
        {
            foreach (var u in s.MyThemes) if (u.Id == id) return u;
            return null;
        }

        /// <summary>The look of a built-in theme or one of yours, by id; null if there's no such theme.</summary>
        public static SavedLook LookForId(AppSettings s, string id)
        {
            var t = Find(id);
            if (t != null) return LookFor(t);
            var u = FindMine(s, id);
            return u == null || u.Look == null ? null : Copy(u.Look);
        }

        /// <summary>
        /// A copy of a picture for a theme of yours, kept with the themes, so the theme still works after the original
        /// is moved or deleted. Returns where the copy is (or the path unchanged, if there's no such picture).
        /// </summary>
        public static string KeepPicture(string path, string themeId, string what)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return path;
            Directory.CreateDirectory(PictureFolder);
            string copy = Path.Combine(PictureFolder, themeId + "-" + what + Path.GetExtension(path).ToLowerInvariant());
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase)) return copy;
            File.Copy(path, copy, true);
            return copy;
        }

        public static string NewId() { return "my-" + Guid.NewGuid().ToString("N").Substring(0, 10); }

        /// <summary>
        /// Removes picture copies made for your own themes ("my-..." files in the themes folder) that nothing uses any
        /// more: not your look, not your kept look, and none of your themes. Built-in theme pictures are left alone.
        /// </summary>
        public static void CleanUpPictures(AppSettings s)
        {
            var used = new List<string> { s.BackgroundImage, s.DockBackgroundImage };
            if (s.OwnLook != null) { used.Add(s.OwnLook.BackgroundImage); used.Add(s.OwnLook.DockBackgroundImage); }
            foreach (var u in s.MyThemes)
                if (u.Look != null) { used.Add(u.Look.BackgroundImage); used.Add(u.Look.DockBackgroundImage); }
            DeleteUnused("my-*", used);
        }

        static void DeleteUnused(string pattern, IEnumerable<string> stillUsed)
        {
            try
            {
                if (!Directory.Exists(PictureFolder)) return;
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in stillUsed)
                    if (!string.IsNullOrWhiteSpace(p)) try { used.Add(Path.GetFullPath(p)); } catch (Exception) { }
                foreach (var file in Directory.GetFiles(PictureFolder, pattern))
                    if (!used.Contains(Path.GetFullPath(file))) File.Delete(file);
            }
            catch (Exception) { }
        }

        /// <summary>A deleted theme's own picture copies go too, unless something still shows them.</summary>
        public static void DeletePictures(UserTheme u, IEnumerable<string> stillUsed)
        {
            DeleteUnused(u.Id + "-*", stillUsed);
        }

        /// <summary>
        /// Christmas Lights: a snowy night, and a house so covered in big old-fashioned colored bulbs that it lights up
        /// the street. Pine green, cranberry red and gold everywhere else.
        /// </summary>
        static LookTheme ChristmasLights()
        {
            return new LookTheme
            {
                Id = "christmas-lights",
                Name = "Christmas Lights",
                Look = new SavedLook
                {
                    BackgroundColor = "#0F2A1C",            // pine green, under the picture
                    BackgroundImageTransparency = 0,
                    ButtonsColor = "#F2C14E",               // gold
                    VolumeColor = "#F2C14E",
                    IconTextColor = "#FFFFFF",              // snow white on the night sky
                    NotesChromeColor = "#F2C14E",
                    NotesBackColor = "#FBF3DF",             // cream, like a holiday card
                    DockBackgroundColor = "#8E1622",        // cranberry red
                    DockBackgroundImage = "",
                    SensorTextColor = "#F2C14E",
                    SensorBackColor = "#12301F",
                    SettingsTheme = SettingsThemeMode.Christmas
                },
                MakePicture = ChristmasPicture.Make
            };
        }
    }

    /// <summary>
    /// A theme as a file to share (".rltheme"): its name, its look, and its pictures packed inside as text. Instead of
    /// packing a picture, a file can also name one sitting right next to it (handy for a theme made by hand or by an AI).
    /// </summary>
    [System.Xml.Serialization.XmlRoot("ReadyLauncherTheme")]
    public class ThemeFile
    {
        public int Format = 1;
        public string Name = "";
        public SavedLook Look;
        public string BackgroundPictureName;   // e.g. "background.jpg"
        public string BackgroundPicture;       // the picture's bytes, base64
        public string StripPictureName;
        public string StripPicture;
    }

    /// <summary>Exporting a theme to a file, and importing one safely: only colors, settings and real pictures get in.</summary>
    internal static class ThemeFiles
    {
        public const string Extension = ".rltheme";
        public const string Filter = "Ready Launcher themes (*.rltheme)|*.rltheme";
        /// <summary>For importing: also theme files saved before the rename (".wltheme").</summary>
        public const string ImportFilter = "Ready Launcher themes (*.rltheme;*.wltheme)|*.rltheme;*.wltheme";
        const string OldRoot = "WinLauncherTheme";
        static System.Xml.Serialization.XmlSerializer oldNameReader;
        const long MaxFileBytes = 60L * 1024 * 1024, MaxPictureBytes = 30L * 1024 * 1024;
        static readonly string[] PictureTypes = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".jfif" };
        static readonly System.Text.RegularExpressions.Regex ColorText = new System.Text.RegularExpressions.Regex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");

        public static void Export(string name, SavedLook look, string file)
        {
            var t = new ThemeFile { Name = name ?? "", Look = Themes.Copy(look) };
            t.Look.BackgroundImage = "";
            t.Look.DockBackgroundImage = "";
            Pack(look.BackgroundImage, "background", out t.BackgroundPicture, out t.BackgroundPictureName);
            Pack(look.DockBackgroundImage, "strip", out t.StripPicture, out t.StripPictureName);
            string tmp = file + ".tmp";
            using (var fs = File.Create(tmp)) new System.Xml.Serialization.XmlSerializer(typeof(ThemeFile)).Serialize(fs, t);
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        static void Pack(string path, string what, out string data, out string name)
        {
            data = null;
            name = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            var info = new FileInfo(path);
            if (info.Length > MaxPictureBytes) throw new Exception("its " + what + " picture is over 30 MB, too big to share.");
            data = Convert.ToBase64String(File.ReadAllBytes(path));
            name = what + info.Extension.ToLowerInvariant();
        }

        /// <summary>Adds the theme in <paramref name="file"/> to your themes (not saved yet) and returns it.</summary>
        public static UserTheme Import(string file, AppSettings s)
        {
            var info = new FileInfo(file);
            if (!info.Exists) throw new Exception("the file isn't there any more.");
            if (info.Length > MaxFileBytes) throw new Exception("the file is too big to be a theme.");
            ThemeFile t;
            try
            {
                var rs = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
                using (var r = System.Xml.XmlReader.Create(file, rs))
                {
                    r.MoveToContent();
                    System.Xml.Serialization.XmlSerializer ser;
                    if (r.LocalName == OldRoot)
                    {
                        if (oldNameReader == null)
                            oldNameReader = new System.Xml.Serialization.XmlSerializer(typeof(ThemeFile), new System.Xml.Serialization.XmlRootAttribute(OldRoot));
                        ser = oldNameReader;
                    }
                    else ser = new System.Xml.Serialization.XmlSerializer(typeof(ThemeFile));
                    t = (ThemeFile)ser.Deserialize(r);
                }
            }
            catch (Exception) { throw new Exception("it isn't a Ready Launcher theme, or it's damaged."); }
            if (t == null || t.Look == null) throw new Exception("it doesn't describe a look.");

            var look = Clean(t.Look);
            string id = Themes.NewId(), folder = info.DirectoryName;
            look.BackgroundImage = Unpack(t.BackgroundPicture, t.BackgroundPictureName, folder, id, "background");
            look.DockBackgroundImage = Unpack(t.StripPicture, t.StripPictureName, folder, id, "strip");
            var u = new UserTheme { Id = id, Name = UniqueName(s, CleanName(t.Name, Path.GetFileNameWithoutExtension(file))), Look = look };
            s.MyThemes.Add(u);
            return u;
        }

        static string Unpack(string data, string name, string folder, string id, string what)
        {
            byte[] bytes = null;
            if (!string.IsNullOrWhiteSpace(data))
            {
                try { bytes = Convert.FromBase64String(data.Trim()); }
                catch (FormatException) { throw new Exception("its " + what + " picture is damaged."); }
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                // A picture right next to the theme file (never anywhere else).
                string leaf;
                try { leaf = Path.GetFileName(name.Trim()); } catch (ArgumentException) { leaf = null; }
                string p = string.IsNullOrEmpty(leaf) ? null : Path.Combine(folder, leaf);
                if (p != null && File.Exists(p) && new FileInfo(p).Length <= MaxPictureBytes) bytes = File.ReadAllBytes(p);
            }
            if (bytes == null) return "";
            if (bytes.Length > MaxPictureBytes) throw new Exception("its " + what + " picture is over 30 MB.");
            if (!IsPicture(bytes)) throw new Exception("its " + what + " picture isn't a picture this PC can show.");
            Directory.CreateDirectory(Themes.PictureFolder);
            string ext = string.IsNullOrEmpty(name) ? "" : SafeExtension(name);
            string path = Path.Combine(Themes.PictureFolder, id + "-" + what + (ext.Length > 0 ? ext : ".png"));
            File.WriteAllBytes(path, bytes);
            return path;
        }

        static string SafeExtension(string name)
        {
            try
            {
                string e = Path.GetExtension(name.Trim()).ToLowerInvariant();
                return Array.IndexOf(PictureTypes, e) >= 0 ? e : "";
            }
            catch (ArgumentException) { return ""; }
        }

        static bool IsPicture(byte[] b)
        {
            try
            {
                using (var ms = new MemoryStream(b))
                {
                    var d = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    return d.Frames.Count > 0 && d.Frames[0].PixelWidth > 0 && d.Frames[0].PixelHeight > 0;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>Only well-formed colors and sensible numbers: anything else falls back to automatic or the usual value.</summary>
        static SavedLook Clean(SavedLook a)
        {
            var l = Themes.Copy(a);
            l.BackgroundColor = IsColor(l.BackgroundColor) ? l.BackgroundColor.Trim() : "#1F4E79";
            l.ButtonsColor = ColorOrAuto(l.ButtonsColor);
            l.VolumeColor = ColorOrAuto(l.VolumeColor);
            l.IconTextColor = ColorOrAuto(l.IconTextColor);
            l.NotesChromeColor = ColorOrAuto(l.NotesChromeColor);
            l.AllPartsColor = ColorOrAuto(l.AllPartsColor);
            l.NotesBackColor = ColorOrAuto(l.NotesBackColor);
            l.DockBackgroundColor = ColorOrAuto(l.DockBackgroundColor);
            l.SensorTextColor = ColorOrAuto(l.SensorTextColor);
            l.SensorBackColor = ColorOrAuto(l.SensorBackColor);
            l.BackgroundImageTransparency = Clamp(l.BackgroundImageTransparency, 0, 100);
            l.DockImageTransparency = Clamp(l.DockImageTransparency, 0, 100);
            l.WindowTransparency = Clamp(l.WindowTransparency, 0, 95);
            l.DockTransparency = Clamp(l.DockTransparency, 0, 100);
            l.AnimationSpeed = l.AnimationSpeed == 0 ? 100 : Clamp(l.AnimationSpeed, 25, 300);
            l.BackgroundImage = "";
            l.DockBackgroundImage = "";
            return l;
        }

        static bool IsColor(string c) { return c != null && ColorText.IsMatch(c.Trim()); }
        static string ColorOrAuto(string c) { return IsColor(c) ? c.Trim() : ""; }
        static int Clamp(int v, int lo, int hi) { return Math.Max(lo, Math.Min(hi, v)); }

        static string CleanName(string name, string fallback)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (name ?? "").Trim()) if (!char.IsControl(ch)) sb.Append(ch);
            string n = sb.ToString().Trim();
            if (n.Length == 0) n = (fallback ?? "").Trim();
            if (n.Length == 0) n = "Imported theme";
            return n.Length > 60 ? n.Substring(0, 60).Trim() : n;
        }

        /// <summary>"Name", or "Name (2)" and so on if that name is taken already.</summary>
        public static string UniqueName(AppSettings s, string name)
        {
            Func<string, bool> taken = n =>
            {
                foreach (var t in Themes.All) if (string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase)) return true;
                foreach (var u in s.MyThemes) if (string.Equals(u.Name, n, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            };
            if (!taken(name)) return name;
            for (int k = 2; ; k++)
                if (!taken(name + " (" + k + ")")) return name + " (" + k + ")";
        }

        /// <summary>A theme's name as a file name (characters Windows doesn't allow in names become spaces).</summary>
        public static string FileNameFor(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name ?? "") sb.Append(Array.IndexOf(bad, ch) >= 0 ? ' ' : ch);
            string n = sb.ToString().Trim();
            return (n.Length == 0 ? "theme" : n) + Extension;
        }
    }

    /// <summary>The Christmas Lights picture, drawn once and saved as a PNG.</summary>
    internal static class ChristmasPicture
    {
        public const string FileName = "christmas-lights-1.png";
        const int W = 1920, H = 1200;
        static readonly Color[] BulbColors =
        {
            Color.FromRgb(0xFF, 0x2B, 0x2B), Color.FromRgb(0x2B, 0xD4, 0x5B), Color.FromRgb(0x2E, 0x6B, 0xFF),
            Color.FromRgb(0xFF, 0xA2, 0x1F), Color.FromRgb(0xFF, 0xE4, 0x5C)
        };
        static readonly Color WarmWhite = Color.FromRgb(0xFF, 0xF1, 0xC2);
        static readonly Dictionary<Color, Brush> glows = new Dictionary<Color, Brush>(), bulbs = new Dictionary<Color, Brush>();
        static int colorTurn;

        public static string Make(string folder)
        {
            string path = Path.Combine(folder, FileName);
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(folder);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) Draw(dc);
            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp)) enc.Save(fs);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        static void Draw(DrawingContext dc)
        {
            var rnd = new Random(1225);
            colorTurn = 0;

            // the night sky, and a moon
            dc.DrawRectangle(Linear(90, "#050A1C", 0, "#0C1C3C", 0.45, "#1A3460", 0.7), null, new Rect(0, 0, W, H));
            dc.DrawEllipse(Radial(Color.FromArgb(70, 220, 230, 255), Color.FromArgb(0, 220, 230, 255)), null, new Point(1660, 180), 230, 230);
            dc.DrawEllipse(Radial(Color.FromRgb(0xFF, 0xFB, 0xEA), Color.FromRgb(0xE4, 0xDD, 0xC2)), null, new Point(1660, 180), 46, 46);
            var crater = Frozen(new SolidColorBrush(Color.FromArgb(22, 60, 60, 80)));
            dc.DrawEllipse(crater, null, new Point(1648, 168), 10, 8);
            dc.DrawEllipse(crater, null, new Point(1676, 196), 7, 6);
            dc.DrawEllipse(crater, null, new Point(1670, 160), 4, 4);
            for (int i = 0; i < 280; i++)
            {
                double x = rnd.NextDouble() * W, y = rnd.NextDouble() * 700;
                if (Math.Abs(x - 1660) < 120 && Math.Abs(y - 180) < 120) continue;
                double r = 0.5 + Math.Pow(rnd.NextDouble(), 3) * 1.6;
                dc.DrawEllipse(Frozen(new SolidColorBrush(Color.FromArgb((byte)(90 + rnd.Next(160)), 255, 255, 255))), null, new Point(x, y), r, r);
            }

            // the street, far off: rolling hills and a line of pines
            var hills = Shape(new Point(0, 800), new Point(260, 760), new Point(560, 790), new Point(900, 770), new Point(1240, 780), new Point(1600, 755), new Point(W, 785), new Point(W, 880), new Point(0, 880));
            dc.DrawGeometry(Hex("#0B1730"), null, hills);
            var pine = Hex("#08122A");
            for (int x = -20; x < W + 40; x += 24 + rnd.Next(30))
            {
                double h = 26 + rnd.Next(48), baseY = 790 + Math.Sin(x / 210.0) * 12;
                dc.DrawGeometry(pine, null, Shape(new Point(x - h * 0.32, baseY), new Point(x, baseY - h), new Point(x + h * 0.32, baseY)));
            }
            // a neighbor's house in the distance, with a modest string of lights
            dc.DrawRectangle(Hex("#121A2C"), null, new Rect(200, 742, 200, 70));
            dc.DrawGeometry(Hex("#161F33"), null, Shape(new Point(186, 746), new Point(300, 690), new Point(414, 746)));
            dc.DrawGeometry(Hex("#DCE6F4"), null, Shape(new Point(184, 744), new Point(300, 686), new Point(416, 744), new Point(408, 748), new Point(300, 694), new Point(192, 748)));
            Window(dc, new Rect(230, 765, 34, 28), 0.6);
            Window(dc, new Rect(336, 765, 34, 28), 0.6);
            Swags(dc, new Point(192, 752), new Point(408, 752), 5, 4, 0.55);

            // the house glows so brightly it lights the snow and the sky around it
            dc.DrawEllipse(Radial(Color.FromArgb(46, 255, 200, 130), Color.FromArgb(0, 255, 200, 130)), null, new Point(1300, 700), 760, 460);

            // snow on the ground
            var ground = Shape(new Point(0, 870), new Point(420, 855), new Point(860, 880), new Point(1300, 868), new Point(1700, 878), new Point(W, 860), new Point(W, H), new Point(0, H));
            dc.DrawGeometry(Linear(90, "#B7C7DE", 0, "#8FA3C2", 0.5, "#6E82A4", 1), null, ground);
            foreach (var c in new[] { BulbColors[0], BulbColors[1], BulbColors[2], BulbColors[3], WarmWhite })
                dc.DrawEllipse(Radial(Color.FromArgb(46, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)), null,
                    new Point(1000 + rnd.Next(640), 930 + rnd.Next(60)), 260, 70);

            // the garage
            dc.DrawRectangle(Linear(90, "#151D2D", 0, "#0F1626", 1), null, new Rect(860, 700, 210, 200));
            dc.DrawGeometry(Hex("#1B2233"), null, Shape(new Point(840, 706), new Point(965, 636), new Point(1090, 706)));
            dc.DrawGeometry(Hex("#E8F0FA"), null, Shape(new Point(836, 704), new Point(965, 630), new Point(1094, 704), new Point(1082, 710), new Point(965, 644), new Point(848, 710)));
            dc.DrawRectangle(Hex("#1E2638"), null, new Rect(885, 770, 160, 130));
            var panel = new Pen(Hex("#121827"), 2);
            for (int k = 1; k < 4; k++) dc.DrawLine(panel, new Point(885, 770 + k * 32), new Point(1045, 770 + k * 32));

            // the house
            dc.DrawRectangle(Linear(90, "#171F30", 0, "#10172A", 1), null, new Rect(1060, 610, 560, 290));
            var siding = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(16, 255, 255, 255))), 1);
            for (double y = 622; y < 900; y += 12) dc.DrawLine(siding, new Point(1062, y), new Point(1618, y));
            dc.DrawRectangle(Hex("#2A1B1A"), null, new Rect(1462, 452, 48, 100));   // chimney
            dc.DrawGeometry(Hex("#1B2233"), null, Shape(new Point(1020, 616), new Point(1340, 400), new Point(1660, 616)));
            dc.DrawRectangle(Hex("#E8F0FA"), null, new Rect(1458, 444, 56, 12));
            dc.DrawGeometry(Hex("#E8F0FA"), null, Shape(new Point(1012, 614), new Point(1340, 392), new Point(1668, 614), new Point(1652, 624), new Point(1340, 412), new Point(1028, 624)));
            // windows, the door and a wreath
            foreach (var r in new[] { new Rect(1100, 735, 72, 92), new Rect(1200, 735, 72, 92), new Rect(1410, 735, 72, 92), new Rect(1510, 735, 72, 92),
                                      new Rect(1130, 640, 66, 52), new Rect(1484, 640, 66, 52), new Rect(1312, 470, 56, 66) })
                Window(dc, r, 1);
            dc.DrawRectangle(Linear(90, "#7A1820", 0, "#5A1016", 1), null, new Rect(1308, 760, 64, 140));
            dc.DrawEllipse(null, new Pen(Hex("#1F5A30"), 9), new Point(1340, 800), 17, 17);
            dc.DrawGeometry(Hex("#D8202E"), null, Shape(new Point(1332, 812), new Point(1340, 818), new Point(1348, 812), new Point(1352, 830), new Point(1340, 820), new Point(1328, 830)));

            // the lights: every edge, three rows deep on the roof, around every window and door, down every corner
            for (int row = 0; row < 3; row++)
            {
                double d = 18 + row * 16;
                Swags(dc, new Point(1030, 618 + d * 0.35), new Point(1340, 404 + d), 7, 6, 1);
                Swags(dc, new Point(1340, 404 + d), new Point(1650, 618 + d * 0.35), 7, 6, 1);
            }
            for (int k = 0; k < 4; k++)
            {
                double y = 470 + k * 38, half = (y - 400) / 216 * 300;
                Swags(dc, new Point(1340 - half + 10, y), new Point(1340 + half - 10, y), Math.Max(2, (int)(half / 45)), 5, 1);
            }
            Swags(dc, new Point(1060, 628), new Point(1620, 628), 12, 7, 1);
            Line(dc, new Point(1066, 632), new Point(1066, 895), 15);
            Line(dc, new Point(1614, 632), new Point(1614, 895), 15);
            foreach (var r in new[] { new Rect(1100, 735, 72, 92), new Rect(1200, 735, 72, 92), new Rect(1410, 735, 72, 92), new Rect(1510, 735, 72, 92),
                                      new Rect(1130, 640, 66, 52), new Rect(1484, 640, 66, 52), new Rect(1308, 760, 64, 140) })
                Outline(dc, new Rect(r.X - 6, r.Y - 6, r.Width + 12, r.Height + 12), 13);
            Outline(dc, new Rect(1456, 450, 60, 104), 14);
            Swags(dc, new Point(848, 714), new Point(965, 650), 3, 5, 1);
            Swags(dc, new Point(965, 650), new Point(1082, 714), 3, 5, 1);
            Swags(dc, new Point(864, 712), new Point(1060, 712), 4, 6, 1);
            Outline(dc, new Rect(879, 764, 172, 136), 14);

            // bushes along the front, wrapped in small lights
            var bush = Hex("#123020");
            foreach (var x in new[] { 1105, 1200, 1290, 1400, 1500, 1580 })
            {
                dc.DrawEllipse(bush, null, new Point(x, 892), 44, 26);
                dc.DrawEllipse(Hex("#E8F0FA"), null, new Point(x, 870), 30, 6);
                for (int k = 0; k < 14; k++)
                    Bulb(dc, new Point(x - 36 + rnd.NextDouble() * 72, 876 + rnd.NextDouble() * 30), NextColor(), 0.55);
            }

            // a big tree in the yard, and a small one by the drive
            Tree(dc, rnd, 700, 905, 340, 120);
            Tree(dc, rnd, 1770, 905, 200, 70);

            // the fence along the front, lit from end to end
            var post = Hex("#E2E8F2");
            for (int x = 600; x <= 1860; x += 90) dc.DrawRectangle(post, null, new Rect(x - 4, 960, 8, 54));
            dc.DrawRectangle(post, null, new Rect(596, 972, 1268, 6));
            Swags(dc, new Point(600, 968), new Point(1860, 968), 14, 9, 1);

            // falling snow, in front of it all
            for (int i = 0; i < 460; i++)
            {
                double r = 0.8 + Math.Pow(rnd.NextDouble(), 2.2) * 3.4;
                var p = new Point(rnd.NextDouble() * W, rnd.NextDouble() * H);
                byte a = (byte)(110 + rnd.Next(130));
                if (r > 2.4) dc.DrawEllipse(Radial(Color.FromArgb(a, 255, 255, 255), Color.FromArgb(0, 255, 255, 255)), null, p, r * 1.6, r * 1.6);
                else dc.DrawEllipse(Frozen(new SolidColorBrush(Color.FromArgb(a, 255, 255, 255))), null, p, r, r);
            }
        }

        // ---------- pieces ----------

        /// <summary>A lit window: a warm glow out onto the snow, the light inside, and the frame across it.</summary>
        static void Window(DrawingContext dc, Rect r, double strength)
        {
            var c = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            dc.DrawEllipse(Radial(Color.FromArgb((byte)(80 * strength), 255, 190, 90), Color.FromArgb(0, 255, 190, 90)), null, c, r.Width * 1.4, r.Height * 1.3);
            dc.DrawRectangle(Linear(90, "#FFE09A", 0, "#FFB347", 1), null, r);
            var frame = new Pen(Hex("#3A2A18"), 3);
            dc.DrawRectangle(null, frame, r);
            dc.DrawLine(frame, new Point(c.X, r.Y), new Point(c.X, r.Bottom));
            dc.DrawLine(frame, new Point(r.X, c.Y), new Point(r.Right, c.Y));
        }

        /// <summary>A string of lights draped from <paramref name="a"/> to <paramref name="b"/> in a few loose swags.</summary>
        static void Swags(DrawingContext dc, Point a, Point b, int swags, double sag, double size)
        {
            var wire = new Pen(Hex("#0D1912"), 1.4);
            for (int s = 0; s < swags; s++)
            {
                var p0 = new Point(a.X + (b.X - a.X) * s / swags, a.Y + (b.Y - a.Y) * s / swags);
                var p2 = new Point(a.X + (b.X - a.X) * (s + 1) / swags, a.Y + (b.Y - a.Y) * (s + 1) / swags);
                var p1 = new Point((p0.X + p2.X) / 2, (p0.Y + p2.Y) / 2 + sag * 2);
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(p0, false, false); c.QuadraticBezierTo(p1, p2, true, false); }
                g.Freeze();
                dc.DrawGeometry(null, wire, g);
                double len = (p2 - p0).Length;
                int n = Math.Max(2, (int)(len / (16 * size)));
                for (int k = 0; k < n; k++)
                {
                    double t = (k + 0.5) / n, u = 1 - t;
                    var p = new Point(u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X, u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y);
                    Bulb(dc, p, NextColor(), size);
                }
            }
        }

        /// <summary>A straight run of lights (down a corner).</summary>
        static void Line(DrawingContext dc, Point a, Point b, double spacing)
        {
            dc.DrawLine(new Pen(Hex("#0D1912"), 1.4), a, b);
            int n = Math.Max(2, (int)((b - a).Length / spacing));
            for (int k = 0; k <= n; k++) Bulb(dc, new Point(a.X + (b.X - a.X) * k / n, a.Y + (b.Y - a.Y) * k / n), NextColor(), 0.9);
        }

        static void Outline(DrawingContext dc, Rect r, double spacing)
        {
            Line(dc, r.TopLeft, r.TopRight, spacing);
            Line(dc, r.TopRight, r.BottomRight, spacing);
            Line(dc, r.BottomRight, r.BottomLeft, spacing);
            Line(dc, r.BottomLeft, r.TopLeft, spacing);
        }

        /// <summary>An evergreen with snow on its branches, lights spiraling around it, and a star on top.</summary>
        static void Tree(DrawingContext dc, Random rnd, double cx, double baseY, double h, double halfW)
        {
            double top = baseY - h;
            dc.DrawRectangle(Hex("#2A1A12"), null, new Rect(cx - 8, baseY - 18, 16, 22));
            for (int tier = 0; tier < 4; tier++)
            {
                double y0 = top + h * tier / 4.6, y1 = top + h * (tier + 1.6) / 4.6, w1 = halfW * (tier + 1.6) / 4.6;
                dc.DrawGeometry(Hex(tier % 2 == 0 ? "#0F2E1C" : "#123622"), null, Shape(new Point(cx, y0), new Point(cx + w1, y1), new Point(cx - w1, y1)));
                dc.DrawGeometry(Hex("#E8F0FA"), null, Shape(new Point(cx - w1, y1), new Point(cx - w1 * 0.4, y1 - 6), new Point(cx + w1 * 0.4, y1 - 6), new Point(cx + w1, y1), new Point(cx, y1 - 3)));
            }
            for (double t = 0.04; t < 1; t += 0.012)
            {
                double y = top + h * t * 0.95, w = halfW * t * 1.02, ang = t * Math.PI * 2 * 5;
                if (Math.Cos(ang) < -0.15) continue;   // the back of the tree
                Bulb(dc, new Point(cx + w * Math.Sin(ang), y + 4), NextColor(), h > 250 ? 0.75 : 0.6);
            }
            var gold = Color.FromRgb(0xFF, 0xD7, 0x5A);
            dc.DrawEllipse(Radial(Color.FromArgb(150, gold.R, gold.G, gold.B), Color.FromArgb(0, gold.R, gold.G, gold.B)), null, new Point(cx, top - 4), 34, 34);
            dc.DrawGeometry(Frozen(new SolidColorBrush(gold)), null, Star(cx, top - 4, 13, 5.5));
        }

        static Geometry Star(double cx, double cy, double outer, double inner)
        {
            var pts = new List<Point>();
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, r = i % 2 == 0 ? outer : inner;
                pts.Add(new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a)));
            }
            return Shape(pts.ToArray());
        }

        /// <summary>One big old-fashioned bulb hanging from the wire, with its glow.</summary>
        static void Bulb(DrawingContext dc, Point p, Color c, double size)
        {
            Brush g, b;
            if (!glows.TryGetValue(c, out g))
            {
                var gr = new RadialGradientBrush();
                gr.GradientStops.Add(new GradientStop(Color.FromArgb(150, c.R, c.G, c.B), 0));
                gr.GradientStops.Add(new GradientStop(Color.FromArgb(46, c.R, c.G, c.B), 0.4));
                gr.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
                glows[c] = g = Frozen(gr);
                var br = new RadialGradientBrush { GradientOrigin = new Point(0.4, 0.3) };
                br.GradientStops.Add(new GradientStop(Mix(c, Colors.White, 0.7), 0));
                br.GradientStops.Add(new GradientStop(c, 0.5));
                br.GradientStops.Add(new GradientStop(Mix(c, Colors.Black, 0.35), 1));
                bulbs[c] = b = Frozen(br);
            }
            else b = bulbs[c];
            dc.DrawEllipse(g, null, new Point(p.X, p.Y + 3 * size), 11 * size, 11 * size);
            dc.DrawRectangle(Hex("#1C2A20"), null, new Rect(p.X - 1.3 * size, p.Y - 0.5, 2.6 * size, 2.4 * size));
            dc.DrawEllipse(b, null, new Point(p.X, p.Y + 4.6 * size), 2.8 * size, 4 * size);
        }

        static Color NextColor()
        {
            colorTurn++;
            return BulbColors[colorTurn % BulbColors.Length];
        }

        // ---------- brushes and shapes ----------

        static Geometry Shape(params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i], false, false);
            }
            g.Freeze();
            return g;
        }

        /// <summary>A gradient at an angle (90 = top to bottom), as color, offset, color, offset...</summary>
        static Brush Linear(double angle, params object[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = angle == 90 ? new Point(0, 1) : new Point(1, 0) };
            for (int i = 0; i + 1 < stops.Length; i += 2)
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString((string)stops[i]), Convert.ToDouble(stops[i + 1])));
            return Frozen(b);
        }

        static Brush Radial(Color inner, Color outer)
        {
            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(inner, 0));
            b.GradientStops.Add(new GradientStop(outer, 1));
            return Frozen(b);
        }

        static Brush Hex(string h) { return Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(h))); }
        static Brush Frozen(Brush b) { b.Freeze(); return b; }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
