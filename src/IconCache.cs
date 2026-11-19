using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReadyLauncher
{
    /// <summary>Loads crisp, alpha-correct shell icons for files, shortcuts and Store apps.</summary>
    internal static class IconCache
    {
        static readonly Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        static readonly object gate = new object();   // icons can be pre-loaded on a background thread

        public static ImageSource Get(string path, int size)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return Cached(path + "|" + size, () => Load(path, size));
        }

        static ImageSource Cached(string key, Func<ImageSource> load)
        {
            ImageSource img;
            lock (gate) { if (cache.TryGetValue(key, out img)) return img; }
            img = load();
            lock (gate) { cache[key] = img; }
            return img;
        }

        public static void Forget(string path)
        {
            lock (gate)
            {
                var dead = new List<string>();
                foreach (var k in cache.Keys) if (k.StartsWith(path + "|", StringComparison.OrdinalIgnoreCase)) dead.Add(k);
                foreach (var k in dead) cache.Remove(k);
            }
        }

        static readonly string[] pictureTypes = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".jfif", ".webp" };

        public static bool IsPicture(string path)
        {
            return !string.IsNullOrEmpty(path) && Array.IndexOf(pictureTypes, System.IO.Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        /// <summary>A chosen icon: a picture file, or icon number <paramref name="index"/> in an .ico/.dll/.exe file.</summary>
        public static ImageSource GetCustom(string path, int index, int size)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return Cached("custom|" + path + "|" + index + "|" + size, () => IsPicture(path) ? LoadPicture(path, size) : ExtractIcon(path, index, size));
        }

        static ImageSource LoadPicture(string path, int size)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.UriSource = new Uri(path);
                b.DecodePixelWidth = size * 2;
                b.EndInit();
                b.Freeze();
                return b;
            }
            catch (Exception) { return null; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[] ids, uint count, uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);

        static ImageSource ExtractIcon(string path, int index, int size)
        {
            var icons = new IntPtr[1];
            var ids = new uint[1];
            try
            {
                path = Environment.ExpandEnvironmentVariables(path);
                int px = size <= 64 ? 64 : size <= 128 ? 128 : 256;
                uint got = PrivateExtractIcons(path, index, px, px, icons, ids, 1, 0);
                if (got == 0 || got == 0xFFFFFFFF || icons[0] == IntPtr.Zero) return null;
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icons[0], System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                src.Freeze();
                return src;
            }
            catch (Exception) { return null; }
            finally { if (icons[0] != IntPtr.Zero) DestroyIcon(icons[0]); }
        }

        static ImageSource Load(string path, int size)
        {
            IShellItem item = null;
            IntPtr hbm = IntPtr.Zero;
            try
            {
                item = Native.CreateItem(path);
                var factory = (IShellItemImageFactory)item;
                var sz = new NativeSize { cx = size, cy = size };
                if (factory.GetImage(sz, Native.SIIGBF_ICONONLY, out hbm) != 0 || hbm == IntPtr.Zero) return null;
                return FromHBitmap(hbm);
            }
            catch (Exception) { return null; }
            finally
            {
                if (hbm != IntPtr.Zero) Native.DeleteObject(hbm);
                Native.Release(item);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
            public uint c0, c1, c2, c3; // room for a color table GDI never needs for 32bpp
        }

        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out BITMAP bm);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFO bmi, uint usage);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        // Read the pixels as top-down 32-bit BGRA (shell icons carry premultiplied alpha).
        static ImageSource FromHBitmap(IntPtr hbm)
        {
            BITMAP bm;
            if (GetObject(hbm, Marshal.SizeOf(typeof(BITMAP)), out bm) == 0) return null;
            int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
            if (w <= 0 || h <= 0) return null;
            var bmi = new BITMAPINFO
            {
                biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0
            };
            var bytes = new byte[w * h * 4];
            IntPtr hdc = GetDC(IntPtr.Zero);
            try { if (GetDIBits(hdc, hbm, 0, (uint)h, bytes, ref bmi, 0) == 0) return null; }
            finally { ReleaseDC(IntPtr.Zero, hdc); }

            bool anyAlpha = false;
            for (int i = 3; i < bytes.Length; i += 4) if (bytes[i] != 0) { anyAlpha = true; break; }
            if (!anyAlpha) for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, bytes, w * 4);
            src.Freeze();
            return src;
        }
    }
}
