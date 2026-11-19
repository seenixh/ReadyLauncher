using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ReadyLauncher
{
    /// <summary>Types a key combination (e.g. Ctrl + Shift + Esc) into whichever window has focus.</summary>
    internal static class KeySender
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        const uint INPUT_KEYBOARD = 1, KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint code, uint mapType);

        public static void Send(ModifierKeys mods, Key key)
        {
            var modVks = new List<ushort>();
            if ((mods & ModifierKeys.Control) != 0) modVks.Add(0x11);
            if ((mods & ModifierKeys.Alt) != 0) modVks.Add(0x12);
            if ((mods & ModifierKeys.Shift) != 0) modVks.Add(0x10);
            if ((mods & ModifierKeys.Windows) != 0) modVks.Add(0x5B);
            ushort main = (ushort)KeyInterop.VirtualKeyFromKey(key);

            var list = new List<INPUT>();
            foreach (var vk in modVks) list.Add(Make(vk, false));
            if (main != 0) { list.Add(Make(main, false)); list.Add(Make(main, true)); }
            for (int i = modVks.Count - 1; i >= 0; i--) list.Add(Make(modVks[i], true));
            SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        static INPUT Make(ushort vk, bool up)
        {
            uint flags = up ? KEYEVENTF_KEYUP : 0;
            if (IsExtended(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
            var input = new INPUT { type = INPUT_KEYBOARD };
            input.u.ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)MapVirtualKey(vk, 0), dwFlags = flags };
            return input;
        }

        static bool IsExtended(ushort vk)
        {
            return (vk >= 0x21 && vk <= 0x28)   // Page Up/Down, End, Home, arrows
                || vk == 0x2D || vk == 0x2E      // Insert, Delete
                || vk == 0x6F || vk == 0x90      // Numpad divide, Num Lock
                || vk == 0x5B || vk == 0x5C      // Windows keys
                || vk == 0xA3 || vk == 0xA5      // right Ctrl / Alt
                || (vk >= 0xA6 && vk <= 0xB7);   // browser, volume and media keys
        }

        /// <summary>Reads a key press into (modifiers, key). False while only a modifier is held.</summary>
        public static bool TryRead(KeyEventArgs e, out ModifierKeys mods, out Key key)
        {
            key = e.Key == Key.System ? e.SystemKey : e.Key;
            mods = Keyboard.Modifiers;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= ModifierKeys.Windows;
            return !(key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin
                || key == Key.None || key == Key.ImeProcessed || key == Key.DeadCharProcessed);
        }
    }
}
