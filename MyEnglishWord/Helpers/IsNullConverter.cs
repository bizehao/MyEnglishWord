using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class IsNullConverter:IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value == null)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        Debug.Assert(false, "This function should never be called");
        throw new NotImplementedException();
    }
}
