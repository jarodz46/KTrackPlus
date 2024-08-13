using Android.Bluetooth;
using Android.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Android.Renderscripts.Sampler;

namespace KTrackPlus.Helpers.Client
{
    internal class GattCallback : BluetoothGattCallback
    {

        bool result = false;
        bool waitingForResult = false;

        public bool WriteCharacteristic(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, byte[] value)
        {
            if (waitingForResult)
            {
                Console.WriteLine("Already wiating for result, abord");
                return false;
            }
            if (gatt == null)
                return false;
            if (characteristic == null)
                return false;

            characteristic.SetValue(value);
            characteristic.WriteType = GattWriteType.Default;
            waitingForResult = true;
            result = false;
            if (!gatt.WriteCharacteristic(characteristic))
            {
                Console.WriteLine("Fail to write charac");
                return false;
            }

            var tickCount = Environment.TickCount;
            while (waitingForResult)
            {
                if (Environment.TickCount - tickCount > 5000)
                {
                    Console.WriteLine("Write response timeout");
                    break;
                }
                Thread.Sleep(1);
            }

            waitingForResult = false;
            return result;
        }

         internal static bool firstCo = true;
         static bool refreshTried = false;
        public override void OnServicesDiscovered(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status)
        {
            base.OnServicesDiscovered(gatt, status);
            var service = gatt.GetService(Common.DEVICE_UUID_SERVICE);
            if (firstCo)
            {
                gatt.Class.GetMethod("refresh").Invoke(gatt);
                firstCo = false;
            }            

            if (service == null || firstCo)
            {               
                if (!refreshTried)
                {
                    try
                    {
                        Console.WriteLine("Unable to get service, try to call refresh...");
                        gatt.Class.GetMethod("refresh").Invoke(gatt);
                        refreshTried = true;
                        Thread.Sleep(8000);
                        gatt.DiscoverServices();
                        return;
                    }
                    catch
                    {
                        Console.WriteLine("Unable to call refresh");
                    }
                }
                Console.WriteLine("Unable to get service, try to disable/enable bluetooth");
                return;
            }
            refreshTried = false;
            var carach = service.GetCharacteristic(Common.DEVICE_UUID_CHAR_WRITE);
            if (carach == null)
            {
                Console.WriteLine("Unable to get write charac, try to disable/enable bluetooth");
                return;
            }
            var notifCarac = service.GetCharacteristic(Common.DEVICE_UUID_CHAR_NOTIFY);
            if (notifCarac == null)
            {
                Console.WriteLine("Unable to get notification charac, try to disable/enable bluetooth");
                return;
            }
            var descriptor = notifCarac.GetDescriptor(Common.CLIENT_CHARACTERISTIC_CONFIG_DESCRIPTOR_UUID);
            if (descriptor == null)
            {
                Console.WriteLine("Unable to get notification charac, try to disable/enable bluetooth");
                return;
            }
            ClientManager.Get.CharacteristicWrite = carach;
            gatt.SetCharacteristicNotification(notifCarac, true);
            descriptor.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
            gatt.WriteDescriptor(descriptor);            
        }

        public override void OnDescriptorWrite(BluetoothGatt? gatt, BluetoothGattDescriptor? descriptor, [GeneratedEnum] GattStatus status)
        {
            base.OnDescriptorWrite(gatt, descriptor, status);
            //if (descriptor != null && descriptor.Uuid == Common.CLIENT_CHARACTERISTIC_CONFIG_DESCRIPTOR_UUID)
            //{

                if (status == GattStatus.Success)
                {
                    Console.WriteLine("Notifications enabled");
                    ClientManager.Get.mGatt = gatt;
                    ClientManager.Get.readyToSend = true;
                    KTrackService.ChangeNotifIcon(Resource.Drawable.ic_stat_fiber_smart_record);
                }
                else
                {
                    Console.WriteLine("Unable to enable notifications");
                }
            //}
        }

        public override void OnMtuChanged(BluetoothGatt? gatt, int mtu, [GeneratedEnum] GattStatus status)
        {
            base.OnMtuChanged(gatt, mtu, status);

            if (status == GattStatus.Success && gatt != null)
            {
                gatt.DiscoverServices();
            }
            else
            {
                KTrackService.UsedManager.LastError = "Fail to request mtu";
                Console.WriteLine(KTrackService.UsedManager.LastError);
            }
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic)
        {
            base.OnCharacteristicChanged(gatt, characteristic);
            try
            {
                var value = characteristic.GetValue();
                if (value != null && value.Length > 0)
                {
                    switch (value[0])
                    {
                        case 6:
                            ClientManager.Get.ServerSignalStrength = BitConverter.ToInt32(value, 1);
                            Console.WriteLine("S:" + ClientManager.Get.ServerSignalStrength);
                            break;
                    }
                }
            }
            catch
            {
                Console.WriteLine("OnCharacteristicChanged crash");
            }
            //Console.WriteLine("Notif2 : " + characteristic.GetValue());
        }

        public override void OnCharacteristicWrite(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, [GeneratedEnum] GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);

            if (status != GattStatus.Success)
            {
                if (status == GattStatus.RequestNotSupported)
                {
                    Console.WriteLine("Server don't seem to have id");
                    ClientManager.Get.settingsSent = false;
                }
                else
                    Console.WriteLine("write fail : " + status);
            }
            result = status == GattStatus.Success;
            waitingForResult = false;
        }

        public override void OnConnectionStateChange(BluetoothGatt? gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
        {
            base.OnConnectionStateChange(gatt, status, newState);

            if (gatt == null)
                return;

            Console.WriteLine("Connection state : " + newState);
            if (newState == ProfileState.Connected)
            {
                Console.WriteLine("Sucessful connected to : " + gatt.Device.Name);
                gatt.RequestMtu(236);
            }
            if (newState == ProfileState.Disconnected)
            {
                gatt.Close();
                ClientManager.Get.CharacteristicWrite = null;
                ClientManager.Get.readyToSend = false;
                KTrackService.ChangeNotifIcon(Resource.Drawable.ic_stat_fiber_manual_record);
                if (KTrackService.isRunning && ClientManager.Get.IsRunning)
                {
                    Console.WriteLine("Connection lost...");
                    ClientManager.Get.Reconnect();
                }
            }
        }
    }

}
