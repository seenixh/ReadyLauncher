using System.Windows;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>Borderless window that holds the quick-launch strip when it is detached from the launcher.</summary>
    internal class DockWindow : Window
    {
        public DockWindow()
        {
            Title = "Quick launch";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        }

        /// <summary>
        /// Horizontal strips size their height to the icons; side strips size their width. Windows won't make a
        /// window narrower than ~136px, so the strip is pinned to the side facing the launcher.
        /// </summary>
        public void SetStrip(FrameworkElement strip, DockPosition pos)
        {
            bool horizontal = pos == DockPosition.Bottom || pos == DockPosition.Top;
            SizeToContent = horizontal ? SizeToContent.Height : SizeToContent.Width;
            strip.HorizontalAlignment = pos == DockPosition.Left ? HorizontalAlignment.Right
                : pos == DockPosition.Right ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            strip.VerticalAlignment = pos == DockPosition.Top ? VerticalAlignment.Bottom
                : pos == DockPosition.Bottom ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            Content = strip;
        }
    }
}
