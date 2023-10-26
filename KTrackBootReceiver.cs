using Android.Content;
using Android.OS;
using Java.IO;
using KTrackPlus.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus
{
    [BroadcastReceiver(Enabled = true, Exported = true)]
    [IntentFilter([Intent.ActionBootCompleted])]
    public class KTrackBootReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            System.Console.SetOut(MainActivity.writer);
            var alreadyRunned = Xamarin.Essentials.Preferences.Get("alreadyRunned", false);
            var serviceOnBoot = Xamarin.Essentials.Preferences.Get("serviceOnBoot", true);
            if (serviceOnBoot && !alreadyRunned)
            {
                System.Console.WriteLine("Can't start on boot : start service manually before");
                return;
            }
            if (serviceOnBoot)
            {                
                System.Console.WriteLine("Start service on boot");
                Common.CheckAppMode();
                Intent i = new Intent(context, typeof(KTrackService));
                i.AddFlags(ActivityFlags.NewTask);
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    context.StartForegroundService(i);
                }
                else
                {
                    context.StartService(i);
                }
            }
        }
    }
}
