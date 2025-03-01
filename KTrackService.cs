using Android.Content;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.VersionedParcelable;
using IO.Hammerhead.Karooext;
using IO.Hammerhead.Karooext.Internal;
using IO.Hammerhead.Karooext.Models;
using Java.Util;
using Kotlin.Jvm.Functions;
using Kotlin.Uuid;
using KTrackPlus.Helpers;
using KTrackPlus.Helpers.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IO.Hammerhead.Karooext.Extension;
using Java.Interop;
using karooext.dotnet.Classes;
using TimberLog;
using Android.Graphics;
using AndroidX.Core.Util;

namespace KTrackPlus
{

    [Service(Exported = true, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeSpecialUse)]
    [IntentFilter(["io.hammerhead.karooext.KAROO_EXTENSION"])]
    [MetaData("io.hammerhead.karooext.EXTENSION_INFO", Resource = "@xml/extension_info")]
    public class KTrackService : KarooExtension
    {

        [Export(SuperArgumentsString = "\"ktrackplus\",\"2.1\"")]
        public KTrackService() : base("ktrackplus", "2.1")
        {
        }

        public override IList<DataTypeImpl> Types
        {
            get
            {
                var list = new List<DataTypeImpl>
                {
                    new KTrackStatusData()
                };
                return list;
            }
        }

        public override void OnCreate()
        {
            System.Console.SetOut(MainActivity.writer);
            base.OnCreate();
        }

        public static bool isRunning { get; set; } = false;

        static string lastRoutePoly = string.Empty;
        public static List<(double Latitude, double Longitude)>? LastRoute {get; private set;}
        internal static bool LastRouteChanged { get; set; } = false;

        internal static Manager? UsedManager { get; set; }

        const int NotificationId = 7259;

        internal static void ChangeNotifIcon(int iconId)
        {
            if (Context != null)
            {
                notificationBuilder.SetSmallIcon(iconId);
                var notificationManagerCompat = NotificationManagerCompat.From(Context);
                notificationManagerCompat.Notify(NotificationId, notificationBuilder.Build());
                //Console.WriteLine("Try change icon");
            }

        }

        static PendingIntent GetPendingIntentFromAction(KTrackReceiverService.KTrackServiceAction action)
        {
            var actionIntent = new Intent(Context, typeof(KTrackReceiverService));
            actionIntent.SetAction(action.ToString());
            //actionIntent.PutExtra("action", (sbyte)action);
            actionIntent.SetFlags(ActivityFlags.SingleTop);
            return PendingIntent.GetBroadcast(Context, 0, actionIntent, PendingIntentFlags.Immutable);
        }

        static void SetNotificationBuild(KTrackReceiverService.KTrackServiceAction action)
        {
            if (Context != null)
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
            if (Context != null)
            {
                SetNotificationBuild(action);
                var notificationManagerCompat = NotificationManagerCompat.From(Context);
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


        internal static Context? Context { get; private set; } = null;

        internal static long StartTime { get; set; } = 0;


        internal static void InitKarooSystem(Context? context = null)
        {
            if (context == null)
            {
                if (Context == null)
                    return;
                else
                    context = Context;
            }
            if (Common.IsKarooDevice)
            {
                if (karooSystemService == null)
                {                    
                    karooSystemService = new KarooSystemService(context);                    
                }
                if (!karooSystemService.Connected)
                {
                    Action<bool> act = delegate (bool connected)
                    {
                        if (connected && karooSystemService != null)
                        {

                            Console.WriteLine("Connected to Karoo Ext !");

                            var style = SystemNotification.Style.Event;
                            var actionIntent = new Intent(context, typeof(KTrackReceiverService));
                            actionIntent.SetAction(KTrackReceiverService.KTrackServiceAction.Start.ToString());
                            var intent = actionIntent.Action;
                            var notif = new SystemNotification("ktpe", "KTrackPlus Connected", null, "KTrackPlus", style, null, null);
                            karooSystemService.Dispatch(notif);

                            var resp = (OnNavigationState s) =>
                            {
                                if (s.State is OnNavigationState.NavigationState.NavigatingRoute)
                                {
                                    var route = s.State as OnNavigationState.NavigationState.NavigatingRoute;
                                    if (route != null)
                                    {
                                        if (route.RoutePolyline != lastRoutePoly)
                                        {
                                            lastRoutePoly = route.RoutePolyline;
                                            LastRoute = PolylineDecoder.DecodePolyline(route.RoutePolyline);
                                            Console.WriteLine("New route : " + route.Name + " (" + LastRoute.Count + " points)");
                                            LastRouteChanged = true;
                                        }
                                    }
                                    else
                                    {
                                        LastRoute = null;
                                    }
                                }
                                if (s.State is OnNavigationState.NavigationState.Idle)
                                {
                                    Console.WriteLine("No route...");
                                    LastRoute = null;
                                    lastRoutePoly = string.Empty;
                                    LastRouteChanged = true;

                                }
                            };
                            var consumerId = karooSystemService.AddConsumer(resp);

                        }
                        else
                        {
                            Console.WriteLine("Karoo ext error !");
                        }
                    };
                    karooSystemService.Connect(act);
                }
            }
        }
        


        internal static KarooSystemService? karooSystemService { get; set; }
        static NotificationCompat.Builder notificationBuilder;
        [return: GeneratedEnum]
        public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
        {
            Context = this;            
            isRunning = false;

            InitKarooSystem(this);

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
                Console.WriteLine("Fail to get main activity intent");
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
                        
            

            ActivityMonitor.Start(this);

            //Common.UsedId = string.Empty;
            Common.CheckAppMode();

            isRunning = true;
            Toast.MakeText(this, "KTrackPlus service Started", ToastLength.Long).Show();
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
            if (Context != null)
                ActivityMonitor.Stop(Context);

            if (karooSystemService != null && karooSystemService.Connected)
            {
                karooSystemService.Disconnect();
            }

            Context = null;
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

    }
}
