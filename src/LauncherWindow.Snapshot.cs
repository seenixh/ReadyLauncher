using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>
    /// Test copies only: save each visible window as a transparent PNG, plus where things are,
    /// so documentation images can be made without screen-grabbing whatever is behind the launcher.
    /// </summary>
    internal partial class LauncherWindow
    {
        public void SaveSnapshots(string dir, double scale)
        {
            Directory.CreateDirectory(dir);
            var json = new List<string>();
            Action<string, Window> shot = (name, w) =>
            {
                if (w == null || !w.IsVisible) return;
                var rtb = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * scale), (int)Math.Ceiling(w.ActualHeight * scale),
                    96 * scale, 96 * scale, PixelFormats.Pbgra32);
                rtb.Render(w);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(Path.Combine(dir, name + ".png"))) enc.Save(fs);
                json.Add(Entry("window:" + name, new Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight)));
            };
            shot("launcher", this);
            shot("notes", notesWindow);
            shot("volume", outsideVolume);
            shot("strip", dockWindow);
            shot("lamps", lampWindow);
            shot("sensors", sensorWindow);

            json.Add(Entry("panel", new Rect(Left + Shadow, Top + Shadow, ActualWidth - 2 * Shadow, ActualHeight - 2 * Shadow)));
            foreach (var t in tiles.Values) json.Add(Entry("tile:" + t.Item.Name, ScreenRect(t, this)));
            int n = 0;
            foreach (var v in decorViews.Values)
                json.Add(Entry("decor:" + (v.D.Kind == DecorKind.Text ? v.D.Text : "line" + (n++) + (v.D.Vertical ? "v" : "h")), ScreenRect(v, this)));
            if (dockArea != null && dockArea.IsVisible)
                json.Add(Entry("dock", ScreenRect(dockArea, S.DockDetached ? (Window)dockWindow : this)));
            if (outsideVolume != null && outsideVolume.IsVisible && outsideVolume.Content is FrameworkElement)
                json.Add(Entry("volumepanel", ScreenRect((FrameworkElement)outsideVolume.Content, outsideVolume)));
            File.WriteAllText(Path.Combine(dir, "layout.json"), "{\n" + string.Join(",\n", json) + "\n}\n", Encoding.UTF8);
        }

        static Rect ScreenRect(FrameworkElement el, Window w)
        {
            var r = el.TransformToAncestor(w).TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight));
            r.Offset(w.Left, w.Top);
            return r;
        }

        static string Entry(string key, Rect r)
        {
            var c = CultureInfo.InvariantCulture;
            return string.Format(c, "  \"{0}\": [{1:0.#}, {2:0.#}, {3:0.#}, {4:0.#}]",
                key.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " "), r.X, r.Y, r.Width, r.Height);
        }
    }
}
