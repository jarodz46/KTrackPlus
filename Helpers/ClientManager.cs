using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Icu.Number;
using Android.Widget;
using Java.Lang;
using Java.Util;
using KTrackPlus.Helpers.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynastream.Fit;
using Xamarin.Essentials;
using Android.OS;
using System.IO.Compression;
using Android.Hardware;
using Android.Locations;
using Android.Runtime;
using Xamarin.KotlinX.Coroutines;

namespace KTrackPlus.Helpers
{
    internal class ClientManager : Manager
    {        

        static ClientManager? instance = null;

        internal static Manager Init(Context context)
        {
            instance = new ClientManager(context);
            return instance;
        }
       
        internal static ClientManager Get
        {
            get
            {
                if (instance == null)
                {
                    throw new System.Exception("Client manager instance not set");
                }
                return instance;
            }
        }

        public ClientManager(Context context) : base(context)
        {
            bluetoothReceiver = new ClientBluetoothReceiver(this);
        }


        bool bluetoothtregistered { get; set; } = false;
        Client.ClientBluetoothReceiver bluetoothReceiver { get; set; }
        internal BluetoothGatt? mGatt { get; set; } = null;
        internal bool readyToSend { get; set; } = false;
        internal BluetoothGattCharacteristic? CharacteristicWrite { get; set; }
        internal BluetoothAdapter? adapter { get; set; } = null;
        internal BluetoothLeScanner? scanner { get; set; } = null;
        internal Client.GattCallback GattHelper { get; set; } = new();
        internal string? DeviceAdress { get; set; } = null;
        internal Client.ScanCallback scanCallback { get; set; } = new();

        string locProvider = string.Empty;

        //CancellationTokenSource cts;
        double lastPressure = 0;
        bool pressureSensor = false;
        int lastPressureTT = 0;
        int minDistance = 3;
        LocListener? locListener;
        LocationManager? locMan;


        void handleGpsLoc(Android.Locations.Location location)
        {
            if (location.Accuracy > 6)
                return;
            var dateTT = DateTimeOffset.FromUnixTimeMilliseconds(location.Time);
            var fitTT = (uint)(dateTT.ToUnixTimeSeconds() - Common.UnixFitTTOffset);
            var altitude = (float?)location.Altitude;
            if (pressureSensor && lastPressure != 0 && (System.Environment.TickCount - lastPressureTT) < 5000)
            {
                altitude = SensorManager.GetAltitude(SensorManager.PressureStandardAtmosphere, (float)lastPressure);
            }
            var newLoc = new SimpleLocation((float)location.Latitude, (float)location.Longitude, fitTT, altitude == null ? 0 : (float)altitude);
            Stats.Push(newLoc);
            lock (locations)
            {
                locations.Add(newLoc);
            }
        }

        protected override bool InternalStart()
        {
            if (IsRunning)
                return true;
            locProvider = Preferences.Get("locationsProvider", Common.IsKarooDevice ? "current" : "gps");

            var mintDistStr = Preferences.Get("minDistance", "3");
            minDistance = int.Parse(mintDistStr);
            Console.WriteLine("Min distance between two points : " + minDistance + "m");


            var id = Preferences.Get("trackId", Common.RandomId());
            if (string.IsNullOrEmpty(id) || id.Length < 8)
            {
                id = Common.RandomId();
                Preferences.Set("trackId", id);
            }

            if (locProvider == "gps")
            {
                InitPressureSensor();
                locMan = mContext?.GetSystemService(Context.LocationService) as LocationManager;
                if (locMan != null)
                {
                    if (!locMan.IsProviderEnabled(LocationManager.GpsProvider))
                    {
                        Console.WriteLine("GPS don't seem enabled");
                        return false;
                    }
                    locListener = new LocListener(handleGpsLoc);
                    locMan.RequestLocationUpdates(LocationManager.GpsProvider, 800, minDistance - 0.1f, locListener);
                    
                }
                else
                {
                    Console.WriteLine("Unable to get gps");
                    return false;
                }
            }

            UsedId = id;
            Settings = new Settings();
            
            Console.WriteLine("Used id : " + UsedId);

            if (Common.CurrentAppMode == Common.AppMode.Client)
            {
                mContext?.RegisterReceiver(bluetoothReceiver, new IntentFilter(BluetoothAdapter.ActionStateChanged));

                var newAdapter = BluetoothAdapter.DefaultAdapter;
                if (newAdapter == null)
                {
                    LastError = "Fail to get bluetooth adapter";
                    Console.WriteLine(LastError);
                    return false;
                }
                adapter = newAdapter;

                readyToSend = false;
                settingsSent = false;

                

                if (adapter.IsEnabled)
                {
                    var newScanner = adapter.BluetoothLeScanner;
                    if (newScanner == null)
                    {
                        adapter.Enable();
                        LastError = "Fail to get bluetooth scanner";
                        Console.WriteLine(LastError);
                        return false;
                    }
                    scanner = newScanner;
                    StartScan();
                }
                else
                    adapter?.Enable();
            }

            Toast.MakeText(mContext, "Start livetracking...", ToastLength.Long);
            Console.WriteLine("Start livetracking as " + Common.CurrentAppMode);
            KTrackService.RefreshNotifAction(KTrackReceiverService.KTrackServiceAction.Stop);
            return true;
        }

