using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>
    /// A strip of blinking indicator lamps, like the front of an early-1980s mainframe: red and amber lamps in modules
    /// of 5 × 3. As its own strip it's two tiers deep, under a dark hood, above a glossy sill; filling a space (behind
    /// the quick launch icons) it takes as many tiers as fit. Rows behave like a computer at work: some march like
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
        public const double PanelHeight = 52;
        const int Rows = 3, Cols = 5, Levels = 10;
        const double ModW = 32, PitchX = 5.8, PitchY = 5.0, Lens = 3.8, Pad = 8;
        const double TierH = 17.5;
        const double SillTop = 45, SillBottom = 52;   // the strip's own sill (not when filling a space)
        const int FrameMs = 40;   // 25 frames a second
        static readonly double[] BasePeriod = { 3.2, 0.22, 0.28, 0.12 };   // steady, shift register, counter, flicker

        static readonly Color[] OffColor = { Color.FromRgb(52, 14, 9), Color.FromRgb(56, 38, 11) };       // red lens, amber lens
        static readonly Color[] LitColor = { Color.FromRgb(255, 62, 30), Color.FromRgb(255, 172, 52) };
        static readonly Color[] HotColor = { Color.FromRgb(255, 222, 200), Color.FromRgb(255, 244, 210) };
        static readonly Color[] GlowColor = { Color.FromRgb(255, 70, 30), Color.FromRgb(255, 165, 50) };
        static readonly Random rnd = new Random();

        class Sprite { public int W, H; public uint[] Px; }

        readonly DispatcherTimer timer;
        readonly Stopwatch clock = new Stopwatch();
        readonly LampActivity mode;
        readonly bool fill;
        double builtWidth = -1, builtHeight = -1, modX0, simTime, last;
        double sx = 1, sy = 1, spritesSx, spritesSy;
        double plateTop, plateBottom;
        int tiers, mods, n, units, pw, ph;
        double[] lx, ly, lb, lmax, avg;
        byte[] lon, lcol;
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

        /// <param name="fill">Fill the space it's given (as a background) instead of being its own 52-high strip.</param>
        public LampPanel(LampActivity mode, bool fill = false)
        {
            this.mode = mode;
            this.fill = fill;
            if (!fill) Height = PanelHeight;
            ClipToBounds = true;
            IsHitTestVisible = false;
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(FrameMs) };
            timer.Tick += delegate { Tick(); };
            IsVisibleChanged += delegate { UpdateRunning(); };
            Loaded += delegate { UpdateRunning(); };
            Unloaded += delegate { timer.Stop(); clock.Stop(); };
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

            if (fill)
            {
                tiers = Math.Max(1, (int)((height - 4) / TierH));
                plateTop = (height - tiers * TierH) / 2;
            }
            else
            {
                tiers = 2;
                plateTop = 8;
            }
            plateBottom = plateTop + tiers * TierH;
            mods = Math.Max(1, (int)((width - 2 * Pad) / ModW));
            modX0 = (width - mods * ModW) / 2;
            units = mods * tiers * Rows;
            n = units * Cols;
            lx = new double[n]; ly = new double[n]; lb = new double[n]; lmax = new double[n];
            lon = new byte[n]; lcol = new byte[n];
            uKind = new byte[units]; uPeriod = new double[units]; uTimer = new double[units]; uDir = new sbyte[units]; uVal = new int[units];
            avg = new double[mods * tiers];
            var lr = new Random(1979);   // the same layout every time
            for (int t = 0; t < tiers; t++)
                for (int m = 0; m < mods; m++)
                {
                    double pick = lr.NextDouble();
                    int scheme = pick < 0.4 ? 0 : pick < 0.7 ? 1 : pick < 0.85 ? 2 : 3;   // all red, amber top row, amber alternate columns, all amber
                    for (int r = 0; r < Rows; r++)
                    {
                        int u = (t * mods + m) * Rows + r;
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
                            ly[i] = plateTop + t * TierH + (TierH - (Rows - 1) * PitchY) / 2 + r * PitchY;
                            lcol[i] = (byte)(scheme == 3 ? 1 : scheme == 1 ? (r == 0 ? 1 : 0) : scheme == 2 ? c % 2 : 0);
                            lmax[i] = lr.NextDouble() < 0.008 ? 0 : 0.72 + lr.NextDouble() * 0.28;   // a few burned out, the rest a little uneven
                            lon[i] = (byte)(lr.NextDouble() < 0.45 ? 1 : 0);
                            lb[i] = lon[i];
                        }
                    }
                }

            pw = Math.Max(1, (int)Math.Ceiling(width * sx));
            ph = Math.Max(1, (int)Math.Ceiling(height * sy));
            background = Render(pw, ph, BuildStill(width, height));
            frame = new uint[pw * ph];
            wb = new WriteableBitmap(pw, ph, 96 * sx, 96 * sy, PixelFormats.Pbgra32, null);
            if (spritesSx != sx || spritesSy != sy) MakeSprites();
            Clip = Shapes.AnyRounding(Corners) ? Shapes.Rounded(new Rect(0, 0, width, height), Corners) : null;
            InvalidateVisual();
        }

        static void Params(LampActivity a, out double slow, out double density)
        {
            switch (a)
            {
                case LampActivity.Idle: slow = 3.0; density = 0.30; break;
                case LampActivity.Alert: slow = 0.7; density = 0.55; break;
                default: slow = 1.0; density = 0.47; break;
            }
        }

        void TickUnit(int u, double density)
        {
            int b = u * Cols;
            switch (uKind[u])
            {
                case 0:   // mostly steady
                    if (rnd.NextDouble() < 0.6) lon[b + rnd.Next(Cols)] ^= 1;
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
                    for (int c = 0; c < Cols; c++) lon[b + c] = (byte)((uVal[u] >> (Cols - 1 - c)) & 1);
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
                    if (rnd.NextDouble() < 0.3) lon[b + rnd.Next(Cols)] ^= 1;
                    break;
            }
        }

        void Step(double dt)
        {
            if (n == 0) return;
            simTime += dt;
            double slow, density;
            Params(mode, out slow, out density);
            for (int u = 0; u < units; u++)
            {
                uTimer[u] -= dt;
                int guard = 0;
                while (uTimer[u] <= 0 && guard++ < 4)
                {
                    TickUnit(u, density);
                    uTimer[u] += uPeriod[u] * slow * (0.7 + rnd.NextDouble() * 0.6);
                }
                if (uTimer[u] <= 0) uTimer[u] = uPeriod[u] * slow;
            }
            // Alert: a band of light sweeps along the strip about every 1.8 s (a moving band, never the whole strip at once).
            double front = -99;
            if (mode == LampActivity.Alert)
            {
                double phase = (simTime % 1.8) / 0.9;
                if (phase <= 1) front = -1 + phase * (mods + 1);
            }
            double kOn = 1 - Math.Exp(-dt / 0.03), kOff = 1 - Math.Exp(-dt / 0.12);
            for (int i = 0; i < n; i++)
            {
                double target = lon[i];
                if (front > -50 && Math.Abs((i / (Rows * Cols)) % mods - front) < 0.7) target = 1;
                lb[i] += (target - lb[i]) * (target > lb[i] ? kOn : kOff);
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
            Array.Clear(avg, 0, avg.Length);
            for (int i = 0; i < n; i++) avg[i / (Rows * Cols)] += lb[i] * lmax[i];
            for (int k = 0; k < avg.Length; k++) avg[k] /= Rows * Cols;

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
                if (lv > 0) Add(hazeSpr[lv], modX0 + (k % mods + 0.5) * ModW, plateTop + (k / mods + 0.5) * TierH);
            }
            // the lamps, then their glow
            for (int i = 0; i < n; i++) Over(lensSpr[lcol[i]][Level(lb[i] * lmax[i])], lx[i], ly[i]);
            for (int i = 0; i < n; i++)
            {
                double b = lb[i] * lmax[i];
                if (b >= 0.08) Add(glowSpr[lcol[i]][Level(b)], lx[i], ly[i]);
            }
            // the glossy sill: a warm wash, and the lower tier mirrored in it
            if (!fill)
            {
                for (int m = 0; m < mods; m++)
                {
                    int lv = Level(avg[(tiers - 1) * mods + m]);
                    if (lv > 0) Add(sillSpr[lv], modX0 + (m + 0.5) * ModW, SillTop + 2.5);
                }
                for (int i = (tiers - 1) * mods * Rows * Cols; i < n; i++)
                {
                    double b = lb[i] * lmax[i];
                    if (b >= 0.15) Add(reflSpr[lcol[i]][Level(b)], lx[i], SillTop + 1.6 + (SillTop - ly[i]) * 0.2);
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

        void MakeSprites()
        {
            spritesSx = sx;
            spritesSy = sy;
            lensSpr = new Sprite[2][];
            glowSpr = new Sprite[2][];
            reflSpr = new Sprite[2][];
            for (int c = 0; c < 2; c++)
            {
                lensSpr[c] = new Sprite[Levels];
                glowSpr[c] = new Sprite[Levels];
                reflSpr[c] = new Sprite[Levels];
                for (int l = 0; l < Levels; l++)
                {
                    double b = l / (double)(Levels - 1);
                    var lens = new RadialGradientBrush { GradientOrigin = new Point(0.38, 0.32) };
                    lens.GradientStops.Add(new GradientStop(Mix(Mix(OffColor[c], Colors.White, 0.08), HotColor[c], b), 0));
                    lens.GradientStops.Add(new GradientStop(Mix(OffColor[c], LitColor[c], b), 0.5));
                    lens.GradientStops.Add(new GradientStop(Mix(Dim(OffColor[c], 0.45), Dim(LitColor[c], 0.55), b), 1));
                    lensSpr[c][l] = MakeSprite(Lens / 2, Lens / 2, Frozen(lens));
                    var glow = new RadialGradientBrush();
                    glow.GradientStops.Add(new GradientStop(WithAlpha(Color.FromRgb(255, 240, 220), 0.85 * b), 0));
                    glow.GradientStops.Add(new GradientStop(WithAlpha(GlowColor[c], 0.55 * b), 0.25));
                    glow.GradientStops.Add(new GradientStop(WithAlpha(GlowColor[c], 0.16 * b), 0.55));
                    glow.GradientStops.Add(new GradientStop(WithAlpha(GlowColor[c], 0), 1));
                    double gr = 2.6 + 2.2 * b;
                    glowSpr[c][l] = MakeSprite(gr, gr, Frozen(glow));
                    reflSpr[c][l] = MakeSprite(2.6, 1.1, Radial(GlowColor[c], 0.45 * b));
                }
            }
            hazeSpr = new Sprite[Levels];
            hoodSpr = new Sprite[Levels];
            sillSpr = new Sprite[Levels];
            var warm = Color.FromRgb(255, 90, 45);
            for (int l = 0; l < Levels; l++)
            {
                double b = l / (double)(Levels - 1);
                hazeSpr[l] = MakeSprite(ModW * 0.75, TierH * 0.95, Radial(warm, 0.22 * b));
                hoodSpr[l] = MakeSprite(ModW * 0.8, 9, Radial(warm, 0.35 * b));
                sillSpr[l] = MakeSprite(ModW * 0.7, 5, Radial(warm, 0.4 * b));
            }
        }

        Drawing BuildStill(double w, double h)
        {
            var dg = new DrawingGroup();
            using (var dc = dg.Open())
            {
                double pwDip = mods * ModW;
                dc.DrawRectangle(Vertical("#060708", "#0d0f10"), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(Hex("#151718"), null, new Rect(modX0 - 5, plateTop - 3, pwDip + 10, plateBottom - plateTop + 6));
                var facePen = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(166, 0, 0, 0))), 0.8);
                facePen.Freeze();
                var face = Vertical("#2e3234", "#232729");
                for (int t = 0; t < tiers; t++)
                    for (int m = 0; m < mods; m++)
                        dc.DrawRoundedRectangle(face, facePen, new Rect(modX0 + m * ModW + 1.4, plateTop + t * TierH + 1, ModW - 2.8, TierH - 2), 1.2, 1.2);
                var divider = Horizontal("#5a6064", "#2c3033");
                for (int m = 0; m <= mods; m++)
                    dc.DrawRectangle(divider, null, new Rect(modX0 + m * ModW - 0.8, plateTop - 3, 1.6, plateBottom - plateTop + 6));
                var rail = Frozen(new SolidColorBrush(Color.FromArgb(90, 120, 128, 132)));
                for (int t = 1; t < tiers; t++)
                    dc.DrawRectangle(rail, null, new Rect(modX0 - 5, plateTop + t * TierH - 0.5, pwDip + 10, 1));
                var socket = Hex("#060404");
                for (int i = 0; i < n; i++) dc.DrawEllipse(socket, null, new Point(lx[i], ly[i]), 2.3, 2.3);
                if (!fill)
                {
                    // the hood above: its lip, and the shadow it casts on the lamps
                    dc.DrawRectangle(Vertical("#30373a", "#151819"), null, new Rect(0, 0, w, 4));
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(210, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(0, 4, w, 9));
                    // the glossy sill below
                    dc.DrawRectangle(Vertical("#111416", "#2e3438"), null, new Rect(0, SillTop, w, SillBottom - SillTop));
                    dc.DrawRectangle(Frozen(new SolidColorBrush(Color.FromArgb(34, 255, 255, 255))), null, new Rect(0, SillTop, w, 0.8));
                }
                else
                {
                    // set into the strip: a little shade along the top and bottom edges
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90)), null, new Rect(0, 0, w, Math.Min(8, h / 3)));
                    dc.DrawRectangle(Frozen(new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(120, 0, 0, 0), 90)), null, new Rect(0, h - Math.Min(6, h / 3), w, Math.Min(6, h / 3)));
                }
            }
            dg.Freeze();
            return dg;
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
