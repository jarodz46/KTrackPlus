using KTrackPlus.Helpers;
using Java.Util;
using Android.Content;
using Xamarin.Essentials;
using KTrackPlus.Helpers.Server;
using Android.OS;
using AndroidX.AppCompat.App;
using Android.Views;
using AndroidX.AppCompat.View.Menu;
using AndroidX.Core.App;
using static Xamarin.Essentials.Platform;
using Android.Widget;
using MetadataExtractor.Formats.Exif;
using Android.Provider;
using static Java.Util.Jar.Attributes;
using Android.App;
using Java.IO;
using Console = System.Console;
using Android.Net;
using Android.Telephony;
using Java.Interop;

namespace KTrackPlus
{
    [Activity(Label = "@string/app_name", MainLauncher = false, Theme = "@style/Theme.AppCompat", Exported = false)]
    [IntentFilter([Android.Content.Intent.ActionSend], Categories = [Android.Content.Intent.CategoryDefault], DataMimeType = "image/*")]
    [IntentFilter([Android.Content.Intent.ActionSendMultiple], Categories = [Android.Content.Intent.CategoryDefault], DataMimeType = "image/*")]
    public class MainActivity : AppCompatActivity
    {
        internal static ControlWriter writer = new ControlWriter();

        

        internal static string trackingUrl
        {
            get
            {
                return "https://track.lazyjarod.com/" + KTrackService.UsedManager?.UsedId;
            }
        }

        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
           
            if (Common.CurrentAppMode == Common.AppMode.Server || (Common.CurrentAppMode == Common.AppMode.Standalone && !Common.IsKarooDevice))
            {
                MenuInflater.Inflate(Resource.Layout.server_menu, menu);
            }
            else
            {
                MenuInflater.Inflate(Resource.Layout.client_menu, menu);               
            }
            return true;
        }

        public void ShowConfirm(string text, Action<bool> after)
        {
            var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(this);
            builder.SetMessage(text);
            builder.SetPositiveButton("YES", new EventHandler<DialogClickEventArgs>(delegate { after?.Invoke(true); }));
            builder.SetNegativeButton("NO", new EventHandler<DialogClickEventArgs>(delegate { after?.Invoke(false); }));
            var alertDialog = builder.Create();
            alertDialog.Show();
        }

        public void ShowAlert(string text, Action? after = null)
        {
            Common.ShowAlert(this, text, after);
        }

        bool CheckIdWithAlert()
        {
            if (string.IsNullOrEmpty(KTrackService.UsedManager?.UsedId))
            {
                ShowAlert("No tracking id received yet");
                return false;
            }
            return true;
        }

