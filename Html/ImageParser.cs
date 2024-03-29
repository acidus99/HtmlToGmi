﻿using System;
using System.Linq;

using AngleSharp.Html.Dom;
using AngleSharp.Dom;

using HtmlToGmi.Models;

namespace HtmlToGmi.Html
{
	public class ImageParser : AbstractParser
	{
        //older sites using non-native lazy loading will have source as a data-src attribute
        static readonly string[] imgSourceAttributes = { "data-src", "data-lazy-src", "src" };

        public string DefaultCaption { get; set; } = "Article Image";

        public ImageParser(Uri baseUrl = null)
            :base(baseUrl)
        { }

        /// <summary>
        /// Attempts to parse an IMG tag into an ImageLink
        /// URL is resolved from the src attribute
        /// Caption comes from the alt or title attribute
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        public ImageLink? ParseImg(IHtmlImageElement img)
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
                IsMap = IsImageMap(img),
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
            IHtmlImageElement img = figure.QuerySelector("img") as IHtmlImageElement;
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
                IsMap = IsImageMap(img),
                Source = url
            };
        }

        private bool IsImageMap(IHtmlImageElement img)
            => (img.UseMap?.Length > 0);

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
            var caption = img.GetAttribute("alt")?.Trim() ?? "";
            if (string.IsNullOrEmpty(caption))
            {
                caption = img.GetAttribute("title")?.Trim() ?? "";
            }
            caption = NormalizeHtmlText(caption);

            return CaptionOrDefault(caption);
        }

        private string GetCaptionForFigure(HtmlElement figure, IElement img)
        {
            //look for a figcaption
            var caption = NormalizeText(figure.QuerySelector("figcaption")?.TextContent ?? "");
            
            if (caption != "")
            {
                return CaptionOrDefault(caption);
            }

            //if we didn't get it, fallback to a caption from img tag attributes
            return caption = GetCaptionForImage(img); ;
        }

        /// <summary>
        /// Gets the fully qualified URL that points to the source of the image
        /// Checks different attributes used to store img URLs and attempts to resolve an absolute URL.
        /// </summary>
        private Uri GetImageUrl(IHtmlImageElement img)
        {
            //try srcset first
            Uri url = GetImageUrlFromSrcSet(img);
            if(url != null)
            {
                return url;
            }

            //otherwise fall back to legacy lazyload attributes, then src
            foreach (string attrib in imgSourceAttributes)
            {
                if (img.HasAttribute(attrib))
                {
                    url = CreateHttpUrl(img.GetAttribute(attrib));
                    if (url != null)
                    {
                        return url;
                    }
                }
            }
            return null;
        }

        private Uri GetImageUrlFromSrcSet(IHtmlImageElement img)
        {
            if(!string.IsNullOrEmpty(img.SourceSet))
            {
                var srcEntry = img.SourceSet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if(srcEntry != null)
                {
                    var src = srcEntry.Split(' ').FirstOrDefault();
                    return CreateHttpUrl(src);
                }
            }
            return null;
        }


        public static bool IsSpacerImage(IHtmlImageElement img)
        {

            //we look for >0 because img tags with a specified width/heigh return 0
            if ((img.DisplayWidth > 0 && img.DisplayWidth < 7) ||
                (img.DisplayHeight > 0 && img.DisplayHeight < 7))
            {
                return true;
            }

            var src = img.Source?.ToLower() ?? "";
            if(src.Contains("spacer.") || src.Contains("1x1."))
            {
                return true;
            }
            return false;
        }
    }
}

