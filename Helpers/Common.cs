using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Telephony;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using Java.Util;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace KTrackPlus.Helpers
{
    internal static class Common
    {
        public static UUID DEVICE_UUID_SERVICE = UUID.FromString("00001523-1212-efde-1523-785feabcd123");
        public static UUID DEVICE_UUID_CHAR_WRITE = UUID.FromString("00001524-1212-efde-1523-785feabcd123");
        public static UUID DEVICE_UUID_CHAR_NOTIFY = UUID.FromString("00001525-1212-efde-1523-785feabcd123");
        public static UUID CLIENT_CHARACTERISTIC_CONFIG_DESCRIPTOR_UUID = UUID.FromString("00002902-0000-1000-8000-00805f9b34fb");

        public const uint UnixFitTTOffset = 631065600;

        public const string KarooFitDir = "/sdcard/FitFiles";
        public const string KarooTempFitDir = KarooFitDir + "/temp";

        public enum AppMode
        {
            None,
            Client,
            Server,
            Standalone,
        }


        static AppMode lastAppMode = AppMode.None;
        internal static bool CheckAppMode()
        {
            var standAlone = Preferences.Get("workingMode", "Auto") == "Standalone";
            if (standAlone)
            {
                CurrentAppMode = AppMode.Standalone;
            }
            else
            {
                if (IsKarooDevice)
                {

                    CurrentAppMode = AppMode.Client;
                }
                else
                {
                    CurrentAppMode = AppMode.Server;
                }
            }
                      

            var result = false;
            if (lastAppMode != CurrentAppMode)
            {
                switch (CurrentAppMode)
                {
                    case AppMode.Standalone:
                        Console.WriteLine("Use standalone mode");
                        break;
                    case AppMode.Client:
                        Console.WriteLine("Karoo device : use client mode");
                        break;
                    case AppMode.Server:
                        Console.WriteLine("Unknown device : use server mode");
                        break;
                }
                result = true;
            }
            lastAppMode = CurrentAppMode;
            return result;
        }
               

        public static AppMode CurrentAppMode { get; set; } = AppMode.Client;

        const string Hammerhead = "Hammerhead";
        static bool? mIsKarooDevice = null;
        internal static bool IsKarooDevice
        {
            get
            {
                if (mIsKarooDevice == null)
                    mIsKarooDevice = DeviceInfo.Manufacturer == Hammerhead;
                return (bool)mIsKarooDevice;
            }
        }

        public static void ShowAlert(Context context, string text, Action? after = null)
        {
            Xamarin.Essentials.MainThread.BeginInvokeOnMainThread(() =>
            {
                var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(context);
                builder.SetMessage(text);
                builder.SetNeutralButton("OK", new EventHandler<DialogClickEventArgs>(delegate { after?.Invoke(); })); ;
                var alertDialog = builder.Create();
                alertDialog.Show();
            });
        }

        public static string RandomId()
        {
            int length = 8;
            var rnd = new System.Random(Guid.NewGuid().GetHashCode());
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ01234567899876543210";

            return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
        }


        public static double CalculateDistance(SimpleLocation loc1, SimpleLocation loc2)
        {
            double lat1Rad = DegreesToRadians(loc1.lat);
            double lon1Rad = DegreesToRadians(loc1.lng);
            double lat2Rad = DegreesToRadians(loc2.lat);
            double lon2Rad = DegreesToRadians(loc2.lng);

            double dLat = lat2Rad - lat1Rad;
            double dLon = lon2Rad - lon1Rad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double distance = EarthRadius * c;

            return distance;
        }

        private const double EarthRadius = 6371000;

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        static string? getActiveSuscriberId(Context context)
        {
            var tm = context.GetSystemService(Context.TelephonyService) as TelephonyManager;
            string? suscriberId = string.Empty;
            if (tm != null)
            {
                suscriberId = tm.SubscriberId;
            }
            return suscriberId;
        }

        public static long GetAppNetworkUsage(Context context, long startTime, long endTime)
        {
            var nsm = context.ApplicationContext.GetSystemService(Context.NetworkStatsService) as NetworkStatsManager;
            var subscriberId = getActiveSuscriberId(context);
            if (string.IsNullOrEmpty(subscriberId))
                return 0;
            NetworkStats netwokStatsByApp;
            long currentAppUsage = 0;
            var puid = context.PackageManager.GetPackageUid(context.PackageName, 0);
            try
            {
                netwokStatsByApp = nsm.QuerySummary(Android.Net.ConnectivityType.Mobile, subscriberId, startTime, endTime);
                var bucket = new NetworkStats.Bucket();
                do
                {
                    netwokStatsByApp.GetNextBucket(bucket);
                    if (bucket.Uid == puid)
                    {
                        currentAppUsage = bucket.RxBytes + bucket.TxBytes;
                    }
                } while (netwokStatsByApp.HasNextBucket);
                netwokStatsByApp.Close();
            }
            catch
            {
                Console.WriteLine("Error while getting network usage");
            }
            return currentAppUsage;

        }
    }
}
