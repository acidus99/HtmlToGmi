using System;
using System.Linq;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Models;

namespace HtmlToGmi.Html
{
	public class ImageParser
	{
        //older sites using non-native lazy loading will have source as a data-src attribute
        static readonly string[] imgSourceAttributes = { "data-src", "data-lazy-src", "src" };
        Uri BaseUrl;

        public string DefaultCaption { get; set; } = "Article Image";


        public ImageParser(Uri basePageUrl = null)
        {
            BaseUrl = basePageUrl;
        }

        /// <summary>
        /// Attempts to parse an IMG tag into an ImageLink
        /// URL is resolved from the src attribute
        /// Caption comes from the alt or title attribute
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public ImageLink ParseImg(HtmlElement img)
        {
            var url = GetImageUrl(img);
            //if we can't get a valid URL, just stop
            if (url == null)
            {
                return null;
            }
            return new ImageLink
            {
                Caption = GetCaptionForImage(img),
                Source = url
            };
        }

        /// <summary>
        /// Attempts to convert a FIGURE tag into a Image, using
        /// the IMG tag for the image, and the FIGCAPTION tag, img alt, or img title
        /// as the caption
        /// </summary>
        /// <param name="figure"></param>
        /// <returns></returns>
        public ImageLink ParseFigure(HtmlElement figure)
        {
            var img = figure.QuerySelector("img");
            //can't find an image? Nothing we can do
            if (img == null)
            {
                return null;
            }

            var url = GetImageUrl(img);
            //no link? nothing I can do
            if (url == null)
            {
                return null;
            }

            return new ImageLink
            {
                Caption = GetCaptionForFigure(figure, img),
                Source = url
            };
        }

        /// <summary>
        /// Uses text, or default text, if text not present
        /// </summary>
        /// <param name="s"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        private string CaptionOrDefault(string s)
            => s.Length > 0 ? s : DefaultCaption;

        /// <summary>
        /// Attempts to get a caption for an image from the alt or title attribute
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        private string GetCaptionForImage(IElement img)
        {
            var caption = img.GetAttribute("alt") ?? "";
            if (string.IsNullOrEmpty(caption))
            {
                caption = img.GetAttribute("title") ?? "";
            }
            caption = NormalizeText(caption);

            return CaptionOrDefault(caption);
        }

        private string GetCaptionForFigure(HtmlElement figure, IElement img)
        {
            //look for a figcaption
            var caption = NormalizeText(figure.QuerySelector("figcaption")?.TextContent ?? "");
            //if we didn't get it, look for image

            caption = GetCaptionForImage(img);

            //already normalized, already defaulted
            return caption;
        }

        /// <summary>
        /// Gets the fully qualified URL that points to the source of the image
        /// Checks different attributes used to store img URLs and attempts to resolve an absolute URL.
        /// </summary>
        private Uri GetImageUrl(IElement img)
        {
            foreach (string attrib in imgSourceAttributes)
            {
                if (img.HasAttribute(attrib))
                {
                    Uri url = CreateUrl(img.GetAttribute(attrib));
                    if (url != null)
                    {
                        return url;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Normalize a string for use as a caption or description
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string NormalizeText(string s)
            => TextConverter.CollapseWhitespace(s);

        /// <summary>
        /// Creates a fully qualified, HTTP(S) URL from a string.
        /// The base URL for the page is used to resolve any relative urls
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private Uri CreateUrl(string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    //if we have a BaseUrl, use it
                    Uri uri = null;
                    if (BaseUrl != null)
                    {
                        uri = new Uri(BaseUrl, url);
                    }
                    else if (url.Contains("://"))
                    {
                        //already absolute, so we are good
                        uri = new Uri(url);
                    }
                    if (uri.IsAbsoluteUri && uri.Scheme.StartsWith("http"))
                    {
                        return uri;
                    }
                }
            }
            catch (Exception)
            { }
            return null;
        }

    }
}

