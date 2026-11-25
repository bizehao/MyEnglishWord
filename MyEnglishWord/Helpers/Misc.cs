using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WordNetSqlite;

namespace MyEnglishWord.Helpers;
public class Misc
{
    public static async Task<List<(int startIndex, int length)>> HightlightHelperAsync(string exampleStr, string[] words, CancellationToken cancellationToken)
    {
        Func<char, bool> isNumberOfAlphabetFunc = (char c) =>
        {
            bool isLower = c >= 'a' && c <= 'z';
            bool isUpper = c >= 'A' && c <= 'Z';
            bool isDigit = c >= '0' && c <= '9';
            return isLower || isUpper || isDigit;
        };

        Action<List<char>> removeNonAlphabetOrNumberEnding = (List<char> charList) =>
        {
            for (int i = charList.Count - 1; i >= 0; --i)
            {
                if (!isNumberOfAlphabetFunc(charList[i]))
                {
                    charList.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
        };

        var poses = new PartOfSpeech[] { PartOfSpeech.Noun, PartOfSpeech.Verb, PartOfSpeech.Adj, PartOfSpeech.Adv };

        var lemmaComponentList = new List<(string LemmaInExample, int Offset, string[] PossibleMorphs)>();

        List<char> lemmaCharList = new List<char>();
        int currPossibleLemmaOffset = 0;
        for (int i = 0; i < exampleStr.Length; ++i)
        {
            var character = exampleStr[i];
            if (lemmaCharList.Count == 0)
            {
                currPossibleLemmaOffset = i;
            }
            lemmaCharList.Add(character);

            if (character == ' ' || i == exampleStr.Length - 1)
            {
                // TODO
                // the logic here is wrong
                // words like "a.d." have a dot in the end
                // gotta find some other way to seperate possible lemmas.
                removeNonAlphabetOrNumberEnding(lemmaCharList);
                if (lemmaCharList.Count > 0)
                {
                    var lemmaInExample = new string(lemmaCharList.ToArray());
                    var possibleMorphsOfThisLemma = new List<string>();
                    List<Task<IReadOnlyList<string>>> morphStrTasks = new List<Task<IReadOnlyList<string>>>();
                    var wordNetDatabase = App.GetService<WordNetDatabase>();
                    foreach (var pos in poses)
                    {
                        morphStrTasks.Add(wordNetDatabase.MorphStrAsync(lemmaInExample, pos, cancellationToken));
                    }
                    foreach(var morphs in await Task.WhenAll(morphStrTasks))
                    {
                        possibleMorphsOfThisLemma.AddRange(morphs);
                    }
                    possibleMorphsOfThisLemma.Add(lemmaInExample);
                    lemmaComponentList.Add((new string(lemmaCharList.ToArray()), currPossibleLemmaOffset, possibleMorphsOfThisLemma.ToArray()));
                }
                lemmaCharList.Clear();
            }
        }

        Func<string[], string, bool> equalAnyFunc = (string[] a, string b) =>
        {
            foreach (var s in a)
            {
                if (s.ToLowerInvariant() == b.ToLowerInvariant())
                {
                    return true;
                }
            }
            return false;
        };

        var matchLemmaComponentInfos = new List<(int StartLemmaComponentIndex, int Count)>();
        for (int lemmaComponentIndex = 0; lemmaComponentIndex < lemmaComponentList.Count; ++lemmaComponentIndex)
        {
            for (int wordIndex = 0; wordIndex < words.Length; ++wordIndex)
            {
                var word = words[wordIndex];

                // a workround for words like former(a)
                var parenthesisMatch = new Regex(@"\(\w+\)$").Match(word);
                if (parenthesisMatch.Success)
                {
                    word = word.Remove(parenthesisMatch.Index, parenthesisMatch.Length);
                }

                var wordComponents = word.Split('_');
                bool match = true;
                for (int componentIndex = 0; componentIndex < wordComponents.Length; ++componentIndex)
                {
                    if (lemmaComponentIndex + componentIndex >= lemmaComponentList.Count)
                    {
                        match = false;
                        break;
                    }
                    else
                    {
                        if (!equalAnyFunc(lemmaComponentList[lemmaComponentIndex + componentIndex].PossibleMorphs, wordComponents[componentIndex]))
                        {
                            match = false;
                            break;
                        }
                    }
                }

                if (match)
                {
                    matchLemmaComponentInfos.Add((lemmaComponentIndex, wordComponents.Length));
                }
            }
        }

        var highlightRanges = new List<(int startIndex, int length)>();
        foreach (var matchLemmaComponentInfo in matchLemmaComponentInfos)
        {
            var startLemmaComponent = lemmaComponentList[matchLemmaComponentInfo.StartLemmaComponentIndex];
            var endLemmaComponent = lemmaComponentList[matchLemmaComponentInfo.StartLemmaComponentIndex + matchLemmaComponentInfo.Count - 1];
            if (highlightRanges.Count == 0 || highlightRanges.Last().startIndex + highlightRanges.Last().length < startLemmaComponent.Offset)
            {
                highlightRanges.Add((startLemmaComponent.Offset, endLemmaComponent.Offset + endLemmaComponent.LemmaInExample.Length - startLemmaComponent.Offset));
            }
            else
            {
                highlightRanges[highlightRanges.Count - 1] = (highlightRanges[highlightRanges.Count - 1].startIndex, endLemmaComponent.Offset + endLemmaComponent.LemmaInExample.Length - highlightRanges[highlightRanges.Count - 1].startIndex);
            }
        }

        return highlightRanges;
    }
}
