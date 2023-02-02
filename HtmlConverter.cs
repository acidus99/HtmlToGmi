﻿using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Filter;
using HtmlToGmi.Models;
using HtmlToGmi.Html;
using AngleSharp;
using AngleSharp.Html.Parser;

namespace HtmlToGmi
{

    public delegate string UrlRewrite(Uri url);

    /// <summary>
    /// Converts HTML into Gemtext
    /// </summary>
    public class HtmlConverter
    {
        private static readonly string[] blockElements = new string[] { "address", "article", "aside", "blockquote", "canvas", "dd", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "noscript", "ol", "p", "pre", "section", "table", "tfoot", "ul", "video" };

        public bool ShouldRenderHyperlinks { get; set; } = true;
        public bool AllowDuplicateLinks { get; set; } = false;

        /// <summary>
        /// Optional function to call which rewrites any anchor tag hyperlinks the converter outputs
        /// </summary>
        public UrlRewrite AnchorRewriteCallback { get; set; }

        /// <summary>
        /// Optional function to call which rewrites any image hyperlinks the converter outputs
        /// </summary>
        public UrlRewrite ImageRewriteCallback { get; set; }

        /// <summary>
        /// All images found in the converted text
        /// </summary>
        List<Image> Images = new List<Image>();

        /// <summary>
        /// tmp buffer used to hold links in the current block of text
        /// </summary>
        List<Hyperlink> linkBuffer = new List<Hyperlink>();

        /// <summary>
        /// Unique, "best", list of anchor hyperlinks found in the converted text
        /// </summary>
        LinkCollection BodyLinks = new LinkCollection();

        /// <summary>
        /// tracks how deep we are in a nested list
        /// </summary>
        int listDepth = 0;

        GemtextBuffer buffer = new GemtextBuffer();
        MediaConverter mediaConverter;
        Uri BaseUrl;

        bool inPreformatted = false;

        int linkCounter = 0;

        public ConvertedContent Convert(string url, string html)
            => Convert(new Uri(url), html);

        public ConvertedContent Convert(Uri url, string html)
        {
            var document = ParseToDocument(html);
            return Convert(url, document.FirstElementChild);
        }

        public ConvertedContent Convert(Uri url, INode current)
        {
            mediaConverter = new MediaConverter(url);
            BaseUrl = url;
            ConvertNode(current);
            FlushLinkBuffer();
            return new ConvertedContent
            {
                Url = url,
                Gemtext = buffer.Content.TrimEnd(),
                Images = Images,
                Links = BodyLinks.GetLinks()
            };
        }

        private IHtmlDocument ParseToDocument(string html)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(html);
        }

        private bool FlushLinkBuffer()
        {
            bool ret = false;
            if (linkBuffer.Count > 0 && !buffer.InBlockquote)
            {
                buffer.EnsureAtLineStart();

                //lets see if the last line is equal to the link text
                if (CanReplaceLastLine())
                {
                    //if so trim the last line
                    buffer.RemoveLastLine();
                    ret = true;
                    var link = linkBuffer[0];
                    buffer.AppendLine($"=> {GetAnchorUrl(link.Url)} {link.Text}");
                    linkBuffer.Clear();
                    //roll back found link counter
                    linkCounter--;
                }
                else
                {
                    foreach (var link in linkBuffer)
                    {
                        ret = true;
                        var hostText = "";
                        if (BaseUrl.Host != link.Url.Host && link.Url.Scheme.StartsWith("http"))
                        {
                            hostText = $"({link.Url.Host}) ";
                        }
                        buffer.AppendLine($"=> {GetAnchorUrl(link.Url)} {link.OrderDetected}. {hostText}\"{link.Text}\"");
                    }
                    buffer.AppendLine();
                    linkBuffer.Clear();
                }
            }
            return ret;
        }

        private bool CanReplaceLastLine()
        {
            if(linkBuffer.Count != 1)
            {
                return false;
            }
            var lastLine = buffer.GetLastLine();
            //bulleted lists can be converted, so trim it off
            if(lastLine.StartsWith("* ") && lastLine.Length >=3)
            {
                lastLine = lastLine.Substring(2);
            }

            return lastLine == $"{linkBuffer[0].Text}[{linkBuffer[0].OrderDetected}]";
        }

