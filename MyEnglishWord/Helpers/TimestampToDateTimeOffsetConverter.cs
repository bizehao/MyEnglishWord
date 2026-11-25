using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class TimestampToDateTimeOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(value is long || value is ulong);
        var dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds((long)value);
        return dateTimeOffset;
        
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(value is DateTimeOffset);
        return (ulong)((DateTimeOffset)value).ToUnixTimeMilliseconds();
    }
}
