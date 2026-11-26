using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

namespace ReadyLauncher
{
    /// <summary>
    /// The Settings window's look: follow Windows (light or dark), light, light grey, grey, dark grey, dark or Christmas. One set of
    /// control styles reads its colors from "T.*" brushes, so switching themes only swaps the brushes.
    /// </summary>
    internal static class SettingsTheme
    {
        public static readonly string[] Names = { "Follow system (light or dark, like Windows)", "Light", "Light grey", "Grey", "Dark grey", "Dark", "Christmas" };

        static readonly string[] Keys =
        {
            "Window", "Panel", "Control", "ControlHover", "ControlPressed", "Border", "BorderStrong", "Text", "TextMuted",
            "Accent", "AccentHover", "AccentText", "Selection", "Link", "Note", "WarnBack", "WarnBorder", "WarnText",
            "Track", "Thumb", "ThumbHover", "TabHover", "CheckBox"
        };

        static readonly string[][] Palettes =
        {
            // Light
            new[] { "#F3F3F3", "#FCFCFC", "#FFFFFF", "#F6F6F6", "#EBEBEB", "#D3D3D3", "#8A8A8A", "#1B1B1B", "#616161",
                    "#005FB8", "#196EBF", "#FFFFFF", "#DCEAF7", "#005FB8", "#8A5A00", "#FFF4CE", "#E0B42A", "#5C4500",
                    "#C9C9C9", "#ABABAB", "#858585", "#E9E9E9", "#EEEEEE" },
            // Light grey
            new[] { "#D6D6D6", "#E6E6E6", "#F2F2F2", "#FAFAFA", "#E3E3E3", "#A9A9A9", "#6E6E6E", "#161616", "#474747",
                    "#005A9E", "#1A6AA8", "#FFFFFF", "#C7DBEE", "#004E8C", "#6E4600", "#FBEFC4", "#C79F22", "#4A3700",
                    "#B4B4B4", "#959595", "#6E6E6E", "#CACACA", "#D9D9D9" },
            // Grey: halfway between light grey and dark grey, with dark text (it reads better than white on a middle grey)
            new[] { "#858585", "#939393", "#A3A3A3", "#ADADAD", "#999999", "#6B6B6B", "#474747", "#0E0E0E", "#262626",
                    "#00467F", "#0B548F", "#FFFFFF", "#A3B6CA", "#002A55", "#3D2700", "#CBBF93", "#8A6F17", "#332600",
                    "#666666", "#686868", "#4C4C4C", "#8F8F8F", "#898989" },
            // Dark grey
            new[] { "#3A3A3A", "#464646", "#525252", "#5C5C5C", "#4B4B4B", "#686868", "#9A9A9A", "#F3F3F3", "#C8C8C8",
                    "#4CC2FF", "#6FCEFF", "#0F0F0F", "#355D7C", "#8AD3FF", "#F2C76A", "#4C4126", "#A08026", "#F7E3AC",
                    "#6E6E6E", "#8C8C8C", "#ABABAB", "#474747", "#3E3E3E" },
            // Dark
            new[] { "#202020", "#2B2B2B", "#373737", "#3F3F3F", "#303030", "#4A4A4A", "#858585", "#FFFFFF", "#C5C5C5",
                    "#4CC2FF", "#6FCEFF", "#000000", "#1F4A6B", "#6CC4FF", "#E8B656", "#3D341A", "#8A6D1A", "#F2DA9A",
                    "#555555", "#6E6E6E", "#9A9A9A", "#2D2D2D", "#262626" },
            // Christmas: deep pine green, cranberry red buttons, gold links, cream text
            new[] { "#10261A", "#153222", "#1C3D2A", "#244B35", "#2B5A3F", "#2F5A3F", "#5C8C6F", "#F6EEDC", "#C3D3C6",
                    "#C8202F", "#DE3341", "#FFFFFF", "#6E1A22", "#F2C14E", "#F2C14E", "#43300D", "#B0801F", "#FFE2A6",
                    "#2A4A35", "#5F8F70", "#83B094", "#1E4230", "#112B1D" },
        };

