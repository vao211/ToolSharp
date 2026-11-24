using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyWidget.Services;

public class InverseBoolToVisibilityConverter : IValueConverter
{
   public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
   {
      var b = value is true;
      return b ? Visibility.Collapsed : Visibility.Visible;
   }

   public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
   {
      return value is not Visibility.Visible;
   }
}
