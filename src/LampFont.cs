using System;
using System.Collections.Generic;
using System.Linq;

namespace ReadyLauncher
{
    /// <summary>
    /// Letters made of lamps, for spelling words on the lamp strip: letters, digits and common punctuation. The ones
    /// in upper and lower case are in LampFontMixed.cs. Anything else shows as a question mark.
    /// </summary>
    internal partial class LampFont
    {
        /// <summary>Six lamps high, the full height of a strip of two tiers: capitals only (small letters are shown as capitals).</summary>
        public static readonly LampFont Capitals = new LampFont(6, 3, false, new Dictionary<char, string[]>
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
        });

        /// <summary>How many lamps high the letters are.</summary>
        public readonly int Height;
        readonly int spaceWidth;
        readonly bool lowercase;
        readonly Dictionary<char, string[]> glyphs;

        LampFont(int height, int spaceWidth, bool lowercase, Dictionary<char, string[]> glyphs)
        {
            Height = height;
            this.spaceWidth = spaceWidth;
            this.lowercase = lowercase;
            this.glyphs = glyphs;
        }

        string[] Glyph(char c)
        {
            string[] g;
            return glyphs.TryGetValue(lowercase ? c : char.ToUpperInvariant(c), out g) ? g : glyphs['?'];
        }

        const int LetterShift = 16;

        /// <summary>
        /// One column of a letter's lamps: bit r is row r from the top, with which letter of the text it is above those
        /// (0 for an empty column).
        /// </summary>
        int ColumnOf(string[] g, int x, int letter)
        {
            int bits = 0;
            for (int r = 0; r < Height; r++) if (g[r][x] == '#') bits |= 1 << r;
            return bits == 0 ? 0 : bits | letter << LetterShift;
        }

        /// <summary>Which letter of the text (spaces not counted) a lit column belongs to.</summary>
        public static int LetterOf(int column) { return column >> LetterShift; }

        /// <summary>
        /// The text as columns of lamps, left to right: bit r of each is row r from the top (see LetterOf for the rest).
        /// Letters have one empty column between them, and a space is a few empty columns.
        /// </summary>
        public List<int> Columns(string text)
        {
            var cols = new List<int>();
            int letter = 0;
            bool first = true;
            foreach (char c in (text ?? "").Trim())
            {
                if (char.IsWhiteSpace(c)) { cols.AddRange(new int[spaceWidth]); first = true; continue; }
                var g = Glyph(c);
                if (!first) cols.Add(0);
                first = false;
                for (int x = 0; x < g[0].Length; x++) cols.Add(ColumnOf(g, x, letter));
                letter++;
            }
            return cols;
        }

        /// <summary>
        /// The text one letter to a cell of <paramref name="cell"/> columns (a narrow letter centered in its cell, a
        /// space an empty cell), so that on the strip each letter sits in a lamp module of its own.
        /// </summary>
        public List<int> CellColumns(string text, int cell)
        {
            var cols = new List<int>();
            int letter = 0;
            foreach (char c in text ?? "")
            {
                var one = new int[cell];
                if (!char.IsWhiteSpace(c))
                {
                    var g = Glyph(c);
                    int w = Math.Min(cell, g[0].Length), pad = (cell - w) / 2;
                    for (int x = 0; x < w; x++) one[pad + x] = ColumnOf(g, x, letter);
                    letter++;
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