        /// <summary>Windows' own app mode (Settings > Personalization > Colors).</summary>
        public static bool WindowsUsesLightApps()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                    return !(v is int) || (int)v != 0;
                }
            }
            catch (Exception) { return true; }
        }

        static int PaletteFor(SettingsThemeMode mode)
        {
            switch (mode)
            {
                case SettingsThemeMode.Light: return 0;
                case SettingsThemeMode.LightGrey: return 1;
                case SettingsThemeMode.Grey: return 2;
                case SettingsThemeMode.DarkGrey: return 3;
                case SettingsThemeMode.Dark: return DarkPalette;
                case SettingsThemeMode.Christmas: return 5;
                default: return WindowsUsesLightApps() ? 0 : DarkPalette;
            }
        }

        const int DarkPalette = 4;

        /// <summary>Whether a palette has light text on a dark window (so the title bar goes dark too).</summary>
        static bool IsDark(int p)
        {
            var t = (Color)ColorConverter.ConvertFromString(Palettes[p][7]);
            return (0.299 * t.R + 0.587 * t.G + 0.114 * t.B) / 255 > 0.5;
        }

        /// <summary>A window in the theme's colors now, and its title bar too once the window exists.</summary>
        public static void Use(Window w, SettingsThemeMode mode)
        {
            Apply(w, mode);
            w.SourceInitialized += delegate { Apply(w, mode); };
        }

        /// <summary>Give <paramref name="w"/> the theme's colors (and its control styles, the first time).</summary>
        public static void Apply(Window w, SettingsThemeMode mode)
        {
            int p = PaletteFor(mode);
            for (int i = 0; i < Keys.Length; i++)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palettes[p][i]));
                b.Freeze();
                w.Resources["T." + Keys[i]] = b;
            }
            if (w.Resources.MergedDictionaries.Count == 0)
                w.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
            w.SetResourceReference(Window.BackgroundProperty, "T.Window");
            w.SetResourceReference(Window.ForegroundProperty, "T.Text");
            TitleBar(w, p);
        }

        /// <summary>The title bar in the theme's colors (Windows 11; older versions keep their own).</summary>
        static void TitleBar(Window w, int p)
        {
            IntPtr h = new WindowInteropHelper(w).Handle;
            if (h == IntPtr.Zero) return;   // done again once the window exists
            int dark = IsDark(p) ? 1 : 0;
            int caption = ColorRef(Palettes[p][0]), text = ColorRef(Palettes[p][7]);
            Native.DwmSetWindowAttribute(h, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
            Native.DwmSetWindowAttribute(h, Native.DWMWA_CAPTION_COLOR, ref caption, 4);
            Native.DwmSetWindowAttribute(h, Native.DWMWA_TEXT_COLOR, ref text, 4);
            Native.SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        }

        /// <summary>
        /// The color of a theme's button: its window color. "Follow system" is split corner to corner between the
        /// light and the dark theme.
        /// </summary>
        public static Brush Swatch(SettingsThemeMode mode)
        {
            Brush b;
            if (mode == SettingsThemeMode.FollowSystem)
            {
                Color light = (Color)ColorConverter.ConvertFromString(Palettes[0][0]), dark = (Color)ColorConverter.ConvertFromString(Palettes[DarkPalette][0]);
                var g = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                g.GradientStops.Add(new GradientStop(light, 0));
                g.GradientStops.Add(new GradientStop(light, 0.5));
                g.GradientStops.Add(new GradientStop(dark, 0.5));
                g.GradientStops.Add(new GradientStop(dark, 1));
                b = g;
            }
            else b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palettes[PaletteFor(mode)][0]));
            b.Freeze();
            return b;
        }

        /// <summary>The theme right-click menus use: the Settings theme (set whenever the settings are applied).</summary>
        public static SettingsThemeMode MenuMode = SettingsThemeMode.FollowSystem;
        static ResourceDictionary menuStyles;

        /// <summary>
        /// Puts a right-click menu (and its submenus) in the theme's colors, with a look to match the theme's drop-down
        /// lists: rounded, a soft shadow, items that light up as you point at them, an accent tick on chosen ones.
        /// A menu opens in a window of its own, outside any themed window, so it gets the colors itself.
        /// </summary>
        public static ContextMenu ThemeMenu(ContextMenu menu)
        {
            int p = PaletteFor(MenuMode);
            var rd = new ResourceDictionary();
            for (int i = 0; i < Keys.Length; i++)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palettes[p][i]));
                b.Freeze();
                rd["T." + Keys[i]] = b;
            }
            if (menuStyles == null) menuStyles = (ResourceDictionary)XamlReader.Parse(Head + SliderXaml + MenuXaml + Tail);
            rd.MergedDictionaries.Add(menuStyles);
            menu.Resources = rd;
            menu.Style = (Style)menuStyles[typeof(ContextMenu)];
            return menu;
        }

        /// <summary>A color of the menus' theme as it is now: "Border", "Accent" and so on.</summary>
        public static Color MenuColor(string key)
        {
            return (Color)ColorConverter.ConvertFromString(Palettes[PaletteFor(MenuMode)][Array.IndexOf(Keys, key)]);
        }

        /// <summary>
        /// A menu entry: a heading or note (a TextBlock) becomes a plain label that doesn't light up or take the
        /// keyboard; anything else stays as it is.
        /// </summary>
        public static object MenuEntry(object o)
        {
            var tb = o as TextBlock;
            if (tb == null) return o;
            return new MenuItem { Header = tb, IsHitTestVisible = false, Focusable = false, Padding = new Thickness(8, 1, 10, 1) };
        }

        /// <summary>The slim scroll bar styles, colored by "T.Thumb" / "T.ThumbHover" brushes that the user of them provides.</summary>
        public static ResourceDictionary ScrollBarStyles()
        {
            return (ResourceDictionary)XamlReader.Parse(Head + ScrollXaml + Tail);
        }

        static int ColorRef(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return c.R | (c.G << 8) | (c.B << 16);
        }

        // Control styles for everything the Settings window uses. Colors come from the T.* brushes above.
        const string Head = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>";
        const string Tail = "</ResourceDictionary>";

        // Slim scroll bars (also used by the launcher itself, in its own colors).
        const string ScrollXaml = @"
  <Style x:Key='ScrollPage' TargetType='{x:Type RepeatButton}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ScrollThumb' TargetType='{x:Type Thumb}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Thumb}'>
          <Border x:Name='t' CornerRadius='3' Background='{DynamicResource T.Thumb}'/>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='t' Property='Background' Value='{DynamicResource T.ThumbHover}'/></Trigger>
            <Trigger Property='IsDragging' Value='True'><Setter TargetName='t' Property='Background' Value='{DynamicResource T.ThumbHover}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <ControlTemplate x:Key='VScroll' TargetType='{x:Type ScrollBar}'>
    <Border Background='Transparent' Padding='2'>
      <Track x:Name='PART_Track' IsDirectionReversed='True'>
        <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource ScrollPage}' Command='{x:Static ScrollBar.PageUpCommand}'/></Track.DecreaseRepeatButton>
        <Track.Thumb><Thumb Style='{StaticResource ScrollThumb}' MinHeight='24'/></Track.Thumb>
        <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource ScrollPage}' Command='{x:Static ScrollBar.PageDownCommand}'/></Track.IncreaseRepeatButton>
      </Track>
    </Border>
  </ControlTemplate>

  <ControlTemplate x:Key='HScroll' TargetType='{x:Type ScrollBar}'>
    <Border Background='Transparent' Padding='2'>
      <Track x:Name='PART_Track'>
        <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource ScrollPage}' Command='{x:Static ScrollBar.PageLeftCommand}'/></Track.DecreaseRepeatButton>
        <Track.Thumb><Thumb Style='{StaticResource ScrollThumb}' MinWidth='24'/></Track.Thumb>
        <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource ScrollPage}' Command='{x:Static ScrollBar.PageRightCommand}'/></Track.IncreaseRepeatButton>
      </Track>
    </Border>
  </ControlTemplate>

  <Style TargetType='{x:Type ScrollBar}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Width' Value='11'/>
    <Setter Property='Height' Value='Auto'/>
    <Setter Property='Template' Value='{StaticResource VScroll}'/>
    <Style.Triggers>
      <Trigger Property='Orientation' Value='Horizontal'>
        <Setter Property='Width' Value='Auto'/>
        <Setter Property='Height' Value='11'/>
        <Setter Property='Template' Value='{StaticResource HScroll}'/>
      </Trigger>
    </Style.Triggers>
  </Style>

