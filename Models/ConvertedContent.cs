using System;
using System.Collections.Generic;
namespace HtmlToGmi.Models
{
	public class ConvertedContent
	{
		public Uri Url { get; internal init; }
		public string Gemtext { get; internal init; }
		public IEnumerable<Hyperlink> Links { get; internal init; }
		public IEnumerable<Image> Images { get; internal init; }

		public HtmlMetaData MetaData { get; internal init; }
    }
}

