using Microsoft.EntityFrameworkCore;

namespace WordNetSqlite
{
    public sealed partial class WordNetDatabase
    {
        private static readonly string[] Suffixes =
        {
            // noun suffixes
            "s", "ses", "xes", "zes", "ches", "shes", "men", "ies",
            // verb suffixes
            "s", "ies", "es", "es", "ed", "ed", "ing", "ing",
            // adjective suffixes
            "er", "est", "er", "est",
        };

        private static readonly string[] Endings =
        {
            // noun endings
            "", "s", "x", "z", "ch", "sh", "man", "y",
            // verb endings
            "", "y", "e", "", "e", "", "e", "",
            // adjective endings
            "", "", "e", "e",
        };

        /// <summary>Index into <see cref="Suffixes"/> of the first rule of each part of speech, indexed by pos.</summary>
        private static readonly int[] SuffixOffsets = { 0, 0, 8, 16, 0 };

        /// <summary>Number of detachment rules of each part of speech, indexed by pos.</summary>
        private static readonly int[] SuffixCounts = { 0, 8, 8, 4, 0 };

        private static readonly string[] Prepositions =
        {
            "to", "at", "of", "on", "off", "in", "out", "up", "down", "from", "with", "into", "for", "about", "between",
        };

        /// <summary>
        /// Tries to find base forms (lemmas) of a word or collocation in the given part of speech,
        /// mirroring the morphstr() loop of the C library: the exception lists are tried first, then
        /// the detachment rules, and finally the individual words of a multi word string.
        /// </summary>
        public Task<IReadOnlyList<string>> MorphStrAsync(string origStr, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync<IReadOnlyList<string>>(async () => await MorphStrCoreAsync(origStr, pos, cancellationToken).ConfigureAwait(false), cancellationToken);

        internal async Task<List<string>> MorphStrCoreAsync(string origStr, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            var results = new List<string>();

            if (pos == PartOfSpeech.Satellite)
            {
                pos = PartOfSpeech.Adj;
            }

            // Assume the string has not had spaces substituted with underscores.
            var str = ToLowerCaseTruncateAtMarker(origStr.Replace(' ', '_'));
            var wordCount = CountWords(str, '_');

            // First try the exception list. Only the first base form is returned here; the remaining
            // ones are returned by the subsequent calls of the C loop.
            var exceptions = await LookupExceptionsCoreAsync(str, pos, cancellationToken).ConfigureAwait(false);
            if (exceptions.Count > 0 && exceptions[0] != str)
            {
                foreach (var exception in exceptions)
                {
                    if (exception != str)
                    {
                        results.Add(exception);
                    }
                }
                return results;
            }

            // Then try simply to morph the original string.
            if (pos != PartOfSpeech.Verb)
            {
                var morphed = await MorphWordCoreAsync(str, pos, cancellationToken).ConfigureAwait(false);
                if (morphed != null && morphed != str)
                {
                    results.Add(morphed);
                    return results;
                }
            }

            if (pos == PartOfSpeech.Verb && wordCount > 1 && HasPreposition(str, wordCount) != 0)
            {
                // Assume we have a verb followed by a preposition.
                var morphPrep = await MorphPrepAsync(str, cancellationToken).ConfigureAwait(false);
                if (morphPrep != null)
                {
                    results.Add(morphPrep);
                }
                return results;
            }

            // Otherwise morph each word of the string separately and rejoin them.
            var joined = await MorphWordsOfCollocationAsync(str, pos, cancellationToken).ConfigureAwait(false);
            if (joined != null)
            {
                results.Add(joined);
            }

            return results;
        }

        /// <summary>
        /// Mirrors the loop at the end of morphstr() that morphs every word of a dashed or underscored
        /// collocation and rejoins the result.
        /// </summary>
        private async Task<string?> MorphWordsOfCollocationAsync(string str, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            var searchStr = string.Empty;
            var count = CountWords(str, '-');
            var startIndex = 0;
            var remaining = count;

            while (--remaining > 0)
            {
                var underscore = str.IndexOf('_', startIndex);
                var hyphen = str.IndexOf('-', startIndex);

                int endIndex;
                string append;
                if (underscore >= 0 && hyphen >= 0)
                {
                    if (underscore < hyphen)
                    {
                        endIndex = underscore;
                        append = "_";
                    }
                    else
                    {
                        endIndex = hyphen;
                        append = "-";
                    }
                }
                else if (underscore >= 0)
                {
                    endIndex = underscore;
                    append = "_";
                }
                else
                {
                    endIndex = hyphen;
                    append = "-";
                }

                if (endIndex < 0)
                {
                    return null;
                }

                var word = str.Substring(startIndex, endIndex - startIndex);
                searchStr += await MorphWordCoreAsync(word, pos, cancellationToken).ConfigureAwait(false) ?? word;
                searchStr += append;
                startIndex = endIndex + 1;
            }

            var lastWord = str.Substring(startIndex);
            var lastMorphed = await MorphWordCoreAsync(lastWord, pos, cancellationToken).ConfigureAwait(false);
            searchStr += lastMorphed ?? lastWord;

            if (searchStr != str && await IsDefinedCoreAsync(searchStr, pos, cancellationToken).ConfigureAwait(false) != 0)
            {
                return searchStr;
            }
            return null;
        }

        /// <summary>
        /// Tries to find the base form of a single word in the given part of speech, mirroring morphword():
        /// the exception list is consulted first, then the detachment rules of morph.c.
        /// </summary>
        public Task<string?> MorphWordAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => MorphWordCoreAsync(word, pos, cancellationToken), cancellationToken);

