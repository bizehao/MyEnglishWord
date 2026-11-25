using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

/// <summary>
/// From https://www.reflectionit.nl/blog/2013/windows-8-xaml-tips-combining-converters
/// </summary>
public class ValueConverterGroup : List<IValueConverter>, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return this.Aggregate(value, (current, converter) =>
            current == DependencyProperty.UnsetValue ?
                current :
                converter.Convert(current, targetType, parameter, language)
        );
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        System.Diagnostics.Debugger.Break(); // Not Implemented
        throw new NotImplementedException();
    }
}
