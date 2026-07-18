using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Poe2Crafter.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        value is Visibility.Visible;
}

public class BoolToCheckConverter : IValueConverter
{
    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? "✓" : "✗";
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}

// Set/unset state colour: green when position is captured, red when still missing
public class BoolToStateBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush Ok =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60));
    private static readonly System.Windows.Media.Brush Missing =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0x39, 0x2B));

    public object Convert(object value, Type _, object __, CultureInfo ___) =>
        value is true ? Ok : Missing;
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}
