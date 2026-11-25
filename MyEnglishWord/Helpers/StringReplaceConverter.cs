using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class StringReplaceConverter:IValueConverter
{
    public string OldValue { get; set; }
    public string NewValue { get; set; }
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(value is string);
        return (value as string).Replace(OldValue, NewValue);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(value is string);
        return (value as string).Replace(NewValue, OldValue);
    }
}
