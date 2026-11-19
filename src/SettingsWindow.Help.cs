using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ReadyLauncher
{
    /// <summary>
    /// A short description for every setting in this window, shown when the mouse rests on a setting's title or on any of
    /// its options. Descriptions are looked up by the text shown (a row's title, a checkbox's or button's words, a tab's
    /// name); boxes, lists and sliders without words of their own get the description of the setting they belong to.
    /// Anything that already has its own tooltip keeps it.
    /// </summary>
    internal partial class SettingsWindow
    {
        // Keys: "Tab|Text" where the same words mean different things on different tabs, "Row|Button" for buttons like
        // Browse..., or just the text shown.
        static readonly Dictionary<string, string> Help = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // The tabs
            { "General", "Opening the launcher, its size and animation, the volume control, how Settings looks, backups and updates." },
            { "Appearance", "How the launcher looks: icon size, names, layout, scrolling, background color and picture." },
            { "Quick launch strip", "The row of your most-used or newest apps along one edge of the launcher." },
            { "Notes", "The notes window beside the launcher: where it sits, how big it is, its text size and color." },
            { "Apps", "Add all your installed apps at once, and add new apps automatically when they're installed." },
            { "Hardware monitoring", "Live readings (processor, memory, network, temperatures...) in a bar above the launcher." },

            // General
            { "Open launcher with", "The key combination that opens or hides the launcher from anywhere in Windows. Click the box, then press the keys." },
            { "Launcher size", "How big the launcher window is: a preset size, or your own width and height in pixels." },
            { "Open / close animation", "How the launcher appears and disappears: fade, slide up or down, zoom, pop, blur, TV static (it comes and goes through snow, like an old TV between channels), flip (each window turns away like a card, and back), digital rain (green characters pour down over the windows), beam (threads of gold light run through the windows and they glow away into light, and back, like a sci-fi transporter), glass (the windows shimmer in like clean glass and shatter apart when they close), or no animation." },
            { "Animation speed", "How fast the open and close animations play: 100% is normal, lower is slower, higher is faster." },
            { "Also animate", "Which windows beside the launcher open and close with the same animation: all of them, or the ones you tick." },
            { "All of them", "Animate the notes window, sensor bar, detached quick launch strip and outside volume control together with the launcher." },
            { "Notes window|Also animate", "Animate the notes window together with the launcher." },
            { "Also animate|Notes window", "Animate the notes window together with the launcher." },
            { "Sensor bar", "Animate the sensor bar above the launcher together with the launcher." },
            { "Quick launch strip (when detached)", "Animate the quick launch strip together with the launcher, when it floats as its own bar." },
            { "Volume control (when outside the launcher)", "Animate the volume control together with the launcher, when it sits outside the launcher." },
            { "Open icons with a double-click (instead of a single click)", "Icons, folders, the quick launch strip and text links open with a double-click instead of a single click." },
            { "Volume control", "A volume slider for your PC's sound: inside or outside the launcher, on the left or right, or off." },
            { "Volume control size", "How big the volume slider is." },
            { "Volume distance from window", "How far a volume control outside the launcher sits from its edge." },
            { "Hide the launcher after opening an app", "Close the launcher by itself as soon as you open an app, file or link from it." },
            { "Hide the launcher when I click outside it", "Close the launcher when you click anywhere outside it, like the Start menu does." },
            { "Start the launcher when I sign in to Windows", "Start the launcher quietly each time you sign in to Windows, so its shortcut key works right away." },
            { "Settings theme", "The colors of this Settings window: follow Windows (light or dark), or always light, light grey, grey, dark grey, dark or Christmas." },
            { "Theme", "A ready-made look: a background picture and matching colors for the launcher, its buttons, the volume control, icon names, the notes window, the quick launch strip, the sensor bar and this Settings window, all at once. You can still change any of them afterward. Save as theme keeps your own look (pictures, colors, see-through settings, animation and lamp strip) as a theme; Export saves a theme to a file to share, and Import adds one someone shared. Choose None to go back to your own look, exactly as it was." },
            { "Backup", "Save everything (icons, notes, settings and pictures) to a file, load it back, or have backups saved automatically." },
            { "Back up automatically every", "Save a backup by itself every so many days (1 to 365) into the folder below. Older backups are kept." },
            { "Backup|Browse...", "Choose the folder for automatic backups. It must be on a drive of this PC." },
            { "Updates", "The version you have, a check for a newer one on GitHub, and a link to the project's page." },

            // Appearance
            { "Appearance|Icon size", "How big the icons on the launcher are." },
            { "Show names under icons", "Show each icon's name under it. Folders always show their names." },
            { "Icon layout", "How icons are arranged: anywhere you drop them, lined up with nearby icons, snapped to a grid, or in order automatically." },
            { "Scroll when there are more icons than fit (scroll bar and mouse wheel)", "Let the launcher scroll when the icons don't all fit. When off, anything below the bottom edge is cut off." },
            { "Appearance|Background color", "The launcher's background color: type a color like #1F4E79, click a preset, or use the color wheel." },
            { "Background transparency", "How see-through the launcher's background is. The icons and text stay solid." },
            { "Background picture", "A picture behind the icons. Disable turns it off but keeps it chosen, so Enable brings it back." },
            { "Background picture|Browse...", "Choose a picture file for the launcher's background." },
            { "Appearance|Picture transparency", "How see-through the background picture is, letting the background color show through." },
            { "Top buttons color", "The color of the buttons at the top of the launcher, and of the quick launch strip's round menu button. Auto keeps them readable on the background." },
            { "Volume control color", "The color of the volume control's mute button, slider, percentage and Mixer text. Auto keeps them readable on what's behind them." },
            { "Icon names color", "The color of the names under the icons, in the launcher and in open folders. Auto keeps them readable on the background." },
            { "Notes title and tabs color", "The color of the notes window's \"Notes\" title, tab names and buttons. The notes text itself keeps its own colors." },
            { "Use the same color for all four", "Give the top buttons, volume control, icon names and notes title and tabs one shared color, chosen in the row below." },
            { "Lamp strip", "Adds a strip of blinking red and amber computer lamps, like the front of an early-1980s mainframe: inside the launcher (along its top edge, with the buttons below it, or along the bottom), outside it right against its top or bottom edge, along the bottom of the quick launch strip, or behind the strip's icons. The last two need the quick launch strip to be showing. It only runs while the launcher is showing." },
            { "Lamp activity", "How busy the lamps look: idle blinks slowly with few lamps lit, computing is steady activity, and alert is busier, with a band of light sweeping along the strip." },
            { "Color for all four","The one color used for the top buttons, volume control, icon names and notes title and tabs while \"Use the same color for all four\" is on." },

            // Quick launch strip
            { "Strip shows", "What the strip lists: your most-used apps, your most recently installed apps, or nothing (hidden)." },
            { "Number of apps", "How many apps the strip shows, from 1 to 30." },
            { "Quick launch strip|Icon size", "How big the strip's icons are, or the same size as the launcher's icons." },
            { "Position", "Which edge of the launcher the strip runs along: bottom, top, left or right." },
            { "Detach the strip from the launcher window (it floats beside it)", "Show the strip as its own floating bar next to the launcher, instead of inside it." },
            { "Quick launch strip|Distance from window", "How far a detached strip floats from the launcher." },
            { "Align icons", "Where the strip's icons sit along it: at the start, in the middle, or at the end." },
            { "Show the divider line", "Show a thin line between the strip and the rest of the launcher." },
            { "Strip color", "The strip's own background color, or None to use the launcher's." },
            { "Strip picture", "A picture behind the strip's icons. Disable turns it off but keeps it chosen, so Enable brings it back." },
            { "Strip picture|Browse...", "Choose a picture file for the strip's background." },
            { "Quick launch strip|Picture transparency", "How see-through the strip picture is." },
            { "Strip transparency", "How see-through the whole strip background is." },

            // Notes
            { "Notes window", "A notes window beside the launcher, on the left or right, or off. It opens and closes with the launcher." },
            { "Notes|Distance from window", "How far the notes window sits from the launcher." },
            { "Width", "How wide the notes window is. You can also drag the notes window's outer edge." },
            { "Notes|Text size", "The normal size of the notes text. Text you've made bigger or smaller keeps its own size." },
            { "Notes|Background color", "The area behind the notes text, in tabs that don't have their own color. Auto is a light tint of the launcher's color." },

            // Apps
            { "Installed apps", "Add every app installed on this PC to the launcher in one go, grouped and sorted the way you choose." },
            { "Add all installed apps...", "Choose how to group and sort them, then add all your installed apps at once." },
            { "New apps", "Add a shortcut by itself whenever a new app is installed, even one installed while the launcher wasn't running." },
            { "Add a shortcut automatically when a new app is installed", "Add a shortcut by itself whenever a new app is installed, even one installed while the launcher wasn't running." },
            { "Put them", "Where the new apps' shortcuts go: into a \"New apps\" folder, or straight onto the launcher." },

            // Hardware monitoring
            { "Line them up", "Where the readings sit in the bar above the launcher: left, center or right." },
            { "Fill rows", "When the readings need more than one row: fill the top row first, or the bottom row (next to the launcher) first." },
            { "Hardware monitoring|Size", "How big the readings in the sensor bar are." },
            { "Show them as", "Readings show each sensor's name and value in a compact row. Tiles are a little bigger and add a mini graph of the recent readings, for every sensor that measures a number (CPU load, temperatures, network speed, ping...). Sensors that are text, like the IP address or the busiest app, get a tile with just the value. Point at a tile to see the highest and lowest of the readings its graph shows, and when they were reached. Click the small gear at a tile's top right (or right-click the tile) to show or hide its graph and set its warning levels; point at the gear for how tiles work." },
            { "Warning colors", "Readings turn amber when they reach their warning level and red at their critical level: the value, a thicker border, and the part of the graph past the level. Sensible levels are built in (85% and 95% for processor, graphics and memory load, 80 °C and 90 °C for temperatures, 100 ms and 250 ms for ping, 20% and 10% for battery charge...). Click Warning levels to set your own, or use a tile's gear (or right-click a reading on the bar)." },
            { "Color readings amber or red when they reach their warning levels", "Readings turn amber when they reach their warning level and red at their critical level. Switch this off to keep every reading in its normal color (your levels are kept)." },
            { "Tile text", "Where the name and value sit on each tile: top left, or centered. Centered text is in the middle from left to right, and on tiles without a graph (an IP address, a name, a graph switched off) it's also in the middle from top to bottom." },
            { "Graph length", "How many recent readings each tile's graph shows. The time this covers depends on how often the sensors are checked: at every 5 seconds, 60 readings are 5 minutes. Readings are only taken while the launcher is open." },
            { "Check sensors", "How often the readings are updated, in seconds." },
            { "Hardware monitoring|Text color", "The color of the sensor bar's text. Auto picks whatever reads well on its background." },
            { "Hardware monitoring|Background color", "The sensor bar's background color. Auto uses the launcher's color." },
            { "Always look for HWiNFO and Libre Hardware Monitor, and add their sensors when they're found",
              "Read extra sensors (temperatures, fans, voltages) from HWiNFO or Libre Hardware Monitor while either one is running and sharing them." },
            { "Sensors", "Tick the readings to show in the bar above the launcher. Rest the mouse on one to see what it measures." },
            { "Check for HWiNFO and Libre Hardware Monitor", "See whether HWiNFO and Libre Hardware Monitor are installed, running and sharing their sensors, and what to switch on if not." },
            { "Show none", "Untick every reading (the sensor bar goes away)." },
            { "PawnIO driver", "Whether the PawnIO driver (which Libre Hardware Monitor installs) is on this PC, and a button to remove it completely." },
            { "Windows", "Readings Windows itself provides. No extra software needed." },
            { "HWiNFO", "Readings shared by HWiNFO while it's running: temperatures, fan speeds, voltages and more." },
            { "Libre Hardware Monitor", "Readings shared by Libre Hardware Monitor while it's running: temperatures, fan speeds, voltages and more." },
            { "Sensor Information", "How to get more sensors, like the processor's temperature and fan speeds." },

            // The buttons at the bottom
            { "OK", "Keep the changes and close Settings." },
            { "Cancel", "Close Settings without keeping the changes." },
        };

        /// <summary>Gives every title, option and button in the window its description.</summary>
        void ExplainEverything(Panel bottomButtons)
        {
            foreach (TabItem tab in tabs.Items)
            {
                string name = tab.Header as string;
                Describe(tab, name, null, name);
                Walk(tab.Content as DependencyObject, null, name);
            }
            foreach (var b in bottomButtons.Children.OfType<Button>()) Describe(b, null, null, b.Content as string);
        }

        void Walk(DependencyObject d, Grid page, string tab)
        {
            if (d == null) return;
            var grid = d as Grid;
            if (grid != null && IsPage(grid)) page = grid;
            var fe = d as FrameworkElement;
            if (fe != null) Explain(fe, page, tab);
            foreach (object child in LogicalTreeHelper.GetChildren(d))
            {
                var c = child as DependencyObject;
                if (c != null) Walk(c, page, tab);
            }
        }

        /// <summary>A settings page: two columns (titles, then options), as made by Page().</summary>
        static bool IsPage(Grid g)
        {
            return g.ColumnDefinitions.Count == 2 && g.ColumnDefinitions[0].Width.IsAuto && g.ColumnDefinitions[1].Width.IsStar;
        }

        void Explain(FrameworkElement fe, Grid page, string tab)
        {
            if (fe.ToolTip != null) return;
            var label = fe as TextBlock;
            if (label != null)
            {
                if (page != null && label.Parent == page && Grid.GetColumn(label) == 0) Describe(label, tab, null, label.Text);   // a setting's title
                return;
            }
            var inner = fe as TabItem;   // e.g. the sensor list's tabs
            if (inner != null) { Describe(inner, tab, null, TextOf(inner.Header)); return; }
            string row = RowTitle(fe, page);
            var button = fe as ButtonBase;   // buttons, checkboxes and option buttons: by their words, else by their setting
            if (button != null) { Describe(button, tab, row, TextOf(button.Content) ?? row); return; }
            if (fe is ComboBox || fe is Slider || fe is TextBox || fe is ListBox) Describe(fe, tab, null, row);
        }

        static string TextOf(object content)
        {
            var s = content as string;
            if (s != null) return s;
            var tb = content as TextBlock;
            if (tb != null) return string.IsNullOrEmpty(tb.Text) ? null : tb.Text;
            return null;
        }

        /// <summary>The title of the settings row an element is in (empty titles count as none).</summary>
        static string RowTitle(FrameworkElement fe, Grid page)
        {
            if (page == null) return null;
            DependencyObject d = fe;
            while (d != null && LogicalTreeHelper.GetParent(d) != page) d = LogicalTreeHelper.GetParent(d);
            var top = d as UIElement;
            if (top == null) return null;
            int row = Grid.GetRow(top);
            var title = page.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetRow(t) == row && Grid.GetColumn(t) == 0);
            return title == null || string.IsNullOrWhiteSpace(title.Text) ? null : title.Text;
        }

        /// <summary>Finds the description for some text: on this tab and setting first, then on this tab, then anywhere.</summary>
        static void Describe(FrameworkElement fe, string tab, string row, string text)
        {
            if (fe == null || fe.ToolTip != null || string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            var keys = new List<string>();
            foreach (var t in new[] { text, text.TrimEnd(':'), System.Text.RegularExpressions.Regex.Replace(text, @"\s*\(\d+\)$", "") })   // e.g. "HWiNFO (295)"
            {
                if (tab != null && row != null) keys.Add(tab + "|" + row + "|" + t);
                if (row != null) keys.Add(row + "|" + t);
                if (tab != null) keys.Add(tab + "|" + t);
                keys.Add(t);
            }
            string help;
            foreach (var k in keys)
                if (Help.TryGetValue(k, out help)) { fe.ToolTip = help; return; }
        }

        // ---------- sensors ----------

        /// <summary>What a reading in the sensor list shows, and how it's labeled on the bar.</summary>
        internal static string SensorHelp(SensorInfo info)
        {
            string id = info.Id ?? "", what;
            string tail = id.Contains("|") ? id.Substring(id.LastIndexOf('|') + 1) : "";
            if (info.Source == "H" || info.Source == "L")
                what = "A reading from " + info.SourceName + (string.IsNullOrEmpty(info.Group) ? "" : " (" + info.Group + ")") + ".";
            else if (id.StartsWith("core|")) what = "How busy this one processor core is. One core near 100% while the others idle usually means a single app is working hard.";
            else if (id.StartsWith("gpu|"))
            {
                string engine = id.Substring(4);
                what = engine == "3D" ? "How busy the graphics card is drawing 3D: games, visual effects, the desktop."
                     : engine == "VideoDecode" ? "How busy the graphics card is playing video."
                     : engine == "VideoEncode" ? "How busy the graphics card is recording or streaming video."
                     : engine == "Compute" ? "How busy the graphics card is with calculations, like AI or video editing."
                     : "How busy the graphics card is moving data around.";
            }
            else if (id.StartsWith("net|"))
            {
                what = tail == "down" ? "How fast data is coming in over this network connection right now."
                     : tail == "up" ? "How fast data is going out over this network connection right now."
                     : tail == "total" ? "How much data this connection has received and sent since Windows started."
                     : tail == "link" ? "The speed this adapter agreed with your router: the most it can carry, not what it's using."
                     : tail == "signal" ? "How strong the Wi-Fi signal is."
                     : tail == "ssid" ? "The name of the Wi-Fi network this adapter is connected to."
                     : "A reading for this network connection.";
            }
            else if (id.StartsWith("disk|"))
                what = tail == "read" ? "How fast this disk is reading data right now."
                     : tail == "write" ? "How fast this disk is writing data right now."
                     : "How much of the time this disk is busy. Near 100% means it's what's slowing the PC down.";
            else if (id.StartsWith("drive|")) what = "How much free space is left on this drive.";
            else
            {
                switch (id)
                {
                    case "cpu.load": what = "How busy the processor is overall, as a percentage of its full capacity."; break;
                    case "cpu.speed": what = "The processor's current speed. It rises when the PC is busy and drops to save power when it's idle."; break;
                    case "cpu.top": what = "The app using the most processor time right now."; break;
                    case "sys.processes": what = "How many programs and background processes are running."; break;
                    case "sys.threads": what = "How many threads (separate tasks inside programs) are running in all."; break;
                    case "mem.used": what = "How much memory (RAM) is in use, out of the total installed."; break;
                    case "mem.percent": what = "How much of the memory (RAM) is in use, as a percentage."; break;
                    case "mem.commit": what = "Memory promised to apps, counting RAM plus the page file on disk, out of the most Windows can give."; break;
                    case "mem.top": what = "The app using the most memory (RAM) right now."; break;
                    case "gpu.load": what = "How busy the graphics card is, by its busiest part (3D, video, compute...)."; break;
                    case "gpu.dedicated": what = "How much of the graphics card's own memory is in use."; break;
                    case "gpu.shared": what = "How much of the PC's memory (RAM) the graphics chip is borrowing."; break;
                    case "inet.ping": what = "How long a quick message takes to reach the ping address (set below) and come back. Lower is better."; break;
                    case "inet.status": what = "Whether this PC is connected to the internet."; break;
                    case "inet.ip": what = "This PC's address on your local network."; break;
                    case "bat.percent": what = "How full the battery (or UPS) is."; break;
                    case "bat.time": what = "Roughly how long the battery (or UPS) will last at the current use."; break;
                    case "sys.uptime": what = "How long it's been since Windows started."; break;
                    case "sys.pc": what = "This PC's name."; break;
                    case "sys.user": what = "The Windows account that's signed in."; break;
                    default: what = info.Name + "."; break;
                }
            }
            return what + "\nOn the bar it reads: " + info.Label;
        }

        // ---------- test copies ----------

        /// <summary>Test copies only: the titles, options and buttons that still have no description.</summary>
        internal List<string> Undescribed()
        {
            var missing = new List<string>();
            foreach (TabItem tab in tabs.Items)
            {
                if (tab.ToolTip == null) missing.Add("tab | " + tab.Header);
                Collect(tab.Content as DependencyObject, null, (string)tab.Header, missing);
            }
            return missing;
        }

        static void Collect(DependencyObject d, Grid page, string tab, List<string> missing)
        {
            if (d == null) return;
            var grid = d as Grid;
            if (grid != null && IsPage(grid)) page = grid;
            var fe = d as FrameworkElement;
            if (fe != null && fe.ToolTip == null)
            {
                var label = fe as TextBlock;
                string what = null;
                if (label != null && page != null && label.Parent == page && Grid.GetColumn(label) == 0 && !string.IsNullOrWhiteSpace(label.Text)) what = "title | " + label.Text;
                else if (fe is ButtonBase) what = fe.GetType().Name + " | " + (TextOf(((ContentControl)fe).Content) ?? "(no text)") + " | row: " + (RowTitle(fe, page) ?? "-");
                else if (fe is ComboBox || fe is Slider || fe is TextBox) what = fe.GetType().Name + " | row: " + (RowTitle(fe, page) ?? "-");
                else if (fe is TabItem) what = "inner tab | " + (TextOf(((TabItem)fe).Header) ?? "?");
                if (what != null) missing.Add(tab + " | " + what);
            }
            foreach (object child in LogicalTreeHelper.GetChildren(d))
            {
                var c = child as DependencyObject;
                if (c != null) Collect(c, page, tab, missing);
            }
        }
    }
}
