using System;

namespace HtmlToGmi.NewsFeeds
{
    /// <summary>
    /// Represents an enclosure from an RSS 2.0 feed
    /// </summary>
	public class FeedEnclosure
	{
        public Uri Url { get; internal set; }

        public int? Length { get; internal set; }

        public string MediaType { get; internal set; }
	}
}

