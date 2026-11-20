using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Xml.Serialization;

namespace ReadyLauncher
{
    public enum ItemKind { App, Link, Folder, Keys }
    public enum ArrangeMode { Free, Grid, Auto, FreeAlign }   // FreeAlign: free placement that lines icons up with nearby ones
    public enum DockSource { Frequent, RecentlyInstalled, Hidden }
    public enum DockPosition { Bottom, Top, Left, Right }
    public enum LampPanelPlace { Off, Top, Bottom, AboveLauncher, BelowLauncher, UnderStrip, StripBackground }   // the blinking lamp strip: saved by name; Settings lists them in this order
    public enum LampActivity { Idle, Computing, Alert }
    public enum DockAlignment { Start, Center, End }
    public enum VolumePlacement { Off, InsideLeft, InsideRight, OutsideLeft, OutsideRight }
    public enum IconSizeMode { ExtraSmall, Small, Medium, Large, Tiny }   // Tiny was added later, so it comes last here
    public enum WindowSizeMode { Small, Medium, Large, Custom }
    public enum OpenAnimation { None, Fade, SlideUp, SlideDown, Zoom, Pop, Blur, Static, Flip, Rain, Beam, Glass, Random }   // Random: a different one each time it opens
    public enum NotesPlacement { Off, Left, Right }
    public enum DecorKind { Line, Text }
    public enum VolumeSizeMode { Small, Medium, Large, ExtraLarge }
    public enum VolumeStyleMode { Slider, Buttons10, Buttons20, ButtonsCustom }
    public enum VolumeButtonSize { Small, Medium, Large }
    public enum StripIconMode { MatchLauncher, ExtraSmall, Small, Medium, Large, ExtraLarge, Tiny }
    public enum SettingsThemeMode { FollowSystem, Light, LightGrey, Grey, DarkGrey, Dark, Christmas }   // saved by name, so the order can change

    /// <summary>A divider line or a piece of text placed on the launcher to help organize icons.</summary>
    public class Decoration
    {
        public string Id;
        public DecorKind Kind;
        public double X;              // where the line / text starts, in launcher (canvas) coordinates
        public double Y;
        // Line
        public bool Vertical;
        public double Length = 240;
        public double Thickness = 2;
        // Text
        public string Text = "";
        public string Font = "Segoe UI";
        public double FontSize = 20;
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public int Shadow;                    // 0 none, 1 small, 2 medium, 3 large
        public string ShadowColor = "#000000";
        public int Outline;                   // 0 none, 1 thin, 2 thick
        public string OutlineColor = "#000000";
        public LauncherItem Link;             // what clicking the text opens (web address, command or keys); null = plain text
        // Both
        public string Color = "";     // empty = follow the launcher's text color

        public Decoration() { Id = Guid.NewGuid().ToString("N"); }

        public Decoration Clone() { return (Decoration)MemberwiseClone(); }

        public void CopyFrom(Decoration o)
        {
            Kind = o.Kind; X = o.X; Y = o.Y;
            Vertical = o.Vertical; Length = o.Length; Thickness = o.Thickness;
            Text = o.Text; Font = o.Font; FontSize = o.FontSize; Bold = o.Bold; Italic = o.Italic;
            Underline = o.Underline; Shadow = o.Shadow; ShadowColor = o.ShadowColor;
            Outline = o.Outline; OutlineColor = o.OutlineColor; Link = o.Link;
            Color = o.Color;
        }
    }

    public class LauncherItem
    {
        public string Id;
        public ItemKind Kind;
        public string Name;
        public string Path;
        public string Arguments;
        public double X;
        public double Y;
        public DateTime DateAdded;
        public int LaunchCount;
        public DateTime LastLaunched;
        public ModifierKeys KeyMods;   // Kind == Keys: the key combination to send
        public Key KeyKey;
        public string IconPath = "";   // custom icon: a picture, or an .ico/.dll/.exe with IconIndex; empty = the app's own icon
        public int IconIndex;
        public List<LauncherItem> Children = new List<LauncherItem>();

