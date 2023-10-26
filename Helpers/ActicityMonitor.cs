using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Java.Util.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    internal static class ActicityMonitor
    {

        static System.Timers.Timer? checkActivityTimer;
        static View? overlay;

        static List<string> files = new();

        static bool thereIsNewFile(string[] newFiles)
        {
            bool somethingnew = false;
            foreach (var file in newFiles)
            {
                if (!files.Contains(file))
                {
                    files.Add(file);
                    somethingnew = true;
                }
            }
            return somethingnew;
        }

        internal static void Start(Service context)
        {
            if (!Common.IsKarooDevice)
                return;
            if (!Xamarin.Essentials.Preferences.Get("showStartAsk", false))
                return;
            if (Android.Provider.Settings.CanDrawOverlays(context))
            {
                Console.WriteLine("Monitor for new activity");
                checkActivityTimer = new System.Timers.Timer(2000);
                files.Clear();
                files.AddRange(Directory.GetFiles(Common.KarooTempFitDir));
                var windowManager = context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
                Executors.NewScheduledThreadPool(1);
                if (windowManager != null)
                {
                    checkActivityTimer.Elapsed += delegate
                    {
                        if (!KTrackService.isRunning)
                            checkActivityTimer.Stop();
                        try
                        {
                                
                            var newFiles = Directory.GetFiles(Common.KarooTempFitDir);
                            if (thereIsNewFile(newFiles))
                            {

                                new Handler(Looper.MainLooper).Post(() =>
                                {
                                    //Toast.MakeText(context, "New activity found", ToastLength.Long).Show();
                                    overlay = View.Inflate(context, Resource.Layout.overlay, null);
                                    overlay.FindViewById<Button>(Resource.Id.ostarttrack).Click += delegate
                                    {
                                        Toast.MakeText(context, "Start live tracking...", ToastLength.Long).Show();
                                        ClientManager.Get.Start();
                                        windowManager.RemoveView(overlay);
                                    };
                                    overlay.FindViewById<Button>(Resource.Id.ostartmail).Click += delegate
                                    {
                                        Toast.MakeText(context, "Start live tracking...", ToastLength.Long).Show();
                                        ClientManager.Get.AskSendMail = true;
                                        ClientManager.Get.Start();
                                        windowManager.RemoveView(overlay);
                                    };
                                    overlay.FindViewById<Button>(Resource.Id.oclose).Click += delegate
                                    {
                                        windowManager.RemoveView(overlay);
                                    };

                                    var wparams = new WindowManagerLayoutParams(
                                        ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent,
                                        WindowManagerTypes.ApplicationOverlay,
                                        WindowManagerFlags.NotFocusable | WindowManagerFlags.Fullscreen,
                                        Android.Graphics.Format.Opaque
                                        );
                                    windowManager.AddView(overlay, wparams);
                                });
                            }
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("New activity mon crash");
                        }
                        

                    };
                    checkActivityTimer.Start();
                }
            }
            else
            {
                Console.WriteLine("No permission to show overlay");
            }


        }

        internal static void Stop(Context context)
        {
            checkActivityTimer?.Close();
            var windowManager = context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            if (windowManager != null && overlay != null)
            {
                windowManager.RemoveView(overlay);
            }
        }

    }
}