        void InitPressureSensor()
        {
            Barometer.ReadingChanged += delegate (object? sender, BarometerChangedEventArgs e)
            {
                lastPressure = e.Reading.PressureInHectopascals;
                lastPressureTT = System.Environment.TickCount;
            };
            pressureSensor = true;
            try
            {
                Barometer.Start(SensorSpeed.Default);
            }
            catch
            {
                pressureSensor = false;
            }
            Console.WriteLine("Find barometer sensor");
        }

        protected override void InternalStop()
        {
            try
            {
                if (Barometer.IsMonitoring)
                {

                    Barometer.Stop();
                }
            }
            catch { }
            if (locMan != null && locListener != null)
            {
                locMan.RemoveUpdates(locListener);
            }
            if (mGatt != null)
            {
                mContext?.UnregisterReceiver(bluetoothReceiver);
                mGatt.Disconnect();
                var tickCount = System.Environment.TickCount;
                while (readyToSend)
                {
                    if (System.Environment.TickCount - tickCount > 5000)
                        break;
                    System.Threading.Thread.Sleep(1);
                }
                mGatt.Close();
                mGatt = null;
            }
            Console.WriteLine("Stop livetracking");
            KTrackService.RefreshNotifAction(KTrackReceiverService.KTrackServiceAction.Start);
            Toast.MakeText(mContext, "Stop livetracking", ToastLength.Long);
        }

        internal void Reconnect()
        {

            Console.WriteLine("Try to reconnect...");

            bool canReconnect = false;
            if (!string.IsNullOrEmpty(DeviceAdress))
            {
                var device = adapter.GetRemoteDevice(DeviceAdress);
                if (device != null && device.Type != BluetoothDeviceType.Unknown)
                {
                    canReconnect = true;
                    device.ConnectGatt(mContext, false, GattHelper, BluetoothTransports.Le);
                }
            }
            if (!canReconnect)
            {
                StartScan();
            }

        }

        internal void StartScan()
        {

            Console.WriteLine("Look for ble server...");

            var relay = Preferences.Get("blerelay", "auto");
            if (relay != "auto" && adapter?.BondedDevices != null)
            {
                foreach (var device in adapter.BondedDevices)
                {
                    if (device.Address != null && device.Address == relay)
                    {
                        GattCallback.firstCo = true;
                        Console.WriteLine("Connect to " + device.Name + "...");
                        mGatt = device.ConnectGatt(mContext, false, GattHelper, BluetoothTransports.Le);
                        return;
                    }
                }
            }

            var parcelId = new ParcelUuid(Common.DEVICE_UUID_SERVICE);
            if (parcelId != null)
            {
                var scanFilter = new ScanFilter.Builder()?.SetServiceUuid(parcelId)?.Build();
                var scanSettings = new ScanSettings.Builder()?
                                        .SetScanMode(Android.Bluetooth.LE.ScanMode.LowPower)?
                                        .SetLegacy(false)?.SetReportDelay(500)?
                                        .SetCallbackType(ScanCallbackType.FirstMatch)?
                                        .SetNumOfMatches(1)?.Build();
                
               
                if (scanFilter != null && scanSettings != null && scanner != null)
                {
                    scanner.StartScan([scanFilter], scanSettings, scanCallback);
                }
                else
                {
                    Console.WriteLine("Fail to build scan options");
                }
            }

        }

        
        string currentFitName = string.Empty;
        SimpleLocation? lastSentLocation = null;

        bool updateStatsRequierd = false;
        internal bool settingsSent = false;

        bool notFoundShowed = false;
        bool dirNotFoundShowed = false;

        uint firstLocTT = 0;
        long lastLocTT
        {
            get
            {
                return Preferences.Get("lastLocTT", (long)0);
            }
            set
            {
                Preferences.Set("lastLocTT", value);
            }
        }
        Decode decode = new();
        long lastStreamPosition = 0;

