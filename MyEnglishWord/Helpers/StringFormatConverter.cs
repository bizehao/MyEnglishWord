using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(parameter is string);
        var format = parameter as string;
        return string.Format(format, value?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(false, "This function should never be called");
        throw new NotImplementedException();
    }
}