        private void ConvertNode(INode current)
        {
            switch (current.NodeType)
            {
                case NodeType.Text:
                    ProcessTextNode(current);
                    break;

                case NodeType.Element:
                    ProcessHtmlElement(current as HtmlElement);
                    break;
            }
        }

        private void ParseChildern(INode node)
        {
            foreach (var child in node.ChildNodes)
            {
                ConvertNode(child);
            }
        }

        private void ProcessTextNode(INode textNode)
        {
            if (inPreformatted)
            {
                buffer.Append(textNode.TextContent);
            }
            else
            {
                //if its not only whitespace add it.
                if (textNode.TextContent.Trim().Length > 0)
                {
                    var text = TextConverter.CollapseWhitespace(textNode.TextContent);
                    if (buffer.AtLineStart)
                    {
                        buffer.Append(text.TrimStart());
                    }
                    else
                    {
                        buffer.Append(text);
                    }
                }
                //if its whitepsace, but doesn't have a newline
                else if (!textNode.TextContent.Contains('\n'))
                {
                    if (buffer.AtLineStart)
                    {
                        buffer.Append(textNode.TextContent.TrimStart());
                    }
                    else
                    {
                        buffer.Append(textNode.TextContent);
                    }
                }
            }
        }

        private void ProcessHtmlElement(HtmlElement element)
        {
            var nodeName = element?.NodeName.ToLower();

            if (!ShouldProcessElement(element, nodeName))
            {
                return;
            }

            switch (nodeName)
            {
                case "a":
                    ProcessAnchor(element);
                    break;

                case "blockquote":
                    buffer.EnsureAtLineStart();
                    buffer.InBlockquote = true;
                    ParseChildern(element);
                    buffer.InBlockquote = false;
                    break;

                case "br":
                    buffer.AppendLine();
                    break;

                case "dd":
                    buffer.EnsureAtLineStart();
                    buffer.SetLineStart("* ");
                    ParseChildern(element);
                    buffer.EnsureAtLineStart();
                    break;

                case "dt":
                    buffer.EnsureAtLineStart();
                    ParseChildern(element);
                    if (!buffer.AtLineStart)
                    {
                        buffer.AppendLine(":");
                    }
                    break;

                case "figure":
                    ProcessFigure(element);
                    break;

                case "h1":
                    buffer.EnsureAtLineStart();
                    buffer.SetLineStart("# ");
                    ParseChildern(element);
                    break;

                case "h2":
                    buffer.EnsureAtLineStart();
                    buffer.SetLineStart("## ");
                    ParseChildern(element);
                    break;

                case "h3":
                    buffer.EnsureAtLineStart();
                    buffer.SetLineStart("### ");
                    ParseChildern(element);
                    break;

                case "i":
                    if (ShouldUseItalics(element))
                    {
                        buffer.Append("\"");
                        ParseChildern(element);
                        buffer.Append("\"");
                    }
                    else
                    {
                        ParseChildern(element);
                    }
                    break;

                case "img":
                    ProcessImg(element);
                    break;

                case "li":
                    ProcessLi(element);
                    break;

                case "ol":
                    ProcessList(element);
                    break;

                case "p":
                    buffer.EnsureAtLineStart();
                    int size = buffer.Content.Length;
                    ParseChildern(element);
                    //make sure the paragraph ends with a new line
                    buffer.EnsureAtLineStart();
                    if (buffer.Content.Length > size)
                    {
                        //add another blank line if this paragraph had content
                        //if we had links to flush, there already is an empty line so skip
                        if (!FlushLinkBuffer())
                        {
                            buffer.AppendLine();
                        }
                    }
                    break;

                case "pre":
                    buffer.EnsureAtLineStart();
                    buffer.AppendLine("```");
                    inPreformatted = true;
                    ParseChildern(element);
                    buffer.EnsureAtLineStart();
                    inPreformatted = false;
                    buffer.AppendLine("```");
                    break;

                case "sub":
                    ProcessSub(element);
                    break;

                case "sup":
                    ProcessSup(element);
                    break;

                case "table":
                    ProcessTable(element);
                    break;

                case "u":
                    buffer.Append("_");
                    ParseChildern(element);
                    buffer.Append("_");
                    break;

                case "ul":
                    ProcessList(element);
                    break;

                //skipping tags
                //header stuff
                case "head":
                case "meta":
                case "link":
                case "style":

                //body content
                case "figcaption": //we have special logic to handle figures. If we encounter a figcaption outside of a figure we want to ignore it
                case "noscript":
                case "script":
                case "svg":
                    return;

                default:
                    ProcessGenericTag(element);
                    break;
            }

            HandlePendingLinks(element);
        }

