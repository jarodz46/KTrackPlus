using Android.Bluetooth;
using Android.Icu.Number;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace KTrackPlus.Helpers.Server
{
    internal class BleGattServerCallback : BluetoothGattServerCallback
    {
        Java.Util.Timer timer = new Java.Util.Timer();


        public BleGattServerCallback()
        {
        }

        public override void OnMtuChanged(BluetoothDevice? device, int mtu)
        {
            base.OnMtuChanged(device, mtu);

            Console.WriteLine("New mtu : " + mtu);
        }

        public override void OnCharacteristicReadRequest(BluetoothDevice device, int requestId, int offset,
            BluetoothGattCharacteristic characteristic)
        {

            Console.WriteLine("Read request from {0}", device.Name);
            ServerManager.Get._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, offset, characteristic.GetValue());
        }

        public override void OnCharacteristicWriteRequest(BluetoothDevice device, int requestId, BluetoothGattCharacteristic characteristic,
            bool preparedWrite, bool responseNeeded, int offset, byte[] value)
        {
            //Console.WriteLine("Receice something");
            var resultStatus = GattStatus.Success;
            try
            {
                if (!ServerManager.Get.HandleRequest(value))
                    resultStatus = GattStatus.RequestNotSupported;  // Tell client don't have ID
            }
            catch (Exception e)
            {
                Console.WriteLine("HandleRequest crash : " + Environment.NewLine + e.Message);
                return;
            }

            if (responseNeeded)
            {
                ServerManager.Get._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, offset, []);
            }

        }

        public override void OnConnectionStateChange(BluetoothDevice device, ProfileState status, ProfileState newState)
        {
            Console.WriteLine("State changed to {0}", newState);
            if (newState == ProfileState.Connected)
                ServerManager.Get.ConnectedDevice = device;
            else
                ServerManager.Get.ConnectedDevice = null;

        }

        public override void OnDescriptorWriteRequest(BluetoothDevice? device, int requestId, BluetoothGattDescriptor? descriptor, bool preparedWrite, bool responseNeeded, int offset, byte[]? value)
        {
            if (responseNeeded)
                ServerManager.Get._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, offset, value);
        }

        public override void OnDescriptorReadRequest(BluetoothDevice? device, int requestId, int offset, BluetoothGattDescriptor? descriptor)
        {
            ServerManager.Get._bluetoothServer.SendResponse(device, requestId, GattStatus.Success, offset, descriptor.GetValue());
        }

        public override void OnNotificationSent(BluetoothDevice device, GattStatus status)
        {


        }

    }
}
