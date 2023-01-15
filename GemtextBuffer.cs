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

        public bool AtLineStart
            => !HasContent || Content.EndsWith('\n');

        public bool InBlockquote { get; set; } = false;

        private StringBuilder sb;

        private string lineStart = null;

        public GemtextBuffer()
        {
            sb = new StringBuilder();
        }

        public void Reset()
        {
            sb.Clear();
            lineStart = null;
        }

        public void SetLineStart(string s)
        {
            lineStart = s;
        }

        public void Append(string s)
        {
            if(s.Contains('\n'))
            {
                foreach(string sub in s.Split('\n'))
                {
                    AppendLine(sub);
                }
                return;
            }

            HandleLineStart(s);
            HandleBlockQuote(s);
            sb.Append(s);
        }

        public void AppendLine(string s = "")
        {
            HandleLineStart(s);
            HandleBlockQuote(s);
            sb.AppendLine(s);
        }

        public void EnsureAtLineStart()
        {
            if(AtLineStart && lineStart != null)
            {
                lineStart = null;
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


        public void HandleLineStart(string s)
        {
            //if we are adding something that is not whitespace, and we have a prefix
            if(lineStart != null)
            {
                sb.Append(lineStart);
                lineStart = null;
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
