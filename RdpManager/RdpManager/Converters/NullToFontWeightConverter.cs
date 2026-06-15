using System.Windows.Data;
using System.Windows;
using System;
using System.Globalization;

namespace RdpManager.Converters
{
    public class NullToFontWeightConverter : IValueConverter
    {
        public FontWeight NullFontWeight { get; set; } = FontWeights.Bold;
        public FontWeight NotNullFontWeight { get; set; } = FontWeights.Normal;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? NullFontWeight : NotNullFontWeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
