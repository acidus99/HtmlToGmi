using System;
using CodeHollow.FeedReader;
using System.IO;
using CodeHollow.FeedReader.Feeds;
using System.Linq;

namespace HtmlToGmi.NewsFeeds
{
	public class FeedConverter : AbstractParser
	{
		public FeedConverter()
            : base(null)
		{
            /*
             * explicitly setting the null, though I will be using the common
             * URL creation functions
             * RSS/Atom feeds say feeds should not contain relative URLs. However
             * if they can via the channel tag. I'll support this in the future by 
             * first checking for that tag, and then setting BaseUri, so that the create
             * functions will use that.
             */
		}

        /// <summary>
        /// create the feed and all necessarily objects under that. Properties are validated.
        /// (E.g. feed items with invalid URLs will not be added)
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public Feed Convert(string xml)
        {
            var sourceFeed = FeedReader.ReadFromString(xml);
            return Convert(sourceFeed);
        }

        public Feed Convert(CodeHollow.FeedReader.Feed sourceFeed)
        {
            var ret = new Feed
            {
                Description = NormalizeHtmlText(sourceFeed.Description),
                FeaturedImage = CreateHttpUrl(sourceFeed.ImageUrl),
                OriginalSize = sourceFeed.OriginalDocument.Length,
                Title = NormalizeHtmlText(sourceFeed.Title),
                SiteName = NormalizeHtmlText(sourceFeed.Copyright)
            };

            //ensure items have valid URLs
            ret.Items = sourceFeed.Items
                .Select(x => CreateFeedItem(x))
                .Where(x => (x.Url != null))
                .ToList();

            return ret;
        }

        private FeedItem CreateFeedItem(CodeHollow.FeedReader.FeedItem sourceItem)
            => new FeedItem
            {
                Title = NormalizeHtmlText(sourceItem.Title),
                Url = CreateHttpUrl(GetArticleLink(sourceItem)),

                Description = NormalizeHtmlText(sourceItem.Description),
                Published = sourceItem.PublishingDate,

                Enclosure = CreateFeedEnclosure(sourceItem)
            };

        /// <summary>
        /// creates a feed enclosure for an feed item.
        /// /// </summary>
        /// <param name="sourceItem"></param>
        /// <returns>an object
        /// if the item has a enclosure which has a valid URL. Otherwise returns null
        /// </returns>
        private FeedEnclosure CreateFeedEnclosure(CodeHollow.FeedReader.FeedItem sourceItem)
        {
            var sourceEnclosure = GetEnclosure(sourceItem);
            if (sourceEnclosure == null)
            {
                return null;
            }

            var url = CreateHttpUrl(sourceEnclosure.Url);
            if (url == null)
            {
                //a enclosure with out a valid URL should be discarded/ignored
                return null;
            }
            return new FeedEnclosure
            {
                Url = url,
                Length = sourceEnclosure.Length,
                MediaType = sourceEnclosure.MediaType
            };
        }

        private FeedItemEnclosure GetEnclosure(CodeHollow.FeedReader.FeedItem item)
            => (item.SpecificItem is Rss20FeedItem) ?
                ((Rss20FeedItem)item.SpecificItem).Enclosure :
                null;

        /// <summary>
        /// gets the link to the HTML article for a feed item. Some feeds types offer
        /// multiple links, so abstracting the logic
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetArticleLink(CodeHollow.FeedReader.FeedItem feedItem)
            => (feedItem.SpecificItem is AtomFeedItem) ?
                    GetArticleLink(feedItem.SpecificItem as AtomFeedItem) :
                    feedItem.Link;
        
        /// <summary>
        /// atom feed can have multiple links tags, so find the appropriate one, with a fallback
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private string GetArticleLink(AtomFeedItem item)
        {
            var htmlLink = item.Links
                .Where(x => x.Relation == "alternate" && x.LinkType == "text/html")
                .FirstOrDefault();
            return htmlLink?.Href ?? item.Link;
        }


    }

}