        public LauncherItem()
        {
            Id = Guid.NewGuid().ToString("N");
            DateAdded = DateTime.Now;
        }

        [XmlIgnore]
        public bool IsFolder { get { return Kind == ItemKind.Folder; } }

        public LauncherItem CloneShortcut()
        {
            var c = new LauncherItem();
            c.Kind = Kind; c.Name = Name; c.Path = Path; c.Arguments = Arguments;
            c.KeyMods = KeyMods; c.KeyKey = KeyKey;
            c.IconPath = IconPath; c.IconIndex = IconIndex;
            return c;
        }
    }

    /// <summary>A HWiNFO / Libre Hardware Monitor sensor found earlier, so it can still be listed while that app is closed.</summary>
    public class KnownSensor
    {
        public string Id, Group, Name, Label, Source;
    }

    /// <summary>
    /// Your own warning levels for one sensor, in the unit the levels window shows (%, °C, ms, MB/s...). Only sensors
    /// whose levels differ from the built-in ones are saved. NaN: no level there (both NaN: never colored).
    /// </summary>
    public class SensorAlert
    {
        public string Id = "";
        public double Warning = double.NaN, Critical = double.NaN;
        public bool LowIsBad;   // lower is worse (battery charge, Wi-Fi signal...)
    }

    /// <summary>
    /// Your own look (the colors and pictures a theme changes), kept while a theme is on so switching the theme off puts
    /// everything back exactly as it was.
    /// </summary>
    public class SavedLook
    {
        public string BackgroundColor = "", BackgroundImage = "";
        public bool BackgroundImageOff;
        public int BackgroundImageTransparency;
        public string ButtonsColor = "", VolumeColor = "", IconTextColor = "", NotesChromeColor = "", AllPartsColor = "";
        public bool SameColorForAll;
        public string NotesBackColor = "", DockBackgroundColor = "", DockBackgroundImage = "";
        public bool DockImageOff;
        public int DockImageTransparency;
        public string SensorTextColor = "", SensorBackColor = "";
        public SettingsThemeMode SettingsTheme;

        // A whole look (your own, or a theme you saved) also has these; a built-in theme leaves them as you have them.
        public bool Full;
        public int WindowTransparency, DockTransparency;
        public OpenAnimation Animation;
        public int AnimationSpeed = 100;
        public bool AnimateNotes, AnimateSensors, AnimateDock, AnimateVolume;
        public LampPanelPlace LampPanel;
        public LampActivity LampMode = LampActivity.Computing;
    }

    /// <summary>A theme you saved from your own look (Settings > Appearance > Save as theme).</summary>
    public class UserTheme
    {
        public string Id = "", Name = "";
        public SavedLook Look;
    }

    public class UsageEntry
    {
        public string Path;
        public string Name;
        public int Count;
        public DateTime Last;
    }

    public class AppSettings
    {
        public ModifierKeys HotkeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
        public Key HotkeyKey = Key.L;
        public ArrangeMode Arrange = ArrangeMode.Free;
        public DockSource Dock = DockSource.Frequent;
        public DockPosition DockPos = DockPosition.Bottom;
        public VolumePlacement Volume = VolumePlacement.Off;
        public string BackgroundColor = "#B8A600";
        public bool HideAfterLaunch = true;
        public bool HideOnDeactivate = false;
        public bool ShowLabels = true;
        public double WinLeft = double.NaN;
        public double WinTop = double.NaN;
        public double WinWidth = 928;   // the "medium" 900 x 560 panel plus its 14px shadow margin
        public double WinHeight = 588;

