using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PrimerPipeline
{
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class Primer3SettingToBackgroundConverter : IValueConverter
    {
        public Brush DefaultBrush { get; set; }
        public Brush AdvancedSettingBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Primer3Settings.Primer3Setting setting = (Primer3Settings.Primer3Setting)value;

            return setting.IsAdvancedSetting ? AdvancedSettingBrush : DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}