";

        // Sliders: an accent track up to a round thumb (in Settings, and in right-click menus).
        const string SliderXaml = @"
  <Style x:Key='SliderPart' TargetType='{x:Type RepeatButton}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type RepeatButton}'>
          <Border Background='Transparent'>
            <Border Height='4' CornerRadius='2' VerticalAlignment='Center' Background='{TemplateBinding Background}'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='SliderThumb' TargetType='{x:Type Thumb}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Thumb}'>
          <Grid Width='18' Height='18'>
            <Ellipse Fill='{DynamicResource T.Control}' Stroke='{DynamicResource T.Border}' StrokeThickness='1'/>
            <Ellipse x:Name='dot' Width='10' Height='10' Fill='{DynamicResource T.Accent}'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='dot' Property='Width' Value='12'/>
              <Setter TargetName='dot' Property='Height' Value='12'/>
            </Trigger>
            <Trigger Property='IsDragging' Value='True'>
              <Setter TargetName='dot' Property='Width' Value='8'/>
              <Setter TargetName='dot' Property='Height' Value='8'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='SliderLook' TargetType='{x:Type Slider}'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Slider}'>
          <Grid MinHeight='22' Background='Transparent'>
            <Track x:Name='PART_Track'>
              <Track.DecreaseRepeatButton>
                <RepeatButton Style='{StaticResource SliderPart}' Background='{DynamicResource T.Accent}' Command='{x:Static Slider.DecreaseLarge}'/>
              </Track.DecreaseRepeatButton>
              <Track.Thumb><Thumb Style='{StaticResource SliderThumb}'/></Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Style='{StaticResource SliderPart}' Background='{DynamicResource T.Track}' Command='{x:Static Slider.IncreaseLarge}'/>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
