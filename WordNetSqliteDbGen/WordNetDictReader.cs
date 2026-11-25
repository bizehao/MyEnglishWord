using WordNetSqlite;

namespace WordNetSqliteDbGen
{
    public record RawWord(string Text, int LexId, int WordNumber);

    public record RawPointer(int PointerType, int TargetPos, long TargetOffset, int SourceWordNumber, int TargetWordNumber);

    public record RawFrame(int FrameNumber, int WordNumber);

    public record RawSynset(
        int Pos,
        long Offset,
        int LexFileNum,
        string SsTypeChar,
        string Gloss,
        List<RawWord> Words,
        List<RawPointer> Pointers,
        List<RawFrame> Frames);

    public record RawIndexEntry(
        string Lemma,
        int Pos,
        List<int> PointerTypes,
        int TaggedSenseCount,
        List<long> Offsets);

    public record RawSenseIndexEntry(string SenseKey, long Offset, int WnSense, int TagCount);

    /// <summary>
    /// Reads the WordNet 3.0 dictionary files (data.* / index.* / *.exc / index.sense / ...) into plain records.
    /// The parsing mirrors parse_synset() / parse_index() / morph.c of the WordNet 3.0 C library.
    /// </summary>
    public static class WordNetDictReader
    {
        /// <summary>License header lines start with two spaces, exactly as the C library's binary search relies on.</summary>
        private static bool IsDataLine(string line) => line.Length > 0 && !line.StartsWith("  ", StringComparison.Ordinal);

        public static string DataFileName(string dictDir, int pos) =>
            Path.Combine(dictDir, $"data.{WordNetConstants.PartNames[pos]}");

        public static string IndexFileName(string dictDir, int pos) =>
            Path.Combine(dictDir, $"index.{WordNetConstants.PartNames[pos]}");

        public static string ExceptionFileName(string dictDir, int pos) =>
            Path.Combine(dictDir, $"{WordNetConstants.PartNames[pos]}.exc");

        /// <summary>
        /// Reads lexnames. Lines look like "00\tadj.all\t3"; the trailing number is the part of speech.
        /// The WordNet 3.1 archive has no lexnames file, and the C library does not read one either:
        /// it carries the same table compiled in, which <see cref="WordNetConstants.LexicographerFileNames"/> mirrors.
        /// </summary>
        public static List<(int Id, string Name, int Pos)> ReadLexNames(string dictDir)
        {
            var path = Path.Combine(dictDir, "lexnames");
            if (!File.Exists(path))
            {
                return WordNetConstants.LexicographerFileNames
                    .Select((name, id) => (Id: id, Name: name, Pos: PosFromLexFileNumber(id)))
                    .ToList();
            }

            var result = new List<(int, string, int)>();
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (fields.Length >= 3)
                {
                    result.Add((int.Parse(fields[0]), fields[1], int.Parse(fields[2])));
                }
            }
            return result;
        }

        /// <summary>Reads data.[pos]. Each returned record is one synset of the data file.</summary>
        public static IEnumerable<RawSynset> ReadDataFile(int pos, string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!IsDataLine(line))
                {
                    continue;
                }

