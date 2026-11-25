using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WordNetSqlite;

namespace MyEnglishWord.Helpers;

internal class PartOfSpeechToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PartOfSpeech ps)
        {
            return new SolidColorBrush(ps switch
            {
                PartOfSpeech.Noun => "#8B13F3".ToColor(),
                PartOfSpeech.Verb => "#F2BA15".ToColor(),
                PartOfSpeech.Adj => "#0BE9DC".ToColor(),
                PartOfSpeech.Adv => "#F00D67CD".ToColor(),
                _ => throw new NotImplementedException()
            });
        }
        throw new NotImplementedException();
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
