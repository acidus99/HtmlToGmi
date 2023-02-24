using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;


namespace HtmlToGmi.Encodings
{
	/// <summary>
	/// Decodes bytes into an IHtmlDocument, using a default charset. If any meta tags inside
	/// the document reset the charset, the document is re-decoded with the correct content type
	/// </summary>
	public class HtmlDecoder
	{
		public string SpecifiedCharset { get; private set; }

		public string Html { get; private set; }

		public IHtmlDocument Document { get; private set; }


		static HtmlDecoder()
		{
			//ensure extended codepages are available
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

		public void Decode(byte[] sourceBytes, string defaultCharset = "utf-8")
		{
			//start with an UTF-8 decode
			DecodeWithCharSet(sourceBytes, defaultCharset);

			//now try and find a charset inside the doc
			var charset = FindCharsetInDocument();

			//we got something other than what we have already used to decode...
			if(charset != "" && charset != defaultCharset)
			{
				DecodeWithCharSet(sourceBytes, charset);
			}
		}

		private void DecodeWithCharSet(byte [] sourceBytes, string charset)
		{
			SpecifiedCharset = charset;
            Html = Encoding.GetEncoding(charset).GetString(sourceBytes);
            Document = ParseToDocument(Html);
        }

        private IHtmlDocument ParseToDocument(string html)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>();
            return parser.ParseDocument(html);
        }

		private string FindCharsetInDocument()
		{
			var charset = FindMetaCharset();
			if(charset != "")
			{
				return charset;
			}

			return FindHttpEquivCharset();
		}

		private string FindMetaCharset()
			=> Document.QuerySelectorAll("meta[charset]")
				.FirstOrDefault()?
				.GetAttribute("charset")?.ToLower() ?? "";

        private string FindHttpEquivCharset()
        {
			var contentType = Document.QuerySelectorAll("meta[http-equiv]")
				.Where(x => x.GetAttribute("http-equiv").ToLower() == "content-type")
				.FirstOrDefault()?
				.GetAttribute("content") ?? "";
			try
			{
				if (contentType.Length > 0)
				{
					return MediaTypeHeaderValue.Parse(contentType).CharSet?.ToLower() ?? "";
				}

			} catch (Exception)
			{
				//not a valid content type
			}
			return "";
        }








    }
}

