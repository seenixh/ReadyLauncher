using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ReadyLauncher
{
    /// <summary>One icon on the launcher: an app, a web link, or a folder (2x2 preview).</summary>
    internal class TileView : Border
    {
        public static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        static readonly Brush HoverBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
        static readonly Brush SelectedBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x44, 0x4A, 0x9E, 0xFF)));
        static readonly Brush SelectedBorder = Frozen(new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)));

        public readonly LauncherItem Item;
        public readonly double IconSize;
        readonly Border iconHost;
        readonly ScaleTransform scale = new ScaleTransform(1, 1);
        bool highlighted, selected;

        Brush RestingBackground { get { return selected ? SelectedBrush : Brushes.Transparent; } }

        /// <summary>Part of a drag-selection: tinted, with a colored border.</summary>
        public void SetSelected(bool on)
        {
            if (selected == on) return;
            selected = on;
            BorderBrush = on ? SelectedBorder : Brushes.Transparent;
            if (!highlighted) Background = IsMouseOver ? HoverBrush : RestingBackground;
        }

        public TileView(LauncherItem item, double iconSize, bool showLabel, Brush foreground, double cellW)
        {
            Item = item;
            IconSize = iconSize;
            Background = Brushes.Transparent;
            CornerRadius = new CornerRadius(8);
            BorderThickness = new Thickness(1.5);   // always there (transparent) so selecting doesn't shift the layout
            BorderBrush = Brushes.Transparent;
            Padding = new Thickness(2.5);
            ToolTip = item.Kind == ItemKind.Keys ? item.Name + "\nSends " + AppSettings.HotkeyText(item.KeyMods, item.KeyKey) : item.Name;
            ToolTipService.SetInitialShowDelay(this, 700);

            var stack = new StackPanel();
            iconHost = new Border
            {
                Width = iconSize, Height = iconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = scale,
                Child = BuildIcon(item, iconSize)
            };
            stack.Children.Add(iconHost);
            if (showLabel)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = item.Name, Foreground = foreground, FontSize = iconSize < 30 ? 10 : iconSize < 40 ? 11 : 12,
                    TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = iconSize < 40 ? 30 : 34,
                    Margin = new Thickness(0, 4, 0, 0), Width = cellW - 14
                });
                Width = cellW - 4;
            }
            Child = stack;
            MouseEnter += delegate { if (!highlighted) Background = HoverBrush; };
            MouseLeave += delegate { if (!highlighted) Background = RestingBackground; };
        }

        static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }

        /// <summary>How far below the tile's top its picture ends (above the name).</summary>
        public double IconBottom { get { return BorderThickness.Top + Padding.Top + IconSize; } }

        /// <summary>Glow + grow when another icon is dragged on top (merge into folder).</summary>
        public void SetMergeTarget(bool on)
        {
            if (on == highlighted) return;
            highlighted = on;
            scale.ScaleX = scale.ScaleY = on ? 1.15 : 1.0;
            iconHost.Effect = on ? new DropShadowEffect { Color = Colors.White, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 } : null;
            Background = on ? HoverBrush : RestingBackground;
        }

        public FrameworkElement CreateGhost()
        {
            return new Border { Width = IconSize, Height = IconSize, Opacity = 0.85, Child = BuildIcon(Item, IconSize), IsHitTestVisible = false };
        }

        /// <summary>Center of the icon (not the label) relative to <paramref name="relativeTo"/>.</summary>
        public Point IconCenter(Visual relativeTo)
        {
            return iconHost.TransformToAncestor(relativeTo).Transform(new Point(IconSize / 2, IconSize / 2));
        }

        public static UIElement BuildIcon(LauncherItem item, double size)
        {
            if (!string.IsNullOrEmpty(item.IconPath))
            {
                var custom = IconCache.GetCustom(item.IconPath, item.IconIndex, size > 56 ? 128 : 64);
                if (custom != null)   // a chosen icon that has since gone missing falls back to the normal one
                {
                    var pic = new Image { Source = custom, Width = size, Height = size, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(pic, BitmapScalingMode.HighQuality);
                    return pic;
                }
            }
            if (item.IsFolder)
            {
                var grid = new UniformGrid { Rows = 2, Columns = 2, Margin = new Thickness(size * 0.12) };
                foreach (var c in item.Children.Take(4))
                    grid.Children.Add(new Border { Margin = new Thickness(size * 0.03), Child = BuildIcon(c, size * 0.32) });
                return new Border
                {
                    CornerRadius = new CornerRadius(size * 0.22),
                    Background = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    Child = grid
                };
            }
            if (item.Kind == ItemKind.Link)
                return Glyph("", size, Color.FromRgb(0x2F, 0x7B, 0xE0), Color.FromRgb(0x1D, 0x4F, 0xA8));
            if (item.Kind == ItemKind.Keys)
                return Glyph("", size, Color.FromRgb(0x8A, 0x5C, 0xE6), Color.FromRgb(0x5B, 0x34, 0xB0));

            var img = IconCache.Get(item.Path, size > 56 ? 96 : 64);
            if (img == null)
                return Glyph("", size, Color.FromRgb(0x70, 0x70, 0x78), Color.FromRgb(0x4A, 0x4A, 0x52));
            var image = new Image { Source = img, Width = size, Height = size, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }

        static UIElement Glyph(string glyph, double size, Color top, Color bottom)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(size * 0.22),
                Background = new LinearGradientBrush(top, bottom, 90),
                Child = new TextBlock
                {
                    Text = glyph, FontFamily = IconFont, FontSize = size * 0.55, Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }
}
