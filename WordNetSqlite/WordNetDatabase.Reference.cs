using Microsoft.EntityFrameworkCore;

namespace WordNetSqlite
{
    public sealed partial class WordNetDatabase
    {
        private Dictionary<int, string>? _lexFileNames;
        private Dictionary<int, string>? _frameTexts;
        private Dictionary<string, List<int>>? _verbSentenceIndex;

        /// <summary>Returns the name of a lexicographer file, as lexfiles[] does in the C library.</summary>
        internal async Task<string> GetLexFileNameAsync(int lexFileNumber, CancellationToken cancellationToken)
        {
            if (_lexFileNames == null)
            {
                var files = await _context.LexicographerFiles
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                _lexFileNames = files.ToDictionary(e => e.Id, e => e.Name);
            }

            return _lexFileNames.TryGetValue(lexFileNumber, out var name)
                ? name
                : lexFileNumber.ToString();
        }

        /// <summary>Returns the text of a generic verb frame, as frametext[] does in the C library.</summary>
        internal async Task<string> GetFrameTextAsync(int frameNumber, CancellationToken cancellationToken)
        {
            if (_frameTexts == null)
            {
                var frames = await _context.VerbFrameTexts
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                _frameTexts = frames.ToDictionary(e => e.FrameNumber, e => e.Text);
            }

            return _frameTexts.TryGetValue(frameNumber, out var text) ? text : string.Empty;
        }

        /// <summary>Returns the example sentence template numbers of a verb sense, from sentidx.vrb.</summary>
        internal async Task<List<int>?> GetVerbSentenceIndexAsync(string senseKey, CancellationToken cancellationToken)
        {
            if (_verbSentenceIndex == null)
            {
                var entries = await _context.VerbSentenceIndexes
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                _verbSentenceIndex = entries.ToDictionary(
                    e => e.SenseKey,
                    e => e.TemplateIds
                        .Split(',')
                        .Select(t => t.Trim())
                        .Where(t => t.Length > 0)
                        .Select(int.Parse)
                        .ToList());
            }

            return _verbSentenceIndex.TryGetValue(senseKey, out var templates) ? templates : null;
        }

        /// <summary>Returns the text of an example sentence template, from sents.vrb.</summary>
        internal async Task<string?> GetVerbSentenceTemplateAsync(int templateId, CancellationToken cancellationToken)
        {
            return await _context.VerbSentenceTemplates
                .AsNoTracking()
                .Where(e => e.TemplateId == templateId)
                .Select(e => e.Text)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
