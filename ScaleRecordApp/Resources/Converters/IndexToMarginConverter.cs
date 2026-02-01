using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScaleRecordApp.Resources.Converters
{
    internal class IndexToMarginConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                if (index == 0)
                    return 15; // первый элемент

                //return index % 2 == 0
                //    ? Colors.White           // чётные
                //    : Color.FromArgb("#f9f9f9"); // нечётные
            }

            return 5;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
