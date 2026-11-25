namespace WordNetSqlite
{
    /// <summary>A pointer of a synset, with its target resolved.</summary>
    internal readonly struct PointerTemplate
    {
        public PointerTemplate(int type, int targetSynsetId, int targetPos, long targetOffset, int sourceWord, int targetWord)
        {
            Type = type;
            TargetSynsetId = targetSynsetId;
            TargetPos = targetPos;
            TargetOffset = targetOffset;
            SourceWord = sourceWord;
            TargetWord = targetWord;
        }

        public int Type { get; }

        public int TargetSynsetId { get; }

        public int TargetPos { get; }

        public long TargetOffset { get; }

        public int SourceWord { get; }

        public int TargetWord { get; }
    }

    /// <summary>A generic verb frame of a synset.</summary>
    internal readonly struct FrameTemplate
    {
        public FrameTemplate(int frameNumber, int wordNumber)
        {
            FrameNumber = frameNumber;
            WordNumber = wordNumber;
        }

        public int FrameNumber { get; }

        public int WordNumber { get; }
    }

    /// <summary>
    /// The immutable part of a synset as stored in the database. Searches create a fresh
    /// <see cref="Synset"/> from a template, so that the mutable search fields never alias.
    /// </summary>
    internal sealed class SynsetTemplate
    {
        public int Id { get; init; }

        public int Pos { get; init; }

        public long Offset { get; init; }

        public int LexFileNum { get; init; }

        public int SsType { get; init; }

        public string Gloss { get; init; } = string.Empty;

        public uint Key { get; init; }

        public string[] Words { get; init; } = Array.Empty<string>();

        public int[] LexIds { get; init; } = Array.Empty<int>();

        public int[] SenseNumbers { get; init; } = Array.Empty<int>();

        public PointerTemplate[] Pointers { get; init; } = Array.Empty<PointerTemplate>();

        public FrameTemplate[] Frames { get; init; } = Array.Empty<FrameTemplate>();
    }

    /// <summary>
    /// A bounded first-in-first-out cache of synset templates. Reads during one search revisit
    /// synsets often (antonyms, similar-to pointers and adjective clusters), so a cache removes
    /// most of the repeated queries.
    /// </summary>
    internal sealed class SynsetCache
    {
        private readonly int _capacity;
        private readonly Dictionary<(int Pos, long Offset), SynsetTemplate> _entries;
        private readonly Queue<(int Pos, long Offset)> _order = new();

        public SynsetCache(int capacity)
        {
            _capacity = Math.Max(1, capacity);
            _entries = new Dictionary<(int, long), SynsetTemplate>(_capacity);
        }

        public static (int Pos, long Offset) MakeKey(int pos, long offset) => (pos, offset);

        public bool TryGet((int Pos, long Offset) key, out SynsetTemplate template) =>
            _entries.TryGetValue(key, out template!);

        public void Add((int Pos, long Offset) key, SynsetTemplate template)
        {
            if (_entries.ContainsKey(key))
            {
                return;
            }

            if (_entries.Count >= _capacity)
            {
                var oldest = _order.Dequeue();
                _entries.Remove(oldest);
            }

            _entries[key] = template;
            _order.Enqueue(key);
        }

        public void Clear()
        {
            _entries.Clear();
            _order.Clear();
        }
    }

    /// <summary>
    /// One entry of an index file, mirroring the Index structure of the WordNet C library.
    /// </summary>
    public sealed class IndexEntryResult
    {
        /// <summary>The lemma (wd).</summary>
        public string Word { get; init; } = string.Empty;

        /// <summary>The part of speech character ("n", "v", "a", "r").</summary>
        public string Pos { get; init; } = string.Empty;

        /// <summary>Number of senses (sense_cnt).</summary>
        public int SenseCount { get; init; }

        /// <summary>Number of senses tagged in the semantic concordances (tagsense_cnt).</summary>
        public int TaggedSenseCount { get; init; }

        /// <summary>Pointer types used by the senses of the lemma (ptruse).</summary>
        public int[] PointerTypes { get; init; } = Array.Empty<int>();

        /// <summary>Data file offsets of the senses, in WordNet sense number order.</summary>
        public long[] Offsets { get; init; } = Array.Empty<long>();

        /// <summary>Number of senses (off_cnt).</summary>
        public int OffsetCount => Offsets.Length;

        /// <summary>Part of speech code of the entry.</summary>
        public PartOfSpeech PartOfSpeech => WordNetDatabase.GetPosOfPartChar(Pos[0]);
    }
}