";

        // Right-click menus: the menu, its items (with icons, ticks, shortcut keys and submenus) and separators.
        const string MenuXaml = @"
  <Style TargetType='{x:Type Slider}' BasedOn='{StaticResource SliderLook}'>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsMoveToPointEnabled' Value='True'/>
  </Style>

  <Style TargetType='{x:Type ContextMenu}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.Control}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.Border}'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ContextMenu}'>
          <Border Margin='0,0,8,8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='1' CornerRadius='6' Padding='4'>
            <Border.Effect><DropShadowEffect BlurRadius='10' ShadowDepth='2' Direction='270' Opacity='0.35'/></Border.Effect>
            <StackPanel IsItemsHost='True' Grid.IsSharedSizeScope='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type MenuItem}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Padding' Value='8,5,10,5'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type MenuItem}'>
          <Border x:Name='bd' Background='Transparent' CornerRadius='4' Padding='{TemplateBinding Padding}'>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='Auto' MinWidth='24' SharedSizeGroup='MenuIcon'/>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto' SharedSizeGroup='MenuKeys'/>
                <ColumnDefinition Width='14'/>
              </Grid.ColumnDefinitions>
              <ContentPresenter x:Name='icon' ContentSource='Icon' VerticalAlignment='Center' HorizontalAlignment='Left' Margin='0,0,8,0'/>
              <Path x:Name='tick' Visibility='Collapsed' Data='M 1 6 L 4.5 9.5 L 11 2.5' Stroke='{DynamicResource T.Accent}' StrokeThickness='2'
                    StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round' VerticalAlignment='Center' HorizontalAlignment='Left' Margin='1,0,8,0'/>
              <ContentPresenter Grid.Column='1' ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center'/>
              <TextBlock Grid.Column='2' Text='{TemplateBinding InputGestureText}' Margin='24,0,0,0' Opacity='0.7' VerticalAlignment='Center'/>
              <Path x:Name='arrow' Grid.Column='3' Visibility='Collapsed' Data='M 0 0 L 4 4 L 0 8 Z' Fill='{TemplateBinding Foreground}'
                    HorizontalAlignment='Right' VerticalAlignment='Center'/>
              <Popup x:Name='PART_Popup' Placement='Right' HorizontalOffset='4' VerticalOffset='-5' AllowsTransparency='True' Focusable='False'
                     PopupAnimation='Fade' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}'>
                <Border Margin='0,0,8,8' Background='{DynamicResource T.Control}' BorderBrush='{DynamicResource T.Border}'
                        BorderThickness='1' CornerRadius='6' Padding='4'>
                  <Border.Effect><DropShadowEffect BlurRadius='10' ShadowDepth='2' Direction='270' Opacity='0.35'/></Border.Effect>
                  <StackPanel IsItemsHost='True' Grid.IsSharedSizeScope='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
                </Border>
              </Popup>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='Role' Value='SubmenuHeader'><Setter TargetName='arrow' Property='Visibility' Value='Visible'/></Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='tick' Property='Visibility' Value='Visible'/>
              <Setter TargetName='icon' Property='Visibility' Value='Collapsed'/>
            </Trigger>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource T.Selection}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='{x:Static MenuItem.SeparatorStyleKey}' TargetType='{x:Type Separator}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Separator}'>
          <Border Height='1' Margin='8,4' Background='{DynamicResource T.Border}' SnapsToDevicePixels='True'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ToolTip}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.Panel}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.BorderStrong}'/>
  </Style>
