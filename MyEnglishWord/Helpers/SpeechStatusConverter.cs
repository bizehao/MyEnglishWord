using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;

namespace MyEnglishWord.Helpers;

public class SpeechStatusConverter
{
    public SpeechStatusConverter()
    {
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool Speeching)
        {
            if (Speeching)
            {
                return "\uE7C8";
            }
            else
            {
                return "\uE720";
            }
        }

        throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new ArgumentException("ExceptionEnumToBooleanConverterParameterMustBeAnEnumName");
    }
}