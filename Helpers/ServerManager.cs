using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Android.Provider.CallLog;
using Xamarin.Essentials;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;
using Android.Health.Connect.DataTypes.Units;
using KTrackPlus.Helpers.Client;
using KTrackPlus.Helpers.Server;

namespace KTrackPlus.Helpers
{
    internal class ServerManager : Manager
    {


        static ServerManager? instance = null;
        static internal Manager Init(Context context)
        {
            instance = new ServerManager(context);
            return instance;
        }
        internal static ServerManager Get
        {
            get
            {
                if (instance == null)
                {
                    throw new System.Exception("Server manager instance not set");
                }
                return instance;
            }
        }

        public ServerManager(Context context) : base(context)
        {
            bluetoothReceiver = new ServerBluetoothReceiver(this);
        }

        private BluetoothManager? _bluetoothManager;
        private BluetoothAdapter? _bluetoothAdapter;
        private Server.BleGattServerCallback _bluettothServerCallback;
        internal BluetoothGattServer? _bluetoothServer;
        private BluetoothGattCharacteristic _characteristicWrite;
        private BluetoothGattCharacteristic _characteristicNotify;
        private BluetoothGattDescriptor? _descriptorNotify;
        private Server.BleAdvertiseCallback _bleAdvertiseCallback;
        BluetoothLeAdvertiser? _BluetoothLeAdvertiser;
        ServerBluetoothReceiver bluetoothReceiver { get; set; }

        protected override bool InternalStart()
        {
            _bluetoothManager = (BluetoothManager)mContext.GetSystemService(Context.BluetoothService);
            _bluetoothAdapter = _bluetoothManager?.Adapter;

            if (_bluetoothAdapter == null)
            {
                Console.WriteLine("Can't get bluetooth adapter");
                return false;
            }

            if (!_bluetoothAdapter.IsEnabled)
            {
                Console.WriteLine("Bluetooth is not enabled !");
                return false;
            }

            mContext?.RegisterReceiver(bluetoothReceiver, new IntentFilter(BluetoothAdapter.ActionStateChanged));

            _bluettothServerCallback = new Server.BleGattServerCallback();
            if (_bluettothServerCallback == null || _bluetoothManager == null)
                return false;

            waitForId = true;

            ImgurUpload.Clear();

            if (!StartAdvertising())
            {
                Console.WriteLine("Fail to start adverstising");
                return false;
            }
            if (!StartServer())
            {
                Console.WriteLine("Fail to start ble server");
                StopAdvsersting();
                return false;
            }

            return true;
        }

        internal bool StartServer()
        {
            _bluetoothServer = _bluetoothManager.OpenGattServer(mContext, _bluettothServerCallback);

            var service = new BluetoothGattService(Common.DEVICE_UUID_SERVICE, GattServiceType.Primary);

            _characteristicWrite = new BluetoothGattCharacteristic(Common.DEVICE_UUID_CHAR_WRITE, GattProperty.Write, GattPermission.Write);
            _characteristicWrite.AddDescriptor(new BluetoothGattDescriptor(UUID.FromString("28765900-7498-4bd4-aa9e-46c4a4fb7b07"),
                    GattDescriptorPermission.Read | GattDescriptorPermission.Write));
            service.AddCharacteristic(_characteristicWrite);

            _characteristicNotify = new BluetoothGattCharacteristic(Common.DEVICE_UUID_CHAR_NOTIFY, GattProperty.Read | GattProperty.Notify, GattPermission.Read | GattPermission.Write);
            _characteristicNotify.AddDescriptor(new BluetoothGattDescriptor(UUID.FromString("28765900-7498-4bd4-aa9e-46c4a4fb7b07"),
                    GattDescriptorPermission.Read | GattDescriptorPermission.Write));
            _descriptorNotify = new BluetoothGattDescriptor(Common.CLIENT_CHARACTERISTIC_CONFIG_DESCRIPTOR_UUID, GattDescriptorPermission.Read | GattDescriptorPermission.Write);
            _descriptorNotify?.SetValue(BluetoothGattDescriptor.EnableNotificationValue?.ToArray());
            _characteristicNotify.AddDescriptor(_descriptorNotify);
            service.AddCharacteristic(_characteristicNotify);

            _bluetoothServer?.AddService(service);
            return true;
        }

        internal void StopServer()
        {
            //_bluetoothServer?.ClearServices();
            _bluetoothServer?.Close();
           
        }