        // Window
        public WindowSizeMode WindowSize = WindowSizeMode.Medium;
        public int CustomWidthPx = 1000;
        public int CustomHeightPx = 640;
        public OpenAnimation Animation = OpenAnimation.None;
        public int AnimationSpeed = 100;          // percent: 50 = half as fast, 200 = twice as fast
        public bool AnimateNotes, AnimateSensors, AnimateDock, AnimateVolume;   // the windows around the launcher animate too
        public bool LayoutLocked;                 // icons, lines and text can't be moved (the lock at the top left)
        public bool OpenWithDoubleClick;          // icons open with a double-click instead of a single click
        public IconSizeMode IconSize = IconSizeMode.Medium;
        public int WindowTransparency;            // percent, background only (icons stay solid)
        public string BackgroundImage = "";
        public int BackgroundImageTransparency;   // percent
        public bool BackgroundImageOff;           // the picture is switched off (but kept, to switch back on)
        // Colors of parts of the launcher ("" = automatic: readable on what's behind them)
        public string ButtonsColor = "";          // the buttons at the top, and the quick launch strip's round button
        public string VolumeColor = "";           // the volume control's text, mute button and slider
        public string IconTextColor = "";         // the names under icons
        public string NotesChromeColor = "";      // the notes window's title, tabs and buttons
        public bool SameColorForAll;              // one color (AllPartsColor) for all four
        public string AllPartsColor = "";
        public LampPanelPlace LampPanel = LampPanelPlace.Off;   // a strip of blinking computer lamps along the top or bottom
        public LampActivity LampMode = LampActivity.Computing;

        // Quick launch strip
        public int DockCount = 12;
        public DockAlignment DockAlign = DockAlignment.Start;
        public bool DockDivider = true;
        public string DockBackgroundColor = "";   // empty = no color
        public string DockBackgroundImage = "";
        public int DockImageTransparency;         // percent
        public bool DockImageOff;                 // the strip picture is switched off (but kept)
        public int DockTransparency;              // percent, whole strip background
        public bool DockDetached;                 // strip floats in its own window beside the launcher
        public int DockGap = 8;                   // pixels between the launcher and a detached strip

        // Volume control
        public int VolumeGap = 4;                 // pixels between the launcher and an outside volume control
        public VolumeSizeMode VolumeSize = VolumeSizeMode.Medium;
        public VolumeStyleMode VolumeStyle = VolumeStyleMode.Slider;   // a slider, or a column of buttons that each set one volume
        public bool VolumeSliderToo;              // buttons: show the slider beside them as well
        public VolumeButtonSize VolumeButtonHeight = VolumeButtonSize.Small;   // how tall each volume button is
        public bool VolumeCloseAfter;             // close the launcher after a volume button is clicked or the slider is moved
        public List<int> VolumeButtons10 = new List<int>();   // your own volume for each of the 10 buttons, top to bottom (empty: 100, 90 ... 10)
        public List<int> VolumeButtons20 = new List<int>();   // the same for 20 buttons (empty: 100, 95 ... 5)
        public int VolumeButtonCount = 5;         // the Custom style: how many volume buttons, 1 to 20
        public List<int> VolumeButtonsCustom = new List<int>();   // your own volume for each Custom button (empty, or made for another count: spread evenly)

        public const int MaxVolumeButtons = 20;

        /// <summary>
        /// The volume each of <paramref name="count"/> buttons sets out of the box, spread evenly from 100% down:
        /// 100, 90 ... 10; 100, 95 ... 5; or 100, 75, 50, 25.
        /// </summary>
        public static int[] DefaultVolumeSteps(int count)
        {
            count = Math.Max(1, Math.Min(MaxVolumeButtons, count));
            var a = new int[count];
            for (int i = 0; i < count; i++) a[i] = (int)Math.Round(100.0 * (count - i) / count, MidpointRounding.AwayFromZero);
            return a;
        }

        /// <summary>How many volume buttons the chosen style has.</summary>
        public int VolumeStepCount()
        {
            switch (VolumeStyle)
            {
                case VolumeStyleMode.Buttons20: return 20;
                case VolumeStyleMode.ButtonsCustom: return Math.Max(1, Math.Min(MaxVolumeButtons, VolumeButtonCount));
                default: return 10;
            }
        }

