using System;
namespace HtmlToGmi
{
	public abstract class AbstractParser
	{
        //the URL of the page we are parsing content from. This is used to properly resolve URLs
        protected Uri BaseUrl;

        public AbstractParser(Uri baseUrl = null)
        {
            BaseUrl = baseUrl;
        }

        /// <summary>
        /// Creates a fully qualified, HTTP(S) URL from a string.
        /// The base URL for the page is used to resolve any relative urls
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        protected Uri CreateUrl(string url)
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

        /// <summary>
        /// Normalize a string to something that is safe to use in a single line of gemtext
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected string Normalize(string s)
        {
            s = s?.Trim() ?? "";
            return TextConverter.CollapseWhitespace(s);
        }

    }
}

