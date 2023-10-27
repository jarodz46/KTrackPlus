using Android.Bluetooth;
using Android.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Server
{
    internal class ServerBluetoothReceiver : BroadcastReceiver
    {

        ServerManager manager;
        public ServerBluetoothReceiver(ServerManager manager)
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
                    Console.WriteLine("Bluetooth off");
                    manager.StopServer();
                    manager.StopAdvsersting();
                }
                if (state == (int)State.On)
                {
                    Console.WriteLine("Bluetooth on");
                    manager.StartAdvertising();
                    manager.StartServer();
                }
            }
        }
    }
}