        /// <summary>
        /// Your own button volumes: 10 and 20 buttons keep their own (as the 10- and 20-button styles did), every other
        /// number shares one list.
        /// </summary>
        List<int> OwnVolumeSteps()
        {
            int count = VolumeStepCount();
            return count == 20 ? VolumeButtons20 : count == 10 ? VolumeButtons10 : VolumeButtonsCustom;
        }

        /// <summary>The volume each button of the chosen style sets, top to bottom: your own where you chose one.</summary>
        public int[] VolumeSteps()
        {
            int count = VolumeStepCount();
            var steps = DefaultVolumeSteps(count);
            var own = OwnVolumeSteps();
            if (own != null && own.Count == count)
                for (int i = 0; i < count; i++) if (own[i] >= 0 && own[i] <= 100) steps[i] = own[i];
            return steps;
        }

        /// <summary>Button <paramref name="index"/> (from the top) of the chosen style sets <paramref name="volume"/> from now on.</summary>
        public void SetVolumeStep(int index, int volume)
        {
            int count = VolumeStepCount();
            var steps = VolumeSteps();
            if (index < 0 || index >= count) return;
            steps[index] = Math.Max(0, Math.Min(100, volume));
            var own = OwnVolumeSteps();
            own.Clear();
            if (!steps.SequenceEqual(DefaultVolumeSteps(count))) own.AddRange(steps);   // saved only when it differs
        }

        // Quick launch strip icons (MatchLauncher follows the launcher's icon size)
        public StripIconMode StripIconSize = StripIconMode.MatchLauncher;

        // Apps
        public bool AutoAddNewApps;               // add a shortcut when an app is installed
        public bool AutoAddToFolder = true;       // ...into a "New apps" folder rather than loose on the launcher
        public int AddAppsGroup = 1;              // last choices in "Add all installed apps"
        public int AddAppsOrder;
        public bool AddAppsSections;              // labeled sections on the launcher instead of folders

        // Hardware monitoring (a bar of live readings above the launcher)
        public List<string> Sensors = new List<string>();   // ids from SensorCatalog, in display order
        public DockAlignment SensorAlign = DockAlignment.Center;
        public double SensorInterval = 5;         // seconds between readings
        public int SensorSize = 1;                // 0 small, 1 medium, 2 large
        public bool SensorFillBottom;             // fill the row next to the launcher first, extra rows go above it
        public bool SensorTiles;                  // tiles with mini graphs instead of compact label-and-value readings
        public int SensorGraphLength = 60;        // how many recent readings each tile's graph shows
        public bool SensorTilesCentered;          // the tiles' text centered (across, and up and down) instead of top left
        public bool SensorWarnings = true;        // readings turn amber / red past their warning / critical levels
        public List<SensorAlert> SensorAlerts = new List<SensorAlert>();   // levels changed from the built-in ones
        public List<string> SensorNoGraph = new List<string>();   // tiles whose graph is switched off (right-click a tile)
        public bool SensorsHidden;                // hidden with the sensors button in the header (the chosen sensors are kept)
        public string SensorTextColor = "";       // empty = automatic (readable on the background)
        public string SensorBackColor = "";       // empty = the launcher's background color
        public bool ExternalSensors = true;       // look for HWiNFO / Libre Hardware Monitor and offer their sensors
        public List<KnownSensor> KnownSensors = new List<KnownSensor>();   // their sensors seen before (kept while the app is closed)
        public string PingHost = "1.1.1.1";

