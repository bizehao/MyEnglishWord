using System.ComponentModel.DataAnnotations;

namespace WordNetSqlite.Model
{
    /// <summary>
    /// One entry of an index.[pos] file: a lemma together with its senses in WordNet sense number order.
    /// </summary>
    public class IndexEntry
    {
        public int Id { get; set; }

        /// <summary>Lower case lemma (wd).</summary>
        public string Lemma { get; set; } = string.Empty;

        /// <summary>Part of speech code (1..4).</summary>
        public int Pos { get; set; }

        /// <summary>Number of senses (synset_cnt / sense_cnt).</summary>
        public int SenseCount { get; set; }

        /// <summary>Number of senses that are tagged in the semantic concordances (tagsense_cnt).</summary>
        public int TaggedSenseCount { get; set; }

        /// <summary>Distinct pointer types used by all senses of the lemma (ptruse list, in file order).</summary>
        public List<IndexPointerType> PointerTypes { get; set; } = new();

        /// <summary>Senses of the lemma in WordNet sense number order.</summary>
        public List<IndexSense> Senses { get; set; } = new();
    }

    /// <summary>One entry of the ptr_symbol list of an index entry.</summary>
    public class IndexPointerType
    {
        public int Id { get; set; }

        public int IndexEntryId { get; set; }
        public IndexEntry? IndexEntry { get; set; }

        /// <summary>Position within the ptr_symbol list.</summary>
        public int Index { get; set; }

        /// <summary>Pointer type code; see <see cref="PointerType"/>.</summary>
        public int PointerType { get; set; }
    }

    /// <summary>Maps a sense number of a lemma to the synset it belongs to.</summary>
    public class IndexSense
    {
        public int Id { get; set; }

        public int IndexEntryId { get; set; }
        public IndexEntry? IndexEntry { get; set; }

        /// <summary>1-based WordNet sense number.</summary>
        public int SenseNumber { get; set; }

        public int SynsetId { get; set; }
        public SynsetRecord? Synset { get; set; }

        /// <summary>Data file offset of the synset; the C library works with offsets rather than ids.</summary>
        public long Offset { get; set; }
    }

    /// <summary>One line of index.sense: the sense key index.</summary>
    public class SenseIndexEntry
    {
        /// <summary>The sense key, e.g. "dog%1:05:00::".</summary>
        [Key]
        public string SenseKey { get; set; } = string.Empty;

        public int SynsetId { get; set; }
        public SynsetRecord? Synset { get; set; }

        /// <summary>WordNet sense number (wnsense).</summary>
        public int WnSense { get; set; }

        /// <summary>Number of times the sense is tagged in the semantic concordance texts.</summary>
        public int TagCount { get; set; }
    }

    /// <summary>One entry of a cntlist.rev line: tags per sense key.</summary>
    public class SenseTagCount
    {
        [Key]
        public string SenseKey { get; set; } = string.Empty;

        public int TagCount { get; set; }
    }
}
