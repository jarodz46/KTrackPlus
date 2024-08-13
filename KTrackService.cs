using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.VersionedParcelable;
using KTrackPlus.Helpers;
using KTrackPlus.Helpers.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus
{
    [Service(Label = "KTrackService", ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
    internal class KTrackService : Service
    {

        

        public static bool isRunning { get; set; } = false;

        internal static Manager? UsedManager { get; set; }

        const int NotificationId = 7259;

        internal static void ChangeNotifIcon(int iconId)
        {
            if (context != null)
            {
                notificationBuilder.SetSmallIcon(iconId);
                var notificationManagerCompat = NotificationManagerCompat.From(context);
                notificationManagerCompat.Notify(NotificationId, notificationBuilder.Build());
                //Console.WriteLine("Try change icon");
            }

        }

        static PendingIntent GetPendingIntentFromAction(KTrackReceiverService.KTrackServiceAction action)
        {
            var actionIntent = new Intent(context, typeof(KTrackReceiverService));
            actionIntent.SetAction(action.ToString());
            //actionIntent.PutExtra("action", (sbyte)action);
            actionIntent.SetFlags(ActivityFlags.SingleTop);
            return PendingIntent.GetBroadcast(context, 0, actionIntent, PendingIntentFlags.Immutable);
        }

        static void SetNotificationBuild(KTrackReceiverService.KTrackServiceAction action)
        {
            if (context != null)
            {                
                notificationBuilder.ClearActions();
                switch (action)
                {
                    case KTrackReceiverService.KTrackServiceAction.Start:
                        notificationBuilder.AddAction(Resource.Drawable.ic_stat_play_arrow, "START", GetPendingIntentFromAction(KTrackReceiverService.KTrackServiceAction.Start));
                        notificationBuilder.AddAction(Resource.Drawable.ic_stat_play_arrow, "START + MAIL", GetPendingIntentFromAction(KTrackReceiverService.KTrackServiceAction.StartMail));
                        notificationBuilder.SetContentText("Livetracking stopped");
                        break;
                    case KTrackReceiverService.KTrackServiceAction.Stop:
                        notificationBuilder.AddAction(Resource.Drawable.ic_stat_stop, "STOP", GetPendingIntentFromAction(KTrackReceiverService.KTrackServiceAction.Stop));
                        notificationBuilder.SetContentText("Livetracking is running...");
                        break;
                }                
            }
        }

        internal static void RefreshNotifAction(KTrackReceiverService.KTrackServiceAction action)
        {
            if (context != null)
            {
                SetNotificationBuild(action);
                var notificationManagerCompat = NotificationManagerCompat.From(context);
                notificationManagerCompat.Notify(NotificationId, notificationBuilder.Build());
                var activity = MainActivity.Get;
                if (activity != null)
                {
                    activity.RunOnUiThread(() =>
                    {
                        activity.refreshUiAndFragment(true);
                    });
                }
            }

        }


        static Context? context;

        internal static long StartTime { get; set; } = 0;


        static NotificationCompat.Builder notificationBuilder;
        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            context = this;

            isRunning = false;
            Common.CheckAppMode();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;


            if (!createNotificationChannel())
            {
                Console.WriteLine("Fail to create notification channel");
            }

            PendingIntent? pendingIntent = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable);
            if (pendingIntent == null)
            {
                Console.WriteLine("Fail to get main acticity intent");
                goto end;
            }
            //var startIntent = new Intent(this, typeof(KTrackReceiverService));
            //startIntent.SetAction(KTrackReceiverService.KTrackServiceAction.Start.ToString());
            //startIntent.SetFlags(ActivityFlags.SingleTop);
            var newBuilder = new NotificationCompat.Builder(this, channel_id);
            notificationBuilder  = newBuilder.SetContentTitle("KarooLiveTrack Service")
                .SetSmallIcon(Resource.Drawable.ic_stat_ktp).SetContentIntent(pendingIntent);
            if (Common.CurrentAppMode == Common.AppMode.Server)
                notificationBuilder.SetContentText("BLE Server running");
            else
            {
                SetNotificationBuild(KTrackReceiverService.KTrackServiceAction.Start);
            }
            var notification = notificationBuilder.Build();

            

            //Console.WriteLine("Start foreground notification...");
            StartForeground(NotificationId, notification);

            
            isRunning = true;

            ActicityMonitor.Start(this);

            //Common.UsedId = string.Empty;
            if (Common.CurrentAppMode == Common.AppMode.Server)
            {
                UsedManager = ServerManager.Init(this);
                if (!UsedManager.Start())
                {
                    Console.WriteLine("Fail to start service");
                    isRunning = false;
                    new Thread(t =>
                    {
                        Thread.Sleep(1000);
                        StopSelf();                        
                    }).Start();

                }
            }
            else
            {
                UsedManager = ClientManager.Init(this);                
            }

            if (Common.CurrentAppMode != Common.AppMode.Server && Xamarin.Essentials.Preferences.Get("autoReset", false))
            {
                var date = DateTime.Now;
                var lastDay = Xamarin.Essentials.Preferences.Get("lastStartDay", "");
                if (lastDay != date.Day + "/" + date.Month)
                {
                    Console.WriteLine("Day changed : auto reset");
                    UsedManager.AskForReset = true;
                }
            }

            Toast.MakeText(this, "KTrackPlus service Started", ToastLength.Long).Show();
            Xamarin.Essentials.Preferences.Set("alreadyRunned", true);
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        end:
            return base.OnStartCommand(intent, flags, startId);
        }

        private void AndroidEnvironment_UnhandledExceptionRaiser(object? sender, RaiseThrowableEventArgs e)
        {
            Console.WriteLine(e.ToString());
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ToString());
        }

        const string channel_id = "karoobletrack";

        public KTrackService()
        {
        }

        bool createNotificationChannel()
        {
            //Console.WriteLine("Create notification channel");
            NotificationChannel serviceChannel = new NotificationChannel(channel_id, "Foreground Service Channel", NotificationImportance.Default);
            var systemService = GetSystemService(NotificationService);
            if (systemService != null)
            {
                var notificationManager = (NotificationManager)systemService;
                notificationManager.CreateNotificationChannel(serviceChannel);
                return true;
            }
            return false;
        }

        void Stop()
        {
            if (context != null)
                ActicityMonitor.Stop(context);
            
            context = null;
            isRunning = false;
            UsedManager?.Stop();            
        }

        public override bool StopService(Intent? name)
        {
            Stop();
            return base.StopService(name);
        }

        public override void OnDestroy()
        {
            Stop();
            base.OnDestroy();
        }

        public override IBinder? OnBind(Intent? intent)
        {
            return null;
        }
    }
}