        internal async Task<string?> MorphWordCoreAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            if (pos == PartOfSpeech.Satellite)
            {
                pos = PartOfSpeech.Adj;
            }

            // First look for the word on the exception list.
            var exceptions = await LookupExceptionsCoreAsync(word, pos, cancellationToken).ConfigureAwait(false);
            if (exceptions.Count > 0)
            {
                return exceptions[0];
            }

            if (pos == PartOfSpeech.Adv)
            {
                // Only the exception list is used for adverbs.
                return null;
            }

            var tempBuffer = string.Empty;
            var end = string.Empty;

            if (pos == PartOfSpeech.Noun)
            {
                if (EndsWith(word, "ful"))
                {
                    var index = word.LastIndexOf('f');
                    tempBuffer = word.Substring(0, index);
                    end = "ful";
                }
                else if (EndsWith(word, "ss") || word.Length <= 2)
                {
                    return null;
                }
            }

            if (tempBuffer.Length == 0)
            {
                tempBuffer = word;
            }

            var offset = SuffixOffsets[(int)pos];
            var count = SuffixCounts[(int)pos];
            for (var i = 0; i < count; i++)
            {
                var baseForm = WordBase(tempBuffer, i + offset);
                if (baseForm != tempBuffer && await IsDefinedCoreAsync(baseForm, pos, cancellationToken).ConfigureAwait(false) != 0)
                {
                    return baseForm + end;
                }
            }
            return null;
        }

        /// <summary>Applies the detachment rule <paramref name="ender"/> to the word, as wordbase() does.</summary>
        private static string WordBase(string word, int ender)
        {
            var suffix = Suffixes[ender];
            if (EndsWith(word, suffix))
            {
                return word.Substring(0, word.Length - suffix.Length) + Endings[ender];
            }
            return word;
        }

        private static bool EndsWith(string str, string suffix) =>
            suffix.Length < str.Length && str.EndsWith(suffix, StringComparison.Ordinal);