        /// <summary>Launcher icon sizes in the order the settings list them (smallest first).</summary>
        public static readonly IconSizeMode[] IconSizeOrder = { IconSizeMode.Tiny, IconSizeMode.ExtraSmall, IconSizeMode.Small, IconSizeMode.Medium, IconSizeMode.Large };
        public static readonly StripIconMode[] StripSizeOrder = { StripIconMode.MatchLauncher, StripIconMode.Tiny, StripIconMode.ExtraSmall,
            StripIconMode.Small, StripIconMode.Medium, StripIconMode.Large, StripIconMode.ExtraLarge };

        // Icon area
        public bool ScrollEnabled = true;         // scroll bar + mouse wheel when icons don't fit

        // The Settings window's own look
        public SettingsThemeMode SettingsTheme = SettingsThemeMode.FollowSystem;

        // A ready-made look (an id from Themes.All, "" = none), and your own look, kept to go back to when it's switched off
        public string Theme = "";
        public SavedLook OwnLook;
        public List<UserTheme> MyThemes = new List<UserTheme>();   // themes you saved from your own look

        // Automatic backups (off unless chosen)
        public bool AutoBackup;
        public int AutoBackupDays = 7;            // save one every this many days
        public string AutoBackupFolder = "";      // empty = DefaultBackupFolder
        public DateTime LastAutoBackup;           // local time of the last automatic backup (MinValue = none yet)

