using Microsoft.EntityFrameworkCore;
using WordNetSqlite.Model;

namespace WordNetSqlite
{
    public sealed partial class WordNetDatabase
    {
        private static uint Bit(int n) => 1u << n;

        /// <summary>
        /// Sets a bit for every search type that is valid for the word, mirroring is_defined() of the
        /// C library. Returns 0 when the word is not in the database.
        /// </summary>
        public Task<uint> IsDefinedAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => IsDefinedCoreAsync(searchStr, pos, cancellationToken), cancellationToken);

        internal async Task<uint> IsDefinedCoreAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            uint result = 0;

            foreach (var index in await GetIndexCoreAsync(searchStr, pos, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                result |= Bit((int)SearchType.Similar) | Bit((int)SearchType.Polysemy) |
                          Bit((int)SearchType.FindSynonyms) | Bit((int)SearchType.FindKeywordsBySubstring) |
                          Bit((int)SearchType.ShowAllSynsetsForWord);

                foreach (var pointerType in index.PointerTypes)
                {
                    if (pointerType <= PointerType.Class)
                    {
                        result |= Bit(pointerType);
                    }
                    else if (pointerType == PointerType.Instance)
                    {
                        result |= Bit(PointerType.Hypernym);
                    }
                    else if (pointerType == PointerType.Instances)
                    {
                        result |= Bit(PointerType.Hyponym);
                    }

                    if (pointerType == PointerType.Similar)
                    {
                        result |= Bit(PointerType.Antonym);
                    }

                    if (pointerType is >= PointerType.IsMemberPart and <= PointerType.IsPartPart)
                    {
                        result |= Bit(PointerType.Holonym);
                    }
                    else if (pointerType is >= PointerType.HasMemberPart and <= PointerType.HasPartPart)
                    {
                        result |= Bit(PointerType.Meronym);
                    }
                }

                if (pos == PartOfSpeech.Noun)
                {
                    if (await HasHoloMeroAsync(index, (int)SearchType.HierarchicalMeronymSearch, cancellationToken).ConfigureAwait(false))
                    {
                        result |= Bit((int)SearchType.HierarchicalMeronymSearch);
                    }
                    if (await HasHoloMeroAsync(index, (int)SearchType.HierarchicalHolonymSearch, cancellationToken).ConfigureAwait(false))
                    {
                        result |= Bit((int)SearchType.HierarchicalHolonymSearch);
                    }
                    if ((result & Bit(PointerType.Hypernym)) != 0)
                    {
                        result |= Bit((int)SearchType.NounCoordinates);
                    }
                }
                else if (pos == PartOfSpeech.Verb)
                {
                    if ((result & Bit(PointerType.Hypernym)) != 0)
                    {
                        result |= Bit((int)SearchType.NounCoordinates);
                    }

                    result |= Bit((int)SearchType.GroupRelatedSenses) | Bit((int)SearchType.VerbExampleSentencesAndGenericFrames);
                }
            }

            return result;
        }

