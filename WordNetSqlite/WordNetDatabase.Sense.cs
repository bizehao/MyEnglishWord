using Microsoft.EntityFrameworkCore;

namespace WordNetSqlite
{
    public sealed partial class WordNetDatabase
    {
        /// <summary>One entry of index.sense, mirroring the SnsIndex structure of the C library.</summary>
        public sealed class SenseIndexResult
        {
            /// <summary>The sense key.</summary>
            public string SenseKey { get; init; } = string.Empty;

            /// <summary>The word part of the sense key.</summary>
            public string Word { get; init; } = string.Empty;

            /// <summary>Data file offset of the synset the sense belongs to.</summary>
            public long Offset { get; init; }

            /// <summary>WordNet sense number.</summary>
            public int WnSense { get; init; }

            /// <summary>Number of times the sense is tagged in the semantic concordance texts.</summary>
            public int TagCount { get; init; }
        }

        /// <summary>Returns the parsed sense index entry of a sense key, mirroring GetSenseIndex().</summary>
        public Task<SenseIndexResult?> GetSenseIndexAsync(string senseKey, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => GetSenseIndexCoreAsync(senseKey, cancellationToken), cancellationToken);

        internal async Task<SenseIndexResult?> GetSenseIndexCoreAsync(string senseKey, CancellationToken cancellationToken)
        {
            var entry = await _context.SenseIndexEntries
                .AsNoTracking()
                .Include(e => e.Synset)
                .FirstOrDefaultAsync(e => e.SenseKey == senseKey, cancellationToken)
                .ConfigureAwait(false);

            if (entry == null)
            {
                return null;
            }

            return new SenseIndexResult
            {
                SenseKey = entry.SenseKey,
                Word = GetWordOfSenseKey(entry.SenseKey),
                Offset = entry.Synset?.Offset ?? 0,
                WnSense = entry.WnSense,
                TagCount = entry.TagCount,
            };
        }

        /// <summary>Returns the data file offset of a sense key, mirroring GetDataOffset().</summary>
        public Task<long> GetDataOffsetAsync(string senseKey, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () => (await GetSenseIndexCoreAsync(senseKey, cancellationToken).ConfigureAwait(false))?.Offset ?? 0, cancellationToken);

        /// <summary>Returns the polysemy count of the word of a sense key, mirroring GetPolyCount().</summary>
        public Task<int> GetPolyCountAsync(string senseKey, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () =>
            {
                var word = GetWordOfSenseKey(senseKey);
                var pos = GetPosOfSenseKey(senseKey);
                var index = await IndexLookupCoreAsync(word, pos, cancellationToken).ConfigureAwait(false);
                return index?.SenseCount ?? 0;
            }, cancellationToken);

        /// <summary>Returns the synset of a sense key, mirroring GetSynsetForSense().</summary>
        public Task<Synset?> GetSynsetForSenseAsync(string senseKey, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () =>
            {
                var offset = (await GetSenseIndexCoreAsync(senseKey, cancellationToken).ConfigureAwait(false))?.Offset ?? 0;
                return offset == 0
                    ? null
                    : await ReadSynsetCoreAsync(GetPosOfSenseKey(senseKey), offset, GetWordOfSenseKey(senseKey), cancellationToken).ConfigureAwait(false);
            }, cancellationToken);

        /// <summary>Returns the WordNet sense number of a word and lexicographer sense, mirroring GetWNSense().</summary>
        public Task<int> GetWnSenseAsync(string word, string lexSense, CancellationToken cancellationToken = default) =>
            ExecuteAsync(async () => (await GetSenseIndexCoreAsync($"{word}%{lexSense}", cancellationToken).ConfigureAwait(false))?.WnSense ?? 0, cancellationToken);

        /// <summary>Returns the word part of a sense key, mirroring GetWORD().</summary>
        public static string GetWordOfSenseKey(string senseKey)
        {
            var percent = senseKey.IndexOf('%');
            return percent < 0 ? senseKey : senseKey.Substring(0, percent);
        }

