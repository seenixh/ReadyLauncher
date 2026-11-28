using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>
    /// A strip of blinking indicator lamps, like the front of an early-1980s mainframe: red and amber lamps in modules
    /// of 5 × 3. As its own strip it's six to ten lamps high, in two or three tiers of modules (some taller than three
    /// lamps), under a dark hood, above a glossy sill; filling a space (behind the quick launch icons) it takes as many tiers as fit. Rows behave like a computer at work: some march like
    /// shift registers, some count in binary, some flicker, some hold steady. A lamp warms up in about 30 ms and fades
    /// over about 120 ms, like a small bulb.
    /// <para>
    /// It's drawn into one picture: the panel once per size, then each frame the lamps are stamped onto it as small
    /// ready-made pictures (hundreds of gradient circles redrawn by WPF each frame cost most of a processor core).
    /// It only updates while it's on screen.
    /// </para>
    /// </summary>
    internal class LampPanel : FrameworkElement
    {
        const int Rows = 3, Cols = 5, Levels = 10;   // modules are this many lamps high (on their own strip some are taller) and wide
        const double ModW = 32, PitchX = 5.8, PitchY = 5.0, Lens = 3.8, Pad = 8;
        const double TierGap = 2.5;   // between tiers, on top of the space between rows
        public const double SillHeight = 7;         // the strip's own sill (not when filling a space)
        const double SparsePitchX = 8.4, SparsePitchY = 7.4, SparseMargin = 5;   // the monitor's window: lamps well apart
        const double ScrollSpeed = 11;              // text scrolling at 100%: lamp columns a second
        const int ScrollGap = 12;                   // dark columns after the text before it comes round again
        const double FlashOn = 1.4, FlashOff = 0.45;   // text flashing: seconds lit, then dark
        const double StillOn = 4;                   // text that stays still, too long to fit at once: seconds for each part
        // Text coming out of the blinking: seconds of blinking, then of lamps settling into the text a few at a time, of the
        // text alone, and of lamps going back to blinking; and how much of the settling each lamp takes for itself.
        const double EmergeBlink = 4, EmergeFade = 6, EmergeHold = 5, EmergeSoft = 0.22;
        // An answer (the Magic 8 Ball's) comes out of the blinking a little quicker, once, and goes back.
        const double AnswerBlink = 0.3, AnswerFade = 3.5;
        const int FrameMs = 40;   // 25 frames a second
        const int FastFrameMs = 20;   // while text scrolls faster than that can show smoothly
        static readonly double[] BasePeriod = { 3.2, 0.22, 0.28, 0.12 };   // steady, shift register, counter, flicker

        static readonly Random rnd = new Random();

        class Sprite { public int W, H; public uint[] Px; }

        readonly DispatcherTimer timer;
        readonly Stopwatch clock = new Stopwatch();
        static readonly List<LampPanel> onScreen = new List<LampPanel>();
        LampActivity activity;   // as chosen
        LampActivity current;    // what the lamps are doing now: the chosen one, or for Random the one it has switched to
        double sweepStart;
        double lastSlow;         // the pace the rows' waits were last drawn for
        readonly bool fill, sparse;
        int rowsHigh;            // six to ten lamps high (more than six: text in upper and lower case)
        double sillTop;
        double builtWidth = -1, builtHeight = -1, modX0, simTime, last;
        double sx = 1, sy = 1, spritesSx, spritesSy;
        double plateTop, plateBottom;
        int tiers, mods, n, units, pw, ph;
        int[] tierRows;          // each tier's rows of lamps, its first row, its top and its height
        int[] tierFirst;
        double[] tierTop, tierHeight;
        double spritesTier;
        double[] lx, ly, lb, lmax, avg, lsweep;
        int[] lmod;              // each lamp's module
        byte[] lon, lcol, lhere;
        ushort[] lshow;          // the color each lamp has now (a number: see LampPalette)
        double[] lpick;          // which of the palette's colors it has (0 to 1)
        double[] lheld, lorder;  // text coming out of the blinking: how far each lamp has settled into it (0 to 1), and when it does
        bool[] lspell;           // whether the text lights it,
        ushort[] lletter;        // and in which color
        LampPalette palette = LampPalette.Usual;
        LampPalette lightFor;    // the palette the light around the lamps was made for
        LampPalette[] thirds = new LampPalette[3];   // the left, middle and right thirds' own colors (null: the strip's)
        byte[] lzone;            // which third each lamp is in (0 left, 1 middle, 2 right)
        bool[] ldead;            // burned out (they light for text all the same: a letter needs every lamp)
        int[] lgx, lgy;          // each lamp's column and row in the strip's grid (for spelling text)
        int gridCols, gridRows;
        int sweepSpan;   // how many steps an alert's band of light moves through, end to end
        byte[] uKind;
        double[] uPeriod, uTimer;
        sbyte[] uDir;
        int[] uVal;
        uint[] background, frame;
        WriteableBitmap wb;
        Sprite[][] lensSpr, glowSpr, reflSpr;
        Sprite[] hazeSpr, hoodSpr, sillSpr;

        /// <summary>Rounded corners to clip to, where the strip meets a rounded edge.</summary>
        public CornerRadius Corners { get; set; }

        /// <summary>Room at the left or right end kept free of lamps (where the monitor stands): a plain panel instead.</summary>
        public double ReserveLeft { get; set; }
        public double ReserveRight { get; set; }

        Func<Point, bool> covered;

        /// <summary>Test copies: how many lamps it has (the monitor's window leaves some places empty).</summary>
        public int LampsHere { get { return lhere == null ? 0 : lhere.Count(h => h == 1); } }

        /// <summary>
        /// The monitor's window: where something stands in front of the lamps (the LED board, or the 8 ball). A lamp there,
        /// or so near that it would show only in part, isn't fitted.
        /// </summary>
        public Func<Point, bool> Covered
        {
            get { return covered; }
            set
            {
                covered = value;
                if (wb != null) { builtWidth = -1; Build(ActualWidth, ActualHeight); ComposeFrame(); }   // laid out again
            }
        }

        /// <param name="rows">How many lamps high, 6 to 10 (six: text in capitals; more: in upper and lower case).</param>
        /// <param name="fill">Fill the space it's given (as a background) instead of being its own strip.</param>
        /// <param name="sparse">The monitor's window: fills its space with lamps spaced well apart, fewer of them lit.</param>
        public LampPanel(LampActivity mode, int rows, bool fill = false, bool sparse = false)
        {
            activity = mode;
            current = mode == LampActivity.Random ? RandomNow() : mode;
            this.sparse = sparse;
            this.fill = fill || sparse;
            RowsHigh = rows;
            ClipToBounds = true;
            IsHitTestVisible = false;
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(FrameMs) };
            timer.Tick += delegate { Tick(); };
            IsVisibleChanged += delegate { UpdateRunning(); };
            Loaded += delegate { if (!onScreen.Contains(this)) onScreen.Add(this); UpdateRunning(); };
            Unloaded += delegate { onScreen.Remove(this); timer.Stop(); clock.Stop(); };
        }

        /// <summary>
        /// How many lamps high it is, 6 to 10: at six, text is in capitals; above that, in upper and lower case. Filling a
        /// space, it only changes the text (letters are no higher than there's room for).
        /// </summary>
        public int RowsHigh
        {
            get { return rowsHigh; }
            set
            {
                rowsHigh = Math.Max(LampFont.MinRows, Math.Min(LampFont.MaxRows, value));
                sillTop = Math.Ceiling(PlateTop + Tiers(rowsHigh).Sum(r => TierHigh(r)) + 2);
                if (!fill) Height = sillTop + SillHeight;
            }
        }

        /// <summary>The colors of the lamps. The color of a lamp that's dark hardly shows, so this can change at any time.</summary>
        public LampPalette Palette
        {
            get { return palette; }
            set { palette = value ?? LampPalette.Usual; }
        }

        /// <summary>
        /// Colors of their own for the strip's left, middle and right thirds (null, or null for one: the strip's). They can
        /// change at any time, like the strip's.
        /// </summary>
        public LampPalette[] Thirds
        {
            set { thirds = value != null && value.Length == 3 ? value : new LampPalette[3]; }
        }

        /// <summary>Which third of the strip a lamp is in, by where it is from end to end.</summary>
        int ZoneOf(int i)
        {
            if (lzone == null || lzone.Length != n)
            {
                lzone = new byte[n];
                double lo = double.MaxValue, hi = double.MinValue;
                for (int k = 0; k < n; k++) { lo = Math.Min(lo, lx[k]); hi = Math.Max(hi, lx[k]); }
                double span = Math.Max(1e-6, hi - lo);
                for (int k = 0; k < n; k++) lzone[k] = (byte)Math.Min(2, (int)(3 * (lx[k] - lo) / span));
            }
            return lzone[i];
        }

        /// <summary>A lamp's colors: its third's own, or the strip's.</summary>
        LampPalette PaletteOf(int i)
        {
            if (thirds[0] == null && thirds[1] == null && thirds[2] == null) return palette;
            return thirds[ZoneOf(i)] ?? palette;
        }

        /// <summary>A lamp's color while it blinks: its own, or one of the palette's, mixed like strings of lights.</summary>
        ushort BlinkColor(int i) { return PaletteOf(i).Blinking(lcol[i], lpick[i]); }

        const double PlateTop = 8;   // the strip's own: under the hood

        /// <summary>The tiers of modules of a strip this many lamps high: how many rows each has.</summary>
        static int[] Tiers(int rows)
        {
            switch (rows)
            {
                case 7: return new[] { 4, 3 };
                case 8: return new[] { 4, 4 };
                case 9: return new[] { 3, 3, 3 };
                case 10: return new[] { 5, 5 };
                default: return new[] { 3, 3 };
            }
        }

        static double TierHigh(int rows) { return rows * PitchY + TierGap; }

        /// <summary>The letters it spells with: as high as was chosen, or as there's room for.</summary>
        LampFont Font { get { return LampFont.For(Math.Min(rowsHigh, gridRows), textTails); } }

        /// <summary>Filling a space (behind the quick launch icons) rather than being its own strip.</summary>
        public bool Fills { get { return fill && !sparse; } }

        /// <summary>The lamps in the monitor's window.</summary>
        public bool InMonitor { get { return sparse; } }

        /// <summary>Every lamp strip shown right now (in the launcher, its own window or the quick launch strip).</summary>
        public static List<LampPanel> OnScreen { get { return onScreen.ToList(); } }

        /// <summary>How busy the lamps look. Random switches between standby, idle, computing and alert on its own now and then.</summary>
        public LampActivity Activity
        {
            get { return activity; }
            set
            {
                if (value == activity) return;
                activity = value;
                var now = value == LampActivity.Random ? RandomNow() : value;
                if (now != current) { current = now; sweepStart = simTime; }   // an alert's first sweep starts from the left end
            }
        }

        /// <summary>What the lamps are doing right now: the chosen activity, or the one Random has switched to.</summary>
        public LampActivity Current { get { return current; } }

        // Random: one turn for every strip on screen (the strip and its monitor), so they all switch together.
        static readonly Stopwatch randomClock = Stopwatch.StartNew();
        static LampActivity randomNow;
        static double randomNext = -1;

        /// <summary>Random: starts a new turn right away, on one of standby, idle, computing and alert other than <paramref name="avoid"/>.</summary>
        public static void NewRandomTurn(LampActivity avoid)
        {
            var choices = new List<LampActivity> { LampActivity.Standby, LampActivity.Idle, LampActivity.Computing, LampActivity.Alert };
            choices.Remove(avoid);
            randomNow = choices[rnd.Next(choices.Count)];
            randomNext = randomClock.Elapsed.TotalSeconds + RandomSeconds();
        }

        // Text spelled out on the strips (not the monitor), the same on every strip on screen.
        static string text = "";
        static LampTextEffect textEffect;
        static string answer;    // shown once, coming out of the blinking, in place of the text (null: none)
        static int answerParts = 1;   // in how many parts it shows, as the strip has room
        static double answerHold = 7;   // seconds it stays, once it's out
        static bool answerAtOnce;   // it shows at once (random numbers), rather than coming out of the blinking
        static bool answerKept;     // it stays until it's cleared (the Magic 8 Ball's, with Instant Response)
        static int textVersion;
        static bool textSides;   // the lamps on either side of the text keep blinking
        static int textTails;    // how many rows below the line are kept for tails (0: none, letters fill the strip's height)
        static double textSpeed = 1;   // how fast text scrolls: 1 is the usual speed
        int seenText = -1, seenCols = -1;
        LampFont seenFont;
        int textFrom, textTo;    // the columns the text has to itself right now (all of them, unless blinking beside it)
        List<int> textCols;
        List<string> textLines;   // flashing: the lines shown one after another
        double textClock;         // flashing, or coming out of the blinking: seconds so far
        bool emergeRising;        // coming out of the blinking (or all there), rather than going back to it
        int emergeTurn = -1;      // which time it's doing that: each has the lamps settling in another order
        double textMoved;         // scrolling: columns moved so far

        /// <summary>
        /// Spells <paramref name="words"/> on the strips, scrolling, flashing, staying still or coming out of the blinking and
        /// going back to it, instead of only blinking (None: blinking again). With <paramref name="sides"/>, the lamps on either side of the text keep blinking.
        /// <paramref name="speed"/> is how fast it scrolls, in percent (100 is the usual speed). <paramref name="tails"/>
        /// is how many of the rows are kept below the line for tails (0: none).
        /// </summary>
        public static void ShowText(string words, LampTextEffect effect, bool sides, int speed, int tails)
        {
            words = (words ?? "").Trim();
            textSides = sides;
            textTails = tails;
            textSpeed = Math.Max(1, speed) / 100.0;   // text already scrolling goes on from where it is
            if (words == text && effect == textEffect) return;
            text = words;
            textEffect = effect;
            textVersion++;
        }

        /// <summary>
        /// Shows <paramref name="words"/> once on the strips, coming out of the blinking and going back to it (each part in
        /// turn, if they don't fit at once), in place of whatever they spell now; then they go back to that. With
        /// <paramref name="atOnce"/>, they're there at once (each part), and only going back to blinking takes a while.
        /// </summary>
        public static void ShowAnswer(string words, double hold = 7, bool atOnce = false)
        {
            answer = (words ?? "").Trim();
            answerHold = hold;
            answerAtOnce = atOnce;
            answerKept = false;
            textVersion++;
        }

        /// <summary>
        /// Shows <paramref name="words"/> on the strips at once, in place of whatever they spell now, and keeps them there
        /// (each part in turn, if they don't fit at once) until <see cref="ClearAnswer"/> or another answer.
        /// </summary>
        public static void KeepAnswer(string words)
        {
            answer = (words ?? "").Trim();
            answerAtOnce = true;
            answerKept = true;
            textVersion++;
        }

        /// <summary>Whether the answer showing now stays until it's cleared.</summary>
        public static bool AnswerKept { get { return answer != null && answerKept; } }

        /// <summary>Takes away the answer showing now (if there is one): the strips go back to what they were showing.</summary>
        public static void ClearAnswer() { EndAnswer(); }

        /// <summary>The answer has come and gone.</summary>
        public static event Action AnswerOver;

        /// <summary>Test copies: the answer showing now, or null.</summary>
        public static string AnswerNow { get { return answer; } }

        static void EndAnswer()
        {
            if (answer == null) return;
            answer = null;
            answerKept = false;
            textVersion++;
            var h = AnswerOver;
            if (h != null) h();
        }

        /// <summary>What the strips spell right now: an answer, or their own text.</summary>
        static string Words { get { return answer ?? text; } }
        static LampTextEffect TextEffectNow { get { return answer != null ? (answerKept ? LampTextEffect.Still : LampTextEffect.Emerge) : textEffect; } }   // one that stays: still

        // How long text takes to come out of the blinking, stay, and go back: an answer is quicker, and one shown at once
        // (random numbers) doesn't wait or come out of the blinking at all.
        static bool AtOnce { get { return answer != null && answerAtOnce; } }
        static double EBlink { get { return AtOnce ? 0 : answer != null ? AnswerBlink : EmergeBlink; } }
        static double EFade { get { return answer != null ? AnswerFade : EmergeFade; } }
        static double ECome { get { return AtOnce ? 0 : EFade; } }
        static double EHold { get { return answer != null ? (answerParts > 1 ? answerHold / 2 : answerHold) : EmergeHold; } }   // an answer in parts: each a shorter time
        static double EEach { get { return EBlink + ECome + EHold + EFade; } }

        /// <summary>Spelling text right now (it needs a strip at least six lamps high).</summary>
        bool ShowingText { get { return !sparse && TextEffectNow != LampTextEffect.None && Words.Length > 0 && gridRows >= LampFont.Capitals.Height; } }

        /// <summary>Text that comes out of the blinking lamps and goes back to them: the rows go on with their own blinking.</summary>
        bool Emerging { get { return ShowingText && TextEffectNow == LampTextEffect.Emerge; } }

        /// <summary>Scrolling text too fast for the usual frame rate to show every step of it.</summary>
        bool ScrollingFast { get { return ShowingText && TextEffectNow == LampTextEffect.Scroll && ScrollSpeed * textSpeed > 15; } }

        /// <summary>Lights the lamps that spell the text where it has got to: scrolling in from the right, or flashing.</summary>
        void StepText(double dt)
        {
            var font = Font;
            if (seenText != textVersion || seenCols != gridCols || seenFont != font)
            {
                seenText = textVersion;
                seenCols = gridCols;
                seenFont = font;
                textCols = font.Columns(Words);
                textLines = LampFont.Lines(Words, gridCols / Cols);
                if (answer != null) answerParts = Math.Max(1, textLines.Count);
                textClock = answer != null ? 0 : textEffect == LampTextEffect.Emerge ? EmergeBlink - 1 : 0;   // it starts to show after a second
                textMoved = 0;
                emergeTurn = -1;
            }
            textClock += dt;
            if (answer != null && !answerKept && textClock >= EEach * Math.Max(1, textLines.Count)) { EndAnswer(); return; }   // each part once
            var effect = TextEffectNow;
            List<int> shown;
            int x0, width;
            bool emerging = effect == LampTextEffect.Emerge;
            if (effect == LampTextEffect.Flash || effect == LampTextEffect.Still || emerging)
            {
                // As many words as fit, centered, lit then dark; then the next words. Each letter gets a module of
                // its own (they're a letter wide), so the gaps between modules never cut through a letter. Staying
                // still, they're always lit, and change (to the next words) only when they don't all fit at once; coming out of
                // the blinking, the next words come the next time round.
                bool still = effect == LampTextEffect.Still || emerging;
                double each = emerging ? EEach : still ? StillOn : FlashOn + FlashOff;
                int k = (int)(textClock / each) % Math.Max(1, textLines.Count);
                bool lit = still || textClock % each < FlashOn;
                string line = textLines.Count > 0 ? textLines[k] : "";
                var cells = font.CellColumns(line, Cols);
                shown = lit && line.Length > 0 ? cells : null;
                x0 = (gridCols / Cols - line.Length) / 2 * Cols;
                // Its edges are its first and last lit columns (a narrow letter doesn't fill its whole module).
                int first = cells.FindIndex(b => b != 0), last = cells.FindLastIndex(b => b != 0);
                if (first < 0) width = 0;
                else { x0 += first; width = last - first + 1; shown = shown == null ? null : cells.GetRange(first, width); }
            }
            else
            {
                // In from the right edge, out past the left, a short dark gap, and round again.
                int period = gridCols + textCols.Count + ScrollGap;
                textMoved = (textMoved + dt * ScrollSpeed * textSpeed) % period;
                int step = (int)textMoved;
                shown = textCols;
                width = textCols.Count;
                x0 = gridCols - step;
            }
            // Blinking beside the text: the text keeps just its own columns, from its first lit column to its last
            // (moving along with scrolling text), and the rest of the strip blinks as usual right up to its edges,
            // all of it while scrolling text is off the strip.
            if (textSides)
            {
                bool onStrip = width > 0 && x0 + width > 0 && x0 < gridCols;
                textFrom = onStrip ? Math.Max(0, x0) : 0;
                textTo = onStrip ? Math.Min(gridCols, x0 + width) : 0;
            }
            else
            {
                textFrom = 0;
                textTo = gridCols;
            }
            int top = (gridRows - font.Height) / 2;
            double came = emerging ? StepEmerge() : 1;
            for (int i = 0; i < n; i++)
            {
                if (lgx[i] < textFrom || lgx[i] >= textTo) continue;   // blinking as usual
                int row = lgy[i] - top, col = lgx[i] - x0;
                bool on = shown != null && row >= 0 && row < font.Height && col >= 0 && col < shown.Count && ((shown[col] >> row) & 1) != 0;
                if (emerging)
                {
                    // (its own blinking goes on underneath)
                    double held = Math.Max(0, Math.Min(1, (came * (1 + EmergeSoft) - lorder[i]) / EmergeSoft));
                    lheld[i] = held * held * (3 - 2 * held);
                    lspell[i] = on;
                    var its = PaletteOf(i);
                    lletter[i] = on && its.ColorsLetters ? its.Letter(LampFont.LetterOf(shown[col])) : BlinkColor(i);
                    continue;
                }
                lon[i] = (byte)(on ? 1 : 0);
                var pal = PaletteOf(i);
                if (on && pal.ColorsLetters) lshow[i] = pal.Letter(LampFont.LetterOf(shown[col]));   // each letter a color of its own
            }
        }

        /// <summary>How much of the text has come out of the blinking by now (0: all blinking, 1: just the text).</summary>
        double StepEmerge()
        {
            double t = textClock % EEach - EBlink, come = ECome, fade = EFade, hold = EHold;
            emergeRising = t >= 0 && t < come + hold;
            int turn = (int)(textClock / EEach) * 2 + (emergeRising ? 1 : 0);
            if (turn != emergeTurn)
            {
                emergeTurn = turn;
                for (int i = 0; i < n; i++) lorder[i] = rnd.NextDouble();
            }
            if (t < 0) return 0;
            if (t < come) return t / come;   // (never, shown at once)
            if (t < come + hold) return 1;
            return Math.Max(0, 1 - (t - come - hold) / fade);
        }

        /// <summary>Test copies: the columns the text has to itself ("from-to", end not included).</summary>
        public string TextStretch { get { return ShowingText ? textFrom + "-" + textTo : "no text"; } }

        /// <summary>Test copies: how many columns scrolling text has moved, the strip's size in lamps, and its frames.</summary>
        public string TextProgress
        {
            get
            {
                return textMoved.ToString("0.0", CultureInfo.InvariantCulture) + " columns; " + (Emerging ? EmergeReport() + "; " : "") + gridCols + " x " + gridRows + " lamps, "
                    + ActualHeight.ToString("0.#", CultureInfo.InvariantCulture) + " high, letters " + Font.Height + " high, a frame every "
                    + timer.Interval.TotalMilliseconds + " ms";
            }
        }

        /// <summary>Test copies: how far text has come out of the blinking: whether it's coming or going, how many of its lamps have
        /// settled, and how many lamps light that aren't the text's (blinking) and that are its own.</summary>
        string EmergeReport()
        {
            double t = textClock % EEach - EBlink;
            string now = (answer != null ? "answer '" + answer + "' " : "") + (t < 0 ? "blinking" : t < ECome ? "coming" : t < ECome + EHold ? "there" : "going");
            var mine = Enumerable.Range(0, n).Where(i => lgx[i] >= textFrom && lgx[i] < textTo).ToList();
            return now + " at " + t.ToString("0.0", CultureInfo.InvariantCulture) + " s, " + mine.Count(i => lheld[i] >= 0.999) + " settled and " + mine.Count(i => lheld[i] <= 0.001) + " free of " + mine.Count
                + ", text lamps lit " + mine.Count(i => lspell[i] && lb[i] * lmax[i] >= 0.5) + " of " + mine.Count(i => lspell[i]) + ", others lit " + mine.Count(i => !lspell[i] && lb[i] * lmax[i] >= 0.5);
        }

        /// <summary>Test copies: the colors of the lamps lit in each third of the strip.</summary>
        public string ThirdsReport()
        {
            var said = new List<string>();
            for (int z = 0; z < 3; z++)
            {
                int zone = z;
                var names = Enumerable.Range(0, n).Where(i => lhere[i] == 1 && lb[i] >= 0.5 && ZoneOf(i) == zone).Select(i => LampPalette.NameOf(lshow[i])).Distinct().OrderBy(x => x);
                said.Add(new[] { "left", "middle", "right" }[z] + ": " + string.Join("/", names));
            }
            return string.Join("; ", said);
        }

        /// <summary>Test copies: how many lit lamps have each color, and the colors of the text's letters in order.</summary>
        public string ColorReport()
        {
            var lit = Enumerable.Range(0, n).Where(i => lhere[i] == 1 && lb[i] >= 0.5).ToList();
            string counts = string.Join(", ", lit.GroupBy(i => lshow[i]).OrderBy(g => g.Key).Select(g => g.Count() + " " + LampPalette.NameOf(g.Key)));
            if (!ShowingText) return counts;
            var byColumn = lit.Where(i => lgx[i] >= textFrom && lgx[i] < textTo).GroupBy(i => lgx[i]).OrderBy(g => g.Key)
                .Select(g => string.Join("+", g.Select(i => lshow[i]).Distinct().OrderBy(c => c).Select(c => LampPalette.NameOf(c)))).ToList();
            return counts + "; across the text: " + string.Join(" ", byColumn.Where((c, k) => k == 0 || c != byColumn[k - 1]));
        }

        /// <summary>Test copies: saves the strip as it looks right now.</summary>
        public void SavePicture(string file)
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(wb.Clone()));
            using (var fs = System.IO.File.Create(file)) enc.Save(fs);
        }

        /// <summary>Test copies: the strip's lamps as rows of # (glowing) and . (dark).</summary>
        public string[] LitRows()
        {
            if (sparse || n == 0) return new string[0];
            var rows = new char[gridRows][];
            for (int r = 0; r < gridRows; r++) rows[r] = new string('.', gridCols).ToCharArray();
            for (int i = 0; i < n; i++) if (lb[i] >= 0.5) rows[lgy[i]][lgx[i]] = '#';   // what's glowing (an alert's sweep included)
            return rows.Select(r => new string(r)).ToArray();
        }

        /// <summary>
        /// The custom activity's levels (<see cref="LampActivity.Custom"/>): the settings' own, read every frame, so a change
        /// shows at once.
        /// </summary>
        public static Func<LampActivityLevels> CustomLevels = () => null;

        static LampActivityLevels CustomNow { get { return CustomLevels() ?? new LampActivityLevels(); } }

        static LampActivity RandomNow()
        {
            if (randomNext < 0) NewRandomTurn(LampActivity.Random);   // the first turn: any of the four
            else if (randomClock.Elapsed.TotalSeconds >= randomNext) NewRandomTurn(randomNow);
            return randomNow;
        }

        /// <summary>Random: how long each activity lasts, 20 to 60 seconds (test copies can set it).</summary>
        static double RandomSeconds()
        {
            double s;
            if (Program.TestMode && double.TryParse(Environment.GetEnvironmentVariable("READYLAUNCHER_LAMP_SWITCH") ?? "",
                    NumberStyles.Float, CultureInfo.InvariantCulture, out s) && s > 0) return s;
            return 20 + rnd.NextDouble() * 40;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo info)
        {
            base.OnRenderSizeChanged(info);
            Build(info.NewSize.Width, info.NewSize.Height);
            ComposeFrame();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            builtWidth = -1;
            Build(ActualWidth, ActualHeight);
            ComposeFrame();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (wb != null) dc.DrawImage(wb, new Rect(0, 0, pw / sx, ph / sy));
        }

        /// <summary>Updates only while it's on screen: a hidden launcher costs nothing.</summary>
        void UpdateRunning()
        {
            if (IsVisible && IsLoaded)
            {
                if (!timer.IsEnabled) { clock.Restart(); last = 0; timer.Start(); }
            }
            else { timer.Stop(); clock.Stop(); }
        }

        void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, Math.Max(0, now - last));
            last = now;
            Step(dt);
            ComposeFrame();
            int ms = ScrollingFast ? FastFrameMs : FrameMs;
            if (timer.Interval.TotalMilliseconds != ms) timer.Interval = TimeSpan.FromMilliseconds(ms);
        }

        // ---------- the lamps and how each row behaves ----------

        void Build(double width, double height)
        {
            if (width < 1 || height < 1) return;
            var dpi = VisualTreeHelper.GetDpi(this);
            if (Math.Abs(width - builtWidth) < 0.5 && Math.Abs(height - builtHeight) < 0.5 && dpi.DpiScaleX == sx && dpi.DpiScaleY == sy && wb != null) return;
            builtWidth = width;
            builtHeight = height;
            sx = dpi.DpiScaleX;
            sy = dpi.DpiScaleY;
            if (sparse) LayOutSparse(width, height); else LayOutModules(width, height);
            for (int i = 0; i < n; i++) lshow[i] = BlinkColor(i);   // right from the first picture

            pw = Math.Max(1, (int)Math.Ceiling(width * sx));
            ph = Math.Max(1, (int)Math.Ceiling(height * sy));
            background = Render(pw, ph, BuildStill(width, height));
            frame = new uint[pw * ph];
            wb = new WriteableBitmap(pw, ph, 96 * sx, 96 * sy, PixelFormats.Pbgra32, null);
            if (spritesSx != sx || spritesSy != sy || spritesTier != HazeTier) MakeSprites();
            Clip = Shapes.AnyRounding(Corners) ? Shapes.Rounded(new Rect(0, 0, width, height), Corners) : null;
            InvalidateVisual();
        }

        void Allocate()
        {
            lx = new double[n]; ly = new double[n]; lb = new double[n]; lmax = new double[n]; lsweep = new double[n];
            lgx = new int[n]; lgy = new int[n];
            lon = new byte[n]; lcol = new byte[n]; lhere = new byte[n]; lmod = new int[n]; ldead = new bool[n]; lshow = new ushort[n]; lpick = new double[n];
            lheld = new double[n]; lorder = new double[n]; lspell = new bool[n]; lletter = new ushort[n];
            lzone = null;   // (worked out again from where the lamps are now)
            emergeTurn = -1;
            var pick = new Random(1225);   // the same colors every time
            for (int i = 0; i < n; i++) lpick[i] = pick.NextDouble();
            uKind = new byte[units]; uPeriod = new double[units]; uTimer = new double[units]; uDir = new sbyte[units]; uVal = new int[units];
        }

        /// <summary>The strip: modules of lamps side by side, in tiers.</summary>
        void LayOutModules(double width, double height)
        {
            if (fill)
            {
                tierRows = Enumerable.Repeat(Rows, Math.Max(1, (int)((height - 4) / TierHigh(Rows)))).ToArray();
                plateTop = (height - tierRows.Length * TierHigh(Rows)) / 2;
            }
            else
            {
                tierRows = Tiers(rowsHigh);
                plateTop = PlateTop;
            }
            tiers = tierRows.Length;
            tierFirst = new int[tiers];
            tierTop = new double[tiers];
            tierHeight = new double[tiers];
            gridRows = 0;
            plateBottom = plateTop;
            for (int t = 0; t < tiers; t++)
            {
                tierFirst[t] = gridRows;
                tierTop[t] = plateBottom;
                tierHeight[t] = TierHigh(tierRows[t]);
                gridRows += tierRows[t];
                plateBottom += tierHeight[t];
            }
            double usable = Math.Max(0, width - 2 * Pad - ReserveLeft - ReserveRight);
            mods = Math.Max(1, (int)(usable / ModW));
            modX0 = ReserveLeft + Pad + (usable - mods * ModW) / 2;
            units = mods * gridRows;
            n = units * Cols;
            Allocate();
            avg = new double[mods * tiers];
            sweepSpan = mods;
            gridCols = mods * Cols;
            var lr = new Random(1979);   // the same layout every time
            for (int t = 0; t < tiers; t++)
                for (int m = 0; m < mods; m++)
                {
                    double pick = lr.NextDouble();
                    int scheme = pick < 0.4 ? 0 : pick < 0.7 ? 1 : pick < 0.85 ? 2 : 3;   // all red, amber top row, amber alternate columns, all amber
                    for (int r = 0; r < tierRows[t]; r++)
                    {
                        int u = mods * tierFirst[t] + m * tierRows[t] + r;
                        double k = lr.NextDouble();
                        uKind[u] = (byte)(k < 0.2 ? 0 : k < 0.55 ? 1 : k < 0.7 ? 2 : 3);
                        uPeriod[u] = BasePeriod[uKind[u]] * (0.55 + lr.NextDouble() * 0.9);
                        uTimer[u] = lr.NextDouble() * uPeriod[u];
                        uDir[u] = (sbyte)(lr.NextDouble() < 0.5 ? 1 : -1);
                        uVal[u] = lr.Next(32);
                        for (int c = 0; c < Cols; c++)
                        {
                            int i = u * Cols + c;
                            lx[i] = modX0 + m * ModW + (ModW - (Cols - 1) * PitchX) / 2 + c * PitchX;
                            ly[i] = tierTop[t] + (tierHeight[t] - (tierRows[t] - 1) * PitchY) / 2 + r * PitchY;
                            lcol[i] = (byte)(scheme == 3 ? 1 : scheme == 1 ? (r == 0 ? 1 : 0) : scheme == 2 ? c % 2 : 0);
                            lmax[i] = lr.NextDouble() < 0.008 ? 0 : 0.72 + lr.NextDouble() * 0.28;   // a few burned out, the rest a little uneven
                            ldead[i] = lmax[i] == 0;
                            lon[i] = (byte)(lr.NextDouble() < 0.45 ? 1 : 0);
                            lb[i] = lon[i];
                            lhere[i] = 1;
                            lsweep[i] = m;
                            lmod[i] = t * mods + m;
                            lgx[i] = m * Cols + c;
                            lgy[i] = tierFirst[t] + r;
                        }
                    }
                }
        }

        /// <summary>
        /// The monitor's window: lamps spaced well apart in rows (each row in runs of 5 that behave like the strip's
        /// rows), a lamp in every place but behind the LED board or the 8 ball, mostly amber.
        /// </summary>
        void LayOutSparse(double width, double height)
        {
            int groups = Math.Max(1, ((int)((width - 2 * SparseMargin) / SparsePitchX) + 1) / Cols);
            int rows = Math.Max(1, (int)((height - 2 * SparseMargin) / SparsePitchY) + 1);
            int cols = groups * Cols;
            tiers = 0;
            mods = 0;
            units = rows * groups;
            n = units * Cols;
            Allocate();
            avg = new double[0];
            sweepSpan = cols;
            gridCols = cols;
            gridRows = 0;   // the monitor doesn't spell text
            double x0 = (width - (cols - 1) * SparsePitchX) / 2, y0 = (height - (rows - 1) * SparsePitchY) / 2;
            var lr = new Random(1983);   // the same layout every time
            for (int r = 0; r < rows; r++)
                for (int g = 0; g < groups; g++)
                {
                    int u = r * groups + g;
                    double k = lr.NextDouble();
                    uKind[u] = (byte)(k < 0.35 ? 0 : k < 0.6 ? 1 : k < 0.68 ? 2 : 3);
                    uPeriod[u] = BasePeriod[uKind[u]] * (0.8 + lr.NextDouble() * 1.2);   // a little more unhurried than the strip
                    uTimer[u] = lr.NextDouble() * uPeriod[u];
                    uDir[u] = (sbyte)(lr.NextDouble() < 0.5 ? 1 : -1);
                    uVal[u] = lr.Next(32);
                    for (int c = 0; c < Cols; c++)
                    {
                        int i = u * Cols + c, col = g * Cols + c;
                        lx[i] = x0 + col * SparsePitchX;
                        ly[i] = y0 + r * SparsePitchY;
                        lcol[i] = (byte)(lr.NextDouble() < 0.7 ? 1 : 0);
                        lr.NextDouble();   // (this once left some places without a lamp; still drawn, so the rest of the layout stays as it was)
                        lhere[i] = (byte)(covered == null || !covered(new Point(lx[i], ly[i])) ? 1 : 0);   // a lamp in every place the board or the ball leaves clear
                        lmax[i] = 0.75 + lr.NextDouble() * 0.25;
                        lon[i] = (byte)(lr.NextDouble() < 0.2 ? 1 : 0);
                        lb[i] = lon[i];
                        lsweep[i] = col;
                    }
                }
        }

        static void Params(LampActivity a, out double slow, out double density)
        {
            switch (a)
            {
                case LampActivity.Standby: slow = 6.0; density = 0.30; break;   // as idle, at half its pace
                case LampActivity.Idle: slow = 3.0; density = 0.30; break;
                case LampActivity.Alert: slow = 0.7; density = 0.55; break;
                default: slow = 1.0; density = 0.47; break;
            }
        }

        /// <param name="exact">Every kind of row keeps about <paramref name="density"/> lit (a custom activity), not only some.</param>
        void TickUnit(int u, double density, bool exact)
        {
            int b = u * Cols;
            switch (uKind[u])
            {
                case 0:   // mostly steady
                    if (rnd.NextDouble() < 0.6)
                    {
                        int i = b + rnd.Next(Cols);
                        if (exact) lon[i] = (byte)(rnd.NextDouble() < density ? 1 : 0);
                        else lon[i] ^= 1;
                    }
                    break;
                case 1:   // shift register
                    if (uDir[u] > 0)
                    {
                        for (int c = Cols - 1; c > 0; c--) lon[b + c] = lon[b + c - 1];
                        lon[b] = (byte)(rnd.NextDouble() < density ? 1 : 0);
                    }
                    else
                    {
                        for (int c = 0; c < Cols - 1; c++) lon[b + c] = lon[b + c + 1];
                        lon[b + Cols - 1] = (byte)(rnd.NextDouble() < density ? 1 : 0);
                    }
                    break;
                case 2:   // binary counter
                    uVal[u] = (uVal[u] + 1) & 31;
                    for (int c = 0; c < Cols; c++)
                    {
                        bool bit = ((uVal[u] >> (Cols - 1 - c)) & 1) != 0;
                        if (exact) bit = density >= 0.5 ? bit || rnd.NextDouble() < 2 * density - 1 : bit && rnd.NextDouble() < 2 * density;   // half lit, made more or fewer
                        lon[b + c] = (byte)(bit ? 1 : 0);
                    }
                    break;
                default:  // flicker, keeping about the wanted share lit
                    int on = 0;
                    for (int c = 0; c < Cols; c++) on += lon[b + c];
                    byte want = (byte)(on / (double)Cols < density ? 1 : 0);
                    int start = rnd.Next(Cols);
                    for (int k = 0; k < Cols; k++)
                    {
                        int i = b + (start + k) % Cols;
                        if (lon[i] != want) { lon[i] = want; break; }
                    }
                    if (rnd.NextDouble() < 0.3)
                    {
                        int i = b + rnd.Next(Cols);
                        if (exact) lon[i] = (byte)(rnd.NextDouble() < density ? 1 : 0);
                        else lon[i] ^= 1;
                    }
                    break;
            }
        }

        void Step(double dt)
        {
            if (n == 0) return;
            simTime += dt;
            if (activity == LampActivity.Random)
            {
                var now = RandomNow();
                if (now != current) { current = now; sweepStart = simTime; }
            }
            double slow, density;
            bool exact = current == LampActivity.Custom;   // your own: about that share lit, on every kind of row
            if (exact)
            {
                var c = CustomNow;
                slow = LampActivityLevels.Slowness(sparse ? c.MonitorSpeed : c.StripSpeed);
                density = (sparse ? c.MonitorLamps : c.StripLamps) / 100.0;
            }
            else
            {
                Params(current, out slow, out density);
                if (sparse) density *= 0.5;   // the monitor: fewer lamps lit
            }
            // A new pace: each row's wait so far is stretched or shrunk to match, so it shows at once.
            if (lastSlow > 0 && slow != lastSlow) for (int u = 0; u < units; u++) uTimer[u] *= slow / lastSlow;
            lastSlow = slow;
            bool spelling = ShowingText, sides = spelling && textSides, emerging = Emerging;
            if (!spelling || sides || emerging) for (int u = 0; u < units; u++)   // the rows' own blinking (then the text, over its stretch)
            {
                uTimer[u] -= dt;
                int guard = 0;
                while (uTimer[u] <= 0 && guard++ < 4)
                {
                    TickUnit(u, density, exact);
                    uTimer[u] += uPeriod[u] * slow * (0.7 + rnd.NextDouble() * 0.6);
                }
                if (uTimer[u] <= 0) uTimer[u] = uPeriod[u] * slow;
            }
            if (spelling) StepText(dt);
            // Alert: a band of light sweeps along the strip about every 1.8 s (a moving band, never the whole strip at once),
            // passing by any text.
            double front = -99;
            if (current == LampActivity.Alert && (!spelling || sides || emerging))
            {
                double phase = ((simTime - sweepStart) % 1.8) / 0.9;
                if (phase <= 1) front = -1 + phase * (sweepSpan + 1);
            }
            // Lamps spelling text fade quicker, so moving letters stay sharp instead of trailing light.
            double kOn = 1 - Math.Exp(-dt / 0.03), kOff = 1 - Math.Exp(-dt / 0.12);
            double tOn = 1 - Math.Exp(-dt / 0.02), tOff = 1 - Math.Exp(-dt / 0.045);
            for (int i = 0; i < n; i++)
            {
                bool colored = PaletteOf(i).ColorsLetters;   // text has its letters' colors
                bool inText = spelling && lgx[i] >= textFrom && lgx[i] < textTo;
                if (inText && emerging)
                {
                    // From its own blinking to what the text has it do, or back, a little at a time.
                    double blink = front > -50 && Math.Abs(lsweep[i] - front) < 0.7 ? 1 : lon[i], held = lheld[i];
                    double to = blink + ((lspell[i] ? 1 : 0) - blink) * held;
                    if (ldead[i]) lmax[i] = lspell[i] ? 0.85 * held : 0;
                    // A letter's color comes while the lamp is dark, or at the latest halfway; its own comes back the same way.
                    ushort own = BlinkColor(i), letter = lletter[i];
                    if (held >= 1 && lspell[i]) lshow[i] = letter;
                    else if (held <= 0 || !lspell[i] || (lshow[i] != own && lshow[i] != letter)) lshow[i] = own;
                    else if (lb[i] < 0.25 || emergeRising == (held >= 0.5)) lshow[i] = emergeRising ? letter : own;
                    lb[i] += (to - lb[i]) * (to > lb[i] ? kOn : kOff);
                    continue;
                }
                if (ldead[i]) lmax[i] = inText ? 0.85 : 0;
                if (!inText || !colored) lshow[i] = BlinkColor(i);
                double target = lon[i];
                if (front > -50 && !inText && Math.Abs(lsweep[i] - front) < 0.7) target = 1;
                lb[i] += (target - lb[i]) * (target > lb[i] ? (inText ? tOn : kOn) : (inText ? tOff : kOff));
            }
        }

        // ---------- each frame ----------

        static int Level(double b)
        {
            int l = (int)Math.Round(b * (Levels - 1));
            return l < 0 ? 0 : l >= Levels ? Levels - 1 : l;
        }

        void ComposeFrame()
        {
            if (n == 0 || wb == null) return;
            Array.Copy(background, frame, frame.Length);
            if (!ReferenceEquals(lightFor, palette)) MakeLight();
            if (lensSpr.Length < LampPalette.Count)   // colors from the wheel that weren't there before
            {
                Array.Resize(ref lensSpr, LampPalette.Count);
                Array.Resize(ref glowSpr, LampPalette.Count);
                Array.Resize(ref reflSpr, LampPalette.Count);
            }
            if (!sparse)
            {
                Array.Clear(avg, 0, avg.Length);
                for (int i = 0; i < n; i++) avg[lmod[i]] += lb[i] * lmax[i];
                for (int k = 0; k < avg.Length; k++) avg[k] /= tierRows[k / mods] * Cols;

                // light on the hood's underside, and a haze around each module
                if (!fill)
                    for (int m = 0; m < mods; m++)
                    {
                        int lv = Level(avg[m]);
                        if (lv > 0) Add(hoodSpr[lv], modX0 + (m + 0.5) * ModW, 3);
                    }
                for (int k = 0; k < avg.Length; k++)
                {
                    int lv = Level(avg[k]);
                    if (lv > 0) Add(hazeSpr[lv], modX0 + (k % mods + 0.5) * ModW, tierTop[k / mods] + tierHeight[k / mods] / 2);
                }
            }
            // the lamps (an unlit one, too: a dim bulb), then their glow
            for (int i = 0; i < n; i++)
            {
                if (lhere[i] == 0) continue;
                int lv = Level(lb[i] * lmax[i]);
                if (lensSpr[lshow[i]] == null) MakeColor(lshow[i]);   // the first time this color shows
                Over(lensSpr[lshow[i]][lv], lx[i], ly[i]);
            }
            for (int i = 0; i < n; i++)
            {
                if (lhere[i] == 0) continue;
                double b = lb[i] * lmax[i];
                if (lensSpr[lshow[i]] == null) continue;
                if (b >= 0.08) Add(glowSpr[lshow[i]][Level(b)], lx[i], ly[i]);
            }
            // the glossy sill: a warm wash, and the lower tier mirrored in it
            if (!fill)
            {
                for (int m = 0; m < mods; m++)
                {
                    int lv = Level(avg[(tiers - 1) * mods + m]);
                    if (lv > 0) Add(sillSpr[lv], modX0 + (m + 0.5) * ModW, sillTop + 2.5);
                }
                for (int i = mods * tierFirst[tiers - 1] * Cols; i < n; i++)
                {
                    double b = lb[i] * lmax[i];
                    if (b >= 0.15) Add(reflSpr[lshow[i]][Level(b)], lx[i], sillTop + 1.6 + (sillTop - ly[i]) * 0.2);
                }
            }
            wb.WritePixels(new Int32Rect(0, 0, pw, ph), frame, pw * 4, 0);
        }

        /// <summary>Lays a picture over the frame (premultiplied "over"), centered at a point given in screen units.</summary>
        void Over(Sprite s, double cx, double cy)
        {
            int x0 = (int)Math.Round(cx * sx - s.W / 2.0), y0 = (int)Math.Round(cy * sy - s.H / 2.0);
            for (int y = 0; y < s.H; y++)
            {
                int fy = y0 + y;
                if (fy < 0 || fy >= ph) continue;
                int si = y * s.W, fi = fy * pw;
                for (int x = 0; x < s.W; x++)
                {
                    int fx = x0 + x;
                    if (fx < 0 || fx >= pw) continue;
                    uint src = s.Px[si + x], sa = src >> 24;
                    if (sa == 0) continue;
                    if (sa == 255) { frame[fi + fx] = src; continue; }
                    uint dst = frame[fi + fx], inv = 255 - sa;
                    uint a = Math.Min(255u, sa + ((dst >> 24) * inv + 127) / 255);
                    uint r = Math.Min(255u, ((src >> 16) & 255) + (((dst >> 16) & 255) * inv + 127) / 255);
                    uint g = Math.Min(255u, ((src >> 8) & 255) + (((dst >> 8) & 255) * inv + 127) / 255);
                    uint b = Math.Min(255u, (src & 255) + ((dst & 255) * inv + 127) / 255);
                    frame[fi + fx] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        /// <summary>Adds a picture's light to the frame (glows and reflections), centered at a point in screen units.</summary>
        void Add(Sprite s, double cx, double cy)
        {
            int x0 = (int)Math.Round(cx * sx - s.W / 2.0), y0 = (int)Math.Round(cy * sy - s.H / 2.0);
            for (int y = 0; y < s.H; y++)
            {
                int fy = y0 + y;
                if (fy < 0 || fy >= ph) continue;
                int si = y * s.W, fi = fy * pw;
                for (int x = 0; x < s.W; x++)
                {
                    int fx = x0 + x;
                    if (fx < 0 || fx >= pw) continue;
                    uint src = s.Px[si + x];
                    if (src == 0) continue;
                    uint dst = frame[fi + fx];
                    uint a = Math.Min(255u, (dst >> 24) + (src >> 24));
                    uint r = Math.Min(255u, ((dst >> 16) & 255) + ((src >> 16) & 255));
                    uint g = Math.Min(255u, ((dst >> 8) & 255) + ((src >> 8) & 255));
                    uint b = Math.Min(255u, (dst & 255) + (src & 255));
                    frame[fi + fx] = (a << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        // ---------- pictures, made once per size or screen scale ----------

        uint[] Render(int w, int h, Drawing drawing)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(sx, sy));
                dc.DrawDrawing(drawing);
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var px = new uint[w * h];
            rtb.CopyPixels(px, w * 4, 0);
            return px;
        }

        Sprite MakeSprite(double rx, double ry, Brush brush)
        {
            int w = (int)Math.Ceiling(rx * 2 * sx) + 2, h = (int)Math.Ceiling(ry * 2 * sy) + 2;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(sx, sy));
                dc.DrawEllipse(brush, null, new Point(w / 2.0 / sx, h / 2.0 / sy), rx, ry);
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var s = new Sprite { W = w, H = h, Px = new uint[w * h] };
            rtb.CopyPixels(s.Px, w * 4, 0);
            return s;
        }

        /// <summary>The height of the tallest tier (the haze around a module covers it).</summary>
        double HazeTier { get { return sparse || tierHeight == null ? TierHigh(Rows) : tierHeight.Max(); } }

        void MakeSprites()
        {
            spritesSx = sx;
            spritesSy = sy;
            spritesTier = HazeTier;
            lensSpr = new Sprite[LampPalette.Count][];
            glowSpr = new Sprite[LampPalette.Count][];
            reflSpr = new Sprite[LampPalette.Count][];
            MakeLight();
        }

        /// <summary>The pictures of a lamp of one color, its glow, and its reflection in the sill, from dark to fully lit.</summary>
        void MakeColor(int c)
        {
            Color off = LampPalette.Off(c), lit = LampPalette.Lit(c), hot = LampPalette.Hot(c), around = LampPalette.Glow(c), core = LampPalette.Core(c);
            lensSpr[c] = new Sprite[Levels];
            glowSpr[c] = new Sprite[Levels];
            reflSpr[c] = new Sprite[Levels];
            for (int l = 0; l < Levels; l++)
            {
                double b = l / (double)(Levels - 1);
                var lens = new RadialGradientBrush { GradientOrigin = new Point(0.38, 0.32) };
                lens.GradientStops.Add(new GradientStop(Mix(Mix(off, Colors.White, 0.08), hot, b), 0));
                lens.GradientStops.Add(new GradientStop(Mix(off, lit, b), 0.5));
                lens.GradientStops.Add(new GradientStop(Mix(Dim(off, 0.45), Dim(lit, 0.55), b), 1));
                lensSpr[c][l] = MakeSprite(Lens / 2, Lens / 2, Frozen(lens));
                var glow = new RadialGradientBrush();
                glow.GradientStops.Add(new GradientStop(WithAlpha(core, 0.85 * b), 0));
                glow.GradientStops.Add(new GradientStop(WithAlpha(around, 0.55 * b), 0.25));
                glow.GradientStops.Add(new GradientStop(WithAlpha(around, 0.16 * b), 0.55));
                glow.GradientStops.Add(new GradientStop(WithAlpha(around, 0), 1));
                double gr = 2.6 + 2.2 * b;
                glowSpr[c][l] = MakeSprite(gr, gr, Frozen(glow));
                reflSpr[c][l] = MakeSprite(2.6, 1.1, Radial(around, 0.45 * b));
            }
        }

        /// <summary>The light around the lamps, in the palette's color.</summary>
        void MakeLight()
        {
            lightFor = palette;
            hazeSpr = new Sprite[Levels];
            hoodSpr = new Sprite[Levels];
            sillSpr = new Sprite[Levels];
            for (int l = 0; l < Levels; l++)
            {
                double b = l / (double)(Levels - 1) * palette.LightMuch;
                hazeSpr[l] = MakeSprite(ModW * 0.75, spritesTier * 0.95, Radial(palette.Light, 0.22 * b));
                hoodSpr[l] = MakeSprite(ModW * 0.8, 9, Radial(palette.Light, 0.35 * b));
                sillSpr[l] = MakeSprite(ModW * 0.7, 5, Radial(palette.Light, 0.4 * b));
            }
        }

        Drawing BuildStill(double w, double h)
        {
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                if (sparse) SparseStill(dc, w, h);
                else ModuleStill(dc, w, h);
            }
            dg.Freeze();
            return dg;
        }

        void ModuleStill(DrawingContext dc, double w, double h)
        {
            {
                double pwDip = mods * ModW;
                dc.DrawRectangle(Vertical("#060708", "#0d0f10"), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(Hex("#151718"), null, new Rect(modX0 - 5, plateTop - 3, pwDip + 10, plateBottom - plateTop + 6));
                var facePen = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(166, 0, 0, 0))), 0.8);
                facePen.Freeze();
                var face = Vertical("#2e3234", "#232729");
                for (int t = 0; t < tiers; t++)
                    for (int m = 0; m < mods; m++)
                        dc.DrawRoundedRectangle(face, facePen, new Rect(modX0 + m * ModW + 1.4, tierTop[t] + 1, ModW - 2.8, tierHeight[t] - 2), 1.2, 1.2);
                var divider = Horizontal("#5a6064", "#2c3033");
                for (int m = 0; m <= mods; m++)
                    dc.DrawRectangle(divider, null, new Rect(modX0 + m * ModW - 0.8, plateTop - 3, 1.6, plateBottom - plateTop + 6));
                var rail = Frozen(new SolidColorBrush(Color.FromArgb(90, 120, 128, 132)));
                for (int t = 1; t < tiers; t++)
                    dc.DrawRectangle(rail, null, new Rect(modX0 - 5, tierTop[t] - 0.5, pwDip + 10, 1));
                var socket = Hex("#060404");
                for (int i = 0; i < n; i++) dc.DrawEllipse(socket, null, new Point(lx[i], ly[i]), 2.3, 2.3);
                if (!fill)
                {
                    // the hood above: its lip, and the shadow it casts on the lamps
                    dc.DrawRectangle(Vertical("#30373a", "#151819"), null, new Rect(0, 0, w, 4));
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(210, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(0, 4, w, 9));
                    // the glossy sill below
                    dc.DrawRectangle(Vertical("#111416", "#2e3438"), null, new Rect(0, sillTop, w, SillHeight));
                    dc.DrawRectangle(Frozen(new SolidColorBrush(Color.FromArgb(34, 255, 255, 255))), null, new Rect(0, sillTop, w, 0.8));
                    // Toward the monitor: one plain panel from the last module's divider to the strip's end (it runs on
                    // behind the monitor), the full height from under the hood's lip down to the sill.
                    if (ReserveRight > 0) BlankPanel(dc, modX0 + pwDip + 0.8, w);
                    if (ReserveLeft > 0) BlankPanel(dc, 0, modX0 - 0.8);
                }
                else
                {
                    // set into the strip: a little shade along the top and bottom edges
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(0, 0, w, Math.Min(8, h / 3)));
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(120, 0, 0, 0), 90)), null, new Rect(0, h - Math.Min(6, h / 3), w, Math.Min(6, h / 3)));
                }
            }
        }

        /// <summary>
        /// A plain panel, the full height of the strip's front from under the hood's lip down to the sill, in the
        /// modules' finish (lighter at the top, darker at the bottom), with a touch of shade just under the lip.
        /// </summary>
        void BlankPanel(DrawingContext dc, double x0, double x1)
        {
            if (x1 - x0 < 2) return;
            dc.DrawRectangle(Vertical("#2b2f31", "#232729"), null, new Rect(x0, 4, x1 - x0, sillTop - 4));
            dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(120, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(x0, 4, x1 - x0, 3));
        }

        /// <summary>The monitor's window: black glass with faint ribs behind it, set back into the case.</summary>
        void SparseStill(DrawingContext dc, double w, double h)
        {
            dc.DrawRectangle(Vertical("#030303", "#0b0807"), null, new Rect(0, 0, w, h));
            var rib = Frozen(new SolidColorBrush(Color.FromArgb(9, 255, 236, 220)));
            for (double x = 1.2; x < w; x += 2.6) dc.DrawRectangle(rib, null, new Rect(x, 0, 0.5, h));
            var socket = Hex("#0d0706");
            for (int i = 0; i < n; i++) if (lhere[i] == 1) dc.DrawEllipse(socket, null, new Point(lx[i], ly[i]), 2.0, 2.0);
            double top = Math.Min(9, h / 3), side = Math.Min(7, w / 4);
            dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(220, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(0, 0, w, top));
            dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 0)), null, new Rect(0, 0, side, h));
            dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(150, 0, 0, 0), 0)), null, new Rect(w - side, 0, side, h));
        }

        // ---------- colors ----------

        static Brush Radial(Color c, double alpha)
        {
            var br = new RadialGradientBrush();
            br.GradientStops.Add(new GradientStop(WithAlpha(c, alpha), 0));
            br.GradientStops.Add(new GradientStop(WithAlpha(c, 0), 1));
            return Frozen(br);
        }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        static Color Dim(Color c, double k) { return Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k)); }

        static Color WithAlpha(Color c, double a)
        {
            return Color.FromArgb((byte)Math.Round(255 * Math.Max(0, Math.Min(1, a))), c.R, c.G, c.B);
        }

        static Brush Frozen(Brush b) { b.Freeze(); return b; }
        static Brush Hex(string h) { return Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(h))); }
        static Brush Vertical(string a, string b)
        {
            return Frozen(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 90));
        }
        static Brush Horizontal(string a, string b)
        {
            return Frozen(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 0));
        }
    }
}
