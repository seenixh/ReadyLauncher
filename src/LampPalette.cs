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

        /// <summary>How many colors from the wheel can be kept.</summary>
        public const int MaxCustom = 6;


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
            switch (s.LampColors)
            {
                case LampColors.Picked:
                    var picked = Tidy(s.LampPicked).Select(h => Number(h)).Concat(Tidy(s.LampCustom).Where(c => c.On).Select(c => Number(c.Color))).ToArray();
                    return new LampPalette(picked.Length == 0 ? null : picked, s.LampPastel);
                default: return new LampPalette(null, s.LampPastel);
            }
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

        /// <summary>Picked colors in their usual order, each once.</summary>
        public static List<LampHue> Tidy(IEnumerable<LampHue> picked)
        {
            var all = (LampHue[])Enum.GetValues(typeof(LampHue));
            return picked == null ? new List<LampHue>() : all.Where(h => picked.Contains(h)).ToList();
        }

        /// <summary>Colors from the wheel: each once, and no more than can be kept (ones ticked first).</summary>
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
            while (list.Count > MaxCustom) list.Remove(list.FirstOrDefault(c => !c.On) ?? list[list.Count - 1]);
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
