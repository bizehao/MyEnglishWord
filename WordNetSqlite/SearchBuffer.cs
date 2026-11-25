using System.Text;

namespace WordNetSqlite
{
    /// <summary>
    /// The result buffer of a text search, mirroring searchbuffer and overflag of the WordNet C library.
    /// </summary>
    internal sealed class SearchBuffer
    {
        /// <summary>SEARCHBUF of wn.h: results larger than this are reported as too large.</summary>
        public const int Capacity = 200 * 1024;

        private readonly StringBuilder _builder = new();

        public bool Overflowed { get; private set; }

        public int Length => _builder.Length;

        public void Clear()
        {
            _builder.Clear();
            Overflowed = false;
        }

        public void Append(string text)
        {
            if (Overflowed)
            {
                return;
            }

            if (_builder.Length + text.Length >= Capacity)
            {
                Overflowed = true;
                return;
            }

            _builder.Append(text);
        }

        /// <summary>Replaces the whole buffer, as the C code does when a search was too large.</summary>
        public void Replace(string text)
        {
            _builder.Clear();
            Overflowed = false;
            _builder.Append(text);
        }

        /// <summary>
        /// Overwrites the buffer at the given position without shifting the remaining text, mirroring
        /// the strncpy() the C library uses to patch the sense count message into the result.
        /// </summary>
        public void Overwrite(int position, string text)
        {
            if (position < 0 || position > _builder.Length)
            {
                return;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var index = position + i;
                if (index < _builder.Length)
                {
                    _builder[index] = text[i];
                }
                else
                {
                    _builder.Append(text[i]);
                }
            }
        }

        /// <summary>Truncates the buffer, as traceinherit() does with the last holo/meronym position.</summary>
        public void Truncate(int length)
        {
            if (length < 0 || length >= _builder.Length)
            {
                return;
            }

            _builder.Length = length;
        }

        public override string ToString() => _builder.ToString();
    }
}