        public bool ShouldProcessElement(HtmlElement element, string normalizedTagName)
        {
            //A MathElement is of type element, but it not an HtmlElement
            //so it will be null
            if (element == null)
            {
                return false;
            }

            //see if we are explicitly filtering
            if (!DomFilter.Global.IsElementAllowed(element, normalizedTagName))
            {
                return false;
            }

            //check the ARIA role
            if (ShouldSkipRole(element.GetAttribute("role")?.ToLower()))
            {
                return false;
            }

            //is it visible?
            if (IsInvisible(element))
            {
                return false;
            }

            return true;
        }

        private void HandlePendingLinks(HtmlElement element)
        {
            if(linkBuffer.Count > 0 && ShouldDisplayAsBlock(element))
            {
                FlushLinkBuffer();
                buffer.EnsureAtLineStart();
            }
        }

        private bool ShouldSkipRole(string role)
        {
            if (string.IsNullOrEmpty(role))
            {
                return false;
            }
            if (role is "button" or
                "checkbox" or
                "form" or
                "searchbox" or
                "search" or
                "slider" or
                "switch")
            {
                return true;
            }
            return false;
        }

        //should we use apply italic formatting around this element?
        private bool ShouldUseItalics(HtmlElement element)
        {
            var siblingTag = element.NextElementSibling?.NodeName?.ToLower() ?? "";
            if (siblingTag == "sub" || siblingTag == "sup")
            {
                return false;
            }
            return true;
        }

        private static bool IsInvisible(HtmlElement element)
           => element.GetAttribute("style")?.Contains("display:none") ?? false;

        private void ProcessAnchor(HtmlElement anchor)
        {
            ParseChildern(anchor);
            //
            //we only care about meaningful links
            //so we can check to see if this anchor had any non-whitespace text
            //(note, A tags with only an IMG inside is common, but we handle that
            //by have a link to media already. No reason to also have a hyperlink
            if (anchor.TextContent.Trim().Length > 0)
            {
                var link = CreateLink(anchor);
                if (link != null)
                {
                    if (ShouldRenderHyperlinks)
                    {
                        buffer.Append($"[{link.OrderDetected}]");
                        linkBuffer.Add(link);
                    }
                    BodyLinks.AddLink(link);
                }
            }
        }

        private void ProcessGenericTag(HtmlElement element)
        {
            if (ShouldDisplayAsBlock(element))
            {
                buffer.EnsureAtLineStart();
                ParseChildern(element);
                buffer.EnsureAtLineStart();
            }
            else
            {
                ParseChildern(element);
            }
        }

        private void ProcessFigure(HtmlElement figure)
            => HandleImage(mediaConverter.ConvertFigure(figure));

        private void ProcessImg(HtmlElement img)
            => HandleImage(mediaConverter.ConvertImg(img));

        private void HandleImage(Image image)
        {
            if (image != null && ShouldUseImage(image))
            {
                Images.Add(image);
                buffer.EnsureAtLineStart();
                buffer.AppendLine($"=> {GetImageUrl(image.Source)} Image: {image.Caption}");
            }
        }

        private void ProcessLi(HtmlElement li)
        {
            if (listDepth == 1)
            {
                buffer.EnsureAtLineStart();
                buffer.SetLineStart("* ");
                ParseChildern(li);
                buffer.EnsureAtLineStart();
            }
            else
            {
                buffer.EnsureAtLineStart();
                buffer.SetLineStart("* * ");
                ParseChildern(li);
                buffer.EnsureAtLineStart();
            }
        }

