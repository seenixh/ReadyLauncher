using System;
using System.Collections.Generic;
using System.Linq;

namespace ReadyLauncher
{
    /// <summary>
    /// A small lamp font, six lamps high (the strip's full height), for spelling words on the lamp strip: capital
    /// letters (small letters are shown as capitals), digits and common punctuation. Anything else shows as a
    /// question mark.
    /// </summary>
    internal static class LampFont
    {
        public const int Height = 6;
        const int SpaceWidth = 3;

        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'A', new[] { ".##.", "#..#", "#..#", "####", "#..#", "#..#" } },
            { 'B', new[] { "###.", "#..#", "###.", "#..#", "#..#", "###." } },
            { 'C', new[] { ".###", "#...", "#...", "#...", "#...", ".###" } },
            { 'D', new[] { "###.", "#..#", "#..#", "#..#", "#..#", "###." } },
            { 'E', new[] { "####", "#...", "###.", "#...", "#...", "####" } },
            { 'F', new[] { "####", "#...", "###.", "#...", "#...", "#..." } },
            { 'G', new[] { ".###", "#...", "#...", "#.##", "#..#", ".###" } },
            { 'H', new[] { "#..#", "#..#", "####", "#..#", "#..#", "#..#" } },
            { 'I', new[] { "###", ".#.", ".#.", ".#.", ".#.", "###" } },
            { 'J', new[] { "..##", "...#", "...#", "...#", "#..#", ".##." } },
            { 'K', new[] { "#..#", "#.#.", "##..", "##..", "#.#.", "#..#" } },
            { 'L', new[] { "#...", "#...", "#...", "#...", "#...", "####" } },
            { 'M', new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#" } },
            { 'N', new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" } },
            { 'O', new[] { ".##.", "#..#", "#..#", "#..#", "#..#", ".##." } },
            { 'P', new[] { "###.", "#..#", "#..#", "###.", "#...", "#..." } },
            { 'Q', new[] { ".##.", "#..#", "#..#", "#..#", "#.#.", ".#.#" } },
            { 'R', new[] { "###.", "#..#", "#..#", "###.", "#.#.", "#..#" } },
            { 'S', new[] { ".###", "#...", ".##.", "...#", "...#", "###." } },
            { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#.." } },
            { 'U', new[] { "#..#", "#..#", "#..#", "#..#", "#..#", ".##." } },
            { 'V', new[] { "#...#", "#...#", "#...#", ".#.#.", ".#.#.", "..#.." } },
            { 'W', new[] { "#...#", "#...#", "#...#", "#.#.#", "##.##", "#...#" } },
            { 'X', new[] { "#...#", ".#.#.", "..#..", "..#..", ".#.#.", "#...#" } },
            { 'Y', new[] { "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." } },
            { 'Z', new[] { "#####", "...#.", "..#..", ".#...", "#....", "#####" } },
            { '0', new[] { ".##.", "#..#", "#.##", "##.#", "#..#", ".##." } },
            { '1', new[] { ".#.", "##.", ".#.", ".#.", ".#.", "###" } },
            { '2', new[] { ".##.", "#..#", "...#", "..#.", ".#..", "####" } },
            { '3', new[] { "###.", "...#", ".##.", "...#", "...#", "###." } },
            { '4', new[] { "#..#", "#..#", "#..#", "####", "...#", "...#" } },
            { '5', new[] { "####", "#...", "###.", "...#", "...#", "###." } },
            { '6', new[] { ".##.", "#...", "###.", "#..#", "#..#", ".##." } },
            { '7', new[] { "####", "...#", "..#.", ".#..", ".#..", ".#.." } },
            { '8', new[] { ".##.", "#..#", ".##.", "#..#", "#..#", ".##." } },
            { '9', new[] { ".##.", "#..#", "#..#", ".###", "...#", ".##." } },
            { '.', new[] { ".", ".", ".", ".", ".", "#" } },
            { ',', new[] { "..", "..", "..", "..", ".#", "#." } },
            { '!', new[] { "#", "#", "#", "#", ".", "#" } },
            { '?', new[] { ".##.", "#..#", "..#.", ".#..", "....", ".#.." } },
            { '-', new[] { "...", "...", "###", "...", "...", "..." } },
            { '_', new[] { "....", "....", "....", "....", "....", "####" } },
            { ':', new[] { ".", "#", ".", ".", "#", "." } },
            { ';', new[] { "..", ".#", "..", "..", ".#", "#." } },
            { '\'', new[] { "#", "#", ".", ".", ".", "." } },
            { '"', new[] { "#.#", "#.#", "...", "...", "...", "..." } },
            { '/', new[] { "...#", "...#", "..#.", ".#..", "#...", "#..." } },
            { '+', new[] { "...", ".#.", "###", ".#.", "...", "..." } },
            { '=', new[] { "...", "###", "...", "###", "...", "..." } },
            { '*', new[] { "...", "#.#", ".#.", "#.#", "...", "..." } },
            { '(', new[] { ".#", "#.", "#.", "#.", "#.", ".#" } },
            { ')', new[] { "#.", ".#", ".#", ".#", ".#", "#." } },
            { '<', new[] { "..#", ".#.", "#..", "#..", ".#.", "..#" } },
            { '>', new[] { "#..", ".#.", "..#", "..#", ".#.", "#.." } },
            { '#', new[] { ".#.#.", "#####", ".#.#.", ".#.#.", "#####", ".#.#." } },
            { '%', new[] { "##..#", "##.#.", "..#..", "..#..", ".#.##", "#..##" } },
            { '&', new[] { ".#..", "#.#.", ".#..", "#.#.", "#..#", ".##." } },
        };

        /// <summary>
        /// The text as columns of lamps, left to right: bit r of each is row r from the top. Letters have one empty
        /// column between them, and a space is three empty columns.
        /// </summary>
        public static List<byte> Columns(string text)
        {
            var cols = new List<byte>();
            bool first = true;
            foreach (char raw in (text ?? "").Trim())
            {
                char c = char.ToUpperInvariant(raw);
                if (char.IsWhiteSpace(c)) { cols.AddRange(new byte[SpaceWidth]); first = true; continue; }
                string[] g;
                if (!Glyphs.TryGetValue(c, out g)) g = Glyphs['?'];
                if (!first) cols.Add(0);
                first = false;
                for (int x = 0; x < g[0].Length; x++)
                {
                    byte bits = 0;
                    for (int r = 0; r < Height; r++) if (g[r][x] == '#') bits |= (byte)(1 << r);
                    cols.Add(bits);
                }
            }
            return cols;
        }

        /// <summary>
        /// The text one letter to a cell of <paramref name="cell"/> columns (a narrow letter centered in its cell, a
        /// space an empty cell), so that on the strip each letter sits in a lamp module of its own.
        /// </summary>
        public static List<byte> CellColumns(string text, int cell)
        {
            var cols = new List<byte>();
            foreach (char raw in text ?? "")
            {
                char c = char.ToUpperInvariant(raw);
                var one = new byte[cell];
                string[] g;
                if (!char.IsWhiteSpace(c))
                {
                    if (!Glyphs.TryGetValue(c, out g)) g = Glyphs['?'];
                    int w = Math.Min(cell, g[0].Length), pad = (cell - w) / 2;
                    for (int x = 0; x < w; x++)
                        for (int r = 0; r < Height; r++) if (g[r][x] == '#') one[pad + x] |= (byte)(1 << r);
                }
                cols.AddRange(one);
            }
            return cols;
        }

        /// <summary>
        /// The text in lines of at most <paramref name="cells"/> letters, for flashing one after another: as many whole
        /// words as fit, a word too long for the strip in parts, and a new line at each line break.
        /// </summary>
        public static List<string> Lines(string text, int cells)
        {
            var lines = new List<string>();
            foreach (var part in (text ?? "").Split('\n')) lines.AddRange(LinesOf(part, cells));   // a line break always starts a new line
            return lines;
        }

        static List<string> LinesOf(string text, int cells)
        {
            var lines = new List<string>();
            string line = "";
            cells = Math.Max(1, cells);
            foreach (var word in (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > cells)
                {
                    if (line.Length > 0) { lines.Add(line); line = ""; }
                    for (int k = 0; k < word.Length; k += cells) lines.Add(word.Substring(k, Math.Min(cells, word.Length - k)));
                    continue;
                }
                string longer = line.Length == 0 ? word : line + " " + word;
                if (longer.Length <= cells) { line = longer; continue; }
                lines.Add(line);
                line = word;
            }
            if (line.Length > 0) lines.Add(line);
            return lines;
        }
    }
}
