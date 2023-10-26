using Imgur.API.Endpoints;
using Imgur.API.Models;
using Imgur.API;
using Java.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imgur.API.Authentication;
using Android.Util;
using Android.Views;
using System.Net.Http;
using System.Net;
using Xamarin.Essentials;
using Java.Nio.Channels;
using Android.Graphics;
using Android.Media;
using AndroidX.Core.Widget;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace KTrackPlus.Helpers
{
    internal static class ImgurUpload
    {
        static List<ImageInfos> toSend { get; set; } = new();
        static System.Timers.Timer? timer;

        public static void AddToSend(ImageInfos infos)
        {
            lock (toSend)
            {
                toSend.Add(infos);
                System.Console.WriteLine("Add picture to upload list : " + infos.Name);
            }
            if (timer == null)
            {
                timer = new System.Timers.Timer(30 * 1000);
                timer.Elapsed += Timer_Elapsed;
                timer.Start();
            }
        }

        internal static void Clear()
        {
            lock (toSend)
            {
                toSend.Clear();
            }
        }

        private static async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (KTrackService.isRunning && Connectivity.NetworkAccess == NetworkAccess.Internet)
            {

                try
                {
                    var sendCache = new List<ImageInfos>();
                    lock (toSend)
                    {
                        sendCache.AddRange(toSend);
                    }
                    foreach (var infos in sendCache)
                    {
                        ImgurInfo? result = null;
                        try
                        {
                            result = await UploadToImgur(infos);
                        }
                        catch
                        {
                            result = null;
                        }
                        if (result == null)
                        {
                            KTrackService.UsedManager.LastError = "Fail to send picture, try again later...";
                            System.Console.WriteLine(KTrackService.UsedManager.LastError);
                            return;
                        }
                        else
                        {
                            lock (toSend)
                            {
                                toSend.Remove(infos);
                            }
                            var imageUrl = result.Original;
                            var tt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var fileDateTime = GetImageDateTimeFromStream(infos.GetStream());
                            if (fileDateTime != null)
                            {
                                tt = new DateTimeOffset((DateTime)fileDateTime).ToUnixTimeSeconds();
                                
                            }
                            lock (ServerManager.Get.pictures)
                            {         
                                ServerManager.Get.pictures.Add(result.ToSimpleClass(tt));
                            }
                        }
                    }
                }
                catch
                {
                    System.Console.WriteLine("Fail to send picture, try again later...");
                }
            }
        }

        static DateTime? GetImageDateTimeFromStream(System.IO.Stream stream)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(stream);
                var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubDirectory != null && exifSubDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTime))
                {
                    return dateTime;
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                stream.Close();
            }
        }

        async static Task<ImgurInfo?> UploadToImgur(ImageInfos infos)
        {
            System.Console.WriteLine("Upload " + infos.Name + " picture to imgur...");
            IDictionary<string, object> otherParameters = new Dictionary<string, object>();


            string responseString = null;

            HttpClient client = new HttpClient();
            var requestUri = new Uri("https://api.imgur.com/3/upload.xml?title=ktrackpic&name=" + infos.Name);
            var stream = infos.GetStream();
            var content = new StreamContent(stream);
            client.DefaultRequestHeaders.ExpectContinue = false;
            client.DefaultRequestHeaders.Add("Authorization", "Client-ID 843830c722700cd");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(infos.ContentType);

            try
            {
                var response = await client.PostAsync(requestUri, content);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Upload to imgur gave an exception: ", ex);
                throw;
            }
            

            if (string.IsNullOrEmpty(responseString))
            {
                return null;
            }

            return ImgurInfo.ParseResponse(responseString);
        }
    }
}