        private void ProcessList(HtmlElement element)
        {
            //block element
            buffer.EnsureAtLineStart();
            listDepth++;
            ParseChildern(element);
            listDepth--;
            buffer.EnsureAtLineStart();
        }

        private void ProcessSub(HtmlElement element)
        {
            var content = element.TextContent.Trim();
            if (content.Length > 0)
            {
                var subConverter = new SubscriptConverter();
                if (subConverter.Convert(content))
                {
                    //we successfully converted everything
                    buffer.Append(subConverter.Converted);
                }
                //couldn't convert, fall back to using ⌄ ...
                else if (content.Length == 1)
                {
                    buffer.Append("˅");
                    buffer.Append(content);
                }
                else
                {
                    buffer.Append("˅(");
                    buffer.Append(content);
                    buffer.Append(")");
                }
            }
        }

        private void ProcessSup(HtmlElement element)
        {
            var content = element.TextContent.Trim();
            if (content.Length > 0)
            {
                var supConverter = new SuperscriptConverter();
                if (supConverter.Convert(content))
                {
                    //we successfully converted everything
                    buffer.Append(supConverter.Converted);
                }
                //couldn't convert, fall back to using ^...
                else if (content.Length == 1)
                {
                    buffer.Append("^");
                    buffer.Append(content);
                }
                else
                {
                    buffer.Append("^(");
                    buffer.Append(content);
                    buffer.Append(")");
                }
            }
        }

        private void ProcessTable(HtmlElement element)
        {
            //TODO: sanity check table
            TableParser parser = new TableParser();
            var table = parser.ParseTable(element);
            if (table != null && !buffer.InBlockquote)
            {
                buffer.EnsureAtLineStart();
                buffer.Append(TableRenderer.RenderTable(table));
                buffer.EnsureAtLineStart();
            }
        }

        public bool ShouldUseImage(Image image)
            => (Images.Where(x => (x.Source == image.Source)).FirstOrDefault() == null);

        public static bool ShouldDisplayAsBlock(HtmlElement element)
        {
            var nodeName = element.NodeName.ToLower();
            if (!blockElements.Contains(nodeName))
            {
                return false;
            }
            //its a block, display it as inline?
            return !IsInline(element);
        }

        private Hyperlink CreateLink(HtmlElement a)
        {
            Uri url = null;
            var href = a.GetAttribute("href") ?? "";

            //Skip navigation links to parts of the same page
            if (href.StartsWith('#'))
            {
                return null;
            }

            //only allow valid, fully qualified URLs
            try
            {
                url = new Uri(BaseUrl, href);
            }
            catch (Exception)
            {
                url = null;
            }
            if (url == null || !url.IsAbsoluteUri)
            {
                return null;
            }
            //if it points to the current URL, skip it
            if(url.Equals(BaseUrl))
            {
                return null;
            }

            //ignore JS links, since those won't do anything in a Gemini client
            if(url.Scheme == "javascript")
            {
                return null;
            }

            if (!AllowDuplicateLinks && BodyLinks.ContainsUrl(url))
            {
                return null;
            }

            linkCounter++;

            return new Hyperlink
            {
                OrderDetected = linkCounter,
                Text = a.TextContent.Trim(),
                Url = url,
                IsExternal = IsExternalLink(url)
            };
        }

        private bool IsExternalLink(Uri url)
            => !url.Host.EndsWith(BaseUrl.Host);

        private string GetImageUrl(Uri url)
            => (ImageRewriteCallback != null) ?
                ImageRewriteCallback(url) :
                url.AbsoluteUri;

        private string GetAnchorUrl(Uri url)
            => (AnchorRewriteCallback != null) ?
                AnchorRewriteCallback(url) :
                url.AbsoluteUri;

        private static bool IsInline(HtmlElement element)
            => element.GetAttribute("style")?.Contains("display:inline") ?? false;
    }
}
