using System;
using System.Text.RegularExpressions;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Models;

namespace HtmlToGmi.Html
{
    public class MediaConverter
    {
        //older sites using non-native lazy loading will have source as a data-src attribute
        static readonly string[] imgSourceAttributes = { "data-src", "data-lazy-src", "src" };

        static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        Uri BaseUrl;

        public MediaConverter(Uri basePageUrl)
        {
            BaseUrl = basePageUrl;
        }

        /// <summary>
        /// Attempts to convert an IMG tag into a Image using
        /// the IMG ALT attribute as the caption
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public Image ConvertImg(HtmlElement img)
        {
            var url = GetUrl(img);
            if(url == null)
            {
                return null;
            }
            return new Image
            {
                Caption = GetAlt(img),
                Source = url
            };
        }

        /// <summary>
        /// Attempts to convert a FIGURE tag into a Image, using
        /// the IMG tag for the image, and the FIGCAPTION tag, or the IMG ALT
        /// as the caption
        /// </summary>
        /// <param name="figure"></param>
        /// <returns></returns>
        public Image ConvertFigure(HtmlElement figure)
        {
            var img = figure.QuerySelector("img");
            //can't find an image? Nothing we can do
            if(img == null)
            {
                return null;
            }

            var url = GetUrl(img);
            //no link? nothing I can do
            if (url == null)
            {
                return null;
            }

            return new Image
            {
                Caption = FindCaption(figure, img),
                Source = url
            };
        }

        private string FindCaption(IElement figure, IElement img)
        {
            var ret = GetFigCaption(figure);
            return (ret.Length > 0) ? ret : GetAlt(img);
        }

        private string GetAlt(IElement img)
        {
            var caption = img.GetAttribute("alt") ?? "";
            caption = TextConverter.CollapseWhitespace(caption);
            return caption.Length > 0 ? caption : "Article Image";
        }

        private string GetFigCaption(IElement figure)
            //TODO: I think this should handle the HTNL decoding as well?
            //maybe "textContent" already does that
            => TextConverter.CollapseWhitespace(figure.QuerySelector("figcaption")?.TextContent ?? "");

        private Uri GetUrl(IElement img)
        {
            foreach(string attrib in imgSourceAttributes)
            {
                if(img.HasAttribute(attrib))
                {
                    var url = img.GetAttribute(attrib).Trim();
                    if(!string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            var uri = new Uri(BaseUrl, url);
                            if (uri.Scheme.StartsWith("http"))
                            {
                                return uri;
                            }
                        } catch(Exception)
                        {
                        }
                    }
                }
            }
            return null;
        }
    }
}