        FileInfo? getMostRecentFitFile()
        {
            var workMode = Preferences.Get("locationsProvider", "current");
            var dirPath = "/sdcard/FitFiles";
            if (workMode == "current")
            {
                dirPath += "/temp";
            }
            var directory = new DirectoryInfo(dirPath);

            if (directory.Exists)
            {
                dirNotFoundShowed = false;
                var now = System.DateTime.Now;
                var prevDate = now.AddHours(-48);
                var files = directory.GetFiles("*.fit", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTime).ToList();
                if (workMode == "current")
                {
                    files = files.Where(f => f.LastWriteTime > prevDate && f.LastWriteTime <= now).ToList();
                }
                return files.FirstOrDefault();
            }
            else
            {
                if (!dirNotFoundShowed)
                {
                    LastError = "Fit directory not found";
                    Console.WriteLine(LastError);
                    dirNotFoundShowed = true;
                }
            }
            return null;
        }

        protected override void InternalReset()
        {
            firstLocTT = 0;
            lastLocTT = 0;
            decode = new Decode();
            lastStreamPosition = 14; // Skip header
            currentFitName = string.Empty;
            //settingsSent = false;
        }

        bool ClientRequestReset()
        {
            if (!GattHelper.WriteCharacteristic(mGatt, CharacteristicWrite, [bRESET]))
            {
                LastError = "Fail to ask for reset...";
                Console.WriteLine(LastError);
                return false;
            }
            return true;
        }

        async Task<bool> ResetRequest()
        {
            if (Common.CurrentAppMode == Common.AppMode.Client)
            {
                return ClientRequestReset();
            }
            else
            {
                return await TryAskResetToAPI();
            }
        }

