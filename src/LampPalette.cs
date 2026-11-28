using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>
    /// The colors of a strip's lamps: the usual red and amber, or ones you picked (from the list, or with the color
    /// wheel), any of them in pastel. Each color is a number, 0 and 1 the usual ones, then
    /// the ones to pick from, then ones from the wheel as they come; its look is at twice that, and in pastel at one more.
    /// </summary>
    internal class LampPalette
    {
        /// <summary>
        /// A lamp's color when it's dark, when it's lit, at the middle of its lens when it's lit, the light around it, and
        /// the middle of that light.
        /// </summary>
        struct Shades
        {
            public Color Off, Lit, Hot, Glow, Core;
        }

        const int Red = 0, Amber = 1, FirstPick = 2;
        static readonly List<Shades> shades = new List<Shades>();
        static readonly List<string> names = new List<string>();
        static readonly Dictionary<string, int> wheelNumbers = new Dictionary<string, int>();   // colors from the wheel that have been used


        /// <summary>How many looks there are, pastel ones included (more as colors from the wheel are used).</summary>
        public static int Count { get { return shades.Count; } }

        static LampPalette()
        {
            Enter("usual red", Shade(Color.FromRgb(52, 14, 9), Color.FromRgb(255, 62, 30), Color.FromRgb(255, 222, 200), Color.FromRgb(255, 70, 30)), false);      // the usual red lens
            Enter("usual amber", Shade(Color.FromRgb(56, 38, 11), Color.FromRgb(255, 172, 52), Color.FromRgb(255, 244, 210), Color.FromRgb(255, 165, 50)), false); // the usual amber lens
            foreach (LampHue h in Enum.GetValues(typeof(LampHue)))   // in the order they're numbered
            {
                var c = Base(h);
                string name = h.ToString().ToLowerInvariant();
                if (h == LampHue.Amber) Enter(name, shades[2 * Amber], false);   // the strip's own amber
                else if (h == LampHue.Grey) Enter(name, Shade(Color.FromRgb(30, 31, 33), c, Color.FromRgb(188, 192, 198), Color.FromRgb(120, 124, 130)), true);   // dimmer than white
                else Enter(name, Shade(Mix(Color.FromRgb(8, 8, 8), c, 0.2), c, Mix(c, Colors.White, 0.82), c), false);
            }
        }

        /// <summary>Adds a color, and the same in pastel; its number.</summary>
        static int Enter(string name, Shades s, bool dim)
        {
            shades.Add(s);
            names.Add(name);
            // Paler, and without the near-white middle a lamp has: that would drown a soft color. (Grey stays below white.)
            Color lit = Mix(s.Lit, Colors.White, dim ? 0.14 : 0.48), hot = Mix(lit, Colors.White, dim ? 0.15 : 0.45);
            var soft = Shade(Mix(s.Off, Color.FromRgb(62, 62, 66), 0.3), lit, hot, Mix(s.Glow, Colors.White, 0.3));
            soft.Core = Mix(lit, Colors.White, dim ? 0.1 : 0.22);
            shades.Add(soft);
            names.Add("pastel " + name);
            return shades.Count / 2 - 1;
        }

        /// <summary>
        /// The number of a color from the wheel. A lamp has to light up: one too dark for that is made brighter, and the
        /// dimmer it is, the less white the middle of its light.
        /// </summary>
        static int Number(string hex)
        {
            int k;
            if (wheelNumbers.TryGetValue(hex, out k)) return k;
            var c = (Color)ColorConverter.ConvertFromString(hex);
            const int least = 110;
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            if (max == 0) c = Color.FromRgb(least, least, least);
            else if (max < least) c = Color.FromRgb((byte)(c.R * least / max), (byte)(c.G * least / max), (byte)(c.B * least / max));
            double bright = Math.Max(max, least) / 255.0;
            var s = Shade(Mix(Color.FromRgb(8, 8, 8), c, 0.2), c, Mix(c, Colors.White, 0.82 * bright), c);
            s.Core = Mix(c, s.Core, bright);
            return wheelNumbers[hex] = Enter(hex, s, bright < 0.6);
        }

        /// <summary>A color from the wheel the way it's kept ("#RRGGBB"), or null when it isn't a color.</summary>
        public static string Hex(string color)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString((color ?? "").Trim());
                return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
            }
            catch (Exception) { return null; }
        }

        static Color Base(LampHue h)
        {
            switch (h)
            {
                case LampHue.Pink: return Color.FromRgb(255, 84, 168);
                case LampHue.Red: return Color.FromRgb(255, 40, 34);
                case LampHue.Orange: return Color.FromRgb(255, 104, 14);
                case LampHue.Amber: return Color.FromRgb(255, 172, 52);
                case LampHue.Gold: return Color.FromRgb(255, 200, 40);
                case LampHue.Yellow: return Color.FromRgb(255, 234, 40);
                case LampHue.Green: return Color.FromRgb(36, 222, 70);
                case LampHue.Cyan: return Color.FromRgb(24, 224, 236);
                case LampHue.Blue: return Color.FromRgb(48, 108, 255);
                case LampHue.Purple: return Color.FromRgb(164, 66, 255);
                case LampHue.Grey: return Color.FromRgb(134, 138, 144);
                default: return Color.FromRgb(240, 244, 255);   // white
            }
        }

        public static Color Off(int c) { return shades[c].Off; }
        public static Color Lit(int c) { return shades[c].Lit; }
        public static Color Hot(int c) { return shades[c].Hot; }
        public static Color Glow(int c) { return shades[c].Glow; }
        public static Color Core(int c) { return shades[c].Core; }
        public static string NameOf(int c) { return names[c]; }

        /// <summary>A color to pick, as it looks lit.</summary>
        public static Color Swatch(LampHue h, bool pastel) { return Lit(2 * Number(h) + (pastel ? 1 : 0)); }
        public static Color Swatch(string hex, bool pastel) { return Lit(2 * Number(hex) + (pastel ? 1 : 0)); }

        static int Number(LampHue h) { return FirstPick + Array.IndexOf(Enum.GetValues(typeof(LampHue)), h); }

        readonly int[] colors;   // null: the usual red and amber, each lamp the one its module gives it
        readonly int soft;       // 1 for pastel, or 0

        /// <summary>The light around the lamps (on the hood, the sill and between modules), and how much of it.</summary>
        public readonly Color Light;
        public readonly double LightMuch = 1;

        public static readonly LampPalette Usual = new LampPalette(null, false);

        /// <summary>The colors chosen in the settings.</summary>
        public static LampPalette From(AppSettings s)
        {
            var listed = s.LampColorList == null ? new List<string>() : TidyList(s.LampColorList);
            if (listed.Count > 0) return new LampPalette(listed.Select(e => EntryNumber(e)).ToArray(), s.LampPastel);   // saved colors, or ones picked at random
            switch (s.LampColors)
            {
                case LampColors.Picked:
                    var picked = Tidy(s.LampPicked).Select(h => Number(h)).Concat(Tidy(s.LampCustom).Where(c => c.On).Select(c => Number(c.Color))).ToArray();
                    return new LampPalette(picked.Length == 0 ? null : picked, s.LampPastel);
                default: return new LampPalette(null, s.LampPastel);
            }
        }

        /// <summary>The lamp strip's colors: its own (chosen in the strip's menu), or those of all the lamps.</summary>
        public static LampPalette ForStrip(AppSettings s) { return Own(s.LampStripColors, s.LampPastel) ?? From(s); }

        /// <summary>The monitor's lamps' colors: their own (chosen in the strip's menu), or those of all the lamps.</summary>
        public static LampPalette ForMonitor(AppSettings s) { return Own(s.LampMonitorColors, s.LampPastel) ?? From(s); }

        /// <summary>The strip's left, middle and right thirds' own colors (null for a third without: it has the strip's).</summary>
        public static LampPalette[] ForThirds(AppSettings s)
        {
            return new[] { Own(s.LampLeftColors, s.LampPastel), Own(s.LampMiddleColors, s.LampPastel), Own(s.LampRightColors, s.LampPastel) };
        }

        static LampPalette Own(List<string> colors, bool pastel)
        {
            var listed = colors == null ? new List<string>() : TidyList(colors);
            return listed.Count == 0 ? null : new LampPalette(listed.Select(e => EntryNumber(e)).ToArray(), pastel);
        }

        /// <summary>The monitor's mini lamps' colors, as they look lit (null: the usual red).</summary>
        public static Color[] ForMini(AppSettings s)
        {
            var listed = s.LampMiniColors == null ? new List<string>() : TidyList(s.LampMiniColors);
            return listed.Count == 0 ? null : listed.Select(e => EntrySwatch(e, s.LampPastel)).ToArray();
        }

        /// <summary>
        /// The Christmas colors there used to be become ones picked: red and green (until 1.96), or red, amber, green and
        /// blue (the Christmas lights of 1.93 to 1.95).
        /// </summary>
        public static void Retire(ref LampColors colors, ref List<LampHue> picked)
        {
            if (colors == LampColors.RedGreen) picked = new List<LampHue> { LampHue.Red, LampHue.Green };
            else if (colors == LampColors.ChristmasLights) picked = new List<LampHue> { LampHue.Red, LampHue.Amber, LampHue.Green, LampHue.Blue };
            else return;
            colors = LampColors.Picked;
        }

        /// <summary>
        /// A list of colors as it's kept: each a ready-made one by its name ("Blue") or another as "#RRGGBB", each once, in
        /// the order given; anything that isn't a color left out.
        /// </summary>
        public static List<string> TidyList(IEnumerable<string> colors)
        {
            var list = new List<string>();
            foreach (var c in colors ?? new string[0])
            {
                string e = Entry(c);
                if (e != null && !list.Contains(e)) list.Add(e);
            }
            return list;
        }

        /// <summary>A color in a list as it's kept, or null when it isn't one.</summary>
        static string Entry(string c)
        {
            c = (c ?? "").Trim();
            if (c.Length == 0) return null;
            LampHue h;
            if (!c.StartsWith("#") && Enum.TryParse(c, true, out h) && Enum.IsDefined(typeof(LampHue), h)) return h.ToString();
            return c.StartsWith("#") ? Hex(c) : null;
        }

        static int EntryNumber(string e)
        {
            LampHue h;
            return !e.StartsWith("#") && Enum.TryParse(e, true, out h) ? Number(h) : Number(e);
        }

        /// <summary>A color in a list, as it looks lit.</summary>
        public static Color EntrySwatch(string e, bool pastel) { return Lit(2 * EntryNumber(e) + (pastel ? 1 : 0)); }

        /// <summary>
        /// Colors from anywhere on the color wheel, bright enough for lamps, as "#RRGGBB": <paramref name="count"/> of them
        /// (always that many), well apart from each other: 40 degrees of the wheel, or as far as that many will go, closer
        /// only if they have to. Each is one of 144 (72 hues, two strengths), so the colors used stay few.
        /// </summary>
        public static List<string> RandomColors(int count, Random rnd)
        {
            count = Math.Max(1, Math.Min(72, count));
            var slots = Enumerable.Range(0, 72).Select(i => i * 5).OrderBy(h => rnd.Next()).ToList();   // the 72 hues, in a random order
            var hues = new List<int>();
            for (int apart = Math.Min(40, 300 / count); ; apart -= 5)
            {
                hues.Clear();
                foreach (int h in slots)
                {
                    if (hues.All(o => Math.Min(Math.Abs(o - h), 360 - Math.Abs(o - h)) >= apart)) hues.Add(h);
                    if (hues.Count == count) break;
                }
                if (hues.Count == count || apart <= 5) break;
            }
            return hues.Select(h => FromHue(h, rnd.Next(2) == 0 ? 0.75 : 1.0)).ToList();
        }

        /// <summary>Test copies only: a color's hue on the wheel, 0 to 360.</summary>
        internal static double HueOf(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            if (d == 0) return 0;
            double h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            return (h * 60 + 360) % 360;
        }

        /// <summary>A color of that hue (0 to 360) and strength (how far from white), at full brightness, as "#RRGGBB".</summary>
        static string FromHue(int hue, double strength)
        {
            double h = hue / 60.0, x = 1 - Math.Abs(h % 2 - 1);
            double r, g, b;
            if (h < 1) { r = 1; g = x; b = 0; }
            else if (h < 2) { r = x; g = 1; b = 0; }
            else if (h < 3) { r = 0; g = 1; b = x; }
            else if (h < 4) { r = 0; g = x; b = 1; }
            else if (h < 5) { r = x; g = 0; b = 1; }
            else { r = 1; g = 0; b = x; }
            Func<double, int> v = c => (int)Math.Round(255 * (1 - strength * (1 - c)));
            return string.Format("#{0:X2}{1:X2}{2:X2}", v(r), v(g), v(b));
        }

        /// <summary>Picked colors in their usual order, each once.</summary>
        public static List<LampHue> Tidy(IEnumerable<LampHue> picked)
        {
            var all = (LampHue[])Enum.GetValues(typeof(LampHue));
            return picked == null ? new List<LampHue>() : all.Where(h => picked.Contains(h)).ToList();
        }

        /// <summary>Colors from the wheel: each once (as many as you like).</summary>
        public static List<LampCustomColor> Tidy(IEnumerable<LampCustomColor> custom)
        {
            var list = new List<LampCustomColor>();
            foreach (var c in custom ?? new LampCustomColor[0])
            {
                string hex = c == null ? null : Hex(c.Color);
                var same = list.FirstOrDefault(o => o.Color == hex);
                if (hex == null) continue;
                if (same != null) same.On |= c.On;
                else list.Add(new LampCustomColor { Color = hex, On = c.On });
            }
            return list;
        }

        LampPalette(int[] colors, bool pastel)
        {
            this.colors = colors;
            soft = pastel ? 1 : 0;
            if (colors == null) Light = pastel ? Color.FromRgb(255, 150, 115) : Color.FromRgb(255, 90, 45);   // warm red
            else
            {
                // The colors picked, all together.
                Light = Color.FromRgb((byte)colors.Average(c => Glow(2 * c + soft).R), (byte)colors.Average(c => Glow(2 * c + soft).G), (byte)colors.Average(c => Glow(2 * c + soft).B));
                LightMuch = colors.Length == 1 && !pastel ? 0.85 : 0.6;
            }
        }

        /// <summary>Whether letters of text each get a color (otherwise lamps keep the ones they have).</summary>
        public bool ColorsLetters { get { return colors != null; } }

        /// <summary>A lamp's color while it blinks: the usual one it has, or one of the colors mixed together.</summary>
        public ushort Blinking(byte usual, double pick) { return (ushort)(2 * (colors == null ? usual : colors[(int)(pick * colors.Length)]) + soft); }

        /// <summary>The color of a letter of text: the colors in turn.</summary>
        public ushort Letter(int letter) { return (ushort)(2 * colors[letter % colors.Length] + soft); }

        /// <summary>Test copies: what it's made of.</summary>
        public override string ToString()
        {
            return (colors == null ? "usual" : string.Join("/", colors.Select(c => names[2 * c]))) + (soft > 0 ? ", pastel" : "");
        }

        static Shades Shade(Color off, Color lit, Color hot, Color glow) { return new Shades { Off = off, Lit = lit, Hot = hot, Glow = glow, Core = Color.FromRgb(255, 240, 220) }; }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
