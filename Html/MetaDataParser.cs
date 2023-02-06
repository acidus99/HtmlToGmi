using System;
using System.Linq;

using AngleSharp.Dom;

using HtmlToGmi.Models;

namespace HtmlToGmi.Html
{
	/// <summary>
    /// Extracts meta data from the HTML
    /// </summary>
	public class MetaDataParser
	{

		IElement Head;
		Uri PageUrl;

		OpenGraphData OpenGraph;

		public MetaDataParser(Uri url, IElement head)
		{
			PageUrl = url;
			Head = head;
        }

		public HtmlMetaData GetMetaData()
		{
			if (Head != null)
			{
                OpenGraph = new OpenGraphData(Head);
                return new HtmlMetaData
				{
					FeedUrl = FindFeedUrl(),
					MetaTitle = GetMetaTitle(),

					OpenGraphDescription = Normalize(OpenGraph.Description),
					OpenGraphImage = CreateUrl(OpenGraph.Image),
					OpenGraphTitle = Normalize(OpenGraph.Title),
					OpenGraphSiteName = Normalize(OpenGraph.SiteName),
					OpenGraphType = Normalize(OpenGraph.Type),
				};
			}
			return new HtmlMetaData();
		}

		private string Normalize(string s)
			=> s?.Replace("\n"," ").Trim() ?? "";

		private string GetMetaTitle()
			=> Normalize(Head.QuerySelector("title")?.TextContent);

		//create fully qualified URLs from a string
        private Uri CreateUrl(string url)
        {
            try
            {
				if (!string.IsNullOrEmpty(url))
				{
					Uri ret = new Uri(PageUrl, url);
					if (ret.IsAbsoluteUri)
					{
						return ret;
					}
				}
            }
            catch (Exception)
            {
            }
            return null;
        }

		private Uri FindFeedUrl()
        {
            var link = Head.QuerySelectorAll("link")
                .Where(x => (x.GetAttribute("rel") == "alternate") &&
                            x.HasAttribute("href") &&
                            (x.GetAttribute("type") == "application/rss+xml" ||
                             x.GetAttribute("type") == "application/atom+xml") &&
							 !(x.GetAttribute("title") ?? "").ToLower().Contains("comment"))
                .FirstOrDefault();

			return (link != null) ?
				CreateUrl(link.GetAttribute("href")) :
				null;
        }


        private class OpenGraphData
		{
			public OpenGraphData(IElement head)
			{
				foreach(var element in head.QuerySelectorAll("meta[property]"))
                {
					string content = element.GetAttribute("content") ?? "";

					switch(element.GetAttribute("property").ToLower())
                    {
						case "og:image":
							Image = content;
							break;
						case "og:site_name":
							SiteName = content;
							break;
						case "og:title":
							Title = content;
							break;
						case "og:type":
							Type = content;
							break;
						case "og:description":
							Description = content;
							break;
                    }
                }
			}

			public string Title { get; set; }
			public string Type { get; set; }
			public string Image { get; set; }
			public string SiteName { get; set; }
			public string Description { get; set; }
		}
	}
}