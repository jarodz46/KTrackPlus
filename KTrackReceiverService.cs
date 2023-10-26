using Android.Content;
using KTrackPlus.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus
{
    [BroadcastReceiver(Enabled = true, Exported = false)]
    internal class KTrackReceiverService : BroadcastReceiver
    {


        public enum KTrackServiceAction : sbyte
        {
            Stop = 0,
            Start = 1,
            StartMail = 2,
        }


        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent != null)
            {
                //var action = (KTrackServiceAction)intent.GetByteExtra("action", -1);
                var action = Enum.Parse<KTrackServiceAction>(intent.Action);
                switch (action)
                {
                    case KTrackServiceAction.Start:
                        ClientManager.Get.Start();
                        break;
                    case KTrackServiceAction.Stop:
                        ClientManager.Get.Stop();
                        break;
                    case KTrackServiceAction.StartMail:
                        Console.WriteLine("Start with send mail");
                        ClientManager.Get.AskSendMail = true;
                        ClientManager.Get.Start();
                        break;
                }
                
            }
            //if (ClientManager.Get.IsRunning)
            //{
            //    ClientManager.Get.Stop();
            //}
            //else
            //{
            //    ClientManager.Get.Start();
            //}
        }
    }
}
