using System;
using System.Collections.Generic;
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

    /// <summary>How the volume control looks: a slider, a column of buttons that each set one volume, or both.</summary>
    internal class VolumeLook
    {
        public VolumeStyleMode Style = VolumeStyleMode.Slider;
        public bool SliderToo;              // buttons: the slider beside them as well
        public VolumeButtonSize Size = VolumeButtonSize.Small;   // buttons: how tall each one is
        public int[] Steps = new int[0];    // buttons: the volume each one sets, top to bottom
        public bool Buttons { get { return Style != VolumeStyleMode.Slider; } }
        public bool ShowSlider { get { return !Buttons || SliderToo; } }
    }

    /// <summary>
    /// The volume control: a mute button, a vertical slider and/or a column of volume buttons (the one nearest the
    /// volume now is lit), and a "Mixer" link.
    /// </summary>
    internal class VolumePanel : Border
    {
        readonly Slider slider;              // null when only the buttons show
        readonly TextBlock muteGlyph, percent;
        readonly TextBox exactBox;           // typing an exact volume, in place of the percentage
        const string ExactBoxName = "exactVolume";
        readonly StackPanel stepColumn;      // null for the slider alone
        readonly List<Button> stepButtons = new List<Button>();
        int[] steps = new int[0];
        readonly Brush litBack, litText, dimBack, dimText;
        readonly double stepHeight, stepFont;
        readonly DispatcherTimer timer;
        bool updating, muted, sliderPressed;
        int level = -1;   // the volume now, 0-100 (-1: unknown)

        /// <summary>Right-click on volume button number n (from the top): choose the volume it sets.</summary>
        public event Action<int> StepEditRequested;
        /// <summary>A volume was set: a volume button clicked, or the slider moved and let go.</summary>
        public event Action VolumeChosen;

        public VolumePanel(Brush background, Brush foreground, Brush border, VolumeLook look)
        {
            if (look == null) look = new VolumeLook();
            var fc = foreground as SolidColorBrush;
            Color c = fc != null ? fc.Color : Colors.White;
            litBack = Frozen(Color.FromRgb(c.R, c.G, c.B));
            litText = Frozen((0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.5 ? Color.FromRgb(0x1B, 0x1B, 0x1B) : Colors.White);
            dimBack = Frozen(Color.FromArgb(0x38, c.R, c.G, c.B));
            dimText = Frozen(Color.FromArgb(0xD0, c.R, c.G, c.B));
            bool twenty = look.Steps.Length > 10;
            int size = (int)look.Size;   // small, medium, large
            stepHeight = twenty ? 12 + 4 * size : 14 + 6 * size;
            stepFont = (twenty ? 9 : 10) + size;

            Width = look.Buttons && look.SliderToo ? 92 : 58;
            if (!look.Buttons) Height = 230;   // the slider fills a set height; the buttons decide their own
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

            if (look.ShowSlider)
            {
                // The volume in numbers; click it to type an exact one.
                percent = new TextBlock
                {
                    FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = foreground, Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(6, 0, 6, 0),
                    Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Click to type an exact volume"
                };
                percent.MouseLeftButtonUp += (s, e) => { e.Handled = true; StartExact(); };
                exactBox = new TextBox
                {
                    Name = ExactBoxName, FontSize = 14, FontWeight = FontWeights.SemiBold, Width = 44, MaxLength = 3,
                    HorizontalAlignment = HorizontalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 1), Padding = new Thickness(0), Visibility = Visibility.Collapsed,
                    Foreground = foreground, CaretBrush = foreground, BorderBrush = foreground, Background = dimBack, SelectionBrush = litBack,
                    Template = ExactBoxTemplate(),
                    ToolTip = "Type a volume from 0 to 100, then press Enter (Esc to cancel)"
                };
                System.Windows.Automation.AutomationProperties.SetName(exactBox, "Exact volume");
                exactBox.PreviewTextInput += (s, e) => { foreach (char ch in e.Text) if (!char.IsDigit(ch)) e.Handled = true; };
                exactBox.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; FinishExact(true); }
                    else if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; FinishExact(false); }
                };
                exactBox.LostKeyboardFocus += delegate { FinishExact(true); };   // clicking elsewhere keeps what was typed
                var value = new Grid();
                value.Children.Add(percent);
                value.Children.Add(exactBox);
                DockPanel.SetDock(value, Dock.Bottom);
                dock.Children.Add(value);

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
                // Pressed and let go (a click on the bar or a drag of the knob): a volume was set.
                slider.PreviewMouseLeftButtonDown += delegate { sliderPressed = true; };
                slider.PreviewMouseLeftButtonUp += delegate
                {
                    if (!sliderPressed) return;
                    sliderPressed = false;
                    if (VolumeChosen != null) VolumeChosen();
                };
                if (fc != null) slider.Template = SliderLook(fc.Color);   // the slider in the volume control's color too
                System.Windows.Automation.AutomationProperties.SetName(slider, "Volume");
            }

            if (!look.Buttons) dock.Children.Add(slider);
            else
            {
                // The buttons, with the slider beside them if chosen.
                var middle = new Grid();
                stepColumn = new StackPanel { Width = slider != null ? 52 : 46, VerticalAlignment = VerticalAlignment.Top };
                if (slider != null)
                {
                    // The slider lines up with the buttons: 100% at the middle of the top button, each button's step
                    // further down level with that button, and 0% half a button below the lowest one. (The knob's
                    // middle travels the slider's height less the knob, so the slider is one knob taller than that.)
                    double pitch = stepHeight + 2, knob = SliderKnob;
                    double shift = Math.Max(0, knob / 2 - pitch / 2);   // room for the knob above the top button
                    stepColumn.Margin = new Thickness(0, shift, 0, 0);
                    slider.MinHeight = 0;
                    slider.VerticalAlignment = VerticalAlignment.Top;
                    slider.Height = look.Steps.Length * pitch + knob;
                    slider.Margin = new Thickness(0, shift + pitch / 2 - knob / 2, 6, 0);
                    middle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    middle.Children.Add(slider);
                }
                middle.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(stepColumn, slider != null ? 1 : 0);
                middle.Children.Add(stepColumn);
                // Made smaller together (never bigger) when they don't fit, e.g. tall buttons inside a short launcher.
                var fit = new Viewbox
                {
                    Child = middle, StretchDirection = StretchDirection.DownOnly, Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 0, 2)
                };
                dock.Children.Add(fit);
                SetSteps(look.Steps);
            }
            Child = dock;

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += delegate { Refresh(); };
            IsVisibleChanged += delegate { if (IsVisible) { Refresh(); timer.Start(); } else timer.Stop(); };
        }

        static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        /// <summary>The exact-volume box: a plain rounded box in the volume control's color (no blue Windows focus border).</summary>
        static ControlTemplate ExactBoxTemplate()
        {
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
              + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TextBox}'>"
              + "<Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='3'>"
              + "<ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>"
              + "</Border></ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        /// <summary>The box for typing an exact volume (Esc there cancels the typing, nothing else).</summary>
        public static bool IsExactVolumeBox(object source)
        {
            var box = source as TextBox;
            return box != null && box.Name == ExactBoxName;
        }

        /// <summary>The percentage becomes a box with the volume now, ready to type over.</summary>
        void StartExact()
        {
            if (exactBox == null || exactBox.Visibility == Visibility.Visible) return;
            exactBox.Text = level >= 0 ? level.ToString() : "";
            percent.Visibility = Visibility.Hidden;
            exactBox.Visibility = Visibility.Visible;
            // Outside the launcher the volume control is a window of its own: make it the active one so typing reaches the box.
            var w = Window.GetWindow(this);
            if (w != null && !w.IsActive && !Program.TestMode) w.Activate();   // test copies never take the focus from you
            exactBox.Focus();
            exactBox.SelectAll();
        }

        /// <summary>Back to the percentage; with <paramref name="keep"/>, a whole number from 0 to 100 becomes the volume.</summary>
        void FinishExact(bool keep)
        {
            if (exactBox == null || exactBox.Visibility != Visibility.Visible) return;
            exactBox.Visibility = Visibility.Collapsed;
            percent.Visibility = Visibility.Visible;
            int v;
            if (keep && int.TryParse(exactBox.Text.Trim(), out v) && v >= 0 && v <= 100 && v != level)
            {
                SystemVolume.SetLevel(v / 100f);
                if (muted && v > 0) SystemVolume.SetMute(false);   // a volume you can hear
            }
            Refresh();
        }

        /// <summary>Test copies only: click the percentage, type <paramref name="text"/> and press Enter (or Esc).</summary>
        public void TestExact(string text, bool enter)
        {
            StartExact();
            if (exactBox == null || text == null) return;   // null: just open the box
            exactBox.Text = text;
            FinishExact(enter);
        }

        /// <summary>Test copies only: click the percentage and press Esc, the key going through the window like a real one.</summary>
        public void TestEscapeKey()
        {
            StartExact();
            var source = exactBox == null ? null : PresentationSource.FromVisual(exactBox);
            if (source == null) return;
            exactBox.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, System.Windows.Input.Key.Escape)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
            });
        }

        const double SliderKnob = 16;   // the slider's round knob (see SliderLook)

        static ControlTemplate stepTemplate;

        /// <summary>A volume button: a small rounded block, a little lighter under the mouse and when pressed.</summary>
        static ControlTemplate StepTemplate()
        {
            if (stepTemplate != null) return stepTemplate;
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' "
              + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Button}'>"
              + "<Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='3'>"
              + "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>"
              + "<ControlTemplate.Triggers>"
              + "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.8'/></Trigger>"
              + "<Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.6'/></Trigger>"
              + "</ControlTemplate.Triggers></ControlTemplate>";
            stepTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
            return stepTemplate;
        }

        /// <summary>The volume each button sets, top to bottom (the buttons are made again).</summary>
        public void SetSteps(int[] values)
        {
            if (stepColumn == null) return;
            steps = values ?? new int[0];
            stepColumn.Children.Clear();
            stepButtons.Clear();
            for (int i = 0; i < steps.Length; i++)
            {
                int k = i, v = steps[i];
                var b = new Button
                {
                    Template = StepTemplate(), Height = stepHeight, Margin = new Thickness(0, 1, 0, 1),
                    Cursor = System.Windows.Input.Cursors.Hand, Focusable = false,
                    Content = new TextBlock { Text = v + "%", FontSize = stepFont, FontWeight = FontWeights.SemiBold },
                    ToolTip = "Set the volume to " + v + "%\nRight-click to choose the volume this button sets"
                };
                System.Windows.Automation.AutomationProperties.SetName(b, "Volume " + v + "%");
                b.Click += delegate
                {
                    SystemVolume.SetLevel(v / 100f);
                    if (muted) SystemVolume.SetMute(false);   // a volume you can hear
                    Refresh();
                    if (VolumeChosen != null) VolumeChosen();
                };
                b.PreviewMouseRightButtonDown += (s, e) => { e.Handled = true; };
                b.PreviewMouseRightButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    if (StepEditRequested != null) StepEditRequested(k);
                };
                stepButtons.Add(b);
                stepColumn.Children.Add(b);
            }
            Refresh();
        }

        static System.Windows.Input.MouseButtonEventArgs Press(System.Windows.Input.MouseButton button, RoutedEvent ev)
        {
            return new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = ev };
        }

        /// <summary>Test copies only: a right-click on button <paramref name="index"/>, as the mouse sends it.</summary>
        public void TestRightClick(int index)
        {
            if (index < 0 || index >= stepButtons.Count) return;
            var b = stepButtons[index];
            b.RaiseEvent(Press(System.Windows.Input.MouseButton.Right, UIElement.PreviewMouseRightButtonDownEvent));
            b.RaiseEvent(Press(System.Windows.Input.MouseButton.Right, UIElement.PreviewMouseRightButtonUpEvent));
        }

        /// <summary>
        /// Test copies only: press and let go on the slider, as the mouse does, without moving it (move-to-point is off
        /// meanwhile, so the volume can't jump to wherever the real pointer happens to be).
        /// </summary>
        public void TestSliderPress()
        {
            if (slider == null) return;
            slider.IsMoveToPointEnabled = false;
            slider.RaiseEvent(Press(System.Windows.Input.MouseButton.Left, UIElement.PreviewMouseLeftButtonDownEvent));
            slider.RaiseEvent(Press(System.Windows.Input.MouseButton.Left, UIElement.PreviewMouseLeftButtonUpEvent));
            slider.IsMoveToPointEnabled = true;
        }

        /// <summary>
        /// Only the button for the volume now is lit (the nearest one, the lower of two equally near); the rest are
        /// faint, all of them while muted.
        /// </summary>
        void ShowLevel()
        {
            int current = -1;
            if (!muted && level >= 0)
                foreach (int v in steps)
                {
                    int d = Math.Abs(v - level), best = current < 0 ? int.MaxValue : Math.Abs(current - level);
                    if (d < best || (d == best && v < current)) current = v;
                }
            for (int i = 0; i < stepButtons.Count; i++)
            {
                bool lit = current >= 0 && steps[i] == current;
                stepButtons[i].Background = lit ? litBack : dimBack;
                stepButtons[i].Foreground = lit ? litText : dimText;
            }
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
              + "<Ellipse Width='" + SliderKnob + "' Height='" + SliderKnob + "' Fill='" + hex + "'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>"
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
            float now; bool m;
            if (!SystemVolume.TryGet(out now, out m))
            {
                if (percent != null) percent.Text = "n/a";
                level = -1;
                ShowLevel();
                return;
            }
            level = (int)Math.Round(now * 100);
            if (slider != null)
            {
                updating = true;
                slider.Value = level;
                updating = false;
            }
            if (percent != null) percent.Text = level + "%";
            muted = m;
            muteGlyph.Text = m ? "" : "";
            ShowLevel();
        }
    }

    /// <summary>Small borderless window that hosts the volume panel beside the launcher.</summary>
    internal class VolumeWindow : Window
    {
        public VolumeWindow(Brush background, Brush foreground, Brush border, VolumeLook look)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            panel = new VolumePanel(background, foreground, border, look) { VerticalAlignment = VerticalAlignment.Top };
            Content = panel;
        }

        readonly VolumePanel panel;
        public VolumePanel Panel { get { return panel; } }

        /// <summary>
        /// Windows won't make this window narrower than ~136px, so pin the 58px (or 92px) panel to the side that
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