                yield return ParseSynsetLine(pos, line);
            }
        }

        private static RawSynset ParseSynsetLine(int pos, string line)
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var i = 0;

            var offset = long.Parse(tokens[i++]);
            var lexFileNum = int.Parse(tokens[i++]);
            var ssTypeChar = tokens[i++];
            var wcount = Convert.ToInt32(tokens[i++], 16);

            var words = new List<RawWord>(wcount);
            for (var w = 0; w < wcount; w++)
            {
                var text = tokens[i++];
                var lexId = Convert.ToInt32(tokens[i++], 16);
                words.Add(new RawWord(text, lexId, w + 1));
            }

            var ptrcount = int.Parse(tokens[i++]);
            var pointers = new List<RawPointer>(ptrcount);
            for (var p = 0; p < ptrcount; p++)
            {
                var symbol = tokens[i++];
                var targetOffset = long.Parse(tokens[i++]);
                var targetPosChar = tokens[i++];
                var sourceTarget = tokens[i++];

                // Four hex digits: the first two are the source word number, the last two the target word number.
                var sourceWord = Convert.ToInt32(sourceTarget[..2], 16);
                var targetWord = Convert.ToInt32(sourceTarget[2..4], 16);

                pointers.Add(new RawPointer(
                    GetPointerType(symbol),
                    GetPos(targetPosChar[0]),
                    targetOffset,
                    sourceWord,
                    targetWord));
            }

            var frames = new List<RawFrame>();
            if (pos == (int)PartOfSpeech.Verb)
            {
                var frameCount = int.Parse(tokens[i++]);
                for (var f = 0; f < frameCount; f++)
                {
                    i++; // the '+' marker
                    var frameNumber = int.Parse(tokens[i++]);
                    var wordNumber = Convert.ToInt32(tokens[i++], 16);
                    frames.Add(new RawFrame(frameNumber, wordNumber));
                }
            }

            // Everything after the '|' is the gloss; the C parser tokenises it and rejoins with single spaces.
            var gloss = string.Empty;
            if (i < tokens.Length && tokens[i] == "|")
            {
                gloss = string.Join(" ", tokens, i + 1, tokens.Length - i - 1);
            }

            return new RawSynset(pos, offset, lexFileNum, ssTypeChar, gloss, words, pointers, frames);
        }

        /// <summary>Reads index.[pos]. Each returned record is one lemma.</summary>
        public static IEnumerable<RawIndexEntry> ReadIndexFile(int pos, string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!IsDataLine(line))
                {
                    continue;
                }

                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var i = 0;

                var lemma = tokens[i++];
                i++; // pos character, implied by the file
                i++; // synset_cnt, redundant with the offset list
                var ptrCount = int.Parse(tokens[i++]);

                var pointerTypes = new List<int>(ptrCount);
                for (var p = 0; p < ptrCount; p++)
                {
                    var symbol = tokens[i++];

                    // index files only use the one and two character pointer symbols
                    pointerTypes.Add(GetPointerType(symbol));
                }

                i++; // sense_cnt, redundant
                var taggedSenseCount = int.Parse(tokens[i++]);

                var offsets = new List<long>(tokens.Length - i);
                while (i < tokens.Length)
                {
                    offsets.Add(long.Parse(tokens[i++]));
                }

                yield return new RawIndexEntry(lemma, pos, pointerTypes, taggedSenseCount, offsets);
            }
        }

        public static IEnumerable<RawSenseIndexEntry> ReadSenseIndex(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!IsDataLine(line))
                {
                    continue;
                }

                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                yield return new RawSenseIndexEntry(
                    tokens[0],
                    long.Parse(tokens[1]),
                    int.Parse(tokens[2]),
                    int.Parse(tokens[3]));
            }
        }

        public static IEnumerable<(string SenseKey, int TagCount)> ReadCntListRev(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path))
            {
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 3)
                {
                    yield return (tokens[0], int.Parse(tokens[2]));
                }
            }
        }

        public static IEnumerable<(string Form, List<string> BaseForms)> ReadExceptionFile(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 2)
                {
                    yield return (tokens[0], tokens.Skip(1).ToList());
                }
            }
        }

        /// <summary>
        /// Reads frames.vrb: "1  Something ----s". The WordNet 3.1 archive has no frames.vrb, and the
        /// C library reads the text from the frametext[] table it carries compiled in,
        /// which FrameTexts mirrors.
        /// </summary>
        public static IEnumerable<(int FrameNumber, string Text)> ReadFrameTexts(string path)
        {
            if (!File.Exists(path))
            {
                return WordNetConstants.FrameTexts
                    .Select((text, frameNumber) => (FrameNumber: frameNumber, Text: text))
                    .Skip(1);
            }

            return ReadFrameTextsFromFile(path);
        }

        private static IEnumerable<(int FrameNumber, string Text)> ReadFrameTextsFromFile(string path)
        {

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var space = line.IndexOf(' ');
                if (space > 0)
                {
                    yield return (int.Parse(line[..space]), line[(space + 1)..].Trim());
                }
            }
        }

        /// <summary>Reads sents.vrb: "1 The children %s to the playground".</summary>
        public static IEnumerable<(int TemplateId, string Text)> ReadVerbSentenceTemplates(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var space = line.IndexOf(' ');
                if (space > 0)
                {
                    yield return (int.Parse(line[..space]), line[(space + 1)..]);
                }
            }
        }

        /// <summary>Reads sentidx.vrb: "abash%2:37:00:: 126,127".</summary>
        public static IEnumerable<(string SenseKey, string TemplateIds)> ReadVerbSentenceIndex(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var space = line.IndexOf(' ');
                if (space > 0)
                {
                    yield return (line[..space], line[(space + 1)..].Trim());
                }
            }
        }

        /// <summary>
        /// The part of speech a lexicographer file number belongs to, derived from the order of
        /// lexfiles[]: the adjective files come first and last, the nouns and verbs in between.
        /// </summary>
        private static int PosFromLexFileNumber(int id) => id switch
        {
            <= 2 => (int)PartOfSpeech.Adj,
            <= 28 => (int)PartOfSpeech.Noun,
            <= 43 => (int)PartOfSpeech.Verb,
            _ => (int)PartOfSpeech.Adj,
        };

        /// <summary>Maps a pointer symbol such as "@", "~i" or ";c" to its pointer type code, as getptrtype() does.</summary>
        public static int GetPointerType(string symbol)
        {
            for (var i = 1; i < WordNetConstants.PointerSymbols.Length; i++)
            {
                if (WordNetConstants.PointerSymbols[i] == symbol)
                {
                    return i;
                }
            }
            return 0;
        }

        /// <summary>Maps a data file part of speech character to the part of speech code, as getpos()/getsstype() do.</summary>
        public static int GetPos(char c) => c switch
        {
            'n' => (int)PartOfSpeech.Noun,
            'v' => (int)PartOfSpeech.Verb,
            'a' or 's' => (int)PartOfSpeech.Adj,
            'r' => (int)PartOfSpeech.Adv,
            _ => throw new InvalidDataException($"Unknown part of speech '{c}'"),
        };

        /// <summary>Maps a synset type character to the synset type code, as getsstype() does.</summary>
        public static int GetSsType(char c) => c switch
        {
            'n' => (int)PartOfSpeech.Noun,
            'v' => (int)PartOfSpeech.Verb,
            'a' => (int)PartOfSpeech.Adj,
            's' => (int)PartOfSpeech.Satellite,
            'r' => (int)PartOfSpeech.Adv,
            _ => throw new InvalidDataException($"Unknown synset type '{c}'"),
        };
    }
}