        ProgressDialog? nDialog;

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.opensettings:
                    if (KTrackService.isRunning)
                    {
                        ShowAlert("Stop service before");
                        return false;
                    }
                    nDialog = new ProgressDialog(this);
                    nDialog.SetMessage("Loading...");
                    nDialog.SetCancelable(false);
                    nDialog.Show();
                    StartActivity(new Android.Content.Intent(this, typeof(Helpers.Client.SettingsActivity)));
                    //nDialog.Dismiss();
                    break;
                case Resource.Id.opentracksite:
                    if (CheckIdWithAlert())
                    {
                        Xamarin.Essentials.Browser.OpenAsync(MainActivity.trackingUrl, BrowserLaunchMode.SystemPreferred);
                    }
                    break;
                case Resource.Id.shareUrl:
                    if (CheckIdWithAlert())
                    {
                        Task.Run(async () =>
                        {
                            await Share.RequestAsync(new ShareTextRequest
                            {
                                Uri = MainActivity.trackingUrl,
                                Title = "Share tracking url"
                            });
                        });
                    }
                    break;
                case Resource.Id.takePic:
                    try
                    {
                        Task.Run(async () =>
                        {
                            var photo = await MediaPicker.PickPhotoAsync();
                            ImgurUpload.AddToSend(new ImageInfos(photo));
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    break;
                case Resource.Id.ShotPic:
                    try
                    {
                        Task.Run(async () =>
                        {
                            var photo = await MediaPicker.CapturePhotoAsync();
                            ImgurUpload.AddToSend(new ImageInfos(photo));
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    break;
                //case Resource.Id.crash:
                //    KTrackService.UsedManager.crash = true;
                //    break;
            }
            
            return base.OnOptionsItemSelected(item);
        }

        

        bool firstStart = true;
        protected override void OnResume()
        {
            base.OnResume();
            nDialog?.Dismiss();
            nDialog = null;

            try
            {
                var fixBut = FindViewById<ImageButton>(Resource.Id.fixBackground);
                if (fixBut != null)
                {
                    fixBut.Visibility = CheckBatteryOptimization() ? ViewStates.Gone : ViewStates.Visible;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }



            if (firstStart)
            {
                firstStart = false;
                return;
            }
                       
            
            if (Common.CheckAppMode())
            {
                refreshUiAndFragment(true);
                InvalidateOptionsMenu();
            }
        }

        bool CheckBatteryOptimization()
        {
            var sdk = (int)Build.VERSION.SdkInt;
            var powerMng = GetSystemService(PowerService) as PowerManager;
            if (sdk >= 28 && powerMng != null)
            {
                return powerMng.IsIgnoringBatteryOptimizations(PackageName);
            }
            return true;
        }

        bool CheckPermissionsAndStart()
        {
            var sdk = (int)Build.VERSION.SdkInt;


            var locProvider = Preferences.Get("locationsProvider", Common.IsKarooDevice ? "current" : "gps");
            if (locProvider != "gps" && !Common.IsKarooDevice)
            {
                Console.WriteLine("It's not a Karoo device, switch to gps loc provider");
                Preferences.Set("locationsProvider", "gps");
                locProvider = "gps";
            }

            var permissions = new List<string>();

            if (CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) != Android.Content.PM.Permission.Granted)
            {
                permissions.Add(Android.Manifest.Permission.ReadPhoneState);
            }

            if (Common.CurrentAppMode == Common.AppMode.Server)
            {
               
                if (sdk >= 31 && CheckSelfPermission(Android.Manifest.Permission.BluetoothConnect) != Android.Content.PM.Permission.Granted)
                {
                    permissions.Add(Android.Manifest.Permission.BluetoothConnect);
                }
                if (sdk >= 31 && CheckSelfPermission(Android.Manifest.Permission.BluetoothAdvertise) != Android.Content.PM.Permission.Granted)
                {
                    permissions.Add(Android.Manifest.Permission.BluetoothAdvertise);
                }
            }
            else
            {                
                if (locProvider == "gps")
                {
                    if (CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.AccessCoarseLocation);
                    }
                    if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.AccessFineLocation);
                    }
                    if (sdk >= 29 && CheckSelfPermission(Android.Manifest.Permission.AccessBackgroundLocation) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.AccessBackgroundLocation);
                    }
                }
                else
                {
                    if (Common.IsKarooDevice && sdk >= 31 && !Android.OS.Environment.IsExternalStorageManager)
                    {
                        var line2 = "";
                        string message = "You need to enable access to device files, it's required to app to be able to read the current activity" +
                                System.Environment.NewLine + line2;

                        ShowAlert(message,
                            () =>
                            {
                                //Toast.MakeText(this, line2, ToastLength.Long)?.Show();
                                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                                intent.SetData(Android.Net.Uri.FromParts("package", PackageName, null));
                                StartActivity(intent);
                            });
                        return false;
                    }
                    if (CheckSelfPermission(Android.Manifest.Permission.AccessCoarseLocation) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.AccessCoarseLocation);
                    }
                    if (CheckSelfPermission(Android.Manifest.Permission.AccessFineLocation) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.AccessFineLocation);
                    }
                    if (CheckSelfPermission(Android.Manifest.Permission.BluetoothConnect) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.BluetoothConnect);
                    }
                    if (CheckSelfPermission(Android.Manifest.Permission.BluetoothScan) != Android.Content.PM.Permission.Granted)
                    {
                        permissions.Add(Android.Manifest.Permission.BluetoothScan);
                    }
                    //if (CheckSelfPermission(Android.Manifest.Permission.ReadExternalStorage) != Android.Content.PM.Permission.Granted)
                    //{
                    //    permissions.Add(Android.Manifest.Permission.ReadExternalStorage);
                    //}
                    //if (CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) != Android.Content.PM.Permission.Granted)
                    //{
                    //    permissions.Add(Android.Manifest.Permission.WriteExternalStorage);
                    //}

                    
                }
            }
            if (permissions.Count > 0)
            {
                // Missing persmission(s) try to start after request
                RequestPermissions(permissions.ToArray(), 1337);
            }
            else
            {
                // All permissions ok start direclty
                var intent = new Android.Content.Intent(this, typeof(KTrackService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    StartForegroundService(intent);
                }
                else
                {
                    StartService(intent);
                }
                return true;
            }
            return false;
        }

        void HandleIntent()
        {
            
            var action = Intent?.Action;
            var type = Intent?.Type;
            if (action != null && type != null && ContentResolver != null)
            {
                if (!KTrackService.isRunning)
                {
                    Console.WriteLine("Can't add pictures : service is not running");
                    return;
                }
                if (Android.Content.Intent.ActionSend == action && type.StartsWith("image/"))
                {
                    var extra = Intent?.GetParcelableExtra(Android.Content.Intent.ExtraStream);
                    if (extra != null)
                    {
                        var imageUri = extra as Android.Net.Uri;
                        try
                        {
                            var infos = new ImageInfos(ContentResolver, imageUri, type);
                            ImgurUpload.AddToSend(infos);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Fail to get picture" + System.Environment.NewLine + e);
                        }
                    }                    
                }
                if (Android.Content.Intent.ActionSendMultiple == action && type.StartsWith("image/"))
                {
                    var extras = Intent?.GetParcelableArrayListExtra(Android.Content.Intent.ExtraStream);
                    if (extras != null)
                    {
                        try
                        {
                            foreach (Android.Net.Uri imageUri in extras)
                            {
                                var infos = new ImageInfos(ContentResolver, imageUri, type);
                                ImgurUpload.AddToSend(infos);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Fail to get picture" + System.Environment.NewLine + e);
                        }
                    }
                }
            }
        }

        protected override void OnNewIntent(Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent();
        }

        MainFragment? mainFragment;
        internal void refreshUiAndFragment()
        {
            refreshUiAndFragment(false);
        }
        internal void refreshUiAndFragment(bool force)
        {
            var serviceToggle = FindViewById<Switch>(Resource.Id.serviceSwitch);
            if (serviceToggle != null)
            {
                serviceToggle.Checked = KTrackService.isRunning;
            }
            if (mainFragment != null && (force || !KTrackService.isRunning || Common.CurrentAppMode == Common.AppMode.Server))
            {
                SupportFragmentManager.BeginTransaction().Remove(mainFragment).Commit();
                mainFragment = null;
            }
            if (mainFragment == null && Common.CurrentAppMode != Common.AppMode.Server)
            {
                mainFragment = new MainFragment();
                SupportFragmentManager.BeginTransaction().Add(Resource.Id.fragment_placeholder, mainFragment).Commit();
            }
        }

        public bool SetService(bool state)
        {
            if (KTrackService.isRunning == state) return true;
            
            var tc = System.Environment.TickCount;
            if (state)
            {
                Console.WriteLine("Try to start service");
                if (CheckPermissionsAndStart())
                {
                    while (!KTrackService.isRunning)
                    {
                        if (System.Environment.TickCount - tc > 5000)
                            return false;
                        Thread.Sleep(1);
                    }
                    RunOnUiThread(refreshUiAndFragment);
                    return true;
                }                
                return false;
                
            }
            else
            {
                Console.WriteLine("Try to stop service");
                var intent = new Android.Content.Intent(this, typeof(KTrackService));
                StopService(intent);
                while (KTrackService.isRunning)
                {
                    if (System.Environment.TickCount - tc > 5000)
                        return false; ;
                    Thread.Sleep(1);
                }
                RunOnUiThread(refreshUiAndFragment);
                return true;
            }
        }

        internal static MainActivity? Get { get; private set; }

        
        
        protected override void OnCreate(Bundle? savedInstanceState)
        {

            try
            {
                base.OnCreate(savedInstanceState);


                Platform.Init(this, savedInstanceState);

                Console.SetOut(writer);

                HandleIntent();

                SetContentView(Resource.Layout.main_layout);
                Common.CheckAppMode();

                

                //if (savedInstanceState == null && Common.CurrentAppMode != Common.AppMode.Server)
                refreshUiAndFragment(true);

                var textview = FindViewById<TextView>(Resource.Id.textView1);
                var scrollView = FindViewById<ScrollView>(Resource.Id.SCROLLER_ID);
                //var button = FindViewById<Button>(Resource.Id.sw);
                if (textview != null && scrollView != null)
                {
                    writer.Set(this, textview, scrollView);
                    textview.Text = writer.stringBuilder.ToString();
                    scrollView.FullScroll(Android.Views.FocusSearchDirection.Down);
                }

                //if (KTrackService.isRunning && button != null)
                //{
                //    button.Text = "Stop";
                //}

                var serviceToggle = FindViewById<Switch>(Resource.Id.serviceSwitch);
                if (serviceToggle != null)
                {
                    serviceToggle.Checked = KTrackService.isRunning;
                    serviceToggle.CheckedChange += delegate (object? sender, CompoundButton.CheckedChangeEventArgs e)
                    {
                        serviceToggle.Enabled = false;
                        new Task(() =>
                        {
                            try
                            {
                                SetService(e.IsChecked);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                            finally
                            {
                                RunOnUiThread(() =>
                                {
                                    serviceToggle.Checked = KTrackService.isRunning;
                                    serviceToggle.Enabled = true;
                                });
                            }
                        }).Start();
                    };
                }

                try
                {
                    var fixBut = FindViewById<ImageButton>(Resource.Id.fixBackground);
                    if (fixBut != null)
                    {
                        fixBut.Click += delegate
                        {

                            var line2 = "Battery optimization->All apps->KTrackPlus->Don't optimize";
                            string message = "You need to disable battery optimization to avoid background issues" +
                                    System.Environment.NewLine + line2;

                            ShowAlert(message,
                                () =>
                                {
                                    Toast.MakeText(this, line2, ToastLength.Long)?.Show();
                                    var intent = new Android.Content.Intent(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
                                    StartActivity(intent);
                                });
                        };
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                Get = this;

                

                var statusView = FindViewById<TextView>(Resource.Id.sync_status);
                var statusLabel = FindViewById<TextView>(Resource.Id.sync_led);

                if (statusView != null && statusLabel != null)
                {
                    
                    refreshUITimer.Elapsed += delegate
                    {
                        RunOnUiThread(() =>
                        {

                            statusView.Text = "Stopped";
                            if (KTrackService.isRunning && KTrackService.UsedManager != null)
                            {
                                //var usedData = Common.GetAppNetworkUsage(this, KTrackService.StartTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                //statusLabel.Text = "Status : data usage : " + usedData;
                                statusLabel.Text = "Status :    internet : " + KTrackService.UsedManager.SignalStrength;
                                if (Common.CurrentAppMode == Common.AppMode.Client)
                                {
                                    var manager = ClientManager.Get;
                                    statusView.Text = "Disconnected : ";
                                    if (manager.readyToSend)
                                    {

                                        statusView.Text = "Connected : ";
                                    }
                                    statusView.Text += manager.locations.Count + " locs to send";
                                }
                                else
                                {
                                    var manager = KTrackService.UsedManager;
                                    statusView.Text = string.Empty;
                                    if (manager.internetStatus != null)
                                    {
                                        if (manager.internetStatus == true)
                                        {
                                            statusView.Text += "[Internet OK] ";
                                        }
                                        else
                                        {
                                            statusView.Text += "[No Internet] ";
                                        }
                                    }
                                    statusView.Text += manager.locations.Count + " locations";
                                    var picCount = manager.pictures.Count;
                                    if (picCount > 0)
                                    {
                                        statusView.Text += "and " + picCount + " pic" + (picCount > 1 ? "s" : string.Empty);
                                    }
                                    statusView.Text += " to send";
                                }
                            }

                        });
                    };
                    refreshUITimer.Start();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        

        System.Timers.Timer refreshUITimer = new(1000);

        bool waitForPermResult = false;
        Android.Content.PM.Permission permissionResult = Android.Content.PM.Permission.Denied;

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Android.Content.PM.Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == 1337)
            {
                for (int i = 0; i < permissions.Length; i++)
                {
                    var result = grantResults[i];
                    var perm = permissions[i];
                    if (result == Android.Content.PM.Permission.Denied)
                    {
                        Console.WriteLine("Unable to start without permissions, please enable them again in app settings.");
                        return;
                    }
                }
            }

        }

        public void delayView(View view)
        {
            new Thread(() =>
            {
                RunOnUiThread(() => { view.Enabled = false; });
                Thread.Sleep(3000);
                RunOnUiThread(() => { view.Enabled = true; });
            }).Start();
        }



        protected override void OnDestroy()
        {
            refreshUITimer.Dispose();
            Get = null;
            base.OnDestroy();            
        }
    }
}