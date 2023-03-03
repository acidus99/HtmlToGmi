using System;
using System.Linq;
using System.Collections.Generic;

namespace HtmlToGmi.Models
{
    public class LinkCollection
    {
        public int Count => links.Count;

        Dictionary<Uri, Hyperlink> links = new Dictionary<Uri, Hyperlink>();

        Dictionary<string, bool> seenText = new Dictionary<string, bool>();

        int counter = 0;

        public void AddLink(Hyperlink link)
            => AddLink(link.Url, link.Text, link.IsExternal);

        public void AddLink(Uri url, string linkText, bool isExternal)
        {
            string normalized = linkText?.ToLower() ?? "";

            //does this Url already exist?
            if (!ContainsUrl(url))
            {
                //has this link text already been used?
                if (!seenText.ContainsKey(normalized))
                {

                    seenText.Add(normalized, true);
                    //add it
                    counter++;
                    links.Add(url, new Hyperlink
                    {
                        Text = linkText,
                        Url = url,
                        OrderDetected = counter,
                        IsExternal = isExternal
                    });
                }
            }
            else if (linkText.Length > 0 && !seenText.ContainsKey(normalized))
            {
                //URL already exists, but link text doesn't.
                //so is this link text "better" that what we are using?
                if (links[url].Text.Length < linkText.Length)
                {
                    links[url].Text = linkText;
                    seenText.Add(normalized, true);
                }
            }
        }

        public void AddLinks(IEnumerable<Hyperlink> links)
        {
            foreach(var link in links)
            {
                AddLink(link);
            }
        }

        public bool ContainsUrl(Uri url)
            => links.ContainsKey(url);

        public void RemoveLinks(List<Hyperlink> linksToRemove)
            => linksToRemove.ForEach(x => links.Remove(x.Url));

        public List<Hyperlink> GetLinks()
            => links.Values.OrderBy(x => x.OrderDetected).ToList();
    }
}