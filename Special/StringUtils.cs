using System;
using System.Net;
using System.Text.RegularExpressions;

namespace HtmlToGmi.Special
{
	public static class StringUtils
	{
        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string RemoveNewlines(string text)
        {
            if (text.Length > 0 && (text.Contains('\n') || text.Contains('\r')))
            {
                text = text.Replace('\r', ' ');
                text = text.Replace('\n', ' ');
                text = whitespace.Replace(text, " ");
            }
            return text;
        }
    }
}
