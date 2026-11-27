using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml.Data;

namespace MyEnglishWord.Helpers;

public class ArithmeticConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null || parameter is null)
        {
            return value!;
        }

        // 把绑定值转为 double 进行运算
        if (!double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var left))
        {
            return value;
        }

        // 解析参数，格式：运算符 + 操作数，例如 "+1"、"-2"、"*3"、"/4"
        var param = parameter.ToString()!.Trim();
        if (param.Length < 2)
        {
            return value;
        }

        var op = param[0];
        if (!double.TryParse(param.AsSpan(1), NumberStyles.Any, CultureInfo.InvariantCulture, out var right))
        {
            return value;
        }

        double result;
        switch (op)
        {
            case '+': result = left + right; break;
            case '-': result = left - right; break;
            case '*': result = left * right; break;
            case '/':
                if (right == 0) return value; // 除零保护
                result = left / right;
                break;
            default: return value;
        }

        // 根据目标类型返回
        if (targetType == typeof(int))
            return (int)result;
        if (targetType == typeof(float))
            return (float)result;
        if (targetType == typeof(decimal))
            return (decimal)result;
        return result; // 默认 double
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}