using Android.Bluetooth.LE;
using Android.Bluetooth;
using Android.Content;
using Android.Runtime;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Client
{
    internal class ScanCallback : Android.Bluetooth.LE.ScanCallback
    {
        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult? result)
        {
            base.OnScanResult(callbackType, result);

            var name = result?.Device?.Name;
            if (!string.IsNullOrEmpty(name))
            {
                ClientManager.Get.DeviceAdress = result?.Device?.Address;
                ClientManager.Get.scanner?.StopScan(ClientManager.Get.scanCallback);
                Console.WriteLine("Found a device : " + name);
                var gatt = result?.Device?.ConnectGatt(ClientManager.Get.mContext, false, ClientManager.Get.GattHelper, BluetoothTransports.Le);
                if (gatt == null)
                {
                    KTrackService.UsedManager.LastError = "Fail to call 'connectgatt'";
                    Console.WriteLine(KTrackService.UsedManager.LastError);
                }

            }
        }

        public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            base.OnScanFailed(errorCode);

        }
    }
}
