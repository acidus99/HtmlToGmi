using System;
using System.Text;

using HtmlToGmi.Models;

namespace HtmlToGmi
{
    /// <summary>
    /// Gemtext-aware text buffer. Ensures that:excessives
    /// - excessive new lines are collapsed
    /// - preformatted text is preserved
    /// - proper start of the line type is used (header, list, etc)
    /// </summary>
    public class GemtextBuffer
    {
        public string Content => sb.ToString();

        public bool HasContent => (sb.Length > 0);

        /// <summary>
        /// are we are the very beginning of a line? There could still be a line prefix
        /// </summary>
        public bool AtLineStart
            => !HasContent || Content.EndsWith('\n');

        public bool HasLinePrefix
            => (linePrefex != null);

        public bool InBlockquote { get; set; } = false;

        private StringBuilder sb;

        private string linePrefex = null;

        public GemtextBuffer()
        {
            sb = new StringBuilder();
        }

        public void Reset()
        {
            sb.Clear();
            linePrefex = null;
        }

        public void SetLinePrefix(string s)
        {
            linePrefex = s;
        }

        public void Append(string s)
        {
            if(s.Contains('\n'))
            {
                AppendMultiline(s);
                return;
            }

            HandleLinePrefix();
            HandleBlockQuote(s);
            sb.Append(s);
        }


        private void AppendMultiline(string s)
        {
            // trying to append multiple lines. Need to break these on the \n
            //and ensure that any prefixes needed  line start is handled properly
            //we need to AppendLine() the first N-1 parts, and just Append() the
            //last to avoid adding an extra new line
            var lines = s.Split('\n');
            for(int i=0; i < lines.Length; i++)
            {
                if (i < lines.Length - 1)
                {
                    AppendLine(lines[i]);
                }
                else
                {
                    Append(lines[i]);
                }
            }
        }

        public void AppendLine(string s = "")
        {
            HandleLinePrefix();
            HandleBlockQuote(s);
            sb.AppendLine(s);
        }

        public void EnsureAtLineStart()
        {
            if(AtLineStart && linePrefex != null)
            {
                linePrefex = null;
            }

            if (!AtLineStart)
            {
                sb.AppendLine();
            }
        }

        /// <summary>
        /// returns the last line of the buffer
        /// </summary>
        /// <returns></returns>
        public String GetLastLine()
        {
            return Content.Substring(GetLastLineStartIndex()).Trim();
        }

        private int GetLastLineStartIndex()
        {
            var secondToLastNewLine = Content.TrimEnd().LastIndexOf('\n');
            if (secondToLastNewLine == -1)
            {
                //only 1 line in the buffer, so return the whole line
                secondToLastNewLine = 0;
            }
            return secondToLastNewLine;
        }

        public void RemoveLastLine()
        {
            if (!Content.Contains('\n'))
            {
                Reset();
            }
            else
            {
                var index = GetLastLineStartIndex();
                //we want all content before the last line index
                if (index > 0)
                {
                    var content = Content.Substring(0, index);
                    Reset();
                    sb.AppendLine(content);
                }
                else
                {
                    Reset();
                }
            }
        }

        private void HandleLinePrefix()
        {
            //if we are adding something that is not whitespace, and we have a prefix
            if(linePrefex != null)
            {
                sb.Append(linePrefex);
                linePrefex = null;
            }
        }

        private void HandleBlockQuote(string s)
        {
            if (InBlockquote && AtLineStart && s.Trim().Length > 0)
            {
                sb.Append(">");
            }
        }
    }
}
