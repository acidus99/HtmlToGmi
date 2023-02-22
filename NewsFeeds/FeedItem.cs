using System;
namespace HtmlToGmi.NewsFeeds
{
    /// <summary>
    /// Represents an item in a news feed. All properties have been parsed and normalized
    /// </summary>
    public class FeedItem
    {
        public string Title { get; internal set; }

        public string Description { get; internal set; }

        public Uri Url { get; internal set; }

        public DateTime? Published { get; internal set; }

        public FeedEnclosure Enclosure { get; internal set; }

        /// <summary>
        /// Gives a human readable string of when a piece was published,
        /// relative to a provided time
        /// </summary>
        /// <param name="fromDate">the date to be relative to</param>
        /// <returns></returns>
        public string GetTimeAgo(DateTime fromDate)
        {
            if (!Published.HasValue)
            {
                return "";
            }

            var s = fromDate.Subtract(Published.Value);
            int dayDiff = (int)s.TotalDays;
            int secDiff = (int)s.TotalSeconds;

            if (secDiff < 0)
            {
                //relative time is before this publish date
                return "from the future";
            }

            if (dayDiff == 0)
            {
                if (secDiff < 60)
                {
                    return "just now";
                }
                if (secDiff < 120)
                {
                    return "1 minute ago";
                }
                if (secDiff < 3600)
                {
                    return $"{Math.Floor((double)secDiff / 60)} minutes ago";
                }
                if (secDiff < 7200)
                {
                    return "1 hour ago";
                }
                if (secDiff < 86400)
                {
                    return $"{Math.Floor((double)secDiff / 3600)} hours ago";
                }
            }
            if (dayDiff == 1)
            {
                return "yesterday";
            }
            return $"{dayDiff} days ago";
        }

    }
}

