using Microsoft.EntityFrameworkCore;
using WordNetSqlite.Model;
using DbSynset = WordNetSqlite.Model.SynsetRecord;

namespace WordNetSqlite
{
    /// <summary>
    /// A WordNet 3.0 database backed by SQLite through EF Core. It offers the same searches as the
    /// C library (findtheinfo / findtheinfo_ds / morphstr) without any native code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="WordNetDatabase"/> is safe to use from several threads; the searches are serialised
    /// because a search keeps working state, exactly like the C library does.
    /// </para>
    /// <para>
    /// Every search is asynchronous. The work is handed to the thread pool, so a caller on a user
    /// interface thread is never blocked, and a search that is waiting for another one to finish can
    /// be cancelled by passing a <see cref="CancellationToken"/>.
    /// </para>
    /// </remarks>
    public sealed partial class WordNetDatabase : IDisposable
    {
        /// <summary>Number of parts of speech (NUMPARTS).</summary>
        public const int NumberOfPos = WordNetConstants.NumParts;

        /// <summary>Number of generic verb sentence frames (NUMFRAMES).</summary>
        public const int NumberOfVerbFrames = WordNetConstants.NumFrames;

        /// <summary>Cache of immutable synset data; the search code creates a fresh <see cref="Synset"/> per read.</summary>
        private readonly SynsetCache _synsetCache;

        private readonly WordNetContext _context;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _initialized;
        private bool _disposed;

        public WordNetDatabase(string dbPath)
            : this(dbPath, readOnly: true)
        {
        }

        public WordNetDatabase(string dbPath, bool readOnly, int cacheCapacity = 8192)
        {
            DbPath = dbPath;
            ReadOnly = readOnly;
            _synsetCache = new SynsetCache(cacheCapacity);
            _context = new WordNetContext(dbPath, readOnly);
        }

        public string DbPath { get; }

        public bool ReadOnly { get; }

        /// <summary>
        /// Opens the database and verifies that it contains a dictionary. Calling it is optional:
        /// the first search opens the database as well. Use it to pay for that up front, or to find
        /// out early whether the file is a usable WordNet database.
        /// </summary>
        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            ExecuteAsync<object?>(async () =>
            {
                EnsureInitialized();
                _ = await _context.Synsets.CountAsync(cancellationToken);
                return null;
            }, cancellationToken);

        /// <summary>
        /// Opens the connection and keeps it open: Microsoft.Data.Sqlite only pools connections from
        /// version 6 on, so without this every query of the netstandard2.0 build would reopen the
        /// database file. Only called while the database lock is held.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _context.Database.OpenConnection();
            _initialized = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _semaphore.Dispose();
            _context.Dispose();
        }

