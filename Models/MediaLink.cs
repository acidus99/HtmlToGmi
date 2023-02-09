using System;
namespace HtmlToGmi.Models
{
	public abstract class MediaLink
	{
		public Uri Source { get; set; }
		public string Caption { get; set; }
	}
}

