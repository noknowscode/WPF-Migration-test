using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI; // Color.FromArgb lives here, Microsoft.UI.Colors is a separate static class

namespace EventLookUno.Converters;

/// <summary>
/// Maps a Windows event-log level (byte?) to its display text.
/// Level 0 is shown as "Information" by the Windows Event Viewer.
/// </summary>
public sealed class EventLevelToDisplayTextConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        value is byte b
            ? b switch
            {
                1 => "Critical",
                2 => "Error",
                3 => "Warning",
                4 => "Information",
                5 => "Verbose",
                _ => "Information",
            }
            : "Information";

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
}

/// <summary>
/// Maps a Windows event-log level to a glyph (Segoe MDL2 Assets font).
/// </summary>
public sealed class EventLevelToGlyphConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        value is byte b
            ? b switch
            {
                1 => "", // Warning filled (critical)
                2 => "", // ErrorBadge
                3 => "", // Warning
                4 => "", // Info
                5 => "", // Info (verbose)
                _ => "",
            }
            : "";

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
}

/// <summary>
/// Maps a Windows event-log level to a foreground brush for the level icon.
/// </summary>
public sealed class EventLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        value is byte b
            ? new SolidColorBrush(b switch
            {
                1 => Colors.DarkRed,
                2 => Colors.Red,
                3 => Colors.Goldenrod,
                4 => Colors.SteelBlue,
                5 => Colors.Gray,
                _ => Colors.SteelBlue,
            })
            : new SolidColorBrush(Colors.SteelBlue);

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
}

/// <summary>
/// Maps the newly-loaded flag to a row background (Yellow when true, Transparent otherwise).
/// Used by <c>DataGrid.LoadingRow</c> to produce the auto-refresh highlight in lieu of a
/// WPF <c>DataTrigger</c>, which isn't supported in WinUI.
/// </summary>
public sealed class NewLoadedToBrushConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        (value is bool b && b)
            ? new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xEB, 0x3B))
            : new SolidColorBrush(Colors.Transparent);

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
}

/// <summary>
/// Inverse boolean → Visibility. (Used for menu items hidden when IsRunAsAdmin is true.)
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language) =>
        (value is bool b && !b) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public object ConvertBack(object value, System.Type targetType, object parameter, string language) => null!;
}
