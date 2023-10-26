﻿using Android.Content;
using Dynastream.Fit;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;
using static Android.Provider.CallLog;

namespace KTrackPlus.Helpers
{
    internal abstract class Manager
    {


        internal const byte bLOCLIST = 16;
        internal const byte bSTATS = 12;
        internal const byte bSETTINGS = 14;
        internal const byte bID = 13;
        internal const byte bRESET = 10;

        internal string UsedId { get; set; } = string.Empty;

        protected abstract bool InternalStart();
        protected abstract void InternalStop();
        protected abstract void TimerTask();
        protected abstract void InternalReset();

        public Manager(Context context)
        {
            mContext = context;
        }

        public bool IsRunning { get; private set; } = false;

        public Context mContext { get; private set; }
        public Java.Util.Timer? timer { get; set; } = null;

        public Settings? Settings { get; protected set; }

        internal string LastError { get; set; } = string.Empty;
        public bool Start()
        {
            try
            {
                if (timer == null)
                {
                    var strInterval = Preferences.Get("updateInterval", "30");
                    var interval = int.Parse(strInterval);
                    timer = new Java.Util.Timer();
                    timer.Schedule(new Task(this), 1000, 1000 * interval);
                }
                var date = System.DateTime.Now;
                Preferences.Set("lastStartDay", date.Day + "/" + date.Month);
                UsedId = string.Empty;
                var result = InternalStart();
                IsRunning = result;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to start service : " + Environment.NewLine + ex.Message);
                return false;
            }
        }

        public bool AskForReset { get; set; } = false;

        protected void Reset()
        {
            locations.Clear();
            pictures.Clear();
            Stats.Reset();
            InternalReset();
            //Settings = new Settings();
        }

        public bool AskStopTask { get; set; } = false;

        public void ChangeInterval(int seconds)
        {
            StopTimer();
            timer = new Java.Util.Timer();
            timer.Schedule(new Task(this), 1000, seconds * 1000);
        }

        void StopTimer()
        {
            if (timer != null)
            {
                timer.Cancel();
                AskStopTask = true;
                var tickCount = Environment.TickCount;
                while (taskIsRunning)
                {
                    if (Environment.TickCount - tickCount > 5000)
                        break;
                    Thread.Sleep(1);
                }
                timer = null;
            }
        }

        bool taskIsRunning = false;
        public void Stop()
        {
            if (IsRunning)
            {
                StopTimer();
                IsRunning = false;
                InternalStop();
            }
        }

        class Task : TimerTask
        {
            Manager manager;

            public Task(Manager manager)
            {
                this.manager = manager;
            }

            public override void Run()
            {
                manager.taskIsRunning = true;
                manager.AskStopTask = false;
                try
                {
                    manager.TimerTask();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Trask crash :" + Environment.NewLine + e);
                }
                finally
                {
                    manager.AskStopTask = false;
                    manager.taskIsRunning = false;
                }
            }
        }

        internal List<SimpleImgurInfo> pictures { get; private set; } = new();
        internal List<SimpleLocation> locations { get; private set; } = new();

        public bool AskSendMail { get; set; } = false;
        internal Stats Stats { get; set; } = new Stats();

        internal const string apiUrl = "https://track.lazyjarod.com/";
        internal bool? internetStatus = null;

        internal bool checkInternet()
        {
            internetStatus = Connectivity.NetworkAccess == NetworkAccess.Internet;
            if (internetStatus != true)
                return false;
            return true;
        }

        internal async Task<bool> TryAskResetToAPI()
        {
            var result = false;
            try
            {
                result = await sendToAPI("reset");
            }
            catch
            {
                result = false;
            }
            if (result)
                return true;
            else
            {
                LastError = "Fail to send reset request";
                Console.WriteLine(LastError);
            }
            return false;
        }

