using System;
using System.Collections.Generic;
using System.Text;

namespace MyEnglishWord.Core.Models;

public enum WordCountDataGroupType
{
    Year,
    Month,
    Day
}

public record WordCountData(DateTime Time, int Count);