        /// <summary>
        /// Runs a search while holding the database lock. Searches mutate engine state, so they must
        /// not run concurrently.
        /// </summary>
        /// <remarks>
        /// The search is handed to the thread pool. Its EF Core queries are asynchronous, but the
        /// SQLite provider runs them synchronously under the hood, so without the thread pool the
        /// caller would still be blocked for the whole search.
        /// </remarks>
        private async Task<T> ExecuteAsync<T>(Func<Task<T>> search, CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(async () =>
                {
                    EnsureInitialized();
                    return await search().ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>Returns the part of speech names, indexed by part of speech code (1..4).</summary>
        public static string GetPartName(PartOfSpeech pos) =>
            WordNetConstants.PartNames[(int)pos];

        private static string GetPartChar(PartOfSpeech pos) =>
            WordNetConstants.PartChars[(int)pos].ToString();

        internal static PartOfSpeech GetPosOfPartChar(char c) => c switch
        {
            'n' => PartOfSpeech.Noun,
            'v' => PartOfSpeech.Verb,
            'a' or 's' => PartOfSpeech.Adj,
            'r' => PartOfSpeech.Adv,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, "Unknown part of speech"),
        };

        #region Index lookups

        /// <summary>
        /// Finds a lemma in the index of the given part of speech. The word must match the index entry
        /// exactly, as index_lookup() in the C library requires.
        /// </summary>
        public Task<IndexEntryResult?> IndexLookupAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => IndexLookupCoreAsync(word, pos, cancellationToken), cancellationToken);

        internal async Task<IndexEntryResult?> IndexLookupCoreAsync(string word, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            // The two collections of an entry are fetched with separate projections: eager loading
            // them together multiplies their rows, which is expensive on EF Core 3.1 and pointless
            // for a handful of rows on any version.
            var entry = await _context.IndexEntries
                .AsNoTracking()
                .Where(e => e.Lemma == word && e.Pos == (int)pos)
                .Select(e => new { e.Id, e.Lemma, e.Pos, e.SenseCount, e.TaggedSenseCount })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (entry == null)
            {
                return null;
            }

            var pointerTypes = await _context.IndexPointerTypes
                .AsNoTracking()
                .Where(p => p.IndexEntryId == entry.Id)
                .OrderBy(p => p.Index)
                .Select(p => p.PointerType)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            var offsets = await _context.IndexSenses
                .AsNoTracking()
                .Where(s => s.IndexEntryId == entry.Id)
                .OrderBy(s => s.SenseNumber)
                .Select(s => s.Offset)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            return new IndexEntryResult
            {
                Word = entry.Lemma,
                Pos = WordNetConstants.PartChars[entry.Pos].ToString(),
                SenseCount = entry.SenseCount,
                TaggedSenseCount = entry.TaggedSenseCount,
                PointerTypes = pointerTypes,
                Offsets = offsets,
            };
        }

        /// <summary>
        /// Returns an evenly spread sample of the lemmas of a part of speech, in index order.
        /// </summary>
        public Task<IReadOnlyList<string>> SampleIndexLemmasAsync(PartOfSpeech pos, int count, CancellationToken cancellationToken = default) =>
            ExecuteAsync<IReadOnlyList<string>>(async () =>
            {
                var lemmas = await _context.IndexEntries
                    .AsNoTracking()
                    .Where(e => e.Pos == (int)pos)
                    .OrderBy(e => e.Lemma)
                    .Select(e => e.Lemma)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (lemmas.Count <= count)
                {
                    return (IReadOnlyList<string>)lemmas;
                }

                var step = Math.Max(1, lemmas.Count / count);
                var sample = new List<string>(count);
                for (var i = 0; i < lemmas.Count && sample.Count < count; i += step)
                {
                    sample.Add(lemmas[i]);
                }
                return sample;
            }, cancellationToken);

        /// <summary>
        /// Returns every lemma of a part of speech in index order, which is what a caller needs to
        /// build a dictionary of the words the database knows.
        /// </summary>
        public Task<IReadOnlyList<string>> GetIndexLemmasAsync(PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync<IReadOnlyList<string>>(async () => await _context.IndexEntries
                .AsNoTracking()
                .Where(e => e.Pos == (int)pos)
                .OrderBy(e => e.Lemma)
                .Select(e => e.Lemma)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false), cancellationToken);

        /// <summary>
        /// 'smart' search of the index, mirroring getindex(): it tries the word itself, underscores
        /// swapped for hyphens and vice versa, and the word with hyphens/underscores or periods removed.
        /// </summary>
        public Task<IReadOnlyList<IndexEntryResult>> GetIndexAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken = default) =>
            ExecuteAsync<IReadOnlyList<IndexEntryResult>>(
                async () => (IReadOnlyList<IndexEntryResult>)await GetIndexCoreAsync(searchStr, pos, cancellationToken).ConfigureAwait(false),
                cancellationToken);

