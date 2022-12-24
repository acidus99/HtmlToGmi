using System;
namespace HtmlToGmi.Models
{
	public class Hyperlink
	{
		public int OrderDetected { get; set; } = 0;
        public Uri Url { get; init; }
		public string Text { get; set; }

		public int Size
			=> Url.AbsoluteUri.Length + Text.Length + 5;
	}
}

