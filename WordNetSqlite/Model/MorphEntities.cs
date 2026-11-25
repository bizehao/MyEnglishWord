namespace WordNetSqlite.Model
{
    /// <summary>
    /// One base form of an inflected word from a pos.exc exception list.
    /// A single inflected form can have several base forms; <see cref="Index"/> preserves file order.
    /// </summary>
    public class MorphException
    {
        public int Id { get; set; }

        /// <summary>Part of speech code (1..4).</summary>
        public int Pos { get; set; }

        /// <summary>The inflected form (first field of the exception list line).</summary>
        public string Form { get; set; } = string.Empty;

        /// <summary>Position of this base form within the line (0-based).</summary>
        public int Index { get; set; }

        /// <summary>The base form (lemma).</summary>
        public string BaseForm { get; set; } = string.Empty;
    }

    /// <summary>Text of a generic verb sentence frame; from frames.vrb / frametext[].</summary>
    public class VerbFrameText
    {
        public int FrameNumber { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Example sentence templates of the verb senses (sents.vrb).</summary>
    public class VerbSentenceTemplate
    {
        public int TemplateId { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Maps a sense key to the example sentence templates that illustrate it (sentidx.vrb).</summary>
    public class VerbSentenceIndex
    {
        public string SenseKey { get; set; } = string.Empty;

        /// <summary>Comma separated template numbers, as written in sentidx.vrb.</summary>
        public string TemplateIds { get; set; } = string.Empty;
    }
}
