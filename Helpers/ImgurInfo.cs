using Imgur.API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace KTrackPlus.Helpers
{
    public class SimpleImgurInfo
    {
        public string Url { get; set; }
        public string Thumbnail { get; set; }
        public long Timestamp {get; set; }

        public SimpleImgurInfo(ImgurInfo imgurInfo, long tt)
        {
            Url = imgurInfo.Original;
            Thumbnail = imgurInfo.SmallSquare;
            Timestamp = tt;
        }
    }

    public class ImgurInfo // Original code from Greenshot
    {

        public string Hash { get; set; }

        private string _deleteHash;

        public string DeleteHash
        {
            get { return _deleteHash; }
            set
            {
                _deleteHash = value;
                DeletePage = "https://imgur.com/delete/" + value;
            }
        }

        public SimpleImgurInfo ToSimpleClass(long tt)
        {
            return new SimpleImgurInfo(this, tt);
        }

        public string Title { get; set; }

        public string ImageType { get; set; }

        public DateTime Timestamp { get; set; }

        public string Original { get; set; }

        public string Page { get; set; }

        public string SmallSquare { get; set; }

        public string LargeThumbnail { get; set; }

        public string DeletePage { get; set; }


        public static ImgurInfo ParseResponse(string response)
        {
            //Log.Debug(response);
            // This is actually a hack for BUG-1695
            // The problem is the (C) sign, we send it HTML encoded "&reg;" to Imgur and get it HTML encoded in the XML back 
            // Added all the encodings I found quickly, I guess these are not all... but it should fix the issue for now.
            response = response.Replace("&cent;", "&#162;");
            response = response.Replace("&pound;", "&#163;");
            response = response.Replace("&yen;", "&#165;");
            response = response.Replace("&euro;", "&#8364;");
            response = response.Replace("&copy;", "&#169;");
            response = response.Replace("&reg;", "&#174;");

            ImgurInfo imgurInfo = new ImgurInfo();
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(response);
                XmlNodeList nodes = doc.GetElementsByTagName("id");
                if (nodes.Count > 0)
                {
                    imgurInfo.Hash = nodes.Item(0)?.InnerText;
                }

                nodes = doc.GetElementsByTagName("hash");
                if (nodes.Count > 0)
                {
                    imgurInfo.Hash = nodes.Item(0)?.InnerText;
                }

                nodes = doc.GetElementsByTagName("deletehash");
                if (nodes.Count > 0)
                {
                    imgurInfo.DeleteHash = nodes.Item(0)?.InnerText;
                }

                nodes = doc.GetElementsByTagName("type");
                if (nodes.Count > 0)
                {
                    imgurInfo.ImageType = nodes.Item(0)?.InnerText;
                }

                nodes = doc.GetElementsByTagName("title");
                if (nodes.Count > 0)
                {
                    imgurInfo.Title = nodes.Item(0)?.InnerText;
                }

                nodes = doc.GetElementsByTagName("datetime");
                if (nodes.Count > 0)
                {
                    // Version 3 has seconds since Epoch
                    if (double.TryParse(nodes.Item(0)?.InnerText, out var secondsSince))
                    {
                        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        imgurInfo.Timestamp = epoch.AddSeconds(secondsSince).DateTime;
                    }
                }

                nodes = doc.GetElementsByTagName("original");
                if (nodes.Count > 0)
                {
                    imgurInfo.Original = nodes.Item(0)?.InnerText.Replace("http:", "https:");
                }

                // Version 3 API only has Link
                nodes = doc.GetElementsByTagName("link");
                if (nodes.Count > 0)
                {
                    imgurInfo.Original = nodes.Item(0)?.InnerText.Replace("http:", "https:");
                }

                nodes = doc.GetElementsByTagName("imgur_page");
                if (nodes.Count > 0)
                {
                    imgurInfo.Page = nodes.Item(0)?.InnerText.Replace("http:", "https:");
                }
                else
                {
                    // Version 3 doesn't have a page link in the response
                    imgurInfo.Page = $"https://imgur.com/{imgurInfo.Hash}";
                }

                nodes = doc.GetElementsByTagName("small_square");
                imgurInfo.SmallSquare = nodes.Count > 0 ? nodes.Item(0)?.InnerText : $"https://i.imgur.com/{imgurInfo.Hash}s.png";
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not parse Imgur response due to error {0}, response was: {1}", e.Message, response);
            }

            return imgurInfo;
        }
    }
}