";

        const string Styles = Head + @"

  <Style x:Key='FocusRect'>
    <Setter Property='Control.Template'>
      <Setter.Value>
        <ControlTemplate>
          <Rectangle Margin='-2' RadiusX='5' RadiusY='5' StrokeThickness='1.5' Stroke='{DynamicResource T.Text}' SnapsToDevicePixels='True'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type Button}'>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
    <Setter Property='Background' Value='{DynamicResource T.Control}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.Border}'/>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='8,3'/>
    <Setter Property='HorizontalContentAlignment' Value='Center'/>
    <Setter Property='VerticalContentAlignment' Value='Center'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Button}'>
          <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='4' SnapsToDevicePixels='True'>
            <ContentPresenter Margin='{TemplateBinding Padding}' RecognizesAccessKey='True'
                              HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                              VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.5'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{DynamicResource T.ControlHover}'/></Trigger>
      <Trigger Property='IsPressed' Value='True'><Setter Property='Background' Value='{DynamicResource T.ControlPressed}'/></Trigger>
      <Trigger Property='IsDefault' Value='True'>
        <Setter Property='Background' Value='{DynamicResource T.Accent}'/>
        <Setter Property='BorderBrush' Value='{DynamicResource T.Accent}'/>
        <Setter Property='Foreground' Value='{DynamicResource T.AccentText}'/>
      </Trigger>
      <MultiTrigger>
        <MultiTrigger.Conditions>
          <Condition Property='IsDefault' Value='True'/>
          <Condition Property='IsMouseOver' Value='True'/>
        </MultiTrigger.Conditions>
        <Setter Property='Background' Value='{DynamicResource T.AccentHover}'/>
      </MultiTrigger>
    </Style.Triggers>
  </Style>

  <Style x:Key='ComboToggle' TargetType='{x:Type ToggleButton}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='ClickMode' Value='Press'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ToggleButton}'>
          <Border x:Name='bd' Background='{DynamicResource T.Control}' BorderBrush='{DynamicResource T.Border}' BorderThickness='1'
                  CornerRadius='4' SnapsToDevicePixels='True'>
            <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,1,10,0' Data='M 0 0 L 4.5 4.5 L 9 0'
                  Stroke='{DynamicResource T.TextMuted}' StrokeThickness='1.5' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource T.ControlHover}'/></Trigger>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource T.Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ComboBox}'>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='MinHeight' Value='26'/>
    <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
    <Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBox}'>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width='*'/>
              <ColumnDefinition Width='28'/>
            </Grid.ColumnDefinitions>
            <ToggleButton Grid.ColumnSpan='2' Style='{StaticResource ComboToggle}'
                          IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'/>
            <ContentPresenter IsHitTestVisible='False' Margin='{TemplateBinding Padding}' VerticalAlignment='Center' HorizontalAlignment='Left'
                              Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                              ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                              ContentStringFormat='{TemplateBinding SelectionBoxItemStringFormat}'/>
            <Popup x:Name='PART_Popup' Grid.ColumnSpan='2' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'
                   IsOpen='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}'>
              <Border MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}'
                      Margin='0,2,0,0' Padding='3' Background='{DynamicResource T.Control}' BorderBrush='{DynamicResource T.Border}'
                      BorderThickness='1' CornerRadius='5' SnapsToDevicePixels='True'>
                <ScrollViewer>
                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ComboBoxItem}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Padding' Value='10,5'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type ComboBoxItem}'>
          <Grid>
            <Border x:Name='bd' Background='Transparent' CornerRadius='3' Padding='{TemplateBinding Padding}'>
              <ContentPresenter/>
            </Border>
            <Rectangle x:Name='mark' Width='3' Height='14' RadiusX='1.5' RadiusY='1.5' HorizontalAlignment='Left' VerticalAlignment='Center'
                       Fill='{DynamicResource T.Accent}' Visibility='Collapsed'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource T.ControlHover}'/>
              <Setter TargetName='mark' Property='Visibility' Value='Visible'/>
            </Trigger>
            <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource T.Selection}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type ToolTip}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.Panel}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.BorderStrong}'/>
  </Style>

  <Style TargetType='{x:Type CheckBox}'>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.CheckBox}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.BorderStrong}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type CheckBox}'>
          <Grid Background='Transparent' SnapsToDevicePixels='True'>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width='Auto'/>
              <ColumnDefinition Width='*'/>
            </Grid.ColumnDefinitions>
            <Border x:Name='box' Width='16' Height='16' Margin='0,1,0,0' VerticalAlignment='Top' CornerRadius='3'
                    Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'>
              <Path x:Name='mark' Visibility='Collapsed' Data='M 3 7.5 L 6 10.5 L 11.5 4.5' Stroke='{DynamicResource T.AccentText}'
                    StrokeThickness='1.8' StrokeStartLineCap='Round' StrokeEndLineCap='Round' StrokeLineJoin='Round'/>
            </Border>
            <ContentPresenter Grid.Column='1' Margin='7,0,0,0' VerticalAlignment='Top' RecognizesAccessKey='True'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource T.Accent}'/></Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='mark' Property='Visibility' Value='Visible'/>
              <Setter TargetName='box' Property='Background' Value='{DynamicResource T.Accent}'/>
              <Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource T.Accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type TextBox}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.Control}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.Border}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource T.Text}'/>
    <Setter Property='SelectionBrush' Value='{DynamicResource T.Accent}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type TextBox}'>
          <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                  BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='4' SnapsToDevicePixels='True'>
            <ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource T.BorderStrong}'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource T.Accent}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type TabControl}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='Background' Value='{DynamicResource T.Panel}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource T.Border}'/>
    <Setter Property='Padding' Value='0'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type TabControl}'>
          <Grid KeyboardNavigation.TabNavigation='Local' SnapsToDevicePixels='True'>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='*'/>
            </Grid.RowDefinitions>
            <TabPanel x:Name='HeaderPanel' IsItemsHost='True' Panel.ZIndex='1' Margin='0,0,0,-1' KeyboardNavigation.TabIndex='1'/>
            <Border Grid.Row='1' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'
                    CornerRadius='0,5,5,5' KeyboardNavigation.TabNavigation='Local' KeyboardNavigation.DirectionalNavigation='Contained'
                    KeyboardNavigation.TabIndex='2'>
              <ContentPresenter x:Name='PART_SelectedContentHost' ContentSource='SelectedContent' Margin='{TemplateBinding Padding}'/>
            </Border>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='{x:Type TabItem}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Text}'/>
    <Setter Property='BorderBrush' Value='Transparent'/>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type TabItem}'>
          <Grid Margin='0,0,2,0' SnapsToDevicePixels='True'>
            <Border x:Name='bd' Background='Transparent' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1,1,1,0' CornerRadius='5,5,0,0' Padding='11,5,11,7'>
              <ContentPresenter ContentSource='Header' RecognizesAccessKey='True' HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <Rectangle x:Name='line' Height='2' RadiusX='1' RadiusY='1' VerticalAlignment='Top' Margin='6,0,6,0'
                       Fill='{DynamicResource T.Accent}' Visibility='Collapsed'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource T.TabHover}'/></Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter Property='Panel.ZIndex' Value='1'/>
              <Setter TargetName='bd' Property='Background' Value='{DynamicResource T.Panel}'/>
              <Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource T.Border}'/>
              <Setter TargetName='line' Property='Visibility' Value='Visible'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>


" + ScrollXaml + @"
" + SliderXaml + @"
  <Style TargetType='{x:Type Slider}' BasedOn='{StaticResource SliderLook}'>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
  </Style>

  <Style TargetType='{x:Type Hyperlink}'>
    <Setter Property='Foreground' Value='{DynamicResource T.Link}'/>
  </Style>

  <Style x:Key='ThemeSwatch' TargetType='{x:Type Button}'>
    <Setter Property='Width' Value='22'/>
    <Setter Property='Height' Value='22'/>
    <Setter Property='Margin' Value='0,0,10,0'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{StaticResource FocusRect}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='{x:Type Button}'>
          <Grid>
            <Border x:Name='ring' CornerRadius='7' BorderThickness='2' BorderBrush='Transparent' Margin='-3'/>
            <Border CornerRadius='5' Background='{TemplateBinding Background}' BorderBrush='{DynamicResource T.BorderStrong}' BorderThickness='1'/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='ring' Property='BorderBrush' Value='{DynamicResource T.Border}'/></Trigger>
            <Trigger Property='Tag' Value='on'><Setter TargetName='ring' Property='BorderBrush' Value='{DynamicResource T.Accent}'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
" + Tail;

    }
}
