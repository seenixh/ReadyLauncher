using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>The blinking lamp strip in its own window, right against the launcher's top or bottom edge.</summary>
    internal partial class LauncherWindow
    {
        LampWindow lampWindow;

        bool LampAbove { get { return S.LampPanel == LampPanelPlace.AboveLauncher; } }
        bool LampBelow { get { return S.LampPanel == LampPanelPlace.BelowLauncher; } }
        CornerRadius panelCorners = new CornerRadius(Corner);

        /// <summary>
        /// The launcher panel's corners: rounded, except where the lamp strip sits right against it, so the two meet
        /// in a clean straight line with no notch at either end.
        /// </summary>
        void ApplyPanelCorners()
        {
            double top = LampAbove ? 0 : Corner, bottom = LampBelow ? 0 : Corner;
            panelCorners = new CornerRadius(top, top, bottom, bottom);
            shadowBorder.CornerRadius = panelCorners;
            bgColor.CornerRadius = panelCorners;
            bgImage.CornerRadius = panelCorners;
            frame.CornerRadius = panelCorners;
            ClipContent();
        }

        /// <summary>Clip the icons and strips to the inside of the panel's frame.</summary>
        void ClipContent()
        {
            var c = panelCorners;
            contentGrid.Clip = Shapes.Rounded(new Rect(0, 0, contentGrid.ActualWidth, contentGrid.ActualHeight),
                new CornerRadius(Math.Max(0, c.TopLeft - 1), Math.Max(0, c.TopRight - 1), Math.Max(0, c.BottomRight - 1), Math.Max(0, c.BottomLeft - 1)));
        }

        /// <summary>Create, show or hide the floating lamp strip to match the settings and the launcher.</summary>
        void UpdateLampWindow()
        {
            if (!LampAbove && !LampBelow)
            {
                CloseLampWindow();
                return;
            }
            if (lampWindow == null)
            {
                var edge = Color.FromArgb((byte)(255 * Math.Max(0.4, bgAlpha)), FrameBorder.R, FrameBorder.G, FrameBorder.B);
                lampWindow = new LampWindow(S.LampMode, edge, Corner, LampAbove);
                lampWindow.Topmost = !inBackground;   // behind other windows too while the launcher is sent to the background
                lampWindow.SizeChanged += delegate { PlaceSatellites(); };
            }
            if (!IsVisible) { lampWindow.Hide(); return; }
            if (lampWindow.Owner == null) lampWindow.Owner = this;
            lampWindow.Show();
            PlaceSatellites();
        }

        void CloseLampWindow()
        {
            if (lampWindow != null) { lampWindow.Close(); lampWindow = null; }
        }

        /// <summary>The launcher's width, touching its top or bottom edge (no gap).</summary>
        void PlaceLampWindow()
        {
            if (lampWindow == null || !lampWindow.IsVisible) return;
            double panelW = ActualWidth - 2 * Shadow;
            lampWindow.Width = panelW;
            lampWindow.UpdateLayout();
            lampWindow.Left = Left + Shadow;
            lampWindow.Top = LampAbove ? Top + Shadow - lampWindow.ActualHeight : Top + ActualHeight - Shadow;
        }

        /// <summary>The top edge of the launcher, or of a floating lamp strip above it (what a strip or sensors above line up to).</summary>
        double TopEdge()
        {
            if (LampAbove && lampWindow != null && lampWindow.IsVisible) return lampWindow.Top;
            return Top + Shadow;
        }

        /// <summary>The bottom edge of the launcher, or of a floating lamp strip below it.</summary>
        double BottomEdge()
        {
            if (LampBelow && lampWindow != null && lampWindow.IsVisible) return lampWindow.Top + lampWindow.ActualHeight;
            return Top + ActualHeight - Shadow;
        }
    }

    /// <summary>
    /// Borderless window that holds the lamp strip above or below the launcher. It touches the launcher, so the side
    /// against it is square and has no edge line: the two read as one piece.
    /// </summary>
    internal class LampWindow : Window
    {
        public LampWindow(LampActivity mode, Color edge, double corner, bool above)
        {
            Title = "Lamp strip";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            SizeToContent = SizeToContent.Height;
            double r = Math.Max(0, corner - 1);
            var lamps = new LampPanel(mode) { Corners = above ? new CornerRadius(r, r, 0, 0) : new CornerRadius(0, 0, r, r) };
            Content = new Border
            {
                CornerRadius = above ? new CornerRadius(corner, corner, 0, 0) : new CornerRadius(0, 0, corner, corner),
                BorderThickness = above ? new Thickness(1, 1, 1, 0) : new Thickness(1, 0, 1, 1),
                BorderBrush = new SolidColorBrush(edge),
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x0C, 0x0D)),
                Child = lamps
            };
        }
    }
}
