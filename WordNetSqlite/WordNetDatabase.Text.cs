namespace WordNetSqlite
{
    public sealed partial class WordNetDatabase
    {
        /// <summary>If set, print the lexicographer file name after each sense (fnflag).</summary>
        public bool PrintFileName { get; set; }

        /// <summary>If set, print definitional glosses (dflag). On by default.</summary>
        public bool PrintGlosses { get; set; } = true;

        /// <summary>If set, print SEE ALSO pointers (saflag). On by default.</summary>
        public bool PrintSeeAlso { get; set; } = true;

        /// <summary>If set, print lexicographer file information on synsets (fileinfoflag).</summary>
        public bool PrintFileInfo { get; set; }

        /// <summary>If set, print verb frames after each synset (frflag).</summary>
        public bool PrintVerbFrames { get; set; }

        /// <summary>If set, print the data file offset of each synset (offsetflag).</summary>
        public bool PrintOffsets { get; set; }

        /// <summary>If set, print the WordNet sense number of each word (wnsnsflag).</summary>
        public bool PrintWordSenseNumbers { get; set; }

        /// <summary>
        /// Runs a search and returns the formatted result, mirroring findtheinfo(). Underscores in the
        /// result are replaced by spaces, as the C library does.
        /// </summary>
        public Task<string> FindTheInfoAsync(string searchStr, PartOfSpeech pos, SearchType searchType, int senseNum, CancellationToken cancellationToken = default) =>
            ExecuteAsync(() => new TextSearch(this, cancellationToken).FindTheInfoAsync(searchStr, pos, (int)searchType, senseNum), cancellationToken);

        /// <summary>
        /// The text producing half of the search code; mirrors the static state and the printers of search.c.
        /// </summary>
        private sealed class TextSearch
        {
            private const int AllWords = 0;
            private const int SkipAnts = 0;
            private const int PrintAnts = 1;
            private const int SkipMarker = 0;
            private const int PrintMarker = 1;
            private const int DefOn = 1;
            private const int DefOff = 0;
            private const int TraceP = 1;
            private const int TraceC = 2;
            private const int TraceI = 3;

            private static readonly string[] AdjectiveMarkers = { "", "(predicate)", "(prenominal)", "(postnominal)" };
            private static readonly string[] AntonymTemplate = { "", "a noun", "a verb", "an adjective", "an adverb" };
            private static readonly string[] FrequencyCategories =
            {
                "extremely rare", "very rare", "rare", "uncommon", "common",
                "familiar", "very familiar", "extremely familiar",
            };

            private readonly WordNetDatabase _db;
            private readonly CancellationToken _cancellationToken;
            private readonly SearchBuffer _buffer = new();
            private readonly int[] _senseCount = new int[32];
            private readonly int[] _outSenseCount = new int[32];
            private readonly List<RelativeGroup> _relList = new();

            private int _numForms;
            private int _printCount;
            private int _prflag;
            private int _sense;
            private int _prlexid;
            private int _lastHolomero;

            public TextSearch(WordNetDatabase db, CancellationToken cancellationToken)
            {
                _db = db;
                _cancellationToken = cancellationToken;
            }

            /// <summary>
            /// Mirrors interface_doevents(): the C library polls its user interface for an interrupt
            /// at the same points at which a cancelled search stops here.
            /// </summary>
            private void CheckCancellation() => _cancellationToken.ThrowIfCancellationRequested();

            public async Task<string> FindTheInfoAsync(string searchStr, PartOfSpeech pos, int pointerType, int whichSense)
            {
                _buffer.Clear();
                _numForms = 0;
                _printCount = 0;

                switch (pointerType)
                {
                    case (int)SearchType.ShowAllSynsetsForWord:
                        await Overview(searchStr, pos);
                        break;

                    case (int)SearchType.Polysemy:
                        foreach (var index in await _db.GetIndexCoreAsync(searchStr, pos, _cancellationToken))
                        {
                            _senseCount[_numForms] = index.OffsetCount;
                            Frequency(index);
                            _numForms++;
                        }
                        break;

                    case (int)SearchType.FindKeywordsBySubstring:
                        _buffer.Append(await _db.WngrepCoreAsync(searchStr, pos, _cancellationToken));
                        break;

                    case (int)SearchType.GroupRelatedSenses:
                    case (int)SearchType.VerbGroup:
                        foreach (var index in await _db.GetIndexCoreAsync(searchStr, pos, _cancellationToken))
                        {
                            _senseCount[_numForms] = index.OffsetCount;
                            await Relatives(index, pos);
                            _numForms++;
                        }
                        break;

                    default:
                        await SearchPointers(searchStr, pos, pointerType, whichSense);
                        break;
                }

                if (_buffer.Overflowed)
                {
                    _buffer.Replace("Search too large.  Narrow search and try again...\n");
                }

                return _buffer.ToString().Replace('_', ' ');
            }

            private async Task SearchPointers(string searchStr, PartOfSpeech pos, int pointerType, int whichSense)
            {
                var depth = 0;
                var offsets = new long[WordNetConstants.MaxSense];
                var offsetCount = 0;
                var bufferStart = _buffer.Length;

                if (pointerType < 0)
                {
                    pointerType = -pointerType;
                    depth = 1;
                }

                foreach (var index in await _db.GetIndexCoreAsync(searchStr, pos, _cancellationToken))
                {
                    CheckCancellation();
                    _senseCount[_numForms] = index.OffsetCount;
                    _outSenseCount[_numForms] = 0;

                    if (whichSense == WordNetConstants.AllSenses)
                    {
                        _buffer.Append("                                                                         \n");
                    }

                    var skipIt = 0;
                    for (_sense = 0; _sense < index.OffsetCount; _sense++)
                    {
                        CheckCancellation();
                        skipIt = 0;
                        if (whichSense == WordNetConstants.AllSenses || whichSense == _sense + 1)
                        {
                            _prflag = 0;

                            for (var i = 0; i < offsetCount && skipIt == 0; i++)
                            {
                                if (offsets[i] == index.Offsets[_sense])
                                {
                                    skipIt = 1;
                                }
                            }

                            if (skipIt != 1)
                            {
                                offsets[offsetCount++] = index.Offsets[_sense];
                                var synset = await _db.ReadSynsetCoreAsync(pos, index.Offsets[_sense], index.Word, _cancellationToken);
                                if (synset != null)
                                {
                                    await TraceOnSynset(synset, pos, pointerType, depth);
                                }
                            }
                        }

                        if (skipIt != 1 && (whichSense == _sense + 1 || _buffer.Overflowed))
                        {
                            break;
                        }
                    }

                    if (whichSense == WordNetConstants.AllSenses)
                    {
                        var printed = _outSenseCount[_numForms];
                        string? message = null;
                        if (printed == index.OffsetCount && printed == 1)
                        {
                            message = $"\n1 sense of {index.Word}";
                        }
                        else if (printed == index.OffsetCount)
                        {
                            message = $"\n{printed} senses of {index.Word}";
                        }
                        else if (printed > 0)
                        {
                            message = $"\n{printed} of {index.OffsetCount} senses of {index.Word}";
                        }

                        if (printed > 0 && message != null)
                        {
                            if (_numForms > 0)
                            {
                                _buffer.Overwrite(bufferStart, "\n");
                                bufferStart++;
                            }
                            _buffer.Overwrite(bufferStart, message);
                            bufferStart = _buffer.Length;
                        }
                    }

                    if (_buffer.Overflowed)
                    {
                        break;
                    }

                    _numForms++;
                }
            }

            /// <summary>Performs the requested trace on one synset, mirroring the switch of findtheinfo().</summary>
            private async Task TraceOnSynset(Synset synset, PartOfSpeech pos, int pointerType, int depth)
            {
                switch (pointerType)
                {
                    case PointerType.Antonym:
                        if (pos == PartOfSpeech.Adj)
                        {
                            await TraceAdjectiveAntonym(synset);
                        }
                        else
                        {
                            await TracePtrs(synset, PointerType.Antonym, pos, depth);
                        }
                        break;

                    case (int)SearchType.NounCoordinates:
                        await TraceCoords(synset, PointerType.Hyponym, pos, depth);
                        break;

                    case (int)SearchType.VerbExampleSentencesAndGenericFrames:
                        await PrintFrame(synset, true);
                        break;

                    case PointerType.Meronym:
                        await TracePtrs(synset, PointerType.HasMemberPart, pos, depth);
                        await TracePtrs(synset, PointerType.HasStuffPart, pos, depth);
                        await TracePtrs(synset, PointerType.HasPartPart, pos, depth);
                        break;

                    case PointerType.Holonym:
                        await TracePtrs(synset, PointerType.IsMemberPart, pos, depth);
                        await TracePtrs(synset, PointerType.IsStuffPart, pos, depth);
                        await TracePtrs(synset, PointerType.IsPartPart, pos, depth);
                        break;

                    case (int)SearchType.HierarchicalMeronymSearch:
                        await PartsAll(synset, (int)SearchType.HierarchicalMeronymSearch);
                        break;

                    case (int)SearchType.HierarchicalHolonymSearch:
                        await PartsAll(synset, (int)SearchType.HierarchicalHolonymSearch);
                        break;

                    case PointerType.SeeAlso:
                        await PrintSeeAlso(synset);
                        break;

                    case PointerType.Similar:
                    case (int)SearchType.FindSynonyms:
                    case PointerType.Hypernym:
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                        await TracePtrs(synset, pointerType, pos, depth);
                        if (pos == PartOfSpeech.Adj)
                        {
                            await TracePtrs(synset, PointerType.Participle, pos, depth);
                        }
                        if (_db.PrintSeeAlso)
                        {
                            await PrintSeeAlso(synset);
                        }
                        if (pos == PartOfSpeech.Verb && _db.PrintVerbFrames)
                        {
                            await PrintFrame(synset, false);
                        }
                        break;

                    case PointerType.Pertainym:
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                        await TracePtrs(synset, PointerType.Pertainym, pos, depth);
                        break;

                    case PointerType.Derivation:
                        await TraceNominalizations(synset);
                        break;

                    case PointerType.Classification:
                    case PointerType.Class:
                        await TraceClassification(synset, pointerType);
                        break;

                    default:
                        await TracePtrs(synset, pointerType, pos, depth);
                        break;
                }
            }

            /// <summary>Mirrors traceptrs(): prints the synsets the requested pointer leads to.</summary>
            private async Task TracePtrs(Synset synset, int pointerType, PartOfSpeech pos, int depth)
            {
                var extraIndent = 0;
                if (pointerType < 0)
                {
                    pointerType = -pointerType;
                    extraIndent = 2;
                }

                for (var i = 0; i < synset.PointerCount; i++)
                {
                    CheckCancellation();

                    var matches =
                        (pointerType == PointerType.Hypernym && (synset.PtrTyp[i] == PointerType.Hypernym || synset.PtrTyp[i] == PointerType.Instance)) ||
                        (pointerType == PointerType.Hyponym && (synset.PtrTyp[i] == PointerType.Hyponym || synset.PtrTyp[i] == PointerType.Instances)) ||
                        (synset.PtrTyp[i] == pointerType && (synset.PFrm[i] == 0 || synset.PFrm[i] == synset.WhichWord));

                    if (!matches)
                    {
                        continue;
                    }

                    var realPointer = synset.PtrTyp[i];

                    if (_prflag == 0)
                    {
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                    }

                    PrintSpaces(TraceP, depth + extraIndent);

                    var prefix = realPointer switch
                    {
                        PointerType.Pertainym => pos == PartOfSpeech.Adv
                            ? $"Derived from {WordNetConstants.PartNames[synset.PPos[i]]} "
                            : $"Pertains to {WordNetConstants.PartNames[synset.PPos[i]]} ",
                        PointerType.Antonym when pos != PartOfSpeech.Adj => "Antonym of ",
                        PointerType.Participle => "Participle of verb ",
                        PointerType.Instance => "INSTANCE OF=> ",
                        PointerType.Instances => "HAS INSTANCE=> ",
                        PointerType.HasMemberPart => "   HAS MEMBER: ",
                        PointerType.HasStuffPart => "   HAS SUBSTANCE: ",
                        PointerType.HasPartPart => "   HAS PART: ",
                        PointerType.IsMemberPart => "   MEMBER OF: ",
                        PointerType.IsStuffPart => "   SUBSTANCE OF: ",
                        PointerType.IsPartPart => "   PART OF: ",
                        _ => "=> ",
                    };

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    if ((pointerType == PointerType.Pertainym || pointerType == PointerType.Participle) && synset.PTo[i] != 0)
                    {
                        var tail = $" (Sense {target.WnSns[synset.PTo[i] - 1]})\n";
                        await PrintSynset(prefix, target, tail, DefOff, synset.PTo[i], SkipAnts, PrintMarker);

                        if (pointerType == PointerType.Participle)
                        {
                            await PrintSynset("      =>", target, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
                            await TracePtrs(target, PointerType.Hypernym, (PartOfSpeech)target.Pos, 0);
                        }
                        else if (pos == PartOfSpeech.Adv)
                        {
                            await PrintSynset("      =>", target, "\n", DefOn, AllWords,
                                target.SsType == SynsetType.IndirectAnt ? SkipAnts : PrintAnts, PrintMarker);
                        }
                        else
                        {
                            await PrintSynset("      =>", target, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
                            await TracePtrs(target, PointerType.Hypernym, (PartOfSpeech)target.Pos, 0);
                        }
                    }
                    else if (pointerType == PointerType.Antonym && pos != PartOfSpeech.Adj && synset.PTo[i] != 0)
                    {
                        var tail = $" (Sense {target.WnSns[synset.PTo[i] - 1]})\n";
                        await PrintSynset(prefix, target, tail, DefOff, synset.PTo[i], SkipAnts, PrintMarker);
                        await PrintSynset("      =>", target, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
                    }
                    else
                    {
                        await PrintSynset(prefix, target, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
                    }

                    if (pointerType is >= PointerType.IsMemberPart and <= PointerType.HasPartPart)
                    {
                        _lastHolomero = _buffer.Length;
                    }

                    if (depth != 0)
                    {
                        depth = DepthCheck(depth, target);
                        await TracePtrs(target, pointerType, (PartOfSpeech)target.Pos, depth + 1);
                    }
                }
            }

            /// <summary>Mirrors tracecoords().</summary>
            private async Task TraceCoords(Synset synset, int pointerType, PartOfSpeech pos, int depth)
            {
                for (var i = 0; i < synset.PointerCount; i++)
                {
                    CheckCancellation();

                    if ((synset.PtrTyp[i] != PointerType.Hypernym && synset.PtrTyp[i] != PointerType.Instance) ||
                        (synset.PFrm[i] != 0 && synset.PFrm[i] != synset.WhichWord))
                    {
                        continue;
                    }

                    if (_prflag == 0)
                    {
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                    }
                    PrintSpaces(TraceC, depth);

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    await PrintSynset("-> ", target, "\n", DefOn, AllWords, SkipAnts, PrintMarker);
                    await TracePtrs(target, pointerType, (PartOfSpeech)target.Pos, depth);

                    if (depth != 0)
                    {
                        depth = DepthCheck(depth, target);
                        await TraceCoords(target, pointerType, (PartOfSpeech)target.Pos, depth + 1);
                    }
                }
            }

            /// <summary>Mirrors traceclassif(): prints the domain of a synset.</summary>
            private async Task TraceClassification(Synset synset, int search)
            {
                var printed = new List<long>();

                for (var i = 0; i < synset.PointerCount; i++)
                {
                    CheckCancellation();

                    var isClassification = synset.PtrTyp[i] is >= WordNetConstants.ClassificationStart and <= (int)SearchType.ShowDomainRegion && search == PointerType.Classification;
                    var isClass = synset.PtrTyp[i] is >= WordNetConstants.ClassStart and <= (int)SearchType.ShowDomainTermsForRegion && search == PointerType.Class;
                    if (!isClassification && !isClass)
                    {
                        continue;
                    }

                    if (_prflag == 0)
                    {
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                    }

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    if (printed.Contains(synset.PtrOff[i]))
                    {
                        continue;
                    }
                    printed.Add(synset.PtrOff[i]);

                    PrintSpaces(TraceP, 0);

                    var head = synset.PtrTyp[i] switch
                    {
                        (int)SearchType.ShowDomainTopic => "TOPIC->(",
                        (int)SearchType.ShowDomainUsage => "USAGE->(",
                        (int)SearchType.ShowDomainRegion => "REGION->(",
                        (int)SearchType.ShowDomainTermsForTopic => "TOPIC_TERM->(",
                        (int)SearchType.ShowDomainTermsForUsage => "USAGE_TERM->(",
                        _ => "REGION_TERM->(",
                    };
                    head += WordNetConstants.PartNames[synset.PPos[i]] + ") ";

                    var savedFlag = _db.PrintWordSenseNumbers;
                    _db.PrintWordSenseNumbers = true;
                    await PrintSynset(head, target, "\n", DefOff, AllWords, SkipAnts, SkipMarker);
                    _db.PrintWordSenseNumbers = savedFlag;
                }
            }

            /// <summary>Mirrors tracenomins(): prints the derivationally related forms of the search word.</summary>
            private async Task TraceNominalizations(Synset synset)
            {
                for (var i = 0; i < synset.PointerCount; i++)
                {
                    CheckCancellation();

                    if (synset.PtrTyp[i] != PointerType.Derivation || synset.PFrm[i] != synset.WhichWord)
                    {
                        continue;
                    }

                    if (_prflag == 0)
                    {
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                    }

                    PrintSpaces(TraceP, 0);
                    var prefix = $"RELATED TO->({WordNetConstants.PartNames[synset.PPos[i]]}) ";

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    var tail = $"#{target.WnSns[synset.PTo[i] - 1]}\n";
                    await PrintSynset(prefix, target, tail, DefOff, synset.PTo[i], SkipAnts, SkipMarker);

                    // The C code de-duplicates this line only under an #ifdef that is not enabled,
                    // so the synset is printed once per derivation pointer.
                    PrintSpaces(TraceP, 2);
                    await PrintSynset("=> ", target, "\n", DefOn, AllWords, SkipAnts, PrintMarker);
                }
            }

            /// <summary>Mirrors traceinherit(): prints the inherited meronyms/holonyms of the hypernyms.</summary>
            private async Task TraceInherit(Synset synset, int pointerBase, PartOfSpeech pos, int depth)
            {
                for (var i = 0; i < synset.PointerCount; i++)
                {
                    CheckCancellation();

                    if (synset.PtrTyp[i] != PointerType.Hypernym ||
                        (synset.PFrm[i] != 0 && synset.PFrm[i] != synset.WhichWord))
                    {
                        continue;
                    }

                    if (_prflag == 0)
                    {
                        await PrintSynsetAndSense(synset, _sense + 1);
                        _prflag = 1;
                    }
                    PrintSpaces(TraceI, depth);

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    await PrintSynset("=> ", target, "\n", DefOn, AllWords, SkipAnts, PrintMarker);
                    await TracePtrs(target, pointerBase, PartOfSpeech.Noun, depth);
                    await TracePtrs(target, pointerBase + 1, PartOfSpeech.Noun, depth);
                    await TracePtrs(target, pointerBase + 2, PartOfSpeech.Noun, depth);

                    if (depth != 0)
                    {
                        depth = DepthCheck(depth, target);
                        await TraceInherit(target, pointerBase, (PartOfSpeech)target.Pos, depth + 1);
                    }
                }

                _buffer.Truncate(_lastHolomero);
            }

            /// <summary>Mirrors partsall(): the hierarchical meronym and holonym searches.</summary>
            private async Task PartsAll(Synset synset, int pointerType)
            {
                var pointerBase = pointerType == (int)SearchType.HierarchicalMeronymSearch
                    ? PointerType.HasMemberPart
                    : PointerType.IsMemberPart;

                for (var i = 0; i < 3; i++)
                {
                    CheckCancellation();

                    if (HasPointer(synset, pointerBase + i))
                    {
                        await TracePtrs(synset, pointerBase + i, PartOfSpeech.Noun, 1);
                    }
                }

                if (pointerType == (int)SearchType.HierarchicalMeronymSearch)
                {
                    _lastHolomero = _buffer.Length;
                    await TraceInherit(synset, pointerBase, PartOfSpeech.Noun, 1);
                }
            }

            /// <summary>Mirrors traceadjant(): the antonym search for adjectives.</summary>
            private async Task TraceAdjectiveAntonym(Synset synset)
            {
                if (synset.SsType != SynsetType.DirectAnt && synset.SsType != SynsetType.IndirectAnt)
                {
                    return;
                }

                await PrintSynsetAndSense(synset, _sense + 1);
                _buffer.Append("\n");

                var antType = SynsetType.DirectAnt;
                Synset newsynptr;
                if (synset.SsType == SynsetType.IndirectAnt)
                {
                    antType = SynsetType.IndirectAnt;
                    var i = 0;
                    while (i < synset.PointerCount && synset.PtrTyp[i] != PointerType.Similar)
                    {
                        i++;
                    }
                    if (i >= synset.PointerCount)
                    {
                        return;
                    }
                    newsynptr = await _db.ReadSynsetCoreAsync(PartOfSpeech.Adj, synset.PtrOff[i], "", _cancellationToken) ?? synset;
                }
                else
                {
                    newsynptr = synset;
                }

                for (var i = 0; i < newsynptr.PointerCount; i++)
                {
                    if (newsynptr.PtrTyp[i] != PointerType.Antonym ||
                        (antType == SynsetType.DirectAnt && newsynptr.PFrm[i] != newsynptr.WhichWord))
                    {
                        continue;
                    }

                    var antptr = await _db.ReadSynsetCoreAsync(PartOfSpeech.Adj, newsynptr.PtrOff[i], "", _cancellationToken);
                    if (antptr == null)
                    {
                        continue;
                    }

                    if (antType == SynsetType.DirectAnt)
                    {
                        await PrintSynset("", antptr, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
                        for (var j = 0; j < antptr.PointerCount; j++)
                        {
                            if (antptr.PtrTyp[j] == PointerType.Similar)
                            {
                                var simptr = await _db.ReadSynsetCoreAsync(PartOfSpeech.Adj, antptr.PtrOff[j], "", _cancellationToken);
                                if (simptr != null)
                                {
                                    await PrintSynset("        => ", simptr, "\n", DefOn, AllWords, SkipAnts, PrintMarker);
                                }
                            }
                        }
                    }
                    else
                    {
                        await PrintAntSynset(antptr, "\n", antType, DefOn);
                    }
                }
            }

            #region Printers

            /// <summary>Mirrors printbuffer().</summary>
            private void PrintBuffer(string text) => _buffer.Append(text);

            /// <summary>Mirrors printsynset().</summary>
            private async Task PrintSynset(string head, Synset synset, string tail, int definition, int wordNumber, int antFlag, int markerFlag)
            {
                var text = head;

                if (_db.PrintOffsets)
                {
                    text += $"{synset.HereIAm,8:D8} ";
                }

                if (_db.PrintFileInfo)
                {
                    text += $"<{await _db.GetLexFileNameAsync(synset.Fnum, _cancellationToken)}> ";
                    _prlexid = 1;
                }
                else
                {
                    _prlexid = 0;
                }

                if (wordNumber != 0)
                {
                    text += await CatWord(synset, wordNumber - 1, markerFlag, antFlag);
                }
                else
                {
                    for (var i = 0; i < synset.WordCount; i++)
                    {
                        text += await CatWord(synset, i, markerFlag, antFlag);
                        if (i < synset.WordCount - 1)
                        {
                            text += ", ";
                        }
                    }
                }

                if (definition == DefOn && _db.PrintGlosses && synset.Defn != null)
                {
                    text += " -- " + synset.Defn;
                }

                text += tail;
                PrintBuffer(text);
            }

            /// <summary>Mirrors printantsynset().</summary>
            private async Task PrintAntSynset(Synset synset, string tail, int antType, int definition)
            {
                var text = string.Empty;

                if (_db.PrintOffsets)
                {
                    text += $"{synset.HereIAm,8:D8} ";
                }

                if (_db.PrintFileInfo)
                {
                    text += $"<{await _db.GetLexFileNameAsync(synset.Fnum, _cancellationToken)}> ";
                    _prlexid = 1;
                }
                else
                {
                    _prlexid = 0;
                }

                text += "INDIRECT (VIA ";
                var first = true;
                for (var i = 0; i < synset.WordCount; i++)
                {
                    var printed = await PrintAnt(PartOfSpeech.Adj, synset, i + 1, first ? "%s" : ", %s", ", ");
                    first = false;
                    if (printed.Length > 0)
                    {
                        text += printed;
                    }
                }
                text += ") -> ";

                for (var i = 0; i < synset.WordCount; i++)
                {
                    text += await CatWord(synset, i, SkipMarker, SkipAnts);
                    if (i < synset.WordCount - 1)
                    {
                        text += ", ";
                    }
                }

                if (_db.PrintGlosses && synset.Defn != null && definition == DefOn)
                {
                    text += " -- " + synset.Defn;
                }

                text += tail;
                PrintBuffer(text);
            }

            /// <summary>Mirrors catword().</summary>
            private async Task<string> CatWord(Synset synset, int wordNumber, int adjectiveMarker, int antFlag)
            {
                var text = Deadjify(synset.Words[wordNumber], out var marker);

                if (_prlexid != 0 && synset.LexId[wordNumber] != 0)
                {
                    text += synset.LexId[wordNumber].ToString();
                }

                if (_db.PrintWordSenseNumbers)
                {
                    text += $"#{synset.WnSns[wordNumber]}";
                }

                if ((PartOfSpeech)synset.Pos == PartOfSpeech.Adj)
                {
                    if (adjectiveMarker == PrintMarker)
                    {
                        text += AdjectiveMarkers[marker];
                    }

                    if (antFlag == PrintAnts)
                    {
                        text += await PrintAnt(PartOfSpeech.Adj, synset, wordNumber + 1, " (vs. %s)", string.Empty);
                    }
                }

                return text;
            }

            /// <summary>Mirrors printant(): the antonyms of a word, looked up through the target synset.</summary>
            private async Task<string> PrintAnt(PartOfSpeech dbase, Synset synset, int wordNumber, string template, string tail)
            {
                var result = string.Empty;
                var first = true;

                for (var i = 0; i < synset.PointerCount; i++)
                {
                    if (synset.PtrTyp[i] != PointerType.Antonym || synset.PFrm[i] != wordNumber)
                    {
                        continue;
                    }

                    var psynptr = await _db.ReadSynsetCoreAsync(dbase, synset.PtrOff[i], "", _cancellationToken);
                    if (psynptr == null)
                    {
                        continue;
                    }

                    for (var j = 0; j < psynptr.PointerCount; j++)
                    {
                        if (psynptr.PtrTyp[j] != PointerType.Antonym ||
                            psynptr.PTo[j] != wordNumber ||
                            psynptr.PtrOff[j] != synset.HereIAm)
                        {
                            continue;
                        }

                        var wordOffset = psynptr.PFrm[j] != 0 ? psynptr.PFrm[j] - 1 : 0;
                        var word = Deadjify(psynptr.Words[wordOffset], out _);

                        if (_prlexid != 0 && psynptr.LexId[wordOffset] != 0)
                        {
                            word += psynptr.LexId[wordOffset].ToString();
                        }
                        if (_db.PrintWordSenseNumbers)
                        {
                            word += $"#{psynptr.WnSns[wordOffset]}";
                        }

                        if (!first)
                        {
                            result += tail;
                        }
                        else
                        {
                            first = false;
                        }

                        result += template.Replace("%s", word);
                    }
                }

                return result;
            }

            /// <summary>Mirrors printsns(): prints the sense number and the synset.</summary>
            private async Task PrintSynsetAndSense(Synset synset, int sense)
            {
                await PrintSense(synset, sense);
                await PrintSynset(string.Empty, synset, "\n", DefOn, AllWords, PrintAnts, PrintMarker);
            }

            /// <summary>Mirrors printsense().</summary>
            private async Task PrintSense(Synset synset, int sense)
            {
                if (_db.PrintFileName)
                {
                    _buffer.Append($"\nSense {sense} in file \"{await _db.GetLexFileNameAsync(synset.Fnum, _cancellationToken)}\"\n");
                }
                else
                {
                    _buffer.Append($"\nSense {sense}\n");
                }

                _outSenseCount[_numForms]++;
                _printCount++;
            }

            /// <summary>Mirrors printspaces().</summary>
            private void PrintSpaces(int trace, int depth)
            {
                for (var j = 0; j < depth; j++)
                {
                    _buffer.Append("    ");
                }

                switch (trace)
                {
                    case TraceP:
                        _buffer.Append(depth != 0 ? "   " : "       ");
                        break;
                    case TraceC:
                        if (depth == 0)
                        {
                            _buffer.Append("    ");
                        }
                        break;
                    case TraceI:
                        if (depth == 0)
                        {
                            _buffer.Append("\n    ");
                        }
                        break;
                }
            }

            /// <summary>Mirrors printframe(): prints the example sentences or the generic frames of a verb.</summary>
            private async Task PrintFrame(Synset synset, bool printSynset)
            {
                if (printSynset)
                {
                    await PrintSynsetAndSense(synset, _sense + 1);
                }

                if (!await FindExample(synset))
                {
                    for (var i = 0; i < synset.FrmId.Length; i++)
                    {
                        if (synset.FrmTo[i] != synset.WhichWord && synset.FrmTo[i] != 0)
                        {
                            continue;
                        }

                        _buffer.Append(synset.FrmTo[i] == synset.WhichWord ? "          => " : "          *> ");
                        _buffer.Append(await _db.GetFrameTextAsync(synset.FrmId[i], _cancellationToken));
                        _buffer.Append("\n");
                    }
                }
            }

            /// <summary>Mirrors findexample(): looks for example sentences of a verb sense.</summary>
            private async Task<bool> FindExample(Synset synset)
            {
                var wordNumber = synset.WhichWord - 1;
                if (wordNumber < 0)
                {
                    return false;
                }

                var senseKey = $"{synset.Words[wordNumber]}%{(int)(PartOfSpeech)synset.Pos:D1}:{synset.Fnum:D2}:{synset.LexId[wordNumber]:D2}::";
                var templates = await _db.GetVerbSentenceIndexAsync(senseKey, _cancellationToken);
                if (templates == null)
                {
                    return false;
                }

                foreach (var template in templates)
                {
                    var text = await _db.GetVerbSentenceTemplateAsync(template, _cancellationToken);
                    if (text != null)
                    {
                        // The C library uses the rest of the sentence file line, which starts with the
                        // separating space and ends with the newline, as the format string.
                        _buffer.Append("          EX: " + " " + text.Replace("%s", synset.Words[wordNumber]) + "\n");
                    }
                }

                return templates.Count > 0;
            }

            /// <summary>Mirrors printseealso().</summary>
            private async Task PrintSeeAlso(Synset synset)
            {
                var prefix = (PartOfSpeech)synset.Pos == PartOfSpeech.Verb
                    ? "          Phrasal Verb-> "
                    : "          Also See-> ";
                var first = true;

                for (var i = 0; i < synset.PointerCount; i++)
                {
                    if (synset.PtrTyp[i] != PointerType.SeeAlso ||
                        (synset.PFrm[i] != 0 && synset.PFrm[i] != synset.WhichWord))
                    {
                        continue;
                    }

                    var target = await _db.ReadSynsetCoreAsync((PartOfSpeech)synset.PPos[i], synset.PtrOff[i], "", _cancellationToken);
                    if (target == null)
                    {
                        continue;
                    }

                    var savedFlag = _db.PrintWordSenseNumbers;
                    _db.PrintWordSenseNumbers = true;
                    await PrintSynset(prefix, target, string.Empty, DefOff, synset.PTo[i] == 0 ? AllWords : synset.PTo[i], SkipAnts, SkipMarker);
                    _db.PrintWordSenseNumbers = savedFlag;

                    if (first)
                    {
                        prefix = "; ";
                        first = false;
                    }
                }

                if (!first)
                {
                    _buffer.Append("\n");
                }
            }

            /// <summary>Mirrors freq_word(): the polysemy count of a lemma.</summary>
            private void Frequency(IndexEntryResult index)
            {
                var count = index.SenseCount;
                var familiar = count switch
                {
                    0 => 0,
                    1 => 1,
                    2 => 2,
                    >= 3 and <= 4 => 3,
                    >= 5 and <= 8 => 4,
                    >= 9 and <= 16 => 5,
                    >= 17 and <= 32 => 6,
                    _ => 7,
                };

                PrintBuffer($"\n{index.Word} used as {AntonymTemplate[(int)GetPosOfPartChar(index.Pos[0])]} is {FrequencyCategories[familiar]} (polysemy count = {count})\n");
            }

            #endregion

            #region Overview

            /// <summary>Mirrors WNOverview(): every sense of a word with its synset.</summary>
            private async Task Overview(string searchStr, PartOfSpeech pos)
            {
                var offsets = new long[WordNetConstants.MaxSense];
                var offsetCount = 0;
                var bufferStart = _buffer.Length;

                foreach (var index in await _db.GetIndexCoreAsync(searchStr, pos, _cancellationToken))
                {
                    _senseCount[_numForms++] = index.OffsetCount;

                    _buffer.Append("                                                                                                   \n");

                    for (var sense = 0; sense < index.OffsetCount; sense++)
                    {
                        CheckCancellation();

                        var skipIt = false;
                        for (var i = 0; i < offsetCount && !skipIt; i++)
                        {
                            if (offsets[i] == index.Offsets[sense])
                            {
                                skipIt = true;
                            }
                        }

                        if (skipIt)
                        {
                            continue;
                        }

                        offsets[offsetCount++] = index.Offsets[sense];
                        var synset = await _db.ReadSynsetCoreAsync(pos, index.Offsets[sense], index.Word, _cancellationToken);
                        if (synset == null)
                        {
                            continue;
                        }

                        string head;
                        if (index.TaggedSenseCount != -1 && sense + 1 <= index.TaggedSenseCount)
                        {
                            head = $"{sense + 1}. ({await _db.GetTagCountCoreAsync(index, sense + 1, _cancellationToken)}) ";
                        }
                        else
                        {
                            head = $"{sense + 1}. ";
                        }

                        var savedFlag = _db.PrintGlosses;
                        _db.PrintGlosses = true;
                        await PrintSynset(head, synset, "\n", DefOn, AllWords, SkipAnts, SkipMarker);
                        _db.PrintGlosses = savedFlag;

                        _outSenseCount[_numForms]++;
                        _printCount++;
                    }

                    var printed = _outSenseCount[_numForms];
                    if (printed > 0)
                    {
                        var message = printed == 1
                            ? $"\nThe {WordNetConstants.PartNames[(int)pos]} {index.Word} has 1 sense"
                            : $"\nThe {WordNetConstants.PartNames[(int)pos]} {index.Word} has {printed} senses";

                        if (index.TaggedSenseCount > 0)
                        {
                            message += $" (first {index.TaggedSenseCount} from tagged texts)\n";
                        }
                        else if (index.TaggedSenseCount == 0)
                        {
                            message += " (no senses from tagged texts)\n";
                        }

                        _buffer.Overwrite(bufferStart, message);
                        bufferStart = _buffer.Length;
                    }
                    else
                    {
                        // Nothing was printed for this word form: the C code drops everything the
                        // form added, which is the separator line it printed before its senses.
                        _buffer.Truncate(bufferStart);
                    }

                    _numForms++;
                }
            }

            #endregion

            #region Relatives

            private sealed class RelativeGroup
            {
                public bool[] Senses { get; } = new bool[WordNetConstants.MaxSense];
            }

            /// <summary>Mirrors relatives(): groups the related senses of a verb.</summary>
            private async Task Relatives(IndexEntryResult index, PartOfSpeech pos)
            {
                _relList.Clear();

                if (pos == PartOfSpeech.Verb)
                {
                    await FindVerbGroups(index);
                    await PrintRelatives(index, pos);
                }
            }

            /// <summary>Mirrors findverbgroups(): builds the groups of verb senses linked by VERBGROUP pointers.</summary>
            private async Task FindVerbGroups(IndexEntryResult index)
            {
                for (var i = 0; i < index.OffsetCount; i++)
                {
                    var synset = await _db.ReadSynsetCoreAsync(PartOfSpeech.Verb, index.Offsets[i], index.Word, _cancellationToken);
                    if (synset == null)
                    {
                        continue;
                    }

                    for (var j = 0; j < synset.PointerCount; j++)
                    {
                        if (synset.PtrTyp[j] != PointerType.VerbGroup)
                        {
                            continue;
                        }

                        for (var k = 0; k < index.OffsetCount; k++)
                        {
                            if (synset.PtrOff[j] == index.Offsets[k])
                            {
                                AddRelatives(index, i, k);
                                break;
                            }
                        }
                    }
                }
            }

            /// <summary>Mirrors add_relatives(), including the transitive merging of groups.</summary>
            private void AddRelatives(IndexEntryResult index, int relative1, int relative2)
            {
                foreach (var group in _relList)
                {
                    if (!group.Senses[relative1] && !group.Senses[relative2])
                    {
                        continue;
                    }

                    group.Senses[relative1] = true;
                    group.Senses[relative2] = true;

                    foreach (var other in _relList)
                    {
                        if (other == group || (!other.Senses[relative1] && !other.Senses[relative2]))
                        {
                            continue;
                        }

                        for (var i = 0; i < group.Senses.Length; i++)
                        {
                            group.Senses[i] |= other.Senses[i];
                        }
                    }
                    return;
                }

                var newGroup = new RelativeGroup();
                newGroup.Senses[relative1] = true;
                newGroup.Senses[relative2] = true;
                _relList.Add(newGroup);
            }

            /// <summary>Mirrors printrelatives().</summary>
            private async Task PrintRelatives(IndexEntryResult index, PartOfSpeech pos)
            {
                var outSenses = new bool[index.OffsetCount];
                _prflag = 1;

                foreach (var group in _relList)
                {
                    var flag = false;
                    for (var i = 0; i < index.OffsetCount; i++)
                    {
                        if (!group.Senses[i] || outSenses[i])
                        {
                            continue;
                        }

                        flag = true;
                        var synset = await _db.ReadSynsetCoreAsync(pos, index.Offsets[i], "", _cancellationToken);
                        if (synset != null)
                        {
                            await PrintSynsetAndSense(synset, i + 1);
                            await TracePtrs(synset, PointerType.Hypernym, pos, 0);
                        }
                        outSenses[i] = true;
                    }

                    if (flag)
                    {
                        PrintBuffer("--------------\n");
                    }
                }

                for (var i = 0; i < index.OffsetCount; i++)
                {
                    if (outSenses[i])
                    {
                        continue;
                    }

                    var synset = await _db.ReadSynsetCoreAsync(pos, index.Offsets[i], "", _cancellationToken);
                    if (synset != null)
                    {
                        await PrintSynsetAndSense(synset, i + 1);
                        await TracePtrs(synset, PointerType.Hypernym, pos, 0);
                    }
                    PrintBuffer("--------------\n");
                }
            }

            #endregion

            /// <summary>Mirrors deadjify(): truncates the word at the adjective marker and reports it.</summary>
            private static string Deadjify(string word, out int marker)
            {
                marker = 0; // UNKNOWN_MARKER
                var index = word.IndexOf('(');
                if (index < 0)
                {
                    return word;
                }

                var rest = word.Substring(index);
                if (rest.StartsWith("(a)", StringComparison.Ordinal))
                {
                    marker = 2; // ATTRIBUTIVE
                }
                else if (rest.StartsWith("(ip)", StringComparison.Ordinal))
                {
                    marker = 3; // IMMED_POSTNOMINAL
                }
                else if (rest.StartsWith("(p)", StringComparison.Ordinal))
                {
                    marker = 1; // PREDICATIVE
                }

                return word.Substring(0, index);
            }
        }
    }
}
