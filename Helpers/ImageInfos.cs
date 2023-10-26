using Android.Content;
using Android.Database;
using Android.Graphics;
using Android.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using static Java.Util.Jar.Attributes;

namespace KTrackPlus.Helpers
{
    public class ImageInfos
    {
        public string Name { get; set; }
        public string ContentType { get; set; }
        public Func<Stream> GetStream { get; set; }

        public ImageInfos(string name, string contentType, Func<Stream> getStream)
        {
            Name = name;
            ContentType = contentType;
            GetStream = getStream;
        }

        public ImageInfos(FileResult result)
        {
            Name = result.FileName;
            ContentType = result.ContentType;
            GetStream = delegate
            {
                return result.OpenReadAsync().Result;
            };
        }



        public ImageInfos(ContentResolver resolver, Android.Net.Uri uri, string contentType)
        {
            var name = uri.LastPathSegment;
            Name = name != null ? name : "photo";
           
            ContentType = contentType;
            GetStream = delegate
            {
                return resolver.OpenInputStream(uri);
            };
        }

    }
}