        /// <summary>Returns the base forms of an inflected form from the exception list, in file order.</summary>
        public Task<IReadOnlyList<string>> LookupExceptionsAsync(string form, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync<IReadOnlyList<string>>(async () => await LookupExceptionsCoreAsync(form, pos, cancellationToken).ConfigureAwait(false), cancellationToken);

        internal async Task<List<string>> LookupExceptionsCoreAsync(string form, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            if (pos == PartOfSpeech.Satellite)
            {
                pos = PartOfSpeech.Adj;
            }

            return await _context.MorphExceptions
                .AsNoTracking()
                .Where(e => e.Pos == (int)pos && e.Form == form)
                .OrderBy(e => e.Index)
                .Select(e => e.BaseForm)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Counts the number of words of a string, mirroring cntwords(): spaces and underscores always
        /// separate words, and so does <paramref name="separator"/>.
        /// </summary>
        internal static int CountWords(string s, char separator)
        {
            var wordCount = 0;
            var i = 0;
            while (i < s.Length)
            {
                if (s[i] == separator || s[i] == ' ' || s[i] == '_')
                {
                    wordCount++;
                    while (i < s.Length && (s[i] == separator || s[i] == ' ' || s[i] == '_'))
                    {
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
            return wordCount + 1;
        }

        /// <summary>Finds a preposition in the verb string and returns its word number, as hasprep() does.</summary>
        private static int HasPreposition(string s, int wordCount)
        {
            for (var wordNumber = 2; wordNumber <= wordCount; wordNumber++)
            {
                var index = s.IndexOf('_');
                if (index < 0)
                {
                    break;
                }

                var rest = s.Substring(index + 1);
                foreach (var preposition in Prepositions)
                {
                    if (rest.StartsWith(preposition, StringComparison.Ordinal) &&
                        (rest.Length == preposition.Length || rest[preposition.Length] == '_'))
                    {
                        return wordNumber;
                    }
                }
                s = rest;
            }
            return 0;
        }

        /// <summary>Morphs a verb followed by a preposition, mirroring morphprep() of morph.c.</summary>
        private async Task<string?> MorphPrepAsync(string s, CancellationToken cancellationToken)
        {
            var restIndex = s.IndexOf('_');
            var lastIndex = s.LastIndexOf('_');
            if (restIndex < 0)
            {
                return null;
            }

            string? lastWordBase = null;
            var end = string.Empty;
            if (restIndex != lastIndex)
            {
                // More than two words: try morphing the last word as a noun.
                lastWordBase = await MorphWordCoreAsync(s.Substring(lastIndex + 1), PartOfSpeech.Noun, cancellationToken).ConfigureAwait(false);
                if (lastWordBase != null)
                {
                    end = s.Substring(restIndex, lastIndex + 1 - restIndex) + lastWordBase;
                }
            }

            var word = s.Substring(0, restIndex);
            if (word.Any(c => !char.IsLetterOrDigit(c)))
            {
                return null;
            }

            var rest = s.Substring(restIndex);
            var offset = SuffixOffsets[(int)PartOfSpeech.Verb - 1];
            var count = SuffixCounts[(int)PartOfSpeech.Verb - 1];

            // First try to find the verb on the exception list.
            var exceptions = await LookupExceptionsCoreAsync(word, PartOfSpeech.Verb, cancellationToken).ConfigureAwait(false);
            if (exceptions.Count > 0 && exceptions[0] != word)
            {
                var candidate = exceptions[0] + rest;
                if (await IsDefinedCoreAsync(candidate, PartOfSpeech.Verb, cancellationToken).ConfigureAwait(false) != 0)
                {
                    return candidate;
                }
                if (lastWordBase != null)
                {
                    candidate = exceptions[0] + end;
                    if (await IsDefinedCoreAsync(candidate, PartOfSpeech.Verb, cancellationToken).ConfigureAwait(false) != 0)
                    {
                        return candidate;
                    }
                }
            }

            for (var i = 0; i < count; i++)
            {
                var baseForm = WordBase(word, i + offset);
                if (baseForm == word)
                {
                    continue;
                }

                var candidate = baseForm + rest;
                if (await IsDefinedCoreAsync(candidate, PartOfSpeech.Verb, cancellationToken).ConfigureAwait(false) != 0)
                {
                    return candidate;
                }
                if (lastWordBase != null)
                {
                    candidate = baseForm + end;
                    if (await IsDefinedCoreAsync(candidate, PartOfSpeech.Verb, cancellationToken).ConfigureAwait(false) != 0)
                    {
                        return candidate;
                    }
                }
            }

            var fallback = word + rest;
            if (fallback != s)
            {
                return fallback;
            }
            if (lastWordBase != null)
            {
                fallback = word + end;
                if (fallback != s)
                {
                    return fallback;
                }
            }
            return null;
        }
    }
}
