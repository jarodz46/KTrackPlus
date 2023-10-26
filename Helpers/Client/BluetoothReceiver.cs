using Android.Bluetooth;
using Android.Content;
using Android.Widget;
using Java.Lang;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Client
{
    class BluetoothReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            var action = intent?.Action;
            if (action != null && action == BluetoothAdapter.ActionStateChanged)
            {
                var state = intent?.GetIntExtra(BluetoothAdapter.ExtraState, BluetoothAdapter.Error);
                if (state == (int)State.Off)
                {
                    ClientManager.Get.mGatt = null;
                    Console.WriteLine("Bluetooth disabled, enabling...");
                    ClientManager.Get.adapter?.Enable();
                }
                if (state == (int)State.On)
                {
                    var newScanner = ClientManager.Get.adapter?.BluetoothLeScanner;
                    if (newScanner != null)
                    {
                        ClientManager.Get.scanner = newScanner;
                    }

                    Console.WriteLine("Bluetooth enabled, try reconnect...");
                    ClientManager.Get.StartScan();
                }
            }
        }
    }
}
