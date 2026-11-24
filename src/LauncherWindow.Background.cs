using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ReadyLauncher
{
    /// <summary>
    /// "Send to background": the launcher stays open behind other windows (it doesn't close itself when you click
    /// elsewhere or open an app) until it's closed; the next time it opens, it's always on top again.
    /// </summary>
    internal partial class LauncherWindow
    {
        bool inBackground;
        Border backgroundButton;

        /// <summary>Sent to the background right now (test copies and the header button read this).</summary>
        public bool InBackground { get { return inBackground; } }

        Border BackgroundButton()
        {
            backgroundButton = GlyphButton("", "", b => { if (inBackground) KeepOnTop(); else SendToBackground(); });
            System.Windows.Automation.AutomationProperties.SetName(backgroundButton, "Send to background or keep on top");
            ShowBackgroundButtonState();
            return backgroundButton;
        }

        void ShowBackgroundButtonState()
        {
            if (backgroundButton == null) return;
            ((TextBlock)backgroundButton.Child).Text = inBackground ? "" : "";   // pin / unpin
            backgroundButton.ToolTip = inBackground
                ? "Keep on top again"
                : "Send to background: keep the launcher open behind your other windows until you close it";
        }

        IEnumerable<Window> Satellites()
        {
            if (dockWindow != null) yield return dockWindow;
            if (lampWindow != null) yield return lampWindow;
            if (sensorWindow != null) yield return sensorWindow;
            if (notesWindow != null) yield return notesWindow;
            if (outsideVolume != null) yield return outsideVolume;
        }

        /// <summary>Always on top, or not, for the launcher and the windows that go with it.</summary>
        void ApplyLayer()
        {
            bool top = !inBackground;
            Topmost = top;
            foreach (var w in Satellites()) w.Topmost = top;
        }

        /// <summary>Keeps the launcher open, but behind other windows, and gives the app you were using its turn again.</summary>
        public void SendToBackground()
        {
            if (!IsVisible) return;
            inBackground = true;
            ApplyLayer();
            const uint keep = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
            foreach (var w in Satellites())
            {
                var sh = new WindowInteropHelper(w).Handle;
                if (sh != IntPtr.Zero && w.IsVisible) Native.SetWindowPos(sh, Native.HWND_BOTTOM, 0, 0, 0, 0, keep);
            }
            Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_BOTTOM, 0, 0, 0, 0, keep);
            if (prevForeground != IntPtr.Zero && !Program.TestMode) Native.SetForegroundWindow(prevForeground);
            ShowBackgroundButtonState();
        }

        /// <summary>Back to always on top, and to the front.</summary>
        public void KeepOnTop()
        {
            inBackground = false;
            ApplyLayer();
            Activate();
            ShowBackgroundButtonState();
        }

        /// <summary>No other app's window is over any part of the launcher (checked at points spread across it).</summary>
        bool InPlainSight()
        {
            const int columns = 8, rows = 6;
            double w = ActualWidth - 2 * Shadow, h = ActualHeight - 2 * Shadow;
            bool seen = false;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    var at = PointToScreen(new Point(Shadow + w * (c + 0.5) / columns, Shadow + h * (r + 0.5) / rows));
                    IntPtr there = Native.WindowFromPoint(new Native.ScreenPoint { X = (int)at.X, Y = (int)at.Y });
                    if (there == IntPtr.Zero) continue;   // off the screen
                    uint pid;
                    Native.GetWindowThreadProcessId(there, out pid);
                    if (pid != OwnPid) return false;
                    seen = true;
                }
            return seen;
        }

        /// <summary>When the launcher closes: the next time it opens, it's always on top again.</summary>
        void LeaveBackground()
        {
            if (!inBackground) return;
            inBackground = false;
            ApplyLayer();
            ShowBackgroundButtonState();
        }
    }
}
