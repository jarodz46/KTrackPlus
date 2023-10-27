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
    class ClientBluetoothReceiver : BroadcastReceiver
    {

        ClientManager manager;
        public ClientBluetoothReceiver(ClientManager manager)
        {
            this.manager = manager;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            var action = intent?.Action;
            if (action != null && action == BluetoothAdapter.ActionStateChanged)
            {
                var state = intent?.GetIntExtra(BluetoothAdapter.ExtraState, BluetoothAdapter.Error);
                if (state == (int)State.Off)
                {
                    manager.mGatt = null;
                    Console.WriteLine("Bluetooth disabled, enabling...");
                    manager.adapter?.Enable();
                }
                if (state == (int)State.On)
                {
                    var newScanner = ClientManager.Get.adapter?.BluetoothLeScanner;
                    if (newScanner != null)
                    {
                        manager.scanner = newScanner;
                    }

                    Console.WriteLine("Bluetooth enabled, try reconnect...");
                    manager.StartScan();
                }
            }
        }
    }
}