        /// <summary>Returns the part of speech of a sense key, mirroring GetPOS().</summary>
        public static PartOfSpeech GetPosOfSenseKey(string senseKey)
        {
            var percent = senseKey.IndexOf('%');
            var ssType = senseKey[percent + 1] - '0';
            return ssType == (int)PartOfSpeech.Satellite ? PartOfSpeech.Adj : (PartOfSpeech)ssType;
        }

        /// <summary>
        /// Converts a sense number of an index entry to its sense key, mirroring WNSnsToStr().
        /// Returns null when the word is not in the synset of that sense.
        /// </summary>
        public Task<string?> SenseNumberToSenseKeyAsync(IndexEntryResult index, int sense, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => SenseNumberToSenseKeyCoreAsync(index, sense, cancellationToken), cancellationToken);

        internal async Task<string?> SenseNumberToSenseKeyCoreAsync(IndexEntryResult index, int sense, CancellationToken cancellationToken)
        {
            var pos = index.PartOfSpeech;
            var position = pos == PartOfSpeech.Satellite ? PartOfSpeech.Adj : pos;
            var synset = await ReadSynsetCoreAsync(position, index.Offsets[sense - 1], null, cancellationToken).ConfigureAwait(false);
            if (synset == null)
            {
                return null;
            }

            var ssType = synset.SsType;
            string? headWord = null;
            int headSense = 0;
            if (ssType == SynsetType.IndirectAnt)
            {
                for (var i = 0; i < synset.PointerCount; i++)
                {
                    if (synset.PtrTyp[i] == PointerType.Similar)
                    {
                        var head = await ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], null, cancellationToken).ConfigureAwait(false);
                        if (head != null)
                        {
                            // strtolower() truncates the head word at the adjective marker, so the
                            // sense key never carries the marker.
                            headWord = ToLowerCaseTruncateAtMarker(head.Words[0]);
                            headSense = head.LexId[0];
                        }
                        break;
                    }
                }
            }

            var wordIndex = -1;
            for (var i = 0; i < synset.WordCount; i++)
            {
                if (ToLowerCaseTruncateAtMarker(synset.Words[i]) == index.Word)
                {
                    wordIndex = i;
                    break;
                }
            }

            if (wordIndex < 0)
            {
                return null;
            }

            var lexId = synset.LexId[wordIndex];
            if (ssType == SynsetType.IndirectAnt)
            {
                return $"{index.Word}%{(int)PartOfSpeech.Satellite}:{synset.Fnum:D2}:{lexId:D2}:{headWord}:{headSense:D2}";
            }

            return $"{index.Word}%{(int)position}:{synset.Fnum:D2}:{lexId:D2}::";
        }

        /// <summary>Returns how often a sense is tagged in the semantic concordances, mirroring GetTagcnt().</summary>
        public Task<int> GetTagCountAsync(IndexEntryResult index, int sense, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => GetTagCountCoreAsync(index, sense, cancellationToken), cancellationToken);

        /// <summary>Lock free variant of <see cref="GetTagCountAsync"/> for use inside a search.</summary>
        internal async Task<int> GetTagCountCoreAsync(IndexEntryResult index, int sense, CancellationToken cancellationToken)
        {
            var senseKey = await SenseNumberToSenseKeyCoreAsync(index, sense, cancellationToken).ConfigureAwait(false);
            if (senseKey == null)
            {
                return 0;
            }

            return await _context.SenseTagCounts
                .AsNoTracking()
                .Where(e => e.SenseKey == senseKey)
                .Select(e => e.TagCount)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Searches the database for the string or a base form of it, mirroring GetValidIndexPointer().
        /// </summary>
        public Task<IndexEntryResult?> GetValidIndexPointerAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => GetValidIndexPointerCoreAsync(word, pos, cancellationToken), cancellationToken);

        internal async Task<IndexEntryResult?> GetValidIndexPointerCoreAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            var index = (await GetIndexCoreAsync(word, pos, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (index != null)
            {
                return index;
            }

            foreach (var morphed in await MorphStrCoreAsync(word, pos, cancellationToken).ConfigureAwait(false))
            {
                index = (await GetIndexCoreAsync(morphed, pos, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
                if (index != null)
                {
                    return index;
                }
            }
            return null;
        }
    }
}
