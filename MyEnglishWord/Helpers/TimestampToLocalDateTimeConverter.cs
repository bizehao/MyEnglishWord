using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class TimestampToLocalDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is long)
        {
            var dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)value);
            return dateTimeOffset.LocalDateTime;
        }
        else
        {
            return null;
        }

    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(value is DateTime);
        return (ulong)new DateTimeOffset((DateTime)value).ToUnixTimeMilliseconds();
    }
}
