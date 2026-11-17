using System;
using System.Collections.Generic;
using System.Text;

namespace MyEnglishWord.Models;

public class PieData(string name, double value)
{
    public string Name { get; set; } = name;
    public double[] Values { get; set; } = [value];
}
