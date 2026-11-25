using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml.Data;
using WordNetSqlite;

namespace MyEnglishWord.Helpers;

internal class PartOfSpeechToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is PartOfSpeech ps)
        {
            return ps switch
            {
                PartOfSpeech.Noun => "名词 (n)",
                PartOfSpeech.Verb => "动词 (v)",
                PartOfSpeech.Adj => "形容词 (a)",
                PartOfSpeech.Adv => "副词 (r)",
                _ => throw new NotImplementedException()
            };
        }
        throw new NotImplementedException();
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
