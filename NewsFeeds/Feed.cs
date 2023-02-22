using System;
using System.Collections.Generic;

namespace HtmlToGmi.NewsFeeds
{
    /// <summary>
    /// Represents a news feed. All properties have been parsed and normalized
    /// </summary>
    public class Feed
    {
        public string Description { get; internal set; }
        public Uri FeaturedImage { get; internal set; }
        public int OriginalSize { get; internal set; }
        public string Title { get; internal set; }
        public string SiteName { get; internal set; }

        public List<FeedItem> Items { get; internal set; } = new List<FeedItem>();
    }
}
