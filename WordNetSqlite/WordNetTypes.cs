namespace WordNetSqlite
{
    /// <summary>
    /// Part of speech codes. Values match the ones used by the WordNet 3.0 C library
    /// (wn.h) and by the existing WordNetRT WinRT projection.
    /// </summary>
    public enum PartOfSpeech
    {
        AllPos = 0,
        Noun = 1, //名词 (n)
        Verb = 2, //动词 (v)
        Adj = 3, //形容词 (a)
        Adv = 4, //副词 (r)
        Satellite = 5,
    }

    /// <summary>
    /// Search types accepted by <see cref="WordNetDatabase.FindTheInfo(string, PartOfSpeech, SearchType, int)"/>.
    /// Values match the WordNet 3.0 C search codes (wn.h) and the WordNetRT projection.
    /// Negating a value requests a recursive (hierarchical) search, as in the C library.
    /// </summary>
    public enum SearchType
    {
        Antonyms = 1,
        Hypernyms = 2,
        Hyponyms = 3,
        Entailment = 4,
        Similar = 5,
        MemberMeronym = 6,
        SubstanceMeronym = 7,
        PartMeronym = 8,
        MemberHolonym = 9,
        SubstanceHolonym = 10,
        PartHolonym = 11,
        AllMeronyms = 12,
        AllHolonyms = 13,
        Cause = 14,
        ParticipleOfVerb = 15,
        AlsoSee = 16,
        PertainsToNounOrDerivedFromAdjective = 17,
        Attribute = 18,
        VerbGroup = 19,
        DerivationallyRelatedForm = 20,
        DomainOfSynset = 21,
        MemberOfThisDomain = 22,
        FindSynonyms = 23,
        Polysemy = 24,
        VerbExampleSentencesAndGenericFrames = 25,
        NounCoordinates = 26,
        GroupRelatedSenses = 27,
        HierarchicalMeronymSearch = 28,
        HierarchicalHolonymSearch = 29,
        FindKeywordsBySubstring = 30,
        ShowAllSynsetsForWord = 31,
        ShowDomainTopic = 32,
        ShowDomainUsage = 33,
        ShowDomainRegion = 34,
        ShowDomainTermsForTopic = 35,
        ShowDomainTermsForUsage = 36,
        ShowDomainTermsForRegion = 37,
        InstanceOf = 38,
        ShowInstances = 39,
    }

    /// <summary>Synset type (ss_type) codes. See search.c in WordNet 3.0.</summary>
    public static class SynsetType
    {
        public const int DontKnow = 0;
        public const int DirectAnt = 1;
        public const int IndirectAnt = 2;
        public const int Pertainym = 3;
    }

    /// <summary>Pointer type codes, identical to the WordNet 3.0 ANTPTR..CLASS constants.</summary>
    public static class PointerType
    {
        public const int Antonym = 1;
        public const int Hypernym = 2;
        public const int Hyponym = 3;
        public const int Entailment = 4;
        public const int Similar = 5;
        public const int IsMemberPart = 6;
        public const int IsStuffPart = 7;
        public const int IsPartPart = 8;
        public const int HasMemberPart = 9;
        public const int HasStuffPart = 10;
        public const int HasPartPart = 11;
        public const int Meronym = 12;
        public const int Holonym = 13;
        public const int CauseTo = 14;
        public const int Participle = 15;
        public const int SeeAlso = 16;
        public const int Pertainym = 17;
        public const int Attribute = 18;
        public const int VerbGroup = 19;
        public const int Derivation = 20;
        public const int Classification = 21;
        public const int Class = 22;
        public const int Instance = 38;
        public const int Instances = 39;
    }

    /// <summary>Constants from wn.h shared by the runtime engine and the database generator.</summary>
    public static class WordNetConstants
    {
        public const int NumParts = 4;
        public const int NumFrames = 35;
        public const int AllSenses = 0;
        public const int MaxSense = 75;
        public const int MaxForms = 5;
        public const int MaxDepth = 20;

        public const int ClassificationStart = 32;
        public const int ClassStart = 35;

        /// <summary>pos code (1..4) to the two/three letter code used in data and index files.</summary>
        public static readonly string[] PartNames = { "", "noun", "verb", "adj", "adv" };

        /// <summary>pos code (1..4) to the single character used by data files. Index 5 is the adjective satellite.</summary>
        public static readonly char[] PartChars = { ' ', 'n', 'v', 'a', 'r', 's' };

        /// <summary>Pointer characters, indexed by pointer type code (1..39). Matches ptrtyp[] in wnglobal.c.</summary>
        public static readonly string[] PointerSymbols =
        {
            "", "!", "@", "~", "*", "&", "#m", "#s", "#p", "%m", "%s", "%p",
            "%", "#", ">", "<", "^", "\\", "=", "$", "+", ";", "-",
            "", "", "+", "", "", "", "", "", "",
            ";c", ";u", ";r", "-c", "-u", "-r", "@i", "~i",
        };

        /// <summary>Adjective syntactic markers, indexed by marker code. Matches adjclass[] in wnglobal.c.</summary>
        public static readonly string[] AdjClass = { "", "(p)", "(a)", "(ip)" };

        /// <summary>
        /// Lexicographer file names, indexed by file number. Matches lexfiles[] in wnglobal.c; the
        /// database generator uses it when the distribution has no lexnames file, which is the case
        /// for the WordNet 3.1 archive.
        /// </summary>
        public static readonly string[] LexicographerFileNames =
        {
            "adj.all", "adj.pert", "adv.all", "noun.Tops", "noun.act", "noun.animal", "noun.artifact",
            "noun.attribute", "noun.body", "noun.cognition", "noun.communication", "noun.event", "noun.feeling",
            "noun.food", "noun.group", "noun.location", "noun.motive", "noun.object", "noun.person",
            "noun.phenomenon", "noun.plant", "noun.possession", "noun.process", "noun.quantity", "noun.relation",
            "noun.shape", "noun.state", "noun.substance", "noun.time", "verb.body", "verb.change", "verb.cognition",
            "verb.communication", "verb.competition", "verb.consumption", "verb.contact", "verb.creation",
            "verb.emotion", "verb.motion", "verb.perception", "verb.possession", "verb.social", "verb.stative",
            "verb.weather", "adj.ppl",
        };

        /// <summary>
        /// Text of the generic verb sentence frames, indexed by frame number. Matches frametext[] in
        /// wnglobal.c, which is what the C library uses; frames.vrb carries the same text and is
        /// missing from the WordNet 3.1 archive.
        /// </summary>
        public static readonly string[] FrameTexts =
        {
            "",
            "Something ----s",
            "Somebody ----s",
            "It is ----ing",
            "Something is ----ing PP",
            "Something ----s something Adjective/Noun",
            "Something ----s Adjective/Noun",
            "Somebody ----s Adjective",
            "Somebody ----s something",
            "Somebody ----s somebody",
            "Something ----s somebody",
            "Something ----s something",
            "Something ----s to somebody",
            "Somebody ----s on something",
            "Somebody ----s somebody something",
            "Somebody ----s something to somebody",
            "Somebody ----s something from somebody",
            "Somebody ----s somebody with something",
            "Somebody ----s somebody of something",
            "Somebody ----s something on somebody",
            "Somebody ----s somebody PP",
            "Somebody ----s something PP",
            "Somebody ----s PP",
            "Somebody's (body part) ----s",
            "Somebody ----s somebody to INFINITIVE",
            "Somebody ----s somebody INFINITIVE",
            "Somebody ----s that CLAUSE",
            "Somebody ----s to somebody",
            "Somebody ----s to INFINITIVE",
            "Somebody ----s whether INFINITIVE",
            "Somebody ----s somebody into V-ing something",
            "Somebody ----s something with something",
            "Somebody ----s INFINITIVE",
            "Somebody ----s VERB-ing",
            "It ----s that CLAUSE",
            "Something ----s INFINITIVE",
        };
    }
}
