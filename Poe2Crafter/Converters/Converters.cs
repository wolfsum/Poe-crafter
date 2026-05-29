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
        value is true ? "✓" : "○";
    public object ConvertBack(object value, Type _, object __, CultureInfo ___) =>
        throw new NotImplementedException();
}