        internal async Task<bool> SendPictures()
        {
            var picsCache = new List<SimpleImgurInfo>();
            lock (pictures)
            {
                picsCache.AddRange(pictures);
            }
            foreach (var pic in picsCache)
            {
                var result = false;
                try
                {
                    result = await sendToAPI("picture", pic);
                }
                catch
                {
                    result = false;
                }
                if (result)
                {
                    lock (pictures)
                    {
                        pictures.Remove(pic);
                    }
                }
                else
                {
                    LastError = "Fail to send picture infos";
                    Console.WriteLine(LastError);
                    return false;
                }
            }
            return true;
        }

        internal async Task<bool> sendStats()
        {
            return await sendToAPI("stats", Stats);
        }

        async Task<bool> sendLocationsPack(List<SimpleLocation> locs)
        {
            if (locs.Count == 0)
                return true;
            Stats.updated = true;
            return await sendToAPI("locations", locs);
        }

        public class MailInfos
        {
            public string mail { get; set; }
            public string name { get; set; }
            public string locale { get; set; } = "en";

            public MailInfos(string mail, string name, string locale)
            {
                this.mail = mail;
                this.name = name;
                this.locale = locale;
            }


        }

        internal async Task<bool> SendMails()
        {
            bool result = true;
            var locale = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (var mail in Settings.Mails.Split(';'))
            {
                var splitedMail = mail.Split('@');
                if (splitedMail.Length == 2 && splitedMail[0].Length > 0 && splitedMail[1].Length > 0 && splitedMail[1].Contains('.'))
                {
                    Console.WriteLine("Try send mail to " + mail);
                    if (!await sendToAPI("mail", new MailInfos(mail, Settings.Name, locale), "sendmail.php"))
                    {
                        result = false;
                        break;
                    }
                }
                else
                {
                    Console.WriteLine(mail + " mail seem invalid, ignore");
                }
                Thread.Sleep(1000);
            }
            return result;
        }

        internal async Task<bool> SendPositions()
        {
            var locsCache = new List<SimpleLocation>();
            lock (locations)
            {
                locsCache.AddRange(locations);
            }
            while (locsCache.Count > 0)
            {
                if (AskStopTask)
                    return false;
                if (!KTrackService.isRunning)
                    return false;
                List<SimpleLocation> locsToSend;
                if (locsCache.Count >= 100)
                    locsToSend = locsCache.GetRange(0, 100);
                else
                    locsToSend = new List<SimpleLocation>(locsCache);
                var result = false;
                try
                {
                    result = await sendLocationsPack(locsToSend);
                }
                catch
                {
                    result = false;
                }
                if (result)
                {
                    locsCache.RemoveAll(l => locsToSend.Contains(l));
                    lock (locations)
                    {
                        locations.RemoveAll(l => locsToSend.Contains(l));
                    }
                }
                else
                {
                    LastError = "Fail to send locations";
                    Console.WriteLine(LastError);
                    return false;
                }
                Thread.Sleep(1);
            }
            return true;
        }

        internal async Task<bool> sendToAPI(string getInfos, object? content = null, string? ofile = null)
        {
            HttpClient client = new HttpClient();
            var file = "pushInfos.php";
            if (ofile != null)
                file = ofile;
            var baseUrl = apiUrl + file + "?id=" + UsedId;
            if (getInfos.Length > 0)
                baseUrl += "&";
            HttpResponseMessage response;
            if (content != null)
            {
                string jsonLocs = JsonSerializer.Serialize(content);
                var jsonLocsBytes = Encoding.UTF8.GetBytes(jsonLocs);
                var memoryStream = new MemoryStream();
                GZipStream zip = new GZipStream(memoryStream, CompressionLevel.SmallestSize, true);
                zip.Write(jsonLocsBytes, 0, jsonLocsBytes.Length);
                zip.Close();
                memoryStream.Position = 0;
                var scontent = new StreamContent(memoryStream);
                scontent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                scontent.Headers.ContentEncoding.Add("gzip");

                response = await client.PostAsync(baseUrl + getInfos, scontent);
                memoryStream.Close();
            }
            else
            {
                response = await client.GetAsync(baseUrl + getInfos);
            }
            if (response.IsSuccessStatusCode)
            {
                if (ofile != null)
                    return true;
                return await response.Content.ReadAsStringAsync() == "OK";
            }
            return false;
        }
    }
}