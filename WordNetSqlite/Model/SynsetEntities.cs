using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WordNetSqlite.Model
{
    /// <summary>
    /// A lexicographer file (lexnames). <see cref="Id"/> is the lex_filenum stored in the data files (0..44).
    /// </summary>
    public class LexicographerFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Part of speech code (1..4) this file belongs to.</summary>
        public int Pos { get; set; }
    }

    /// <summary>
    /// One synset, i.e. one line of a data.[pos] file.
    /// </summary>
    public class SynsetRecord
    {
        public int Id { get; set; }

        /// <summary>Part of speech code (1..4).</summary>
        public int Pos { get; set; }

        /// <summary>Byte offset the synset had in the original data file. Unique together with <see cref="Pos"/>.</summary>
        public long Offset { get; set; }

        /// <summary>Lexicographer file number (fnum).</summary>
        public int LexFileNum { get; set; }

        /// <summary>Synset type; see <see cref="SynsetType"/>. Only meaningful for adjectives.</summary>
        public int SsType { get; set; }

        /// <summary>Gloss without the surrounding parentheses. Whitespace has been normalised to single spaces.</summary>
        public string Gloss { get; set; } = string.Empty;

        /// <summary>Unique key from index.key.rev, 0 when the file is not part of the dataset.</summary>
        public uint Key { get; set; }

        public List<SynsetWord> Words { get; set; } = new();

        public List<Pointer> Pointers { get; set; } = new();

        public List<VerbFrame> Frames { get; set; } = new();
    }

    /// <summary>One word of a synset (the "words" array of the C Synset struct).</summary>
    public class SynsetWord
    {
        public int Id { get; set; }

        public int SynsetId { get; set; }
        public SynsetRecord? Synset { get; set; }

        /// <summary>1-based word number, matching the C word numbering used by pointers and frames.</summary>
        public int WordNumber { get; set; }

        /// <summary>Word text as written in the data file; underscores instead of spaces, adjective markers kept.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Lexicographer id (lex_id), a 0..15 value identifying the sense within a lexicographer file.</summary>
        public int LexId { get; set; }

        /// <summary>
        /// WordNet sense number of this word within its synset (wnsns), 0 when the word is not in the index file.
        /// Precomputed at generation time; the C library computes it in getsearchsense().
        /// </summary>
        public int SenseNumber { get; set; }
    }

    /// <summary>One pointer of a synset (the "ptr" fields of the C Synset struct).</summary>
    public class Pointer
    {
        public int Id { get; set; }

        public int SynsetId { get; set; }
        public SynsetRecord? Synset { get; set; }

        /// <summary>Position of the pointer within its synset; the C library preserves file order.</summary>
        public int Index { get; set; }

        /// <summary>Pointer type code; see <see cref="PointerType"/>.</summary>
        public int PointerType { get; set; }

        public int TargetSynsetId { get; set; }
        public SynsetRecord? TargetSynset { get; set; }

        /// <summary>Word number in the source synset (pfrm); 0 means the pointer is semantic.</summary>
        public int SourceWordNumber { get; set; }

        /// <summary>Word number in the target synset (pto); 0 means all words.</summary>
        public int TargetWordNumber { get; set; }
    }

    /// <summary>One generic sentence frame of a verb synset.</summary>
    public class VerbFrame
    {
        public int Id { get; set; }

        public int SynsetId { get; set; }
        public SynsetRecord? Synset { get; set; }

        /// <summary>Position of the frame within its synset; the C library preserves file order.</summary>
        public int Index { get; set; }

        /// <summary>Frame number (frmid), 1..35.</summary>
        public int FrameNumber { get; set; }

        /// <summary>Word number the frame applies to (frmto); 0 means all words of the synset.</summary>
        public int WordNumber { get; set; }
    }
}
