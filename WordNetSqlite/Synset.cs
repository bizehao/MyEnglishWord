namespace WordNetSqlite
{
    /// <summary>
    /// The result of a search on a synset. Mirrors the Synset structure of the WordNet 3.0 C library;
    /// the parallel arrays keep the same indexing as the C fields (word numbers are 1-based).
    /// </summary>
    public sealed class Synset
    {
        /// <summary>Primary key of the synset in the database.</summary>
        public int Id { get; init; }

        /// <summary>Part of speech code (1..4).</summary>
        public int Pos { get; init; }

        /// <summary>Synset type; see <see cref="SynsetType"/>.</summary>
        public int SsType { get; init; }

        /// <summary>Lexicographer file number (fnum).</summary>
        public int Fnum { get; init; }

        /// <summary>Byte offset the synset had in the original data file (hereiam).</summary>
        public long HereIAm { get; init; }

        /// <summary>Unique key of the synset, 0 when the dataset has no key index.</summary>
        public uint Key { get; init; }

        /// <summary>Words of the synset, in data file order.</summary>
        public string[] Words { get; init; } = Array.Empty<string>();

        /// <summary>Lexicographer ids, parallel to <see cref="Words"/>.</summary>
        public int[] LexId { get; init; } = Array.Empty<int>();

        /// <summary>WordNet sense numbers, parallel to <see cref="Words"/>. 0 when the word is not indexed.</summary>
        public int[] WnSns { get; init; } = Array.Empty<int>();

        /// <summary>The word of the search that led to this synset, 1-based; 0 when not known.</summary>
        public int WhichWord { get; set; }

        /// <summary>Pointer types of the synset, in data file order.</summary>
        public int[] PtrTyp { get; init; } = Array.Empty<int>();

        /// <summary>Byte offsets the pointers point at, parallel to <see cref="PtrTyp"/>.</summary>
        public long[] PtrOff { get; init; } = Array.Empty<long>();

        /// <summary>Part of speech of the pointer targets, parallel to <see cref="PtrTyp"/>.</summary>
        public int[] PPos { get; init; } = Array.Empty<int>();

        /// <summary>Target word numbers of the pointers (pto), parallel to <see cref="PtrTyp"/>.</summary>
        public int[] PTo { get; init; } = Array.Empty<int>();

        /// <summary>Source word numbers of the pointers (pfrm), parallel to <see cref="PtrTyp"/>.</summary>
        public int[] PFrm { get; init; } = Array.Empty<int>();

        /// <summary>Generic verb frame numbers of the synset.</summary>
        public int[] FrmId { get; init; } = Array.Empty<int>();

        /// <summary>Word number each frame applies to, parallel to <see cref="FrmId"/>; 0 means all words.</summary>
        public int[] FrmTo { get; init; } = Array.Empty<int>();

        /// <summary>Gloss of the synset including the surrounding parentheses, as the C library stores it.</summary>
        public string? Defn { get; init; }

        /// <summary>Next synset of the result (another sense or another word form).</summary>
        public Synset? NextSS { get; set; }

        /// <summary>First synset of the next word form.</summary>
        public Synset? NextForm { get; set; }

        /// <summary>The search type that produced this synset.</summary>
        public int SearchType { get; set; } = -1;

        /// <summary>Synsets found by tracing the pointers of this synset.</summary>
        public Synset? PtrList { get; set; }

        /// <summary>Head word of the head synset, for adjective satellites.</summary>
        public string? HeadWord { get; set; }

        /// <summary>Sense number of the head word, for adjective satellites.</summary>
        public short HeadSense { get; set; }

        /// <summary>Number of words in the synset.</summary>
        public int WordCount => Words.Length;

        /// <summary>Number of pointers of the synset.</summary>
        public int PointerCount => PtrTyp.Length;

        /// <summary>Enumerates this synset and the ones linked through <see cref="NextSS"/>.</summary>
        public IEnumerable<Synset> EnumerateNextSS()
        {
            for (var synset = this; synset != null; synset = synset.NextSS)
            {
                yield return synset;
            }
        }

        /// <summary>Enumerates this synset and the ones linked through <see cref="PtrList"/>.</summary>
        public IEnumerable<Synset> EnumeratePtrList()
        {
            for (var synset = this; synset != null; synset = synset.PtrList)
            {
                yield return synset;
            }
        }
    }
}
