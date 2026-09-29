using System.Globalization;
using System.Windows.Data;

namespace EnterpriseAeroStudio.Converters
{
    /// <summary>Rəqəmi Azərbaycan formatında "N2 AZN" kimi göstərir.</summary>
    public sealed class AzCurrencyConverter : IValueConverter
    {
        private static readonly CultureInfo Az = CultureInfo.GetCultureInfo("az-AZ");

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var number = value switch
            {
                decimal d => d,
                double db => (decimal)db,
                int i => i,
                _ => 0m
            };
            return number.ToString("N2", Az) + " AZN";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
