using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WordNetSqlite;
using WordNetSqlite.Model;
using DbSynset = WordNetSqlite.Model.SynsetRecord;

namespace WordNetSqliteDbGen
{
    /// <summary>
    /// Converts the WordNet 3.0 dictionary files into the SQLite database used by WordNetSqlite.
    /// </summary>
    public sealed class WordNetDbBuilder
    {
        private const int BatchSize = 20_000;

        private readonly string _dictDir;
        private readonly string _dbPath;
        private readonly Action<string> _log;

        /// <summary>(pos, offset) of the original data file to the generated synset id.</summary>
        private readonly Dictionary<(int Pos, long Offset), int> _synsetIds = new();

        /// <summary>(lemma, pos) to (synset offset, sense number); mirrors what getsearchsense() computes.</summary>
        private Dictionary<(string Lemma, int Pos), Dictionary<long, int>> _senseNumberLookup = new();

        public WordNetDbBuilder(string dictDir, string dbPath, Action<string> log)
        {
            _dictDir = dictDir;
            _dbPath = dbPath;
            _log = log;
        }

        public void Build()
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }

            using var context = new WordNetContext(_dbPath);
            context.Database.EnsureCreated();
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                context.Database.OpenConnection();
                // Bulk load: durability during generation is not interesting and the defaults make inserts slow.
                command.CommandText = "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF;";
                command.ExecuteNonQuery();
            }

            _senseNumberLookup = BuildSenseNumberLookup();

            InsertLexicographerFiles(context);
            foreach (var pos in new[] { 1, 2, 3, 4 })
            {
                InsertSynsets(context, pos);
            }
            foreach (var pos in new[] { 1, 2, 3, 4 })
            {
                InsertPointers(context, pos);
            }
            foreach (var pos in new[] { 1, 2, 3, 4 })
            {
                InsertIndexEntries(context, pos);
            }

            InsertSenseIndex(context);
            InsertMorphExceptions(context);
            InsertVerbSentences(context);

            _log($"Synsets: {_synsetIds.Count}");

            // The caller renames the generated file into place. Microsoft.Data.Sqlite pools its
            // connections, so the last one has to be closed for the file handle to be released.
            SqliteConnection.ClearAllPools();
        }

        private void InsertLexicographerFiles(WordNetContext context)
        {
            var lexNames = WordNetDictReader.ReadLexNames(_dictDir);
            foreach (var (id, name, pos) in lexNames.OrderBy(l => l.Id))
            {
                context.LexicographerFiles.Add(new LexicographerFile
                {
                    Id = id,
                    Name = name,
                    Pos = pos,
                });
            }
            context.SaveChanges();
            context.ChangeTracker.Clear();
            _log($"Lexicographer files: {lexNames.Count}");
        }

        private void InsertSynsets(WordNetContext context, int pos)
        {
            var path = WordNetDictReader.DataFileName(_dictDir, pos);
            var batch = new List<DbSynset>(BatchSize);
            var count = 0;

            void Flush()
            {
                if (batch.Count == 0)
                {
                    return;
                }

                context.Synsets.AddRange(batch);
                context.SaveChanges();
                foreach (var synset in batch)
                {
                    _synsetIds[(synset.Pos, synset.Offset)] = synset.Id;
                }
                context.ChangeTracker.Clear();
                batch.Clear();
            }

            foreach (var raw in WordNetDictReader.ReadDataFile(pos, path))
            {
                batch.Add(ToSynset(raw, GetSenseNumber));
                count++;
                if (batch.Count >= BatchSize)
                {
                    Flush();
                    _log($"  {WordNetConstants.PartNames[pos]}: {count} synsets");
                }
            }

            Flush();
            _log($"{WordNetConstants.PartNames[pos]}: {count} synsets");
        }

        private static DbSynset ToSynset(RawSynset raw, Func<string, int, long, int> senseNumber)
        {
            var synset = new DbSynset
            {
                Pos = raw.Pos,
                Offset = raw.Offset,
                LexFileNum = raw.LexFileNum,
                SsType = ComputeSsType(raw),
                Gloss = raw.Gloss,
                Words = raw.Words
                    .Select(w => new SynsetWord
                    {
                        WordNumber = w.WordNumber,
                        Text = w.Text,
                        LexId = w.LexId,
                        SenseNumber = senseNumber(w.Text, raw.Pos, raw.Offset),
                    })
                    .ToList(),
                Frames = raw.Frames
                    .Select((f, index) => new VerbFrame { Index = index, FrameNumber = f.FrameNumber, WordNumber = f.WordNumber })
                    .ToList(),
            };
            return synset;
        }

        /// <summary>Mirrors the ss_type computation of parse_synset() in search.c.</summary>
        private static int ComputeSsType(RawSynset raw)
        {
            if (WordNetDictReader.GetSsType(raw.SsTypeChar[0]) == (int)PartOfSpeech.Satellite)
            {
                return SynsetType.IndirectAnt;
            }

            if (raw.Pos == (int)PartOfSpeech.Adj)
            {
                var foundPertainym = false;
                foreach (var pointer in raw.Pointers)
                {
                    if (pointer.PointerType == PointerType.Antonym)
                    {
                        return SynsetType.DirectAnt;
                    }
                    if (pointer.PointerType == PointerType.Pertainym)
                    {
                        foundPertainym = true;
                    }
                }

                if (foundPertainym)
                {
                    return SynsetType.Pertainym;
                }
            }

            return SynsetType.DontKnow;
        }

        private void InsertPointers(WordNetContext context, int pos)
        {
            var path = WordNetDictReader.DataFileName(_dictDir, pos);
            var batch = new List<Pointer>(BatchSize);
            var count = 0;

            void Flush()
            {
                if (batch.Count == 0)
                {
                    return;
                }

                context.Pointers.AddRange(batch);
                context.SaveChanges();
                context.ChangeTracker.Clear();
                batch.Clear();
            }

            foreach (var raw in WordNetDictReader.ReadDataFile(pos, path))
            {
                var synsetId = _synsetIds[(raw.Pos, raw.Offset)];
                for (var i = 0; i < raw.Pointers.Count; i++)
                {
                    var rawPointer = raw.Pointers[i];
                    if (!_synsetIds.TryGetValue((rawPointer.TargetPos, rawPointer.TargetOffset), out var targetId))
                    {
                        throw new InvalidDataException(
                            $"Pointer target {rawPointer.TargetPos}/{rawPointer.TargetOffset} of synset {raw.Offset} not found");
                    }

                    batch.Add(new Pointer
                    {
                        SynsetId = synsetId,
                        Index = i,
                        PointerType = rawPointer.PointerType,
                        TargetSynsetId = targetId,
                        SourceWordNumber = rawPointer.SourceWordNumber,
                        TargetWordNumber = rawPointer.TargetWordNumber,
                    });
                    count++;
                }

                if (batch.Count >= BatchSize)
                {
                    Flush();
                }
            }

            Flush();
            _log($"{WordNetConstants.PartNames[pos]}: {count} pointers");
        }

        /// <summary>
        /// Builds the (lemma, pos) to (synset offset, sense number) mapping from the index files.
        /// </summary>
        private Dictionary<(string Lemma, int Pos), Dictionary<long, int>> BuildSenseNumberLookup()
        {
            var lookup = new Dictionary<(string, int), Dictionary<long, int>>();
            foreach (var pos in new[] { 1, 2, 3, 4 })
            {
                foreach (var entry in WordNetDictReader.ReadIndexFile(pos, WordNetDictReader.IndexFileName(_dictDir, pos)))
                {
                    var offsets = new Dictionary<long, int>();
                    for (var i = 0; i < entry.Offsets.Count; i++)
                    {
                        // The first occurrence wins, exactly like the loop in getsearchsense().
                        offsets.TryAdd(entry.Offsets[i], i + 1);
                    }
                    lookup[(entry.Lemma, entry.Pos)] = offsets;
                }
            }
            return lookup;
        }

        private void InsertIndexEntries(WordNetContext context, int pos)
        {
            var path = WordNetDictReader.IndexFileName(_dictDir, pos);
            var batch = new List<IndexEntry>(BatchSize);

            void Flush()
            {
                if (batch.Count == 0)
                {
                    return;
                }

                context.IndexEntries.AddRange(batch);
                context.SaveChanges();
                context.ChangeTracker.Clear();
                batch.Clear();
            }

            foreach (var raw in WordNetDictReader.ReadIndexFile(pos, path))
            {
                var entry = new IndexEntry
                {
                    Lemma = raw.Lemma,
                    Pos = raw.Pos,
                    SenseCount = raw.Offsets.Count,
                    TaggedSenseCount = raw.TaggedSenseCount,
                    PointerTypes = raw.PointerTypes
                        .Select((t, index) => new IndexPointerType { Index = index, PointerType = t })
                        .ToList(),
                };

                for (var i = 0; i < raw.Offsets.Count; i++)
                {
                    var target = _synsetIds[(raw.Pos, raw.Offsets[i])];
                    entry.Senses.Add(new IndexSense
                    {
                        SenseNumber = i + 1,
                        SynsetId = target,
                        Offset = raw.Offsets[i],
                    });
                }

                batch.Add(entry);
                if (batch.Count >= BatchSize)
                {
                    Flush();
                }
            }

            Flush();
        }

        /// <summary>
        /// Returns the WordNet sense number of a synset word, mirroring getsearchsense(): the word is
        /// lower cased, truncated at the adjective marker and looked up in the index file of its part of speech.
        /// </summary>
        private int GetSenseNumber(string word, int pos, long offset)
        {
            var lemma = ToLowerCaseTruncateAtMarker(word);
            if (_senseNumberLookup.TryGetValue((lemma, pos), out var offsets) &&
                offsets.TryGetValue(offset, out var senseNumber))
            {
                return senseNumber;
            }
            return 0;
        }

        /// <summary>Mirrors strtolower() of morph.c: lower case up to the first opening parenthesis.</summary>
        private static string ToLowerCaseTruncateAtMarker(string word)
        {
            var marker = word.IndexOf('(');
            var text = marker >= 0 ? word[..marker] : word;
            return text.ToLowerInvariant();
        }

        private void InsertSenseIndex(WordNetContext context)
        {
            var sensePath = Path.Combine(_dictDir, "index.sense");
            var batch = new List<SenseIndexEntry>(BatchSize);
            var count = 0;

            foreach (var raw in WordNetDictReader.ReadSenseIndex(sensePath))
            {
                var pos = GetSenseKeyPos(raw.SenseKey);
                if (!_synsetIds.TryGetValue((pos, raw.Offset), out var synsetId))
                {
                    // A handful of sense index entries can point at synsets of a different part of speech.
                    continue;
                }

                batch.Add(new SenseIndexEntry
                {
                    SenseKey = raw.SenseKey,
                    SynsetId = synsetId,
                    WnSense = raw.WnSense,
                    TagCount = raw.TagCount,
                });
                count++;

                if (batch.Count >= BatchSize)
                {
                    context.SenseIndexEntries.AddRange(batch);
                    context.SaveChanges();
                    context.ChangeTracker.Clear();
                    batch.Clear();
                }
            }

            context.SenseIndexEntries.AddRange(batch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            _log($"Sense index entries: {count}");

            var cntListPath = Path.Combine(_dictDir, "cntlist.rev");
            var tags = WordNetDictReader.ReadCntListRev(cntListPath)
                .Select(t => new SenseTagCount { SenseKey = t.SenseKey, TagCount = t.TagCount })
                .ToList();
            context.SenseTagCounts.AddRange(tags);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            _log($"Sense tag counts: {tags.Count}");
        }

        /// <summary>Sense keys encode the part of speech as the first digit of the lex_sense field.</summary>
        private static int GetSenseKeyPos(string senseKey)
        {
            var percent = senseKey.IndexOf('%');
            var ssType = senseKey[percent + 1] - '0';
            return ssType == (int)PartOfSpeech.Satellite ? (int)PartOfSpeech.Adj : ssType;
        }

        private void InsertMorphExceptions(WordNetContext context)
        {
            var batch = new List<MorphException>(BatchSize);
            var count = 0;

            foreach (var pos in new[] { 1, 2, 3, 4 })
            {
                var path = WordNetDictReader.ExceptionFileName(_dictDir, pos);
                foreach (var (form, baseForms) in WordNetDictReader.ReadExceptionFile(path))
                {
                    for (var i = 0; i < baseForms.Count; i++)
                    {
                        batch.Add(new MorphException { Pos = pos, Form = form, Index = i, BaseForm = baseForms[i] });
                        count++;
                    }
                }
            }

            context.MorphExceptions.AddRange(batch);
            context.SaveChanges();
            context.ChangeTracker.Clear();
            _log($"Morphology exceptions: {count}");
        }

        private void InsertVerbSentences(WordNetContext context)
        {
            var frameTexts = WordNetDictReader.ReadFrameTexts(Path.Combine(_dictDir, "frames.vrb"))
                .Select(f => new VerbFrameText { FrameNumber = f.FrameNumber, Text = f.Text })
                .ToList();
            context.VerbFrameTexts.AddRange(frameTexts);
            context.SaveChanges();

            var templates = WordNetDictReader.ReadVerbSentenceTemplates(Path.Combine(_dictDir, "sents.vrb"))
                .Select(t => new VerbSentenceTemplate { TemplateId = t.TemplateId, Text = t.Text })
                .ToList();
            context.VerbSentenceTemplates.AddRange(templates);
            context.SaveChanges();

            var index = WordNetDictReader.ReadVerbSentenceIndex(Path.Combine(_dictDir, "sentidx.vrb"))
                .Select(t => new VerbSentenceIndex { SenseKey = t.SenseKey, TemplateIds = t.TemplateIds })
                .ToList();
            context.VerbSentenceIndexes.AddRange(index);
            context.SaveChanges();
            context.ChangeTracker.Clear();

            _log($"Verb frames: {frameTexts.Count}, sentence templates: {templates.Count}, sentence index: {index.Count}");
        }
    }
}
