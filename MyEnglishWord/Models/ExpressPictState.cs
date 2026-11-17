using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.UI;

namespace MyEnglishWord.Models;

public enum ExpressPictOp
{
    Equal,  //==
    GreaterThan, // >
};

public sealed partial class ExpressPictState(int value, ExpressPictOp op, Color color, Func<int, bool> express) : ObservableObject
{
    public int Value => value;

    public ExpressPictOp Op => op;

    [ObservableProperty]
    public partial Color Color
    {
        get;
        set;
    } = color;

    public Func<int, bool> Express => express;

    public override string ToString()
    {
        if (op == ExpressPictOp.Equal)
        {
            return $"== {value}";
        }
        if (op == ExpressPictOp.GreaterThan)
        {
            return $"> {value}";
        }
        return "{value} ???";
    }
}

