using System.Globalization;
using System.Windows.Data;

namespace EnterpriseAeroStudio.Converters
{
    /// <summary>Boolean dəyərini tərsinə çevirir (IsEnabled bağlaması üçün).</summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;
    }
}
