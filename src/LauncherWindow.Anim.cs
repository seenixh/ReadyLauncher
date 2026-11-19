using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ReadyLauncher
{
    /// <summary>Open / close animations: fade, slide up, slide down, zoom, pop and blur.</summary>
    internal partial class LauncherWindow
    {
        const double SlideDistance = 48;
        readonly ScaleTransform shellScale = new ScaleTransform(1, 1);
        readonly TranslateTransform shellMove = new TranslateTransform();
        bool hiding;
        int animToken;
        IOverlayAnimation shellOverlay;   // TV static or flip, drawn over the panel while it plays

        void InitShellTransforms()
        {
            var g = new TransformGroup();
            g.Children.Add(shellScale);
            g.Children.Add(shellMove);
            shell.RenderTransform = g;
        }

        /// <summary>Start invisible so the very first frame on screen is the start of the animation.</summary>
        void PrepareAnimateIn()
        {
            StopAnimations();
            if (S.Animation != OpenAnimation.None) shell.Opacity = 0;
        }

        void AnimateIn()
        {
            var a = S.Animation;
            StopAnimations();
            if (a == OpenAnimation.None) return;
            if ((shellOverlay = ElementAnimator.StartOverlay(a, shell, true, S.AnimationSpeed, null)) != null) return;
            int token = animToken;
            int ms = ElementAnimator.Ms(a == OpenAnimation.Pop ? 340 : 240, S.AnimationSpeed);
            IEasingFunction ease = a == OpenAnimation.Pop
                ? (IEasingFunction)new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseOut };

            Run(shell, UIElement.OpacityProperty, 0, 1, Math.Min(ms, ElementAnimator.Ms(200, S.AnimationSpeed)), new QuadraticEase { EasingMode = EasingMode.EaseOut }, null);
            switch (a)
            {
                case OpenAnimation.SlideUp: Run(shellMove, TranslateTransform.YProperty, SlideDistance, 0, ms, ease, null); break;
                case OpenAnimation.SlideDown: Run(shellMove, TranslateTransform.YProperty, -SlideDistance, 0, ms, ease, null); break;
                case OpenAnimation.Zoom: Scale(0.9, 1, ms, ease); break;
                case OpenAnimation.Pop: Scale(0.8, 1, ms, ease); break;
                case OpenAnimation.Blur:
                    var blur = new BlurEffect { Radius = 28, RenderingBias = RenderingBias.Performance };
                    shell.Effect = blur;
                    Run(blur, BlurEffect.RadiusProperty, 28, 0, ms, ease, () => { if (token == animToken) shell.Effect = null; });
                    break;
            }
        }

        void AnimateOut(Action done)
        {
            var a = S.Animation;
            StopAnimations();
            if (a == OpenAnimation.None) { done(); return; }
            hiding = true;
            int token = animToken;
            int ms = ElementAnimator.Ms(170, S.AnimationSpeed);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            Action finish = () =>
            {
                if (token != animToken) return;   // re-opened while closing
                hiding = false;
                done();
            };

            if ((shellOverlay = ElementAnimator.StartOverlay(a, shell, false, S.AnimationSpeed, finish)) != null) return;
            Run(shell, UIElement.OpacityProperty, 1, 0, ms, ease, finish);
            switch (a)
            {
                case OpenAnimation.SlideUp: Run(shellMove, TranslateTransform.YProperty, 0, SlideDistance, ms, ease, null); break;
                case OpenAnimation.SlideDown: Run(shellMove, TranslateTransform.YProperty, 0, -SlideDistance, ms, ease, null); break;
                case OpenAnimation.Zoom: Scale(1, 0.9, ms, ease); break;
                case OpenAnimation.Pop: Scale(1, 0.8, ms, ease); break;
                case OpenAnimation.Blur:
                    var blur = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
                    shell.Effect = blur;
                    Run(blur, BlurEffect.RadiusProperty, 0, 28, ms, ease, null);
                    break;
            }
        }

        void StopAnimations()
        {
            animToken++;
            hiding = false;
            if (shellOverlay != null) { shellOverlay.Stop(); shellOverlay = null; }
            shell.BeginAnimation(UIElement.OpacityProperty, null);
            shell.Opacity = 1;
            shellMove.BeginAnimation(TranslateTransform.YProperty, null);
            shellMove.Y = 0;
            shellScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            shellScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            shellScale.ScaleX = 1;
            shellScale.ScaleY = 1;
            shell.Effect = null;
        }

        void Scale(double from, double to, int ms, IEasingFunction ease)
        {
            Run(shellScale, ScaleTransform.ScaleXProperty, from, to, ms, ease, null);
            Run(shellScale, ScaleTransform.ScaleYProperty, from, to, ms, ease, null);
        }

        static void Run(IAnimatable target, DependencyProperty prop, double from, double to, int ms, IEasingFunction ease, Action completed)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            if (completed != null) anim.Completed += delegate { completed(); };
            target.BeginAnimation(prop, anim);
        }

        // ---------- the windows around the launcher ----------

        readonly System.Collections.Generic.Dictionary<Window, ElementAnimator> animators = new System.Collections.Generic.Dictionary<Window, ElementAnimator>();

        /// <summary>Whether a window beside the launcher animates too (chosen in Settings, under the animation).</summary>
        bool Animates(Window w)
        {
            if (w == null || S.Animation == OpenAnimation.None) return false;
            if (w == notesWindow) return S.AnimateNotes;
            if (w == sensorWindow) return S.AnimateSensors;
            if (w == dockWindow) return S.AnimateDock;
            if (w == outsideVolume) return S.AnimateVolume;
            if (w == lampWindow) return true;   // it sits right against the launcher, so it always goes with it
            return false;
        }

        ElementAnimator AnimatorFor(Window w)
        {
            var root = w.Content as FrameworkElement;
            if (root == null) return null;
            ElementAnimator a;
            if (animators.TryGetValue(w, out a))
            {
                if (a.Element == root) return a;
                // The window kept, its content rebuilt (the detached quick launch strip, after a settings change):
                // animate what's in it now, not the strip that was replaced.
                a.Stop();
            }
            else w.Closed += delegate { animators.Remove(w); };
            a = new ElementAnimator(root);
            animators[w] = a;
            return a;
        }

        /// <summary>
        /// Test copies only: render the launcher window itself (not the screen behind it) partway through closing and
        /// opening, as transparent PNGs in a folder (close20.png, open35.png...): for pictures of the animations.
        /// </summary>
        void TestAnimFrames(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            Action<int, Action> after = (ms, act) =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, ms)) };
                timer.Tick += delegate { timer.Stop(); act(); };
                timer.Start();
            };
            Action<string> frame = name =>
            {
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(ActualWidth * 1.5), (int)Math.Ceiling(ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
                rtb.Render(this);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = System.IO.File.Create(System.IO.Path.Combine(folder, name + ".png"))) enc.Save(fs);
            };
            int close = ElementAnimator.OverlayDuration(S.Animation, false, S.AnimationSpeed), open = ElementAnimator.OverlayDuration(S.Animation, true, S.AnimationSpeed);
            after(5000, () =>
            {
                frame("opened");
                HideLauncher();
                foreach (int pct in new[] { 20, 45, 70 }) { int k = pct; after(close * k / 100, () => frame("close" + k)); }
                after(close + 1200, () =>
                {
                    ShowLauncher();
                    foreach (int pct in new[] { 15, 35, 55, 80 }) { int k = pct; after(open * k / 100, () => frame("open" + k)); }
                    after(open + 800, () => System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "done.txt"), "close " + close + " ms, open " + open + " ms"));
                });
            });
        }

        /// <summary>
        /// Test copies only: picture the launcher and the windows beside it partway through closing (at 8%, 30% and 55%) and
        /// opening (at 10%, 30%, 55% and 80%), as PNGs in a folder.
        /// </summary>
        void TestAnimShots(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            Action<int, Action> after = (ms, act) =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, ms)) };
                timer.Tick += delegate { timer.Stop(); act(); };
                timer.Start();
            };
            int close = ElementAnimator.OverlayDuration(S.Animation, false, S.AnimationSpeed), open = ElementAnimator.OverlayDuration(S.Animation, true, S.AnimationSpeed);
            after(4000, () =>   // once the first opening and the sensor readings are done
            {
                var rect = ScreenArea();   // taken while everything is showing
                HideLauncher();
                foreach (int pct in new[] { 8, 30, 55 })
                {
                    int k = pct;
                    after(close * k / 100, () => TestShot(rect, System.IO.Path.Combine(folder, "close" + k + ".png")));
                }
                after(close + 1200, () =>
                {
                    ShowLauncher();
                    foreach (int pct in new[] { 10, 30, 55, 80 })
                    {
                        int k = pct;
                        after(open * k / 100, () => TestShot(rect, System.IO.Path.Combine(folder, "open" + k + ".png")));
                    }
                    after(open + 600, () => TestShot(rect, System.IO.Path.Combine(folder, "opened.png")));
                });
            });
        }

        /// <summary>
        /// Test copies only: close and then reopen the launcher, counting the frames drawn during each animation,
        /// and write down how smooth it was (frames per second and the longest wait between two frames).
        /// </summary>
        void TestAnimFps(string file)
        {
            Action<int, Action> after = (ms, act) =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, ms)) };
                timer.Tick += delegate { timer.Stop(); act(); };
                timer.Start();
            };
            Action<string, int, Action> measure = (what, ms, then) =>
            {
                var times = new System.Collections.Generic.List<double>();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                EventHandler tick = delegate { times.Add(clock.Elapsed.TotalMilliseconds); };
                CompositionTarget.Rendering += tick;
                after(ms, () =>
                {
                    CompositionTarget.Rendering -= tick;
                    double worst = 0;
                    int slow = 0;
                    for (int i = 1; i < times.Count; i++)
                    {
                        double gap = times[i] - times[i - 1];
                        worst = Math.Max(worst, gap);
                        if (gap > 34) slow++;
                    }
                    double span = times.Count > 1 ? times[times.Count - 1] - times[0] : 0;
                    System.IO.File.AppendAllText(file, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0} {1}: {2} frames in {3:0} ms = {4:0} fps; longest gap {5:0} ms; gaps over 34 ms: {6}\r\n",
                        S.Animation, what, times.Count, span, span > 0 ? (times.Count - 1) * 1000 / span : 0, worst, slow));
                    if (then != null) then();
                });
            };
            int close = ElementAnimator.OverlayDuration(S.Animation, false, S.AnimationSpeed);
            int open = ElementAnimator.OverlayDuration(S.Animation, true, S.AnimationSpeed);
            after(4000, () =>
            {
                measure("closing", close, null);
                HideLauncher();
                after(close + 1200, () =>
                {
                    measure("opening", open, null);
                    ShowLauncher();
                });
            });
        }

        /// <summary>The screen area (in pixels) covering the launcher and the windows beside it that are showing.</summary>
        Rect ScreenArea()
        {
            var all = new System.Collections.Generic.List<Window> { this };
            all.AddRange(Companions());
            var area = Rect.Empty;
            foreach (var w in all)
            {
                if (!w.IsVisible) continue;
                area.Union(new Rect(w.PointToScreen(new Point(0, 0)), w.PointToScreen(new Point(w.ActualWidth, w.ActualHeight))));
            }
            return area;
        }

        static void TestShot(Rect r, string file)
        {
            if (r.IsEmpty) return;
            using (var bmp = new System.Drawing.Bitmap((int)r.Width, (int)r.Height))
            {
                using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen((int)r.X, (int)r.Y, 0, 0, bmp.Size);
                bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>
        /// Test copies only: apply the settings (as OK in Settings does), close and reopen the launcher, and partway
        /// through the opening animation write down how far along the quick launch strip is.
        /// </summary>
        void TestAnimCheck(string file)
        {
            ApplySettings();
            HideLauncher();
            Action<string> report = when =>
            {
                var strip = dockWindow == null ? null : dockWindow.Content as FrameworkElement;
                ElementAnimator a;
                bool bound = strip != null && animators.TryGetValue(dockWindow, out a) && a.Element == strip;
                var g = strip == null ? null : strip.RenderTransform as TransformGroup;
                var sc = g == null || g.Children.Count == 0 ? null : g.Children[0] as ScaleTransform;
                System.IO.File.AppendAllText(file, string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0}: strip shown {1}; animator on the strip in the window {2}; opacity {3:0.00}; scale {4}\r\n",
                    when, dockWindow != null && dockWindow.IsVisible, bound, strip == null ? -1 : strip.Opacity,
                    sc == null ? "none" : sc.ScaleX.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
            };
            Action<int, Action> after = (ms, act) =>
            {
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                timer.Tick += delegate { timer.Stop(); act(); };
                timer.Start();
            };
            after(1500, () =>
            {
                report("closed");
                ShowLauncher();
                after(120, () => report("opening"));
                after(2500, () => report("opened"));
            });
        }

        System.Collections.Generic.IEnumerable<Window> Companions()
        {
            if (notesWindow != null) yield return notesWindow;
            if (sensorWindow != null) yield return sensorWindow;
            if (dockWindow != null) yield return dockWindow;
            if (lampWindow != null) yield return lampWindow;
            if (outsideVolume != null) yield return outsideVolume;
        }

        /// <summary>Before the windows beside the launcher are shown: cancel any closing animation, and start the animated ones invisible.</summary>
        void PrepareCompanions(bool appearing)
        {
            foreach (var w in Companions())
            {
                ElementAnimator a;
                if (animators.TryGetValue(w, out a)) a.Stop();   // also cancels a "hide when done" still pending
                if (appearing && Animates(w)) { var an = AnimatorFor(w); if (an != null) an.Prepare(S.Animation); }
            }
        }

        void AnimateCompanionsIn()
        {
            foreach (var w in Companions())
                if (w.IsVisible && Animates(w)) { var a = AnimatorFor(w); if (a != null) a.In(S.Animation, S.AnimationSpeed); }
        }

        /// <summary>Hides a window beside the launcher, with the animation when it's chosen to animate.</summary>
        void HideCompanion(Window w)
        {
            if (w == null) return;
            var a = w.IsVisible && Animates(w) ? AnimatorFor(w) : null;
            if (a == null) { w.Hide(); return; }
            a.Out(S.Animation, S.AnimationSpeed, () => w.Hide());
        }
    }

    /// <summary>An open / close animation drawn over a window's content while it plays (TV static, flip).</summary>
    internal interface IOverlayAnimation
    {
        void Stop();
    }

    /// <summary>The open / close animations for any window's content: fade, slide up, slide down, zoom, pop, blur, TV static and flip.</summary>
    internal class ElementAnimator
    {
        const double SlideDistance = 48;
        readonly FrameworkElement el;
        readonly ScaleTransform scale = new ScaleTransform(1, 1);
        readonly TranslateTransform move = new TranslateTransform();
        int token;
        IOverlayAnimation overlay;

        /// <summary>The element this animates (a window's content when it was made).</summary>
        public FrameworkElement Element { get { return el; } }

        public ElementAnimator(FrameworkElement el)
        {
            this.el = el;
            var g = new TransformGroup();
            g.Children.Add(scale);
            g.Children.Add(move);
            el.RenderTransform = g;
            el.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        /// <summary>How long an animation step lasts at a speed (100 = normal, 200 = twice as fast, 50 = half as fast).</summary>
        public static int Ms(int normalMs, int speed)
        {
            return Math.Max(15, normalMs * 100 / Math.Max(25, Math.Min(400, speed)));
        }

        /// <summary>Starts an animation that's drawn over the element (TV static, flip); null for the others, or if it can't start.</summary>
        public static IOverlayAnimation StartOverlay(OpenAnimation a, FrameworkElement el, bool opening, int speed, Action done)
        {
            if (a == OpenAnimation.Static) return TvStatic.Start(el, opening, speed, done);
            if (a == OpenAnimation.Flip) return WindowFlip.Start(el, opening, speed, done);
            if (a == OpenAnimation.Rain) return DigitalRain.Start(el, opening, speed, done);
            if (a == OpenAnimation.Beam) return Teleport.Start(el, opening, speed, done);
            if (a == OpenAnimation.Glass) return Glass.Start(el, opening, speed, done);
            return null;
        }

        /// <summary>About how long opening or closing takes with an animation, in milliseconds.</summary>
        public static int OverlayDuration(OpenAnimation a, bool opening, int speed)
        {
            if (a == OpenAnimation.Static) return TvStatic.Duration(opening, speed);
            if (a == OpenAnimation.Flip) return WindowFlip.Duration(opening, speed);
            if (a == OpenAnimation.Rain) return DigitalRain.Duration(opening, speed);
            if (a == OpenAnimation.Beam) return Teleport.Duration(opening, speed);
            if (a == OpenAnimation.Glass) return Glass.Duration(opening, speed);
            return Ms(opening ? (a == OpenAnimation.Pop ? 340 : 240) : 170, speed);
        }

        public void Prepare(OpenAnimation a)
        {
            Stop();
            if (a != OpenAnimation.None) el.Opacity = 0;
        }

        public void In(OpenAnimation a, int speed)
        {
            Stop();
            if (a == OpenAnimation.None) return;
            if ((overlay = StartOverlay(a, el, true, speed, null)) != null) return;
            int t = token;
            int ms = Ms(a == OpenAnimation.Pop ? 340 : 240, speed);
            IEasingFunction ease = a == OpenAnimation.Pop
                ? (IEasingFunction)new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseOut };
            Run(el, UIElement.OpacityProperty, 0, 1, Math.Min(ms, Ms(200, speed)), new QuadraticEase { EasingMode = EasingMode.EaseOut }, null);
            Moves(a, true, ms, ease, () => { if (t == token) el.Effect = null; });
        }

        public void Out(OpenAnimation a, int speed, Action done)
        {
            Stop();
            if (a == OpenAnimation.None) { done(); return; }
            int t = token;
            Action finish = () =>
            {
                if (t != token) return;   // shown again while closing
                done();
                Stop();                   // ready, unchanged, for next time
            };
            if ((overlay = StartOverlay(a, el, false, speed, finish)) != null) return;
            int ms = Ms(170, speed);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            Run(el, UIElement.OpacityProperty, 1, 0, ms, ease, finish);
            Moves(a, false, ms, ease, null);
        }

        void Moves(OpenAnimation a, bool opening, int ms, IEasingFunction ease, Action blurDone)
        {
            switch (a)
            {
                case OpenAnimation.SlideUp: Run(move, TranslateTransform.YProperty, opening ? SlideDistance : 0, opening ? 0 : SlideDistance, ms, ease, null); break;
                case OpenAnimation.SlideDown: Run(move, TranslateTransform.YProperty, opening ? -SlideDistance : 0, opening ? 0 : -SlideDistance, ms, ease, null); break;
                case OpenAnimation.Zoom: Scale(opening ? 0.9 : 1, opening ? 1 : 0.9, ms, ease); break;
                case OpenAnimation.Pop: Scale(opening ? 0.8 : 1, opening ? 1 : 0.8, ms, ease); break;
                case OpenAnimation.Blur:
                    var blur = new BlurEffect { Radius = opening ? 28 : 0, RenderingBias = RenderingBias.Performance };
                    el.Effect = blur;
                    Run(blur, BlurEffect.RadiusProperty, opening ? 28 : 0, opening ? 0 : 28, ms, ease, opening ? blurDone : null);
                    break;
            }
        }

        public void Stop()
        {
            token++;
            if (overlay != null) { overlay.Stop(); overlay = null; }
            el.BeginAnimation(UIElement.OpacityProperty, null);
            el.Opacity = 1;
            move.BeginAnimation(TranslateTransform.YProperty, null);
            move.Y = 0;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            scale.ScaleX = 1;
            scale.ScaleY = 1;
            el.Effect = null;
        }

        void Scale(double from, double to, int ms, IEasingFunction ease)
        {
            Run(scale, ScaleTransform.ScaleXProperty, from, to, ms, ease, null);
            Run(scale, ScaleTransform.ScaleYProperty, from, to, ms, ease, null);
        }

        static void Run(IAnimatable target, DependencyProperty prop, double from, double to, int ms, IEasingFunction ease, Action completed)
        {
            var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
            if (completed != null) anim.Completed += delegate { completed(); };
            target.BeginAnimation(prop, anim);
        }
    }
}
