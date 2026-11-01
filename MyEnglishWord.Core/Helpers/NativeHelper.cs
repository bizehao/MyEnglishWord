using System;
using System.Collections.Generic;
using System.Text;

namespace MyEnglishWord.Core.Helpers;

public static class NativeHelper
{
    public static unsafe string Utf8ByteToString(byte* bytePtr)
    {
        byte* b;
        for (b = bytePtr; *b != 0; ++b) ;
        return Encoding.UTF8.GetString(bytePtr, (int)(b - bytePtr));
    }
}
