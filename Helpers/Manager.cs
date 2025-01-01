using Android.Content;
using Android.Telephony;
using Dynastream.Fit;
using IO.Hammerhead.Karooext;
using IO.Hammerhead.Karooext.Models;
using Java.IO;
using Java.Util;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

        public static IO.Hammerhead.Karooext.KarooSystemService? KarooSystemService { get; set; }

        protected abstract bool InternalStart();
        protected abstract void InternalStop();
        protected abstract Task TimerTask();
        protected abstract void InternalReset();
        protected abstract Task FastTimerTask();

        public Manager()
        {
        }

        public bool crash { get; set; } = false;

        public bool IsRunning { get; private set; } = false;

        public Context mContext
        {
            get
            {
                return KTrackService.Context;
            }
        }
        public System.Threading.Timer? timer { get; set; } = null;

        public Settings? Settings { get; protected set; }

        public System.DateTime? LastSendPosSuccess { get; protected set; } = null;

        internal string LastError { get; set; } = string.Empty;
        public bool Start(bool allowAskPersmissions = false)
        {
            try
            {
                if (!Common.CheckPermissions(mContext))
                {
                    if (allowAskPersmissions && MainActivity.Get != null)
                    {
                        MainActivity.Get.AskPermissions();
                    }
                    else
                    {
                        System.Console.WriteLine("Permissions are required, please start livetrack from app!");                        
                    }
                    return false;
                }

                if (Common.IsKarooDevice)
                {
                    if (KTrackService.karooSystemService == null || !KTrackService.karooSystemService.Connected)
                        KTrackService.InitKarooSystem();

                    if (KTrackService.karooSystemService != null && KTrackService.karooSystemService.Connected)
                    {
                        var ble = new IO.Hammerhead.Karooext.Models.RequestBluetooth("ktrackble");
                        if (KTrackService.karooSystemService.Dispatch(ble))
                            System.Console.WriteLine("Karoo ble access granted !");
                        else
                            System.Console.WriteLine("Fail to get ble access !");
                    }
                }

                var date = System.DateTime.Now;
                Preferences.Set("lastStartDay", date.Day + "/" + date.Month);
                UsedId = string.Empty;
                var result = InternalStart();
                
                if (result && timer == null)
                {
                    var strInterval = Preferences.Get("updateInterval", "30");
                    UpdateInterval = int.Parse(strInterval);
                    timer = new System.Threading.Timer(Timer_Elapsed, null, 1000, 1000);
                }
                IsRunning = result;
                return result;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Unable to start : " + Environment.NewLine + ex.Message);
                return false;
            }
        }

        int lastTaskTT = 0;
        private async void Timer_Elapsed(object? sender)
        {
            if (taskIsRunning)
                return;
            taskIsRunning = true;
            AskStopTask = false;
            try
            {
                await FastTimerTask();
                var ttOffset = Environment.TickCount - lastTaskTT;
                if (ttOffset >= UpdateInterval * 1000)
                {
                    await TimerTask();
                    lastTaskTT = Environment.TickCount;
                }
            }
            catch (Exception te)
            {
                System.Console.WriteLine("Task crash :" + Environment.NewLine + te);
            }
            finally
            {
                AskStopTask = false;
                taskIsRunning = false;
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

        public int UpdateInterval { get; set; } = 30;

        public void ChangeInterval(int seconds)
        {
            UpdateInterval = seconds;
        }

        void StopTimer()
        {
            if (timer != null)
            {
                timer.Dispose();
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
            if (KTrackService.karooSystemService != null && KTrackService.karooSystemService.Connected)
            {
                var ble = new IO.Hammerhead.Karooext.Models.ReleaseBluetooth("ktrackble");
                KTrackService.karooSystemService.Dispatch(ble);
            }

            if (IsRunning)
            {
                StopTimer();
                IsRunning = false;
                InternalStop();
            }
        }
               

        internal List<SimpleImgurInfo> pictures { get; private set; } = new();
        internal List<SimpleLocation> locations { get; private set; } = new();

        public bool AskSendMail { get; set; } = false;
        internal Stats Stats { get; set; } = new Stats();

        internal const string apiUrl = "https://track.lazyjarod.com/";
        internal bool? internetStatus = null;

        internal int SignalStrength { get; set; } = 4;

        internal bool checkInternet()
        {
            if (Common.CurrentAppMode == Common.AppMode.Standalone && Common.NewKarooCapabilities)
            {
                internetStatus = true;
                return true;
            }
            internetStatus = Connectivity.NetworkAccess == NetworkAccess.Internet;
            if (internetStatus != true)
                return false;
            return true;
        }

        public int GetInternetLevel()
        {
            int newVal = -1;
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                if (mContext.CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) == Android.Content.PM.Permission.Granted)
                {
                    try
                    {
                        var tm = mContext.GetSystemService(Context.TelephonyService) as TelephonyManager;
                        if (tm != null && tm.SignalStrength != null)
                            newVal = tm.SignalStrength.Level;
                        if (tm != null && tm.NetworkType == NetworkType.Edge)
                            newVal = 0;
                    }
                    catch
                    {
                        newVal = 4;
                    }
                }
                else
                {
                    newVal = 4;
                }
            }
            return newVal;
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
                System.Console.WriteLine(LastError);
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
                    System.Console.WriteLine(LastError);
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
            return await sendToAPI("locations", new LocationsPack(locs));
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
                    System.Console.WriteLine("Try send mail to " + mail);
                    if (!await sendToAPI("mail", new MailInfos(mail, Settings.Name, locale), "sendmail2.php"))
                    {
                        result = false;
                        break;
                    }
                }
                else
                {
                    System.Console.WriteLine(mail + " mail seem invalid, ignore");
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
                if (locations.Count >= 300 && Common.CurrentAppMode == Common.AppMode.Standalone && Common.IsKarooDevice)
                    locsCache.AddRange(locations.GetRange(0, 300));
                else
                    locsCache.AddRange(locations);
            }
            while (locsCache.Count > 0)
            {
                if (AskStopTask)
                {
                    return false;
                }
                if (!KTrackService.isRunning)
                {
                    return false;
                }
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
                catch (Exception e)
                {
                    //Console.WriteLine(e);
                    result = false;
                }
                if (result)
                {
                    LastSendPosSuccess = System.DateTime.Now;
                    locsCache.RemoveAll(l => locsToSend.Contains(l));
                    lock (locations)
                    {
                        locations.RemoveAll(l => locsToSend.Contains(l));
                    }
                }
                else
                {
                    LastError = "Fail to send locations";
                    System.Console.WriteLine(LastError);
                    return false;
                }
                Thread.Sleep(1);
            }
            
            return true;
        }

        internal const string ApiPushFile = "pushInfos2.php";

        string getApiCallUrl(string getInfos, string? ofile = null)
        {
            var file = ApiPushFile;
            if (ofile != null)
                file = ofile;
            var baseUrl = apiUrl + file + "?id=" + UsedId;
            if (getInfos.Length > 0)
                baseUrl += "&";
            return baseUrl + getInfos;
        }
        async Task<bool> sendToAPI(string getInfos, object? content = null, string? ofile = null)
        {
            if (Common.CurrentAppMode == Common.AppMode.Standalone && 
                Common.IsKarooDevice && KTrackService.karooSystemService != null && KTrackService.karooSystemService.Connected)
            {
                var result = await sendToAPIThroughKarooAPI(getInfos, content, ofile);
                return result;
            }
            else
            {
                var result = await sendToAPIDirectly(getInfos, content, ofile);
                return result;
            }
        }

        MemoryStream getStreamContent(object content)
        {
            var mpLocsBytes = MessagePackSerializer.Serialize(content);
            var memoryStream = new MemoryStream();
            GZipStream zip = new GZipStream(memoryStream, CompressionLevel.SmallestSize, true);
            zip.Write(mpLocsBytes, 0, mpLocsBytes.Length);
            zip.Close();
            memoryStream.Position = 0;
            return memoryStream;
        }

        internal async Task<bool> sendToAPIDirectly(string getInfos, object? content = null, string? ofile = null)
        {
            try
            {

                HttpClient client = new HttpClient();
                var callUrl = getApiCallUrl(getInfos, ofile);
                HttpResponseMessage response;
                if (content != null)
                {
                    var memoryStream = getStreamContent(content);
                    var scontent = new StreamContent(memoryStream);
                    scontent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    scontent.Headers.ContentEncoding.Add("gzip");
                    response = await client.PostAsync(callUrl, scontent);
                    memoryStream.Close();
                }
                else
                {
                    response = await client.GetAsync(callUrl);
                }
                if (response.IsSuccessStatusCode)
                {
                    if (ofile != null)
                        return true;
                    return await response.Content.ReadAsStringAsync() == "OK";
                }
                return false;
            }
            catch
            {
                //Console.WriteLine("Exception on api call");
                return false;
            }
        }
        
        internal async Task<bool> sendToAPIThroughKarooAPI(string getInfos, object? content = null, string? ofile = null)
        {
            try
            {
                var kss = KTrackService.karooSystemService;
                if (kss != null && kss.Connected)
                {
                    var callUrl = getApiCallUrl(getInfos, ofile);
                    OnHttpResponse.MakeHttpRequest request;
                    var headers = new Dictionary<string, string>();
                    if (content != null)
                    {
                        var memoryStream = getStreamContent(content);
                        headers.Add("Content-Type", "application/octet-stream");
                        headers.Add("Content-Encoding", "gzip");
                        request = new OnHttpResponse.MakeHttpRequest("POST", callUrl, headers, memoryStream.ToArray(), false);
                        memoryStream.Close();
                    }
                    else
                    {
                        request = new OnHttpResponse.MakeHttpRequest("GET", callUrl, headers, null, false);
                    }
                    var tcs = new TaskCompletionSource<bool>();
                    string consumerId = "";
                    var resp = (OnHttpResponse r) =>
                    {
                        if (r.State is HttpResponseState.Complete)
                        {                           
                            var complete = r.State as HttpResponseState.Complete;
                            if (complete != null)
                            {
                                if (complete.StatusCode == 200)
                                {
                                    if (ofile == null)
                                    {
                                        var bodyArray = complete.GetBody();
                                        if (bodyArray != null)
                                        {
                                            var body = Encoding.Default.GetString(bodyArray);
                                            tcs.TrySetResult(body == "OK");
                                            return;
                                        }
                                    }
                                    tcs.TrySetResult(true);
                                    return;
                                }
                            }
                        }
                        if (r.State is HttpResponseState.Queued ||
                            r.State is HttpResponseState.InProgress)
                            return;
                        tcs.TrySetResult(false);
                    };
                    consumerId = kss.AddConsumer(resp, request);
                    var timeout2Task = new Task(() =>
                    {
                        var tc = Environment.TickCount;
                        while (!AskStopTask && (Environment.TickCount - tc) < 20 * 1000)
                            Task.Delay(100);
                    });
                    //var timeoutTask = Task.Delay(20000);
                    var completedTask = await Task.WhenAny(tcs.Task, timeout2Task);
                    //In case of timeout
                    if (completedTask == timeout2Task)
                    {
                        kss.RemoveConsumer(consumerId);
                        tcs.TrySetResult(false);
                    }
                    return await tcs.Task;
                }
                return false;
            }
            catch
            {
                //Console.WriteLine("Exception on api call");
                return false;
            }
        }

       
    }
}
