﻿using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;

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
    public class HtmlConverter : AbstractParser
    {
        private static readonly string[] blockElements = new string[] { "address", "article", "aside", "blockquote", "canvas", "dd", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "noscript", "ol", "p", "pre", "section", "table", "tfoot", "ul", "video" };

        public bool ShouldRenderHyperlinks { get; set; } = true;
        public bool AllowDuplicateLinks { get; set; } = false;

        /// <summary>
        /// Aria roles which mean the element should be skipped
        /// </summary>
        public List<string> RolesToSkip = new List<string>
        {
            "alert", "alertdialog", "button", "checkbox", "dialog", "form",
            "log", "search", "searchbox", "slider", "switch"
        };

        public List<string> ElementsToSkip = new List<string>
        {
            //header stuff
            "head", "meta", "link",

            //presentational info
            "style",

            //interactivity
            "applet",
            "dialog",
            "embed",
            "noembed",
            "object",
            "param",
            "script",
            "template",

            //noscript is rarely valuable, as it usually just tells you to enable JS
            "noscript",

            //form stuff
            "button", "datalist", "fieldset", "form", "input", "keygen", "label", "legend", "optgroup", "option", "select", "textarea",

            //we have special logic to handle these. We shouldn't encounter this tags by themselves while parsing, so ignore them
            "area",
            "figcaption",
        };

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
        List<ImageLink> images = new List<ImageLink>();

        /// <summary>
        /// tmp buffer used to hold links in the current block of text
        /// </summary>
        List<Hyperlink> linkBuffer = new List<Hyperlink>();

        /// <summary>
        /// Unique, "best", list of anchor hyperlinks found in the converted text
        /// </summary>
        LinkCollection bodyLinks = new LinkCollection();

        /// <summary>
        /// tracks how deep we are in a nested list
        /// </summary>
        int listDepth = 0;

        GemtextBuffer buffer = new GemtextBuffer();
        ImageParser imageParser;

        bool inPreformatted = false;

        int linkCounter = 0;

        IHtmlDocument document;
        IElement documentRoot;

        TextConverter linkTextExractor;

        HtmlMetaData metaData;

        bool isProcessingDeferred = false;
        List<HtmlElement> deferredElements = new List<HtmlElement>();

        bool isInOrderedList = false;
        int listItemNumber = 0;

        public HtmlConverter(Uri baseUri = null)
            : base(baseUri)
        { }


        public ConvertedContent Convert(Uri url, string html)
            => Convert(url, ParseToDocument(html));

        public ConvertedContent Convert(Uri url, IHtmlDocument document)
        {
            this.document = document;
            // AngleSharp creates well-formed HTML documents, with an html, head
            // and body tags. 
            documentRoot = this.document.FirstElementChild;

            BaseUrl = url;
            imageParser = new ImageParser(url);
            linkTextExractor = new TextConverter(url)
            {
                ShouldCollapseNewlines = true,
                ShouldConvertImages = true
            };

            ConvertDocument();
            metaData = PopulateMetaData();

            FlushLinkBuffer();
            return new ConvertedContent
            {
                Url = url,
                Gemtext = buffer.Content.TrimEnd(),
                Images = images,
                Links = bodyLinks.GetLinks(),
                MetaData = metaData
            };
        }

        #region Converting Methods

        //Converts the document, body first, and then any deferred elements
        private void ConvertDocument()
        {
            isProcessingDeferred = false;
            deferredElements.Clear();
            ConvertChildren(documentRoot.QuerySelector("body"));
            isProcessingDeferred = true;
            foreach (var element in deferredElements)
            {
                ProcessHtmlElement(element);
            }
        }

        private IHtmlDocument ParseToDocument(string html)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(html);
        }

        private void ConvertChildren(INode node)
        {
            foreach (var child in node.ChildNodes)
            {
                ConvertNode(child);
            }
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

        private void ProcessTextNode(INode textNode)
        {
            var text = textNode.TextContent;

            if (inPreformatted)
            {
                buffer.Append(text);
            }
            else
            {
                //if its not only whitespace add it.
                if (text.Trim().Length > 0)
                {
                    text = CollapseWhitespace(text);
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
                else if (!text.Contains('\n'))
                {
                    if (buffer.AtLineStart)
                    {
                        buffer.Append(text.TrimStart());
                    }
                    else
                    {
                        buffer.Append(text);
                    }
                }
            }
        }

        #endregion

        private void ProcessHtmlElement(HtmlElement element)
        {
            var nodeName = element?.NodeName.ToLower();

            if (ShouldSkipElement(element, nodeName))
            {
                return;
            }

            HandlePendingLinks(element);

            switch (nodeName)
            {
                case "a":
                    ProcessAnchor(element);
                    break;

                case "aside":
                    ProcessAside(element);
                    break;

                case "blockquote":
                    ProcessBlockQuote(element);
                    break;

                case "br":
                    ProcessBR(element);
                    break;

                case "dd":
                    ProcessDD(element);
                    break;

                case "dt":
                    ProcessDT(element);
                    break;

                case "figure":
                    ProcessFigure(element);
                    break;

                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    ProcessHeadingLevel(element);
                    break;

                case "hr":
                    ProcessHR(element);
                    break;

                case "i":
                    ProcessItalics(element);
                    break;

                case "img":
                    ProcessImg(element as IHtmlImageElement);
                    break;

                case "li":
                    ProcessLi(element);
                    break;

                case "map":
                    ProcessMap(element);
                    break;

                case "nav":
                    ProcessNav(element);
                    break;

                case "ol":
                    ProcessOrderedList(element);
                    break;

                case "p":
                    ProcessParagraph(element);
                    break;

                case "pre":
                    ProcessPre(element);
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
                    ProcessUnderline(element);
                    break;

                case "ul":
                    ProcessList(element);
                    break;

                default:
                    ProcessGenericTag(element);
                    break;
            }

            HandlePendingLinks(element);
        }

        #region Element skipping methods 

        private bool ShouldSkipElement(HtmlElement element, string tagName)
        {
            //A MathElement is of type element, but it not an HtmlElement
            //so it will be null
            if (element == null)
            {
                return true;
            }

            if (ElementsToSkip.Contains(tagName))
            {
                return true;
            }

            //ARIA telling us its hidden to screen readers
            if (element.IsHidden || (element.GetAttribute("aria-hidden") ?? "") == "true")
            {
                return true;
            }

            //check the ARIA role
            if (ShouldSkipRole(element.GetAttribute("role")?.ToLower()))
            {
                return true;
            }

            //is it visible?
            if (IsInvisible(element))
            {
                return true;
            }

            return false;
        }

        private bool ShouldSkipRole(string role)
         => RolesToSkip.Contains(role);

        private void HandlePendingLinks(HtmlElement element)
        {
            if(linkBuffer.Count > 0 && ShouldDisplayAsBlock(element))
            {
                FlushLinkBuffer();
                buffer.EnsureAtLineStart();
            }
        }

        private static bool IsInvisible(HtmlElement element)
         => element.GetAttribute("style")?.Contains("display:none") ?? false;

        #endregion

        #region <a> Methods

        private void ProcessAnchor(HtmlElement anchor)
        {
            //should we do anything at all with this?
            if (!ShouldRenderAnchorText(anchor))
            {
                return;
            }

            //is it a real hyperlink we want to use?
            Uri url = CreateUrl(anchor.GetAttribute("href"));

            if (url == null)
            {
                //nope, just process the child, we aren't going to do anything special
                ConvertChildren(anchor);
                return;
            }

            //do we have aria text? That takes precedence for link text
            string linkText = GetAriaLabel(anchor);
            List<ImageLink> images = null;
            if (string.IsNullOrEmpty(linkText))
            {
                linkText = linkTextExractor.Convert(anchor);
                images = linkTextExractor.Images;
            }

            //if we don't have link text after all that, this is probably not a valuable link
            //so skip it
            if (string.IsNullOrEmpty(linkText))
            {
                return;
            }

            linkCounter++;
            var link = new Hyperlink
            {
                OrderDetected = linkCounter,
                Text = linkText,
                Url = url,
                IsExternal = IsExternalLink(url)
            };

            if (!ShouldRenderHyperlinks)
            {
                //not doing anything wild, so just append the text
                buffer.Append(linkText);
            }
            else
            {
                if (buffer.AtLineStart && !buffer.HasLinePrefix)
                {
                    //if we are at the start of a line, just make this a link line
                    //and no reason to use a footnote-style anchor
                    buffer.AppendLine($"=> {GetAnchorUrl(link.Url)} {link.Text}");
                    //rollback linkcounter
                    linkCounter--;
                }
                else
                {
                    //use a footnote and append to the link buffer
                    buffer.Append($"{linkText}[{link.OrderDetected}]");
                    linkBuffer.Add(link);
                }
            }

            if (images != null)
            {
                images.ForEach(x => HandleImage(x));
            }

            bodyLinks.AddLink(link);
        }

        private bool ShouldRenderAnchorText(HtmlElement a)
        {
            if (a.GetAttribute("role")?.ToLower() == "doc-backlink")
            {
                return false;
            }
            else if (a.GetAttribute("rev")?.ToLower() == "footnote")
            {
                return false;
            }
            //many platforms just use this character
            else if (a.TextContent == "↩")
            {
                return false;
            }
            return true;
        }

        private Uri CreateUrl(string href)
        {
            Uri url = null;
            href = href ?? "";

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
            if (url.Equals(BaseUrl))
            {
                return null;
            }

            //ignore JS links, since those won't do anything in a Gemini client
            if (url.Scheme == "javascript")
            {
                return null;
            }

            if (!AllowDuplicateLinks && bodyLinks.ContainsUrl(url))
            {
                return null;
            }
            return url;
        }

        #endregion

        private void ProcessAside(HtmlElement aside)
        {
            if (DeferProcessing(aside))
            {
                return;
            }
            buffer.EnsureAtLineStart();
            buffer.AppendLine("### Aside");
            ConvertChildren(aside);
        }

        private void ProcessBlockQuote(HtmlElement blockQuote)
        {
            buffer.EnsureAtLineStart(true);
            buffer.InBlockquote = true;
            ConvertChildren(blockQuote);
            buffer.InBlockquote = false;
            buffer.EnsureAtLineStart(true);
        }

        private void ProcessBR(HtmlElement br)
        {
            buffer.AppendLine();
        }

        private void ProcessDD(HtmlElement dd)
        {
            buffer.EnsureAtLineStart(true);
            buffer.SetLinePrefix("* ");
            ConvertChildren(dd);
            buffer.EnsureAtLineStart(true);
        }

        private void ProcessDT(HtmlElement dt)
        {
            buffer.EnsureAtLineStart();
            ConvertChildren(dt);
            if (!buffer.AtLineStart)
            {
                buffer.AppendLine(":");
            }
        }

        private void ProcessHeadingLevel(HtmlElement hx)
        {
            int level = System.Convert.ToInt32(hx.NodeName.Substring(1, 1));
            //even though Gemtext doesn't allow for 
            if(level > 3)
            {
                level = 3;
            }
            buffer.EnsureAtLineStart(true);
            buffer.SetLinePrefix("###".Substring(0,level) + " ");
            ConvertChildren(hx);
            buffer.EnsureAtLineStart(true);
        }

        private void ProcessHR(HtmlElement hr)
        {
            buffer.EnsureAtLineStart(true);
            buffer.AppendLine("-=-=-=-=-=-=-=-=-=-=-");
        }

        #region Italics

        private void ProcessItalics(HtmlElement i)
        {
            if (ShouldUseItalics(i))
            {
                buffer.Append("\"");
                ConvertChildren(i);
                buffer.Append("\"");
            }
            else
            {
                ConvertChildren(i);
            }

        }

        //should we use apply italic formatting around this element?
        private bool ShouldUseItalics(HtmlElement element)
        {
            var siblingTag = element.NextElementSibling?.NodeName?.ToLower() ?? "";
            if (siblingTag == "sub" || siblingTag == "sup")
            {
                return false;
            }
            //if there is no content, don't enclose it
            if (element.TextContent.Trim().Length == 0)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Image Elements (<img>, <figure>)

        private void ProcessFigure(HtmlElement figure)
          => HandleImage(imageParser.ParseFigure(figure));

        private void ProcessImg(IHtmlImageElement img)
        {
            if(ImageParser.IsSpacerImage(img))
            {
                return;
            }
            HandleImage(imageParser.ParseImg(img));
        }

        private void HandleImage(ImageLink image)
        {
            if (image != null && ShouldRenderImageLink(image))
            {
                images.Add(image);
                buffer.EnsureAtLineStart(true);
                var label = image.IsMap ? "Image Map" : "Image";
                buffer.AppendLine($"=> {GetImageUrl(image.Source)} {label}: {image.Caption}");
            }
        }



        private bool ShouldRenderImageLink(ImageLink image)
           => (images.Where(x => (x.Source == image.Source)).FirstOrDefault() == null);

        #endregion

        #region List elements (<ul> <ol> <li>)

        private void ProcessOrderedList(HtmlElement ol)
        {
            isInOrderedList = true;

            if (!int.TryParse(ol.GetAttribute("start"), out listItemNumber))
            {
                listItemNumber = 1;
            }
            ProcessList(ol);
            isInOrderedList = false;
        }

        private void ProcessList(HtmlElement element)
        {
            //block element
            buffer.EnsureAtLineStart();
            listDepth++;
            ConvertChildren(element);
            listDepth--;
            buffer.EnsureAtLineStart();
        }

        private void ProcessLi(HtmlElement li)
        {
            var prefix = "* ";
            if (listDepth > 1)
            {
                prefix += "* ";
            }
            if (isInOrderedList)
            {
                prefix += $"{listItemNumber}. ";
                listItemNumber++;
            }
            buffer.EnsureAtLineStart();
            buffer.SetLinePrefix(prefix);
            ConvertChildren(li);
            buffer.EnsureAtLineStart();
        }

        #endregion

        private void ProcessMap(HtmlElement map)
        {
            var links = map.QuerySelectorAll("area")
                .Select(x => ParseAreaToLink(x as HtmlElement))
                .Where(x => x != null);

            //also add the links to the list of body links
            bodyLinks.AddLinks(links);

            if(ShouldRenderHyperlinks)
            {
                buffer.EnsureAtLineStart(true);
                buffer.AppendLine("Image Map Links:");
                foreach (var link in links)
                {
                    buffer.AppendLine($"=> {GetAnchorUrl(link.Url)} {link.Text}");
                    //rollback linkcounter
                    linkCounter--;
                }
                buffer.EnsureAtLineStart(true);
            }
        }

        private Hyperlink ParseAreaToLink(HtmlElement area)
        {
            var url = CreateUrl(area.GetAttribute("href"));
            if (url == null)
            {
                //nothing to do here so more on
                return null;
            }

            var linkText = area.GetAttribute("alt") ?? "";
            if(linkText == "")
            {
                linkText = area.Title ?? "";
            }

            linkCounter++;
            return new Hyperlink
            {
                OrderDetected = linkCounter,
                Text = linkText,
                Url = url,
                IsExternal = IsExternalLink(url)
            };
        }

        private void ProcessNav(HtmlElement nav)
        {

            if (DeferProcessing(nav))
            {
                return;
            }

            var links = nav.QuerySelectorAll("a")
                .Where(x => !ShouldSkipElement(x as HtmlElement, "a"));
            if (links.Count() > 0)
            {
                buffer.EnsureAtLineStart();
                buffer.AppendLine("### Navigation Links");
                foreach (var anchor in links)
                {
                    buffer.EnsureAtLineStart();
                    ProcessAnchor(anchor as HtmlElement);
                    FlushLinkBuffer();
                    buffer.EnsureAtLineStart();
                }
            }
        }

        private void ProcessParagraph(HtmlElement p)
        {
            buffer.EnsureAtLineStart();
            int size = buffer.Content.Length;
            ConvertChildren(p);
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
        }

        private void ProcessPre(HtmlElement pre)
        {
            buffer.EnsureAtLineStart(true);
            buffer.AppendLine("```");
            inPreformatted = true;
            ConvertChildren(pre);
            buffer.EnsureAtLineStart(true);
            inPreformatted = false;
            buffer.AppendLine("```");
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

        #region Table 

        private void ProcessTable(HtmlElement element)
        {
            IHtmlTableElement table = element as IHtmlTableElement;
            if (TableReducer.IsLayoutTable(table))
            {
                ProcessLayoutTable(table);
            }
            else
            {
                ProcessDateTable(element);
            }
        }

        private void ProcessLayoutTable(IHtmlTableElement table)
        {
            var reducer = new TableReducer();
            var div = reducer.ConvertTable(table);
            ConvertChildren(div);
        }

        private void ProcessDateTable(HtmlElement element)
        {
            TableParser parser = new TableParser();
            var table = parser.ParseTable(element);
            if (table.Caption == "")
            {
                //attempt to use an aria label to annotate the table
                table.Caption = GetAriaLabel(element);
            }
            if (table != null && !buffer.InBlockquote)
            {
                buffer.EnsureAtLineStart();
                buffer.Append(TableRenderer.RenderTable(table));
                buffer.EnsureAtLineStart();
            }
        }

        #endregion


        private void ProcessUnderline(HtmlElement u)
        {
            buffer.Append("_");
            ConvertChildren(u);
            buffer.Append("_");
        }

        private bool DeferProcessing(HtmlElement element)
        {
            if(!isProcessingDeferred)
            {
                deferredElements.Add(element);
                return true;
            }
            return false;
        }

        private void ProcessGenericTag(HtmlElement element)
        {
            if (ShouldDisplayAsBlock(element))
            {
                buffer.EnsureAtLineStart();
                ConvertChildren(element);
                buffer.EnsureAtLineStart();
            }
            else
            {
                ConvertChildren(element);
            }
        }


        private bool FlushLinkBuffer()
        {
            bool ret = false;
            if (linkBuffer.Count > 0 && !buffer.InBlockquote)
            {
                //need to reset prefix because line must start with link line character
                buffer.EnsureAtLineStart(true);

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
            if (linkBuffer.Count != 1)
            {
                return false;
            }
            var lastLine = buffer.GetLastLine();
            //bulleted lists can be converted, so trim it off
            if (lastLine.StartsWith("* ") && lastLine.Length >= 3)
            {
                lastLine = lastLine.Substring(2);
            }

            return lastLine == $"{linkBuffer[0].Text}[{linkBuffer[0].OrderDetected}]";
        }

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

        private string GetImageUrl(Uri url)
            => (ImageRewriteCallback != null) ?
                ImageRewriteCallback(url) :
                url.AbsoluteUri;

        private string GetAnchorUrl(Uri url)
            => (AnchorRewriteCallback != null) ?
                AnchorRewriteCallback(url) :
                url.AbsoluteUri;

        private string GetAriaLabel(HtmlElement element)
            => element.GetAttribute("aria-label") ?? "";

        private static bool IsInline(HtmlElement element)
            => element.GetAttribute("style")?.Contains("display:inline") ?? false;

        private HtmlMetaData PopulateMetaData()
        {
            var metaParser = new MetaDataParser(BaseUrl, documentRoot.QuerySelector("head"));
            return metaParser.GetMetaData();
        }

    }
}
