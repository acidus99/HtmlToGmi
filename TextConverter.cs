using System.Linq;
using System.Text.RegularExpressions;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Models;

namespace HtmlToGmi
{ 
    /// <summary>
    /// Extracts text
    /// </summary>
    public class TextConverter
    {
        public bool ShouldCollapseNewlines { get; set; } = false;
        public bool ShouldConvertImages { get; set; } = false;

        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private GemtextBuffer buffer = new GemtextBuffer();

        public string Convert(INode current)
        {
            buffer.Reset();
            ExtractInnerTextHelper(current);
            return ShouldCollapseNewlines?
                CollapseWhitespace(buffer.Content) :
                buffer.Content;
        }

        private void ExtractInnerTextHelper(INode current)
        {
            if(current == null)
            {
                return;
            }

            switch (current.NodeType)
            {
                case NodeType.Text:
                    //if its not only whitespace add it.
                    if (current.TextContent.Trim().Length > 0)
                    {
                        buffer.Append(current.TextContent);
                    }
                    //if its whitepsace, but doesn't have a newline
                    else if (!current.TextContent.Contains('\n'))
                    {
                        buffer.Append(current.TextContent);
                    }
                    break;

                case NodeType.Element:
                    {
                        HtmlElement element = current as HtmlElement;
                        if(element == null)
                        {
                            return;
                        }
                        var nodeName = element?.NodeName.ToLower();

                        switch (nodeName)
                        {
                            case "a":
                                ExtractChildrenText(current);
                                break;

                            case "br":
                                buffer.AppendLine();
                                break;

                            case "img":
                                if (ShouldConvertImages)
                                {
                                    buffer.Append(ConvertImage(element));
                                }
                                break;

                            case "figure":
                            case "picture":
                            case "table":
                            case "style":
                            case "script":
                                break;

                            default:
                                if (HtmlConverter.ShouldDisplayAsBlock(element))
                                {
                                    buffer.EnsureAtLineStart();
                                    ExtractChildrenText(current);
                                    buffer.EnsureAtLineStart();
                                }
                                else
                                {
                                    ExtractChildrenText(current);
                                }
                                break;
                        }
                    }
                    break;
            }
        }

        private void ExtractChildrenText(INode element)
            => element.ChildNodes.ToList().ForEach(x => ExtractInnerTextHelper(x));

        private string ConvertImage(HtmlElement element)
        {
            var alt = element.GetAttribute("alt");
            if(string.IsNullOrEmpty(alt))
            {
                alt = element.GetAttribute("title");
            }
            return !string.IsNullOrEmpty(alt) ?
                $"[Image: {alt}] " :
                "";
        }

        /// <summary>
        /// Removes \r, \n, and collapses runs of white space
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string CollapseWhitespace(string text)
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