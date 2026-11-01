using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml.Controls;

namespace MyEnglishWord.Helpers;

public static class OtherBindHelper
{
    public static Symbol OxfordToSymbol(bool oxford)
    {
        return oxford ? Symbol.Accept : Symbol.Cancel;
    }

    public static string OxfordToText(bool oxford)
    {
        return oxford ? "√" : "×";
    }
}
