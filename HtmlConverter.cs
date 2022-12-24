using System;
using System.Collections.Generic;

using AngleSharp.Dom;

using HtmlToGmi.Models;
using HtmlToGmi.Special;

namespace HtmlToGmi
{
	public class HtmlConverter
	{
        public ConvertedContent Convert(Uri url, IElement element)
        {

			HtmlTagParser parser = new HtmlTagParser();
			parser.Parse(element);

			return new ConvertedContent
			{
				Gemtext = parser.Gemtext,
                Images = parser.Images,
                Links = parser.BodyLinks.GetLinks(),
                Url = url
            };
        }

    }
}