        internal async Task<List<IndexEntryResult>> GetIndexCoreAsync(string searchStr, PartOfSpeech pos, CancellationToken cancellationToken)
        {
            var forms = BuildSearchForms(searchStr);
            var results = new List<IndexEntryResult>(forms.Count);

            var first = await IndexLookupCoreAsync(forms[0], pos, cancellationToken).ConfigureAwait(false);
            if (first != null)
            {
                results.Add(first);
            }

            for (var i = 1; i < forms.Count; i++)
            {
                // The C code skips a form that is identical to the search word; identical forms
                // among the alternatives themselves are looked up again.
                if (forms[i].Length == 0 || forms[i] == forms[0])
                {
                    continue;
                }

                var entry = await IndexLookupCoreAsync(forms[i], pos, cancellationToken).ConfigureAwait(false);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        /// <summary>
        /// Builds the word forms getindex() tries, in the same order: the lower cased word, underscores
        /// replaced by hyphens, hyphens replaced by underscores, hyphens and underscores removed,
        /// and periods removed.
        /// </summary>
        internal static List<string> BuildSearchForms(string searchStr)
        {
            var baseForm = ToLowerCaseTruncateAtMarker(searchStr);

            var forms = new List<string>(WordNetConstants.MaxForms)
            {
                baseForm,
                baseForm.Replace('_', '-'),
                baseForm.Replace('-', '_'),
                baseForm.Replace("_", string.Empty).Replace("-", string.Empty),
                baseForm.Replace(".", string.Empty),
            };
            return forms;
        }

        /// <summary>Mirrors strtolower() of morph.c: lower cases up to the first opening parenthesis.</summary>
        internal static string ToLowerCaseTruncateAtMarker(string str)
        {
            var marker = str.IndexOf('(');
            var text = marker >= 0 ? str.Substring(0, marker) : str;
            return text.ToLowerInvariant();
        }

        /// <summary>Mirrors ToLowerCase() of morph.c: lower cases the whole string.</summary>
        internal static string ToLowerCase(string str) => str.ToLowerInvariant();

        #endregion

        #region Synset reading

        /// <summary>
        /// Reads the synset at the given data file offset, mirroring read_synset(). When <paramref name="word"/>
        /// is given, the matching word number is stored in <see cref="Synset.WhichWord"/>.
        /// </summary>
        public Task<Synset?> ReadSynsetAsync(PartOfSpeech pos, long offset, string? word = null, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => ReadSynsetCoreAsync(pos, offset, word, cancellationToken), cancellationToken);

        internal async Task<Synset?> ReadSynsetCoreAsync(PartOfSpeech pos, long offset, string? word, CancellationToken cancellationToken)
        {
            var template = await GetTemplateAsync(pos, offset, cancellationToken).ConfigureAwait(false);
            return template == null ? null : CreateSynset(template, word);
        }

        private async Task<SynsetTemplate?> GetTemplateAsync(PartOfSpeech pos, long offset, CancellationToken cancellationToken)
        {
            var key = SynsetCache.MakeKey((int)pos, offset);
            if (_synsetCache.TryGet(key, out var cached))
            {
                return cached;
            }

            var template = await LoadTemplateAsync(pos, offset, cancellationToken).ConfigureAwait(false);
            if (template != null)
            {
                _synsetCache.Add(key, template);
            }
            return template;
        }

        /// <summary>Creates the mutable search result from the cached, immutable synset data.</summary>
        private static Synset CreateSynset(SynsetTemplate template, string? word)
        {
            var whichWord = 0;
            if (word != null)
            {
                for (var i = 0; i < template.Words.Length; i++)
                {
                    if (ToLowerCaseTruncateAtMarker(template.Words[i]) == word)
                    {
                        whichWord = i + 1;
                        break;
                    }
                }
            }

            return new Synset
            {
                Id = template.Id,
                Pos = template.Pos,
                SsType = template.SsType,
                Fnum = template.LexFileNum,
                HereIAm = template.Offset,
                Key = template.Key,
                Words = (string[])template.Words.Clone(),
                LexId = (int[])template.LexIds.Clone(),
                WnSns = (int[])template.SenseNumbers.Clone(),
                WhichWord = whichWord,
                PtrTyp = Array.ConvertAll(template.Pointers, p => p.Type),
                PtrOff = Array.ConvertAll(template.Pointers, p => p.TargetOffset),
                PPos = Array.ConvertAll(template.Pointers, p => p.TargetPos),
                PTo = Array.ConvertAll(template.Pointers, p => p.TargetWord),
                PFrm = Array.ConvertAll(template.Pointers, p => p.SourceWord),
                FrmId = Array.ConvertAll(template.Frames, f => f.FrameNumber),
                FrmTo = Array.ConvertAll(template.Frames, f => f.WordNumber),
                Defn = "(" + template.Gloss + ")",
            };
        }

        /// <summary>
        /// Reads one synset from the database. Words, pointers and frames are fetched with separate
        /// projections rather than eager loading: a single query would multiply their rows, and the
        /// search reads one synset at a time anyway.
        /// </summary>
        private async Task<SynsetTemplate?> LoadTemplateAsync(PartOfSpeech pos, long offset, CancellationToken cancellationToken)
        {
            var synset = await _context.Synsets
                .AsNoTracking()
                .Where(s => s.Pos == (int)pos && s.Offset == offset)
                .Select(s => new { s.Id, s.Pos, s.Offset, s.LexFileNum, s.SsType, s.Gloss, s.Key })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (synset == null)
            {
                return null;
            }

            var words = await _context.SynsetWords
                .AsNoTracking()
                .Where(w => w.SynsetId == synset.Id)
                .OrderBy(w => w.WordNumber)
                .Select(w => new { w.Text, w.LexId, w.SenseNumber })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var pointers = await _context.Pointers
                .AsNoTracking()
                .Where(p => p.SynsetId == synset.Id)
                .OrderBy(p => p.Index)
                .Select(p => new
                {
                    p.PointerType,
                    p.TargetSynsetId,
                    TargetPos = p.TargetSynset!.Pos,
                    TargetOffset = p.TargetSynset!.Offset,
                    p.SourceWordNumber,
                    p.TargetWordNumber,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var frames = await _context.VerbFrames
                .AsNoTracking()
                .Where(f => f.SynsetId == synset.Id)
                .OrderBy(f => f.Index)
                .Select(f => new { f.FrameNumber, f.WordNumber })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return new SynsetTemplate
            {
                Id = synset.Id,
                Pos = synset.Pos,
                Offset = synset.Offset,
                LexFileNum = synset.LexFileNum,
                SsType = synset.SsType,
                Gloss = synset.Gloss,
                Key = synset.Key,
                Words = words.Select(w => w.Text).ToArray(),
                LexIds = words.Select(w => w.LexId).ToArray(),
                SenseNumbers = words.Select(w => w.SenseNumber).ToArray(),
                Pointers = pointers
                    .Select(p => new PointerTemplate(
                        p.PointerType,
                        p.TargetSynsetId,
                        p.TargetPos,
                        p.TargetOffset,
                        p.SourceWordNumber,
                        p.TargetWordNumber))
                    .ToArray(),
                Frames = frames
                    .Select(f => new FrameTemplate(f.FrameNumber, f.WordNumber))
                    .ToArray(),
            };
        }

        #endregion
    }
}
