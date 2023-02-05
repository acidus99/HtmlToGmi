using System;
namespace HtmlToGmi.Models
{
	public class HtmlMetaData
	{
        public Uri FeedUrl { get; set; }
        public string MetaTitle { get; set; }

        public string OpenGraphDescription { get; set; }
        public Uri OpenGraphImage { get; set; }
        public string OpenGraphSiteName { get; set; }
        public string OpenGraphTitle { get; set; }
        public string OpenGraphType { get; set; }

        public bool HasTitle
            => !string.IsNullOrEmpty(Title);

        public string Title
            => !string.IsNullOrEmpty(OpenGraphTitle) ? OpenGraphTitle : MetaTitle;

    }
}