        bool unableResetShowed = false;
        protected override async void TimerTask()
        {
            try
            {
                if (Common.CurrentAppMode == Common.AppMode.Client && !readyToSend)
                    return;
                if (AskForReset)
                {
                    if ((Common.CurrentAppMode != Common.AppMode.Client && !checkInternet())|| !await ResetRequest())
                    {
                        if (!unableResetShowed)
                        {
                            Console.WriteLine("Can't reset yet, waiting for ble or internet...");
                            unableResetShowed= true;
                        }
                        return;
                    }
                    Console.WriteLine("Reseting...");
                    Reset();
                    unableResetShowed = false;
                    AskForReset = false;
                }
                if (AskSendMail && Settings != null && Common.CurrentAppMode == Common.AppMode.Standalone && checkInternet())
                {
                    //Console.WriteLine("Send mail...");
                    if (await SendMails())
                        AskSendMail = false;
                }
                if ((!settingsSent || AskSendMail) && Common.CurrentAppMode == Common.AppMode.Client)
                {
                    Console.WriteLine("Send settings...");
                    var idMesg = new byte[] { bID }.Concat(Encoding.ASCII.GetBytes(UsedId)).ToArray();
                    if (!GattHelper.WriteCharacteristic(mGatt, CharacteristicWrite, idMesg))
                    {
                        LastError = "Fail to send id...";
                        Console.WriteLine(LastError);
                        return;
                    }
                    System.Threading.Thread.Sleep(200);
                    Settings = new Settings(AskSendMail);
                    if (!GattHelper.WriteCharacteristic(mGatt, CharacteristicWrite, Settings.GetBytes()))
                    {
                        LastError = "Fail to send settings...";
                        Console.WriteLine(LastError);
                        return;
                    }
                    AskSendMail = false;
                    settingsSent = true;
                }
                if (locProvider != "gps")
                {
                    #region readfitfile
                    int addedLocs = 0;
                    var lastFitFile = getMostRecentFitFile();
                    if (lastFitFile != null)
                    {
                        notFoundShowed = false;
                        var stream = lastFitFile.OpenRead();
                        //int count = 0;  
                        if (currentFitName != lastFitFile.Name)
                        {

                            Console.WriteLine("Found new activity" + (locProvider == "last" ? " (test mode)" : ""));
                            
                            Reset();
                            if (!await ResetRequest())
                            {
                                return;
                            }
                            currentFitName = lastFitFile.Name;
                        }

                        var newLocs = new List<SimpleLocation>();
                        var cachedLastLocTT = lastLocTT;
                        decode.MesgEvent += delegate (object sender, MesgEventArgs e)
                        {
                            var mesg = e.mesg;
                            if (mesg.Num == 20) // a record mesg
                            {
                                var recordMesg = new RecordMesg(mesg);
                                var tt = recordMesg.GetTimestamp().GetTimeStamp();
                                if (firstLocTT == 0)
                                {
                                    firstLocTT = tt;
                                }
                                if (cachedLastLocTT > 0 && tt < cachedLastLocTT)
                                {
                                    return;
                                }
                                cachedLastLocTT = tt;
                                var latRaw = recordMesg.GetPositionLat();
                                if (latRaw == null) return;
                                var lat = latRaw * (180.0 / System.Math.Pow(2, 31));
                                var lngRaw = recordMesg.GetPositionLong();
                                if (lngRaw == null) return;
                                var lng = lngRaw * (180.0 / System.Math.Pow(2, 31));
                                var alt = recordMesg.GetAltitude();
                                if (alt == null) alt = 0;
                                var dateTime = new DateTimeOffset(recordMesg.GetTimestamp().GetDateTime());
                                var newLoc = new SimpleLocation((float)lat, (float)lng, tt, (float)alt);
                                newLocs.Add(newLoc);
                                addedLocs++;

                                var altitude = recordMesg.GetAltitude();
                                var speed = recordMesg.GetSpeed();
                                var distance = recordMesg.GetDistance();
                                var time = tt - firstLocTT;
                                Stats.Push(altitude, distance, time, speed);

                            }
                        };

                        stream.Seek(lastStreamPosition, SeekOrigin.Begin);
                        var max = stream.Length - 2;
                        while (stream.Position < max)
                        {
                            if (AskStopTask)
                                return;
                            if (addedLocs >= 3000)
                            {
                                break;
                            }
                            decode.DecodeNextMessage(stream);
                        }
                        lastLocTT = cachedLastLocTT;
                        lastStreamPosition = stream.Position;
                        stream.Close();

                        locations.AddRange(newLocs);

                    }
                    else
                    {
                        if (!notFoundShowed)
                        {
                            Console.WriteLine("Waiting for activity.");
                            notFoundShowed = true;
                        }
                        return;
                    }
                    #endregion 
                }
                else
                { // GPS Provider
                }
                if (Common.CurrentAppMode == Common.AppMode.Client)
                {
                    #region send to relay
                    int sendCount = 0;
                    while (locations.Count > 0)
                    {
                        if (AskStopTask)
                            return;
                        if (!KTrackService.isRunning)
                            return;
                        if (!readyToSend)
                            return;
                        List<SimpleLocation> proceedLocs = new();
                        List<byte> sendBytes = new();
                        int count = 0;
                        MemoryStream memoryStream = new MemoryStream();
                        using (GZipStream zip = new GZipStream(memoryStream, CompressionLevel.SmallestSize, true))
                        {

                            foreach (var loc in locations)
                            {
                                double diff = double.MaxValue;
                                if (lastSentLocation != null)
                                {
                                    diff = lastSentLocation.DistanceTo(loc);
                                }
                                proceedLocs.Add(loc);
                                if (diff > minDistance)
                                {
                                    zip.Write(loc.ToByeArray());
                                    sendCount++;
                                    count++;
                                }
                                if (count >= 30)
                                {
                                    break;
                                }
                            }
                        }

                        memoryStream.Position = 0;
                        if (!GattHelper.WriteCharacteristic(mGatt, CharacteristicWrite, new byte[] { bLOCLIST }.Concat(memoryStream.ToArray()).ToArray()))
                        {
                            LastError = "Fail to send compressed loc list";
                            Console.WriteLine(LastError);
                            memoryStream.Close();
                            return;
                        }
                        updateStatsRequierd = true;
                        memoryStream.Close();

                        locations.RemoveAll(l => proceedLocs.Contains(l));

                        System.Threading.Thread.Sleep(10);
                    }
                    if (updateStatsRequierd)
                    {
                        var statsArray = Stats.ToBytesArray();
                        var statsMessage = new byte[] { bSTATS }.Concat(statsArray).ToArray();
                        if (!GattHelper.WriteCharacteristic(mGatt, CharacteristicWrite, statsMessage))
                        {
                            LastError = "Fail to send stats...";
                            Console.WriteLine(LastError);
                            return;
                        }
                        updateStatsRequierd = false;
                    }
                    #endregion 
                }
                if (Common.CurrentAppMode == Common.AppMode.Standalone)
                {
                    #region send directly
                    if (!checkInternet())
                        return;
                    if (!await SendPictures())
                        return;
                    if (!await SendPositions())
                        return;
                    await sendStats();
                    #endregion
                }
                
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
