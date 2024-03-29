﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Html;
using HtmlToGmi.Models;

namespace HtmlToGmi
{ 
    /// <summary>
    /// Extracts text
    /// </summary>
    public class TextConverter : AbstractParser
    {
        public bool ShouldCollapseNewlines { get; set; } = false;
        public bool ShouldConvertImages { get; set; } = false;

        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private GemtextBuffer buffer;

        public List<ImageLink> Images;

        ImageParser imageParser;

        public TextConverter(Uri baseUri = null)
            : base(baseUri)
        {
            buffer = new GemtextBuffer();
            Images = new List<ImageLink>();
            imageParser = new ImageParser(BaseUrl);
        }

        public string Convert(INode current)
        {
            Images.Clear();
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

                            case "br":
                                buffer.AppendLine();
                                break;

                            case "img":
                                ProcessImg(element as IHtmlImageElement);
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

        private void ProcessImg(IHtmlImageElement img)
        {
            var image = imageParser.ParseImg(img);
            if (image != null)
            {
                Images.Add(image);
                if (ShouldConvertImages)
                {
                    buffer.Append(image.Caption);
                }
            }
        }
    }
}