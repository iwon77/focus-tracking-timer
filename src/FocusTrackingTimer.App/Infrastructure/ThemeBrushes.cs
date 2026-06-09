using System.Windows;
using System.Windows.Media;

namespace FocusTrackingTimer.App.Infrastructure;

internal static class ThemeBrushes
{
    public static Brush Active => Get("ThemeActiveBrush");

    public static Brush ActiveText => Get("ThemeActiveTextBrush");

    public static Brush HoverBorder => Get("ThemeHoverBorderBrush");

    public static Brush PrimaryText => Get("ThemePrimaryTextBrush");

    public static Brush SecondaryText => Get("ThemeSecondaryTextBrush");

    public static Brush HintText => Get("ThemeHintTextBrush");

    public static Brush Status => Get("ThemeStatusBrush");

    public static Brush Focus => Get("ThemeFocusBrush");

    public static Brush Sunday => Get("ThemeSundayBrush");

    public static Brush Transparent => Get("ThemeTransparentBrush");

    private static Brush Get(string resourceKey)
    {
        return (Brush)Application.Current.FindResource(resourceKey);
    }
}
