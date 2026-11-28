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
            { "Appearance", "How the launcher looks: themes, icon size, names, layout, scrolling, background color and picture, and the colors of its parts. The lamp strip has a tab of its own." },
            { "Lamp strip", "A strip of blinking computer lamps: where it goes, its colors, height and activity, the WOPR monitor and its Magic 8 Ball, text spelled out in lamps, random numbers and reminders. Right-click the strip for a menu with all of them." },
            { "Quick launch strip", "The row of your most-used or newest apps along one edge of the launcher." },
            { "Notes", "The notes window beside the launcher: where it sits, how big it is, its text size and color." },
            { "Apps", "Add all your installed apps at once, and add new apps automatically when they're installed." },
            { "Hardware monitoring", "Live readings (processor, memory, network, temperatures...) in a bar above the launcher." },

            // General
            { "Open launcher with", "The key combination that opens or hides the launcher from anywhere in Windows: one press hides it while it's showing, whatever you're using. Click the box, then press the keys." },
            { "Launcher size", "How big the launcher window is: a preset size, or your own width and height in pixels." },
            { "Open / close animation", "How the launcher appears and disappears. Randomize picks a different one of these each time it opens: fade, slide up or down, zoom, pop, blur, TV static (it comes and goes through snow, like an old TV between channels), flip (each window turns away like a card, and back), digital rain (green characters pour down over the windows), beam (threads of gold light run through the windows and they glow away into light, and back, like a sci-fi transporter), glass (the windows shimmer in like clean glass and shatter apart when they close), or no animation." },
            { "Animation speed", "How fast the open and close animations play: 100% is normal, lower is slower, higher is faster." },
            { "Also animate", "Which windows beside the launcher open and close with the same animation: all of them, or the ones you tick." },
            { "All of them", "Animate the notes window, sensor bar, detached quick launch strip and outside volume control together with the launcher." },
            { "Notes window|Also animate", "Animate the notes window together with the launcher." },
            { "Also animate|Notes window", "Animate the notes window together with the launcher." },
            { "Sensor bar", "Animate the sensor bar above the launcher together with the launcher." },
            { "Quick launch strip (when detached)", "Animate the quick launch strip together with the launcher, when it floats as its own bar." },
            { "Volume control (when outside the launcher)", "Animate the volume control together with the launcher, when it sits outside the launcher." },
            { "Open icons with a double-click (instead of a single click)", "Icons, folders, the quick launch strip and text links open with a double-click instead of a single click." },
            { "Volume control", "A volume control for your PC's sound (a slider, buttons, or both): inside or outside the launcher, on the left or right, or off." },
            { "Volume control size", "How big the volume control is." },
            { "Volume control style", "Buttons: show a column of volume buttons that each set one volume (the one for the volume now is lit); the slider beside it sets how many, from 1 to 20, spread evenly from 100% down (4 buttons: 100%, 75%, 50%, 25%). Hide Slider: the buttons only, without the volume slider (unticking Buttons clears it). Without Buttons, the volume control is the slider. Right-click a volume button to choose the volume it sets." },
            { "Volume button height", "How tall each volume button is: small, medium or large. Buttons that don't fit inside the launcher are made smaller." },
            { "Volume options", "Close the launcher once you've chosen a volume." },
            { "Close the launcher after choosing a volume", "Close the launcher by itself once you click a volume button or move the slider. Muting, and choosing what a button sets, keep it open." },
            { "Volume distance from window", "How far a volume control outside the launcher sits from its edge." },
            { "Hide the launcher after opening an app", "Close the launcher by itself as soon as you open an app, file or link from it." },
            { "Hide the launcher when I click outside it", "Close the launcher when you click anywhere outside it, like the Start menu does." },
            { "Start the launcher when I sign in to Windows", "Start the launcher quietly each time you sign in to Windows, so its shortcut key works right away. If you start to sign out or restart and it's canceled, the launcher stays open." },
            { "Settings theme", "The colors of this Settings window and of the right-click menus (on icons, the lamp strip, the quick launch strip, notes and sensors): follow Windows (light or dark), or always light, light grey, grey, dark grey, dark or Christmas." },
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
            { "Volume control color", "The color of the volume control's mute button, slider, buttons, percentage and Mixer text. Auto keeps them readable on what's behind them." },
            { "Icon names color", "The color of the names under the icons, in the launcher and in open folders. Auto keeps them readable on the background." },
            { "Notes title and tabs color", "The color of the notes window's \"Notes\" title, tab names and buttons. The notes text itself keeps its own colors." },
            { "Use the same color for all four", "Give the top buttons, volume control, icon names and notes title and tabs one shared color, chosen in the row below." },
            { "Lamp strip|Position", "Adds a strip of blinking computer lamps, like the front of an early-1980s mainframe: inside the launcher (along its top edge, with the buttons below it, or along the bottom), outside it right against its top or bottom edge, along the bottom of the quick launch strip, or behind the strip's icons. The last two need the quick launch strip to be showing. It only runs while the launcher is showing. Right-click the strip for a menu with all of its settings, this one too (Position); Off takes it away until you choose a place for it here." },
            { "Lamp colors", "The colors of the strip's lamps, and of the ones in the monitor: red and amber (the usual), or your own, one or more that you tick below, from the list or chosen with a color wheel. Blinking, several colors are mixed (the usual red and amber go by modules); text and reminders have each letter in one color, taking turns. Right-click the strip, Activity & Colors, for more: colors saved under a name (click one to use it), and colors picked at random, right away or each time the launcher opens (your custom ones or any color, one or several). While saved or random colors are in use, a line here says so; choosing colors here puts them in their place. Right-click a custom color or saved colors there to give the strip, the monitor's lamps or its mini lamps those colors of their own, or just the strip's left, middle or right third (up to two thirds for one color). Default (red & amber) puts them all back." },
            { "Your own colors", "With Lamp colors set to Your own colors: tick one color for all the lamps, or as many as you like. At least one stays ticked. (The strip's right-click menu has your custom colors only: ticking one there uses the custom colors ticked, without these.)" },
            { "Custom colors", "Lamp colors of your own from a color wheel: click Add a color..., pick any color, and it's ticked and used. Keep as many as you like; tick or untick each like the colors above, click its colored dot to change it with the color wheel, or click its X to delete it (you're asked first). A color too dark to see on a lamp is made brighter. You can also right-click the strip, Activity & Colors, to do all of this there." },
            { "Pastel (softer colors)", "Makes the lamps' colors softer and paler, whichever colors you've chosen." },
            { "Lamp strip height", "How many lamps high the strip is, from 6 to 10. At 6, the usual strip, text is all capitals; higher, it's in upper and lower case, the way you type it, and the letters are as tall as the strip. Behind the quick launch strip's icons the lamps fill whatever space there is, so this only changes the text there (the letters are no taller than there's room for). Reminders use the same letters. You can also right-click the strip to change it." },
            { "Leave room for the tails of g, j, p, q and y", "With the strip 7 or more lamps high: keeps some of the rows below the line, so the tails of g, j, p, q and y can hang there; choose how many in Lamps for tails. Unticked, capitals, numbers and tall letters fill the strip's height, and tails are drawn inside their letters. Reminders use the same letters. You can also right-click the strip to change it." },
            { "Lamps for tails", "How many of the strip's rows are kept below the line for tails. The letters get the rest, so more room for tails makes the letters smaller; each choice says how high the capitals are. They stay at least 5 high, so a strip 7 lamps high has room for 1 or 2, 8 for up to 3, and 9 or 10 for up to 4. With 3 or 4, the tails of g and y have room to curl. You can also right-click the strip to change it." },
            { "Lamp activity", "How busy the lamps look: standby is the slowest, blinking at half the pace of idle, idle blinks slowly with few lamps lit, computing is steady activity, and alert is busier, with a band of light sweeping along the strip. Randomize switches between the four on its own, every 20 to 60 seconds. Custom is your own, set with the sliders below. Right-click the strip, Activity & Colors, for Default (every lamp at speed 5, with 25% of them lit) or your own." },
            { "Custom activity", "Your own lamp activity, used when Lamp activity is Custom. For the strip's lamps, the monitor's lamps and the monitor's mini lamps: how quick they are (1 is the slowest, slower than standby; 10 is as quick as computing; 20 is four times as quick) and how many of them are lit at a time (0 to 100%). Moving a slider chooses Custom. Right-click the strip, Activity & Colors, to change them there: tick the lamps two sliders change (the strip's, the monitor's, its mini lamps), and the lamps change as you move them. There, Randomize now picks a speed and a number lit at random for the lamps ticked, Random activity does that each time the launcher opens, and Add random activity to saved activities keeps one you like. Changes here are kept when you click OK or Apply." },
            { "Saved activities", "Custom activities you've saved, by name. Choose one to put it on the sliders above and use it. Save... keeps the sliders' settings under a name (one already called that gets them instead); Delete takes the one chosen off the list (you're asked first). As many as you like. Changes are kept when you click OK or Apply." },
            { "Monitor", "Stands a computer cabinet lettered WOPR at the left or right end of the lamp strip, with a few lamps blinking in its dark window (spaced well apart, and fewer lit than on the strip, doing whatever the strip is doing) and a small board of tiny red LEDs flashing in the middle of it, busier or calmer with the strip's activity (with the Magic 8 Ball, the ball takes its place). It rises above the strip, so with the strip below the launcher it stands over the launcher's bottom corner. It shows when the strip is above or below the launcher (outside); right-click the strip or the monitor to move it or take it away." },
            { "Magic 8 Ball", "A Magic 8 Ball in the monitor's window that answers yes-or-no questions on the lamp strip, and how its answers show." },
            { "In the monitor (click it for an answer)", "Puts a Magic 8 Ball, a black pool ball with its white 8, in the monitor's window. Ask it a yes-or-no question and click it: it shakes and spins for a few seconds and stops with its answer window turned to you, and one of its 20 answers, in capitals, slowly comes out of the strip's blinking lamps, stays for a few seconds and goes back to blinking. Then the strip goes back to what it was showing, and the ball turns back to its 8. It needs the monitor, so ticking it puts the monitor at the right end of the strip if there isn't one, and unticking it takes that monitor away again, so the strip is back as it was. You can also right-click the strip: Magic 8 Ball is just above Random numbers." },
            { "Instant Response (the answer shows at once and stays until you click the ball again)", "When the ball stops spinning, its answer is on the strip at once, instead of slowly coming out of the blinking lamps, and it stays there, still, until you click the ball again: the answer goes as the ball starts to spin, and a new one comes when it stops. Unticking this, or the Magic 8 Ball, takes an answer that's staying off the strip. You can also right-click the strip: Magic 8 Ball, Instant Response." },
            { "Random numbers", "The pattern random numbers are picked with when you right-click the strip and choose Pick random numbers: they show on the strip's lamps at once, stay for 12 seconds and go back to blinking. Write groups separated by commas, each like xxx/0-999: an x for each digit, then a slash and the lowest and highest number it can be, with a dash between them. A smaller number gets zeros in front, so every group has as many digits as x's. Leave out the slash and range for any number with that many digits. For example, xx/0-99,xxx/0-999,xxxx/0-9999 picks numbers like 24 432 8476, and x/1-6,x/1-6 rolls two dice. Up to 20 groups of up to 9 digits. Below the box is an example of what it picks, or what's wrong with it; a pattern that isn't one isn't kept. Save... keeps the pattern under a name. Saved patterns are buttons in the strip's right-click menu, under Random numbers: click one to pick numbers with it (the menu stays open, to click again), point at it to see its pattern and how many times it's been clicked, or right-click it to delete it. The menu's Picked numbers lists the numbers picked, newest first, with the date and time. You can also right-click the strip, Number pattern, to type or save a pattern." },
            { "Saved patterns", "Number patterns you've saved, by name. Choose one to put it in the box above and use it; Delete takes the one chosen off the list (you're asked first). Changes are kept when you click OK or Apply." },
            { "Lamp text", "Spells out the text below in lamps on the strip instead of blinking: scrolling across from the right, flashing (as many words as fit at once, then the next ones), staying still (if it doesn't all fit, it changes to the next words every 4 seconds), or fading in out of the blinking lamps and back: the lamps blink for a few seconds, slowly settle into the text a few at a time, show it for 5 seconds, then slowly go back to blinking, over and over (text too long to fit at once shows the next words the next time round). The monitor keeps blinking. The letters are the strip's full height: all capitals on a strip six lamps high, and in upper and lower case on a higher one (see Lamp strip height). Behind the quick launch strip's icons they show only when that strip is tall enough. You can also right-click the strip to switch it or change the text." },
            { "Keep the lamps blinking on either side of the text", "While text (or a reminder) shows, the lamps on either side of it keep blinking as usual instead of going dark. The blinking comes right up to the edges of the text, and moves along with scrolling text; the whole strip blinks while scrolling text is off the strip. You can also switch this by right-clicking the strip." },
            { "Text scroll speed", "How fast the text moves when it scrolls across the strip: 100% is the usual speed, lower is slower, higher is faster. It doesn't change how fast text or reminders flash. You can also right-click the strip to change it." },
            { "Reminders", "Your reminders, soonest first, each with the date and time it starts flashing on the lamp strip and its text. From then on the strip flashes it (instead of its own text or blinking) until you right-click the strip and dismiss it; several due at once flash in turn. Delete takes one off the list; changes are kept when you click OK or Apply. You can also right-click the strip to add, change or delete them. If a reminder's time comes while the launcher is hidden or the PC is off, it flashes the next time the launcher shows." },
            { "New reminder", "The date and time a new reminder starts flashing (times are in half-hour steps). A day that doesn't exist in that month, like February 30, becomes the month's last day." },
            { "Reminder text", "What a new reminder flashes: letters (shown as capitals when the strip is 6 lamps high), numbers and common punctuation, up to 200 characters. Click Add to put it on the list; text left here when you click OK or Apply is added too." },
            { "Text to show", "What the lamp strip spells when Lamp text is on: letters (shown as capitals when the strip is 6 lamps high), numbers and common punctuation, up to 200 characters. About 22 letters fit across a 900-wide strip at once." },
            { "Color for all four","The one color used for the top buttons, volume control, icon names and notes title and tabs while \"Use the same color for all four\" is on." },

            // Quick launch strip
            { "Strip shows", "What the strip lists: your most-used apps, your most recently installed apps, or nothing (hidden). Right-click an app on the strip to remove it from the list the strip is showing (it stays in the other list); right-click the strip's round button to see the apps you've hidden, each with the list it was removed from, and bring one back." },
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
