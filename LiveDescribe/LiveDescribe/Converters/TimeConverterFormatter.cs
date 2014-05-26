﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LiveDescribe.Converters
{
    class TimeConverterFormatter : IValueConverter
    {
        private StringBuilder _builder;

        public TimeConverterFormatter()
        {
            _builder = new StringBuilder();
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            _builder.Clear();
            TimeSpan timespan = (TimeSpan)value;

            return timespan.ToString("hh\\:mm\\:ss\\.fff");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
