using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dh4Launcher.Forms.Converters;

/// <summary>value(int)와 parameter가 같으면 Visible, 아니면 Collapsed. (탭 전환용)</summary>
public class EqualsVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var v = System.Convert.ToInt32(value);
        var p = System.Convert.ToInt32(parameter);
        return v == p ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