        /// <summary>Determines whether the hypernyms of the senses have inherited meronyms or holonyms.</summary>
        private async Task<bool> HasHoloMeroAsync(IndexEntryResult index, int pointerType, CancellationToken cancellationToken)
        {
            var pointerBase = pointerType == (int)SearchType.HierarchicalMeronymSearch
                ? PointerType.HasMemberPart
                : PointerType.IsMemberPart;

            var pos = index.PartOfSpeech;
            foreach (var offset in index.Offsets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var synset = await ReadSynsetCoreAsync(pos, offset, null, cancellationToken).ConfigureAwait(false);
                if (synset == null)
                {
                    continue;
                }

                foreach (var targetOffset in GetPointerTargets(synset, PointerType.Hypernym))
                {
                    var hypernym = await ReadSynsetCoreAsync(PartOfSpeech.Noun, targetOffset, null, cancellationToken).ConfigureAwait(false);
                    if (hypernym == null)
                    {
                        continue;
                    }

                    if (HasPointer(hypernym, pointerBase) ||
                        HasPointer(hypernym, pointerBase + 1) ||
                        HasPointer(hypernym, pointerBase + 2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasPointer(Synset synset, int pointerType)
        {
            for (var i = 0; i < synset.PointerCount; i++)
            {
                if (synset.PtrTyp[i] == pointerType)
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<long> GetPointerTargets(Synset synset, int pointerType)
        {
            for (var i = 0; i < synset.PointerCount; i++)
            {
                if (synset.PtrTyp[i] == pointerType)
                {
                    yield return synset.PtrOff[i];
                }
            }
        }

        /// <summary>
        /// Sets a bit for every part of speech the word is in, mirroring in_wn(). The word must match
        /// an index entry exactly; it is not lower cased.
        /// </summary>
        public Task<uint> InWnAsync(string word, PartOfSpeech pos = PartOfSpeech.AllPos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => InWnCoreAsync(word, pos, cancellationToken), cancellationToken);

        internal async Task<uint> InWnCoreAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            uint result = 0;
            if (pos == PartOfSpeech.AllPos)
            {
                for (var i = 1; i <= NumberOfPos; i++)
                {
                    if (await IndexLookupCoreAsync(word, (PartOfSpeech)i, cancellationToken).ConfigureAwait(false) != null)
                    {
                        result |= Bit(i);
                    }
                }
            }
            else if (await IndexLookupCoreAsync(word, pos, cancellationToken).ConfigureAwait(false) != null)
            {
                result |= Bit((int)pos);
            }
            return result;
        }

        /// <summary>
        /// Finds words by substring, mirroring wngrep(): every lemmas of the part of speech that contains
        /// <paramref name="searchStr"/> is returned together with its sense count.
        /// </summary>
        public Task<string> FindKeywordsAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => WngrepCoreAsync(searchStr, pos, cancellationToken), cancellationToken);

        private async Task<string> WngrepCoreAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            var word = ToLowerCase(searchStr).Replace(' ', '_');
            var escaped = word
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
            var pattern = $"%{escaped}%";

            // The LIKE filter is a superset of what the C code accepts; the exact rule is applied below.
            var candidates = await _context.IndexEntries
                .AsNoTracking()
                .Where(e => e.Pos == (int)pos && EF.Functions.Like(e.Lemma, pattern, "\\"))
                .OrderBy(e => e.Lemma)
                .Select(e => e.Lemma)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var buffer = new SearchBuffer();
            foreach (var lemma in candidates)
            {
                if (MatchesWngrep(lemma, word))
                {
                    buffer.Append(lemma.Replace('_', ' ') + "\n");
                }
            }

            return buffer.ToString();
        }

        /// <summary>
        /// Mirrors the match test of wngrep(): the word must be at the start or the end of the index
        /// entry, or stand as a whole word between hyphens or underscores.
        /// </summary>
        private static bool MatchesWngrep(string lemma, string word)
        {
            if (word.Length == 0)
            {
                return false;
            }

            var location = lemma.IndexOf(word, StringComparison.Ordinal);
            while (location >= 0)
            {
                if (location == 0 ||
                    location == lemma.Length - word.Length ||
                    (IsWordSeparator(lemma[location - 1]) &&
                     location + word.Length < lemma.Length &&
                     IsWordSeparator(lemma[location + word.Length])))
                {
                    return true;
                }

                location = lemma.IndexOf(word, location + 1, StringComparison.Ordinal);
            }

            return false;

            static bool IsWordSeparator(char c) => c is '-' or '_';
        }

        /// <summary>
        /// Returns the senses of a word, mirroring findtheinfo_ds(): one synset per requested sense with
        /// the requested pointers traced into <see cref="Synset.PtrList"/>.
        /// </summary>
        /// <param name="searchStr">Word or collocation to look up.</param>
        /// <param name="pos">Part of speech to search in.</param>
        /// <param name="searchType">Search to perform; a negative value requests a recursive search.</param>
        /// <param name="senseNum">1-based sense number, or 0 for all senses.</param>
        public Task<Synset?> FindTheInfoDsAsync(string searchStr, PartOfSpeech pos, SearchType searchType, int senseNum, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => FindTheInfoDsCoreAsync(searchStr, pos, (int)searchType, senseNum, cancellationToken), cancellationToken);

        internal async Task<Synset?> FindTheInfoDsCoreAsync(string searchStr, PartOfSpeech pos, int searchType, int senseNum, CancellationToken cancellationToken)
        {
            var pointerType = searchType;
            var depth = 0;
            Synset? synlist = null;
            Synset? lastsyn = null;
            var searchStrForNextForm = searchStr;

            foreach (var index in await GetIndexCoreAsync(searchStrForNextForm, pos, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                searchStrForNextForm = null!;
                var newSense = true;

                if (pointerType < 0)
                {
                    pointerType = -pointerType;
                    depth = 1;
                }

                for (var sense = 0; sense < index.OffsetCount; sense++)
                {
                    if (senseNum != WordNetConstants.AllSenses && senseNum != sense + 1)
                    {
                        continue;
                    }

                    var synset = await ReadSynsetCoreAsync(pos, index.Offsets[sense], index.Word, cancellationToken).ConfigureAwait(false);
                    if (synset == null)
                    {
                        continue;
                    }

                    if (lastsyn != null)
                    {
                        if (newSense)
                        {
                            lastsyn.NextForm = synset;
                        }
                        else
                        {
                            lastsyn.NextSS = synset;
                        }
                    }

                    synlist ??= synset;
                    newSense = false;

                    synset.SearchType = pointerType;
                    synset.PtrList = await TracePtrsDsAsync(synset, pointerType, depth, cancellationToken).ConfigureAwait(false);

                    lastsyn = synset;

                    if (senseNum == sense + 1)
                    {
                        break;
                    }
                }

                if (pointerType == (int)SearchType.NounCoordinates && synlist is { PtrList: not null })
                {
                    // The C library removes the hypernym that was only needed to find the coordinates.
                    var hypernym = synlist.PtrList;
                    synlist.PtrList = hypernym.PtrList;
                }
            }

            return synlist;
        }

        /// <summary>
        /// Traces the pointers of a synset and returns the synsets found, mirroring traceptrs_ds().
        /// </summary>
        internal async Task<Synset?> TracePtrsDsAsync(Synset synset, int pointerType, int depth, CancellationToken cancellationToken)
        {
            // If the synset is a satellite, remember the head word of its head synset and its sense number.
            if (IsSatellite(synset))
            {
                for (var i = 0; i < synset.PointerCount; i++)
                {
                    if (synset.PtrTyp[i] == PointerType.Similar)
                    {
                        var head = await ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], null, cancellationToken).ConfigureAwait(false);
                        if (head != null)
                        {
                            synset.HeadWord = head.Words[0];
                            synset.HeadSense = (short)head.LexId[0];
                        }
                        break;
                    }
                }
            }

            int targetPointerType;
            bool doCoords;
            if (pointerType == (int)SearchType.NounCoordinates)
            {
                targetPointerType = PointerType.Hypernym;
                doCoords = true;
            }
            else
            {
                targetPointerType = pointerType;
                doCoords = false;
            }

            Synset? synlist = null;
            Synset? lastsyn = null;

            for (var i = 0; i < synset.PointerCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (synset.PtrTyp[i] != targetPointerType ||
                    (synset.PFrm[i] != 0 && synset.PFrm[i] != synset.WhichWord))
                {
                    continue;
                }

                var target = await ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], null, cancellationToken).ConfigureAwait(false);
                if (target == null)
                {
                    continue;
                }

                target.SearchType = pointerType;

                if (lastsyn != null)
                {
                    lastsyn.NextSS = target;
                }
                synlist ??= target;
                lastsyn = target;

                if (depth != 0)
                {
                    depth = DepthCheck(depth, target);
                    target.PtrList = await TracePtrsDsAsync(target, pointerType, depth + 1, cancellationToken).ConfigureAwait(false);
                }
                else if (doCoords)
                {
                    target.PtrList = await TracePtrsDsAsync(target, PointerType.Hyponym, 0, cancellationToken).ConfigureAwait(false);
                }
            }

            return synlist;
        }

        private static int DepthCheck(int depth, Synset synset) =>
            depth >= WordNetConstants.MaxDepth ? -1 : depth;

        /// <summary>
        /// Whether the synset is an adjective satellite. The C library reads this from the ss_type
        /// character of the data file; parse_synset() turns it into the indirect antonym synset type.
        /// </summary>
        private static bool IsSatellite(Synset synset) => synset.SsType == SynsetType.IndirectAnt;
    }
}
