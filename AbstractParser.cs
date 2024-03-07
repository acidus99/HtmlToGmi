using System;
using System.Net;
using System.Text.RegularExpressions;

namespace HtmlToGmi
{
	public abstract class AbstractParser
	{
        //the URL of the page we are parsing content from. This is used to properly resolve URLs
        protected Uri BaseUrl;

        //replaces runs of whitespace
        private static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public AbstractParser(Uri baseUrl = null)
        {
            BaseUrl = baseUrl;
        }

        protected bool IsExternalLink(Uri url)
        {
            if (BaseUrl == null)
            {
                throw new NullReferenceException("baseUrl is ");
            }
            return !url.Host.EndsWith(BaseUrl.Host);
        }

        /// <summary>
        /// Creates a fully qualified, HTTP(S) URL from a string.
        /// The base URL for the page is used to resolve any relative urls
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        protected Uri CreateHttpUrl(string url)
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
        protected string NormalizeText(string s)
        {
            s = s?.Trim() ?? "";
            return CollapseWhitespace(s);
        }


        /// <summary>
        /// normalizes a string that can contain HTML tags to something that is safe to use in a single line of gemtext
        /// - HTML decodes it
        /// - strips any remaining HTML tags
        /// - converts \n, \t, and \r tabs to space
        /// - collapses runs of whitespace into a single space
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        protected string NormalizeHtmlText(string s)
        {
            if (s == null)
            {
                return "";
            }

            //decode
            s = WebUtility.HtmlDecode(s);
            //strip tags
            s = Regex.Replace(s, @"<[^>]*>", "");
            if (s.Contains('\t'))
            {
                s.Replace('\t', ' ');
            }
            return CollapseWhitespace(s);
        }

        protected string CollapseWhitespace(string text)
        {
            if (text.Length > 0 && (text.Contains('\n') || text.Contains('\r')))
            {
                text = text.Replace('\r', ' ');
                text = text.Replace('\n', ' ');
                text = whitespace.Replace(text, " ");
                text = text.Trim();
            }
            return text;
        }


    }
}