        internal bool StartAdvertising()
        {
            _BluetoothLeAdvertiser = _bluetoothAdapter.BluetoothLeAdvertiser;

            if (_BluetoothLeAdvertiser != null)
            {

                var builder = new AdvertiseSettings.Builder();
                builder.SetAdvertiseMode(AdvertiseMode.LowLatency);
                builder.SetConnectable(true);
                builder.SetTimeout(0);
                builder.SetTxPowerLevel(AdvertiseTx.PowerHigh);
                AdvertiseData.Builder dataBuilder = new AdvertiseData.Builder();
                dataBuilder.SetIncludeDeviceName(false);
                dataBuilder.AddServiceUuid(new Android.OS.ParcelUuid(Common.DEVICE_UUID_SERVICE));
                dataBuilder.SetIncludeTxPowerLevel(false);

                _bleAdvertiseCallback = new BleAdvertiseCallback();
                _BluetoothLeAdvertiser.StartAdvertising(builder.Build(), dataBuilder.Build(), _bleAdvertiseCallback);
                return true;

            }
            return false;
        }

        internal void StopAdvsersting()
        {
            _BluetoothLeAdvertiser?.StopAdvertising(_bleAdvertiseCallback);
        }

        protected override void InternalStop()
        {
            new Task(() =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Console.WriteLine("Stop ble server...");
                    StopServer();
                    StopAdvsersting();
                });
            }).Start();
            
        }

        protected override void InternalReset()
        {
            
        }


        int receiveCount = 0;

        internal int updateInterval = 30;
        bool waitForId = false;
        
        NumberFormatInfo nfi = new NumberFormatInfo()
        {
            NumberDecimalSeparator = "."
        };

        async protected override void TimerTask()
        {
            if (receiveCount > 0)
            {
                //Console.WriteLine(receiveCount + " locations received");
                receiveCount = 0;
            }

            if (string.IsNullOrEmpty(UsedId))
            {
                if (!waitForId)
                {
                    Console.WriteLine("Wait for an id...");
                    waitForId = true;
                }
                return;
            }

            if (!checkInternet())
                return;

            if (AskSendMail && Settings != null)
            {
                if (await SendMails())
                    AskSendMail = false;
            }

            if (AskForReset)
            {
                if (!await TryAskResetToAPI())
                {
                    return;
                }
                AskForReset = false;
            }

            if (!await SendPictures())
                return;

            if (!await SendPositions())
                return;

            await sendStats();
        }

        internal bool HandleRequest(byte[] value)
        {
            if (value.Length > 0)
            {
                var firstBye = value[0];
                if (firstBye != bID && string.IsNullOrEmpty(UsedId))
                {
                    return false;
                }
                switch (firstBye)
                {
                    case bRESET:
                        Console.WriteLine("Receive reset request !");
                        Reset();
                        AskForReset = true;
                        break;
                    case bSTATS:
                        if (value.Length >= 1 + 4 * 4)
                        {
                            Stats.rideTime = BitConverter.ToUInt32(value, 1);
                            Stats.ascend = BitConverter.ToSingle(value, 5);
                            Stats.distance = BitConverter.ToSingle(value, 9);
                            Stats.avgSpeed = BitConverter.ToSingle(value, 13);
                        }
                        break;
                    case bID:
                        if (value.Length > 1)
                        {
                            UsedId = Encoding.ASCII.GetString(value, 1, value.Length - 1);
                            Console.WriteLine("Received id : " + UsedId);
                        }
                        break;
                    case bSETTINGS:
                        Settings = new Settings(value);
                        Console.WriteLine("Received settings");// : " + Environment.NewLine + Settings);
                        ChangeInterval(Settings.UpdateInterval);
                        AskSendMail = Settings.AskSendMail;

                        break;
                    case bLOCLIST:
                        MemoryStream memoryStream = new MemoryStream(value, 1, value.Length - 1);
                        GZipStream zip = new GZipStream(memoryStream, CompressionMode.Decompress, false);
                        List<SimpleLocation> newLocs = new();
                        byte[] buffer = new byte[16];
                        while (zip.Read(buffer, 0, buffer.Length) > 0)
                        {
                            var tt = BitConverter.ToUInt32(buffer, 0);
                            var lat = BitConverter.ToSingle(buffer, 4);
                            var lng = BitConverter.ToSingle(buffer, 8);
                            var alt = BitConverter.ToSingle(buffer, 12);
                            var newLoc = new SimpleLocation(lat, lng, tt, alt);
                            newLocs.Add(newLoc);
                        }
                        zip.Close();
                        lock (locations)
                        {
                            locations.AddRange(newLocs);
                        }
                        break;
                }
            }
            return true;
        }
        
        
    }
}
