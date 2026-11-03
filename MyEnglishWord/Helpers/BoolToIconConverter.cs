using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class BoolToIconConverter : IValueConverter
{
    public BoolToIconConverter()
    {
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string param)
        {
            // Handle the parameter if needed
            var icons = param.Split('|');
            if (icons.Length == 2)
            {
                if (value is bool speeching)
                {
                    if (speeching)
                    {
                        return icons[0];
                    }
                    else
                    {
                        return icons[1];
                    }
                }
            }
        }

        throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
    }
}