        /// <summary>Documents\Ready Launcher backups (or the folder from before the rename, while it's the only one).</summary>
        public static string DefaultBackupFolder
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string now = System.IO.Path.Combine(docs, "Ready Launcher backups"), old = System.IO.Path.Combine(docs, "WinLauncher backups");
                return !Directory.Exists(now) && Directory.Exists(old) ? old : now;
            }
        }

        [XmlIgnore]
        public string BackupFolder { get { return string.IsNullOrWhiteSpace(AutoBackupFolder) ? DefaultBackupFolder : AutoBackupFolder.Trim(); } }

        // Notes window
        public NotesPlacement NotesPos = NotesPlacement.Off;
        public int NotesGap = 8;                  // pixels between the launcher and the notes window
        public int NotesWidth = 300;              // pixels
        public int NotesFontSize = 14;
        public string NotesBackColor = "";        // "" = the launcher's background color

        public AppSettings Clone() { return (AppSettings)MemberwiseClone(); }

        public static string HotkeyText(ModifierKeys mods, Key key)
        {
            var parts = new List<string>();
            if ((mods & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((mods & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((mods & ModifierKeys.Shift) != 0) parts.Add("Shift");
            if ((mods & ModifierKeys.Windows) != 0) parts.Add("Win");
            parts.Add(KeyName(key));
            return string.Join(" + ", parts);
        }

        public static string KeyName(Key k)
        {
            if (k >= Key.D0 && k <= Key.D9) return ((int)(k - Key.D0)).ToString();
            switch (k)
            {
                case Key.Return: return "Enter";
                case Key.Back: return "Backspace";
                case Key.Capital: return "Caps Lock";
                case Key.Next: return "Page Down";
                case Key.Prior: return "Page Up";
                case Key.Snapshot: return "Print Screen";
                case Key.OemPlus: return "=";
                case Key.OemMinus: return "-";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.Oem1: return ";";
                case Key.Oem2: return "/";
                case Key.Oem3: return "`";
                case Key.Oem4: return "[";
                case Key.Oem5: return "\\";
                case Key.Oem6: return "]";
                case Key.Oem7: return "'";
                case Key.VolumeMute: return "Mute";
                case Key.VolumeUp: return "Volume Up";
                case Key.VolumeDown: return "Volume Down";
                case Key.MediaPlayPause: return "Play/Pause";
                case Key.MediaNextTrack: return "Next Track";
                case Key.MediaPreviousTrack: return "Previous Track";
                default: return k.ToString();
            }
        }

        public static double VolumeScale(VolumeSizeMode m)
        {
            switch (m)
            {
                case VolumeSizeMode.Small: return 0.8;
                case VolumeSizeMode.Large: return 1.25;
                case VolumeSizeMode.ExtraLarge: return 1.5;
                default: return 1.0;
            }
        }

        public static double IconPixels(IconSizeMode m)
        {
            switch (m)
            {
                case IconSizeMode.Tiny: return 24;
                case IconSizeMode.ExtraSmall: return 32;
                case IconSizeMode.Small: return 42;
                case IconSizeMode.Large: return 66;
                default: return 52;
            }
        }
    }

    /// <summary>One tab of the notes window: its name and its formatted text, paragraph by paragraph.</summary>
    public class NoteTab
    {
        public string Name = "Notes";
        [XmlElement("P")] public List<NotePara> Paras = new List<NotePara>();
        /// <summary>Web addresses in this tab that were turned back into plain text (so they don't become links again).</summary>
        [XmlElement("NoLink")] public List<string> NoLinks = new List<string>();
        /// <summary>This tab's own text-area color ("#RRGGBB"); "" = the notes background from Settings.</summary>
        [XmlAttribute, System.ComponentModel.DefaultValue("")] public string Color = "";

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Name)) Name = "Notes";
            if (Paras == null) Paras = new List<NotePara>();
            if (NoLinks == null) NoLinks = new List<string>();
            NoLinks.RemoveAll(string.IsNullOrWhiteSpace);
            Paras.RemoveAll(p => p == null);
            foreach (var p in Paras)
            {
                if (p.Runs == null) p.Runs = new List<NoteRun>();
                p.Runs.RemoveAll(r => r == null);
                foreach (var r in p.Runs) { if (r.T == null) r.T = ""; if (r.Color == null) r.Color = ""; if (r.Back == null) r.Back = ""; }
                if (p.List == null) p.List = "";
                if (p.Align == null) p.Align = "";
                p.Level = Math.Max(0, Math.Min(p.Level, 9));
            }
            if (Color == null) Color = "";
        }

        public static NoteTab FromPlainText(string name, string text)
        {
            var tab = new NoteTab { Name = name };
            foreach (var line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var p = new NotePara();
                if (line.Length > 0) p.Runs.Add(new NoteRun { T = line });
                tab.Paras.Add(p);
            }
            return tab;
        }

        /// <summary>The tab as plain text: checkboxes as [ ] / [x], bullets as •, numbered lines as 1. 2. 3.</summary>
        public string PlainText()
        {
            var sb = new System.Text.StringBuilder();
            var numbers = new int[10];   // the count at each indent level
            for (int i = 0; i < Paras.Count; i++)
            {
                var p = Paras[i];
                int level = Math.Max(0, Math.Min(p.Level, 9));
                if (p.List == "number") { numbers[level]++; for (int k = level + 1; k < 10; k++) numbers[k] = 0; }
                else if (p.List == "") Array.Clear(numbers, 0, 10);
                if (p.List != "") sb.Append(new string(' ', level * 4));   // indented lines stay indented
                if (p.List == "bullet") sb.Append("• ");
                else if (p.List == "number") sb.Append(numbers[level]).Append(". ");
                if (p.Check) sb.Append(p.Done ? "[x] " : "[ ] ");
                foreach (var r in p.Runs) sb.Append(r.T);
                if (i < Paras.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>A paragraph of notes: in a bulleted or numbered list or not, maybe with a checkbox in front.</summary>
    public class NotePara
    {
        [XmlAttribute, System.ComponentModel.DefaultValue("")] public string List = "";   // "", "bullet" or "number"
        [XmlAttribute, System.ComponentModel.DefaultValue(0)] public int Level;           // list indent: 0, or 1+ under the line above
        [XmlAttribute, System.ComponentModel.DefaultValue("")] public string Align = "";  // "", "center", "right" or "justify"
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool Check;      // has a checkbox
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool Done;       // ...and it's ticked
        [XmlElement("R")] public List<NoteRun> Runs = new List<NoteRun>();
    }

    /// <summary>A stretch of text with one formatting. Size 0, Color "" and Back "" mean the notes window's own.</summary>
    public class NoteRun
    {
        [XmlAttribute] public string T = "";
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool B;   // bold
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool I;   // italic
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool U;   // underline
        [XmlAttribute, System.ComponentModel.DefaultValue(false)] public bool S;   // strikethrough
        [XmlAttribute, System.ComponentModel.DefaultValue(0.0)] public double Size;
        [XmlAttribute, System.ComponentModel.DefaultValue("")] public string Color = "";
        [XmlAttribute, System.ComponentModel.DefaultValue("")] public string Back = "";   // highlight
    }

    public class LauncherData
    {
        public AppSettings Settings = new AppSettings();
        public List<LauncherItem> Items = new List<LauncherItem>();
        public List<UsageEntry> Usage = new List<UsageEntry>();
        public List<Decoration> Decorations = new List<Decoration>();
        public string NotesText = "";   // plain text of all the notes (what versions before tabs read)
        public List<NoteTab> NoteTabs = new List<NoteTab>();
        public int NoteTabIndex;        // the tab that was open
        public List<string> SeenApps = new List<string>();   // installed apps already noticed (for auto-add)

        /// <summary>Fill in anything a file left out, so the rest of the app never sees nulls.</summary>
        public void Normalize()
        {
            if (Settings == null) Settings = new AppSettings();
            if (Items == null) Items = new List<LauncherItem>();
            if (Usage == null) Usage = new List<UsageEntry>();
            if (Decorations == null) Decorations = new List<Decoration>();
            if (NotesText == null) NotesText = "";
            if (NoteTabs == null) NoteTabs = new List<NoteTab>();
            NoteTabs.RemoveAll(t => t == null);
            foreach (var t in NoteTabs) t.Normalize();
            if (NoteTabs.Count == 0) NoteTabs.Add(NoteTab.FromPlainText("Notes", NotesText));   // notes from before tabs
            NoteTabIndex = Math.Max(0, Math.Min(NoteTabIndex, NoteTabs.Count - 1));
            if (Settings.NotesBackColor == null) Settings.NotesBackColor = "";
            if (Settings.Theme == null) Settings.Theme = "";
            if (Settings.MyThemes == null) Settings.MyThemes = new List<UserTheme>();
            if (SeenApps == null) SeenApps = new List<string>();
            if (Settings.Sensors == null) Settings.Sensors = new List<string>();
            if (Settings.KnownSensors == null) Settings.KnownSensors = new List<KnownSensor>();
            if (Settings.SensorNoGraph == null) Settings.SensorNoGraph = new List<string>();
            if (Settings.SensorAlerts == null) Settings.SensorAlerts = new List<SensorAlert>();
            Settings.SensorAlerts.RemoveAll(a => a == null || string.IsNullOrEmpty(a.Id));
            Settings.SensorGraphLength = Math.Max(10, Math.Min(240, Settings.SensorGraphLength));
            Settings.KnownSensors.RemoveAll(k => k == null || string.IsNullOrEmpty(k.Id));
            if (Settings.VolumeButtons10 == null) Settings.VolumeButtons10 = new List<int>();
            if (Settings.VolumeButtons20 == null) Settings.VolumeButtons20 = new List<int>();
            if (Settings.VolumeButtonsCustom == null) Settings.VolumeButtonsCustom = new List<int>();
            Settings.VolumeButtonCount = Math.Max(1, Math.Min(AppSettings.MaxVolumeButtons, Settings.VolumeButtonCount));
            if (string.IsNullOrWhiteSpace(Settings.PingHost)) Settings.PingHost = "1.1.1.1";
            Items.RemoveAll(i => i == null);
            foreach (var i in Items) if (i.Children == null) i.Children = new List<LauncherItem>();
            Decorations.RemoveAll(d => d == null);
        }

        public static string DataFolder
        {
            get
            {
                string over = Environment.GetEnvironmentVariable("READYLAUNCHER_DATA"); // for testing with a separate data set
                if (!string.IsNullOrEmpty(over)) return over;
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReadyLauncher");
            }
        }

        /// <summary>
        /// Where the setup was kept before the rename (%APPDATA%\WinLauncher); a test copy names a stand-in with
        /// READYLAUNCHER_OLD_DATA. null: none.
        /// </summary>
        static string OldDataFolder
        {
            get
            {
                if (Program.TestMode)
                {
                    string test = Environment.GetEnvironmentVariable("READYLAUNCHER_OLD_DATA");
                    return string.IsNullOrEmpty(test) ? null : test;
                }
                return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinLauncher");
            }
        }

        /// <summary>
        /// The first start after the rename: copy the icons, notes, settings and pictures from the old folder, and point
        /// the pictures that were kept there at their copies. The old folder is left as it was. Does nothing once the new
        /// folder has a setup. Returns true if it copied.
        /// </summary>
        public static bool CopyFromOldName()
        {
            string old = OldDataFolder, now = DataFolder;
            if (old == null) return false;
            old = System.IO.Path.GetFullPath(old).TrimEnd('\\');
            now = System.IO.Path.GetFullPath(now).TrimEnd('\\');
            string xml = System.IO.Path.Combine(now, "launcher.xml");
            if (string.Equals(old, now, StringComparison.OrdinalIgnoreCase) || File.Exists(xml) || !File.Exists(System.IO.Path.Combine(old, "launcher.xml")))
                return false;
            try
            {
                foreach (string f in Directory.GetFiles(old, "*", SearchOption.AllDirectories))
                {
                    string to = now + f.Substring(old.Length);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to));
                    if (!File.Exists(to)) File.Copy(f, to);
                }
                string text = File.ReadAllText(xml);
                string moved = System.Text.RegularExpressions.Regex.Replace(text,
                    System.Text.RegularExpressions.Regex.Escape(old + "\\"), (now + "\\").Replace("$", "$$"),
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (moved != text)
                {
                    File.WriteAllText(xml + ".tmp", moved, new System.Text.UTF8Encoding(false));
                    File.Replace(xml + ".tmp", xml, null);
                }
                return true;
            }
            catch (Exception)
            {
                // Half a copy is worse than none: start over next time (the old folder is untouched).
                try { if (File.Exists(xml)) File.Delete(xml); } catch (Exception) { }
                return false;
            }
        }

        static string FilePath { get { return System.IO.Path.Combine(DataFolder, "launcher.xml"); } }

        public static LauncherData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var fs = File.OpenRead(FilePath))
                    {
                        var d = (LauncherData)new XmlSerializer(typeof(LauncherData)).Deserialize(fs);
                        d.Normalize();
                        return d;
                    }
                }
            }
            catch (Exception)
            {
                try { File.Copy(FilePath, FilePath + ".bad", true); } catch { }
            }
            return new LauncherData();
        }

        public void Save()
        {
            try { WriteTo(FilePath); }
            catch (Exception) { }
        }

        /// <summary>Write this data as a launcher.xml-style file (via a temp file, so a crash never leaves half a file).</summary>
        public void WriteTo(string path)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(LauncherData)).Serialize(fs, this);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public void RecordLaunch(string path, string name)
        {
            if (string.IsNullOrEmpty(path)) return;
            var e = Usage.FirstOrDefault(u => string.Equals(u.Path, path, StringComparison.OrdinalIgnoreCase));
            if (e == null) { e = new UsageEntry { Path = path, Name = name }; Usage.Add(e); }
            e.Count++;
            e.Last = DateTime.Now;
        }

        /// <summary>Every item, including the ones inside folders.</summary>
        public IEnumerable<LauncherItem> AllItems()
        {
            foreach (var i in Items)
            {
                yield return i;
                foreach (var c in i.Children) yield return c;
            }
        }
    }
}
