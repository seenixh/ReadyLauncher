using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReadyLauncher
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorCo { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr notify);
        void UnregisterControlChangeNotify(IntPtr notify);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float levelDB, ref Guid ctx);
        void SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        void GetMasterVolumeLevel(out float levelDB);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint ch, float levelDB, ref Guid ctx);
        void SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        void GetChannelVolumeLevel(uint ch, out float levelDB);
        void GetChannelVolumeLevelScalar(uint ch, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    /// <summary>Master volume of the default playback device (Core Audio).</summary>
    internal static class SystemVolume
    {
        static Guid ctx = Guid.Empty;

        static IAudioEndpointVolume Endpoint()
        {
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCo();
                if (en.GetDefaultAudioEndpoint(0 /*render*/, 1 /*multimedia*/, out dev) != 0 || dev == null) return null;
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                object o;
                if (dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out o) != 0) return null;
                return o as IAudioEndpointVolume;
            }
            catch (Exception) { return null; }
            finally { Native.Release(dev); Native.Release(en); }
        }

        public static bool TryGet(out float level, out bool muted)
        {
            level = 0; muted = false;
            var ep = Endpoint();
            if (ep == null) return false;
            try { ep.GetMasterVolumeLevelScalar(out level); ep.GetMute(out muted); return true; }
            catch (Exception) { return false; }
            finally { Native.Release(ep); }
        }

        public static void SetLevel(float level)
        {
            var ep = Endpoint();
            if (ep == null) return;
            try { ep.SetMasterVolumeLevelScalar(Math.Max(0f, Math.Min(1f, level)), ref ctx); }
            catch (Exception) { }
            finally { Native.Release(ep); }
        }

        public static void SetMute(bool mute)
        {
            var ep = Endpoint();
            if (ep == null) return;
            try { ep.SetMute(mute, ref ctx); }
            catch (Exception) { }
            finally { Native.Release(ep); }
        }
    }

    /// <summary>Vertical volume slider with a mute button and a "Mixer" link.</summary>
    internal class VolumePanel : Border
    {
        readonly Slider slider;
        readonly TextBlock muteGlyph, percent;
        readonly DispatcherTimer timer;
        bool updating, muted;

        public VolumePanel(Brush background, Brush foreground, Brush border)
        {
            Width = 58;
            Padding = new Thickness(6, 10, 6, 8);
            CornerRadius = new CornerRadius(12);
            Background = background;
            if (border != null)
            {
                BorderBrush = border;
                BorderThickness = new Thickness(1);
            }

            var dock = new DockPanel();
            muteGlyph = new TextBlock { FontFamily = TileView.IconFont, FontSize = 18, Text = "", Foreground = foreground };
            var muteBtn = FlatButton(muteGlyph, "Mute / unmute");
            muteBtn.Click += delegate { SystemVolume.SetMute(!muted); Refresh(); };
            DockPanel.SetDock(muteBtn, Dock.Top);
            dock.Children.Add(muteBtn);

            var mixer = FlatButton(new TextBlock { Text = "Mixer", FontSize = 11, Foreground = foreground }, "Open the volume mixer");
            mixer.Click += delegate { try { Process.Start("sndvol.exe"); } catch (Exception) { } };
            DockPanel.SetDock(mixer, Dock.Bottom);
            dock.Children.Add(mixer);

            percent = new TextBlock { FontSize = 11, Foreground = foreground, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            DockPanel.SetDock(percent, Dock.Bottom);
            dock.Children.Add(percent);

            slider = new Slider
            {
                Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, MinHeight = 90,
                IsMoveToPointEnabled = true, SmallChange = 2, LargeChange = 10,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4)
            };
            slider.ValueChanged += delegate
            {
                percent.Text = Math.Round(slider.Value) + "%";
                if (!updating) SystemVolume.SetLevel((float)(slider.Value / 100.0));
            };
            var color = foreground as SolidColorBrush;
            if (color != null) slider.Template = SliderLook(color.Color);   // the slider in the volume control's color too
            dock.Children.Add(slider);
            Child = dock;

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += delegate { Refresh(); };
            IsVisibleChanged += delegate { if (IsVisible) { Refresh(); timer.Start(); } else timer.Stop(); };
        }

        /// <summary>A slim vertical slider in one color: solid below the knob (the volume), faint above it.</summary>
        static ControlTemplate SliderLook(Color c)
        {
            string hex = Color.FromRgb(c.R, c.G, c.B).ToString();
            string part = "<RepeatButton Command='{x:Static Slider.{0}}' Focusable='False' IsTabStop='False'><RepeatButton.Template>"
                        + "<ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'>"
                        + "<Border Width='4' CornerRadius='2' HorizontalAlignment='Center' Background='" + hex + "' Opacity='{1}'/>"
                        + "</Border></ControlTemplate></RepeatButton.Template></RepeatButton>";
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
              + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Slider}'>"
              + "<Grid Background='Transparent' MinWidth='22'><Track x:Name='PART_Track' Orientation='Vertical'>"
              + "<Track.DecreaseRepeatButton>" + part.Replace("{0}", "DecreaseLarge").Replace("{1}", "1") + "</Track.DecreaseRepeatButton>"
              + "<Track.IncreaseRepeatButton>" + part.Replace("{0}", "IncreaseLarge").Replace("{1}", "0.3") + "</Track.IncreaseRepeatButton>"
              + "<Track.Thumb><Thumb Focusable='False'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'>"
              + "<Ellipse Width='16' Height='16' Fill='" + hex + "'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>"
              + "</Track></Grid></ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        static Button FlatButton(UIElement content, string tip)
        {
            return new Button
            {
                Content = content, ToolTip = tip, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Padding = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Center, Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public void Refresh()
        {
            float level; bool m;
            if (!SystemVolume.TryGet(out level, out m)) { percent.Text = "n/a"; return; }
            updating = true;
            slider.Value = Math.Round(level * 100);
            updating = false;
            percent.Text = Math.Round(level * 100) + "%";
            muted = m;
            muteGlyph.Text = m ? "" : "";
        }
    }

    /// <summary>Small borderless window that hosts the volume panel beside the launcher.</summary>
    internal class VolumeWindow : Window
    {
        public VolumeWindow(Brush background, Brush foreground, Brush border)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            panel = new VolumePanel(background, foreground, border) { Height = 230, VerticalAlignment = VerticalAlignment.Top };
            Content = panel;
        }

        readonly VolumePanel panel;

        /// <summary>
        /// Windows won't make this window narrower than ~136px, so pin the 58px panel to the side that
        /// faces the launcher; the spare (invisible, click-through) width then sits on the far side.
        /// </summary>
        public void SetSide(bool leftOfLauncher)
        {
            panel.HorizontalAlignment = leftOfLauncher ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        /// <summary>Small (0.8) to extra large (1.5); everything in the panel grows together.</summary>
        public void SetScale(double scale)
        {
            panel.LayoutTransform = Math.Abs(scale - 1) < 0.01 ? Transform.Identity : new ScaleTransform(scale, scale);
        }
    }
}
