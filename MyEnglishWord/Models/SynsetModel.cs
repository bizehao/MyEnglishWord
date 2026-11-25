using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using WordNetSqlite;

namespace MyEnglishWord.Models;

public partial class SynsetModel
{
    public PartOfSpeech PSType
    {
        get;
    }
    /// <summary>
    /// Index in the synset's definition array.
    /// </summary>
    public uint Key
    {
        get;
    }
    public string Definition
    {
        get;
    } = string.Empty;

    public string[] Words
    {
        get;
    }
    public string Word
    {
        get;
    }
    public ExampleModel[] Examples
    {
        get;
    } = [];

    public SynsetModel(PartOfSpeech type, Synset synset)
    {
        PSType = type;
        Key = synset.Key;
        Words = synset.Words.ToArray();
        Word = synset.Words[synset.WhichWord - 1];

        /* several very hard "definition"s
unacceptable:
(not conforming to standard usage; "the following use of `access' was judged unacceptable by a panel of linguists; `You can access your cash at any of 300 automatic tellers'")

pathetic:
(deserving or inciting pity; "a hapless victim"; "miserable victims of war"; "the shabby room struck her as extraordinarily pathetic"- Galsworthy; "piteous appeals for help"; "pitiable homeless children"; "a pitiful fate"; "Oh, you poor thing"; "his poor distorted limbs"; "a wretched life")

received:
(conforming to the established language usage of educated native speakers; "standard English" (American); "received standard English is sometimes called the King's English" (British))
         */


        // regex: \(([^"]+)(;\s*"([^"]+)"(\s*-\s*[^;]+)?(\s*\([^;]+\))?)*\)
        var defAndExampleSeperateRegex = MyRegex();
        var match = defAndExampleSeperateRegex.Match(synset.Defn!);

        if (match.Success)
        {
            Debug.Assert(match.Groups.Count == 6);
            Debug.Assert(match.Groups[1].Success);
            if (match.Groups[1].Success)
            {
                Definition = match.Groups[1].Captures[0].Value;
            }
            if (match.Groups[3].Success)
            {
                var examples = new ExampleModel[match.Groups[3].Captures.Count];
                for (int i = 0; i < match.Groups[3].Captures.Count; ++i)
                {
                    ExampleModel em = new ExampleModel(Words, match.Groups[3].Captures[i].Value);
                    examples[i] = em;
                }
                Examples = examples;
            }
        }
    }

    [GeneratedRegex(@"\(([^""]+)(;\s*""([^""]+)""(\s*-\s*[^;]+)?(\s*\([^;]+\))?)*\)")]
    private static partial Regex MyRegex();

    public record class ExampleModel(string[] Words, string Example);
}