using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace ReadyLauncher
{
    /// <summary>
    /// Keeps the app running through a sign-out, restart or shutdown that's canceled. When one starts, Windows asks
    /// every program whether the session may end, and only afterwards says whether it really will (another program may
    /// hold it up, and it may then be canceled). WPF's own answer quits the app on the spot, so a canceled sign-out
    /// used to leave the launcher closed until the next sign-in. This answers "yes" on WPF's behalf and closes the app
    /// (saving first) only when Windows confirms the session is ending.
    /// </summary>
    internal static class SessionEndGuard
    {
        const int WM_QUERYENDSESSION = 0x0011, WM_ENDSESSION = 0x0016, GWLP_WNDPROC = -4;

        delegate IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        static WndProc filter;    // held here for as long as the window calls it
        static IntPtr wpfProc;
        static Action ending;

        /// <summary>
        /// Takes over the question on the app's hidden WPF window (the one WPF answers it on); false when that window
        /// can't be found, and WPF goes on answering as before.
        /// </summary>
        public static bool Install(Application app, Action sessionEnding)
        {
            if (filter != null) return true;
            try
            {
                var field = typeof(Application).GetField("_parkingHwnd", BindingFlags.Instance | BindingFlags.NonPublic);
                object window = field == null ? null : field.GetValue(app);
                var handle = window == null ? null : window.GetType().GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IntPtr hwnd = handle == null ? IntPtr.Zero : (IntPtr)handle.GetValue(window, null);
                if (hwnd == IntPtr.Zero) return false;
                var f = new WndProc(Filter);
                ending = sessionEnding;
                IntPtr old = SetWindowProc(hwnd, Marshal.GetFunctionPointerForDelegate(f));
                if (old == IntPtr.Zero) { ending = null; return false; }
                wpfProc = old;
                filter = f;
                return true;
            }
            catch (Exception) { return false; }
        }

        static IntPtr Filter(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            // "May the session end?" Yes: everything is saved as it changes, so there's nothing to hold it up for.
            if (msg == WM_QUERYENDSESSION) return new IntPtr(1);
            if (msg == WM_ENDSESSION)
            {
                // It is ending: save and close now, as Windows may stop the app at any moment once this returns.
                // Canceled (wParam false): carry on as if nothing happened.
                if (wParam != IntPtr.Zero && ending != null)
                {
                    var a = ending;
                    ending = null;
                    try { a(); } catch (Exception) { }
                }
                return IntPtr.Zero;
            }
            return CallWindowProc(wpfProc, hwnd, msg, wParam, lParam);
        }

        static IntPtr SetWindowProc(IntPtr hwnd, IntPtr proc)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr(hwnd, GWLP_WNDPROC, proc) : new IntPtr(SetWindowLong(hwnd, GWLP_WNDPROC, proc.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        static extern IntPtr CallWindowProc(IntPtr proc, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
