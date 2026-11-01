using System;
using System.Collections.Generic;
using System.Text;

namespace MyEnglishWord.Core.Helpers;

public static class EnglishWordHelper
{
    /// <summary>
    /// pos缩写 -> 中文全称
    /// </summary>
    private static readonly Dictionary<string, string> PosMap = new()
    {
        { "n.", "名词" },
        { "cn.", "可数名词" },
        { "un.", "不可数名词" },
        { "u.", "不可数名词" },
        { "v.", "动词" },
        { "vt.", "及物动词" },
        { "vi.", "不及物动词" },
        { "aux.", "助动词" },
        { "modal.", "情态动词" },
        { "adj.", "形容词" },
        { "a.", "形容词" },
        { "adv.", "副词" },
        { "ad.", "副词" },
        { "pron.", "代词" },
        { "prep.", "介词" },
        { "conj.", "连词" },
        { "art.", "冠词" },
        { "num.", "数词" },
        { "det.", "限定词" },
        { "int.", "感叹词" },
        { "part.", "小品词" },
        { "suf.", "后缀" },
        { "pref.", "前缀" },
        { "abbr.", "缩写词" },
        { "sym.", "符号" }
    };

    /// <summary>
    /// 标记说明映射：
    /// p:过去式(did)
    /// d:过去分词(done)
    /// i:现在分词(doing)
    /// 3:三单(does)
    /// r:比较级(-er)
    /// t:最高级(-est)
    /// s:名词复数
    /// 0:Lemma原形
    /// 1:Lemma的变形标记
    /// </summary>
    public static string GetWordTagDescription(char c)
    {
        return c switch
        {
            'p' => "过去式（did）",
            'd' => "过去分词（done）",
            'i' => "现在分词（doing）",
            '3' => "第三人称单数（does）",
            'r' => "形容词比较级（-er）",
            't' => "形容词最高级（-est）",
            's' => "名词复数形式",
            '0' => "Lemma（原形）",
            '1' => "Lemma 的变换形式",
            _ => "未知标记"
        };
    }

    /// <summary>
    /// 解析 "n./vt./adj." 这种字符串，返回中文说明列表
    /// </summary>
    /// <param name="posText">原始pos字符串，如 n./vt./vi.</param>
    /// <returns>中文释义数组</returns>
    public static List<string> ParsePos(string posText)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(posText))
            return result;

        var items = posText.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var item in items)
        {
            if (PosMap.TryGetValue(item, out var cn))
                result.Add(cn);
            else
                result.Add(item); // 未知的保留原文本
        }
        return result;
    }
}
