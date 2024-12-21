using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Preferences;
using Android.Runtime;
using Android.Service.Autofill;
using Android.Text;
using Android.Views;
using AndroidX.Core.Text;
using AndroidX.Preference;
using Java.Interop;
using Java.Lang;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Client
{
    internal class SettingsFragment : PreferenceFragmentCompat
    {

        class IdSummaryProvider : Java.Lang.Object, AndroidX.Preference.ListPreference.ISummaryProvider
        {

            public ICharSequence? ProvideSummaryFormatted(AndroidX.Preference.Preference p0)
            {

                return CharSequence.ArrayFromStringArray([p0.SharedPreferences.GetString(p0.Key, Common.RandomId())])[0];
            }
        }

        AndroidX.Preference.SwitchPreference? showStartAsk;

        public override void OnResume()
        {
            if (showStartAsk != null)
                showStartAsk.Checked = Xamarin.Essentials.Preferences.Get("showStartAsk", false);
            base.OnResume();
        }

        public override void OnStop()
        {
            base.OnStop();
        }

        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            
            PreferenceScreen = PreferenceManager.CreatePreferenceScreen(Context);

            if (Common.CurrentAppMode != Common.AppMode.Server)
            {
                
                var prefCat = new AndroidX.Preference.PreferenceCategory(Context);
                prefCat.Title = "General settings";
                PreferenceScreen.AddPreference(prefCat);

                var riderName = new AndroidX.Preference.EditTextPreference(Context);
                riderName.Title = "Name (showed on tracking site and mail)";
                riderName.Key = "riderName";
                riderName.SetDefaultValue("Anon");
                riderName.SummaryProvider = AndroidX.Preference.EditTextPreference.SimpleSummaryProvider.Instance;
                prefCat.AddPreference(riderName);

                var sendMailTo = new AndroidX.Preference.EditTextPreference(Context);
                sendMailTo.Title = "Send tracking link mail to (separate with ; char)";
                sendMailTo.Key = "sendMailTo";
                sendMailTo.SetDefaultValue("");
                sendMailTo.SummaryProvider = AndroidX.Preference.EditTextPreference.SimpleSummaryProvider.Instance;
                prefCat.AddPreference(sendMailTo);                
                
                var simplePreference = new AndroidX.Preference.ListPreference(Context);
                simplePreference.Title = "Tracking ID";
                simplePreference.Key = "trackId";
                simplePreference.SummaryProvider = new IdSummaryProvider();
                simplePreference.SetEntries(new[] { "Regen it" });
                simplePreference.SetEntryValues(new[] { Common.RandomId() });
                simplePreference.SetDefaultValue(Common.RandomId());
                simplePreference.PreferenceClick += delegate
                {
                    simplePreference.SetEntryValues(new[] { Common.RandomId() });
                };
                prefCat.AddPreference(simplePreference);
                
                var autoReset = new AndroidX.Preference.SwitchPreference(Context);
                autoReset.Title = "Auto reset tracking infos if day changed";
                autoReset.Key = "autoReset";
                autoReset.SetDefaultValue(true);
                prefCat.AddPreference(autoReset);

                if (Common.IsKarooDevice)
                {
                    if (!Android.Provider.Settings.CanDrawOverlays(Context))
                    {
                        Xamarin.Essentials.Preferences.Set("showStartAsk", false);
                    }
                    showStartAsk = new AndroidX.Preference.SwitchPreference(Context);
                    showStartAsk.Title = "Show dialog on new karoo activity";
                    showStartAsk.Key = "showStartAsk";
                    showStartAsk.SetDefaultValue(false);
                    showStartAsk.PreferenceChange += delegate (object? sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs e)
                    {
                        if ((bool)e.NewValue)
                        {
                            if (!Android.Provider.Settings.CanDrawOverlays(Context))
                            {
                                Common.ShowAlert(Activity, "You need to enable overlay permission manualy", () =>
                                {
                                    Intent intent = new Intent(Android.Provider.Settings.ActionManageOverlayPermission);
                                    intent.SetData(Android.Net.Uri.FromParts("package", Activity.PackageName, null));
                                    Activity.StartActivityForResult(intent, 654);
                                });
                            }
                        }
                    };
                    prefCat.AddPreference(showStartAsk);
                }


                if (Common.CurrentAppMode == Common.AppMode.Client)
                {
                    var relayName = new AndroidX.Preference.ListPreference(Context);
                    relayName.Title = "Phone Relay";
                    relayName.Key = "blerelay";
                    relayName.SetDefaultValue("auto");
                    var entries = new List<string>(["First Found"]);
                    var values = new List<string>(["auto"]);
                    if (Activity != null && Activity.CheckSelfPermission(Android.Manifest.Permission.BluetoothConnect) == Permission.Granted)
                    {
                        var adapter = BluetoothAdapter.DefaultAdapter;
                        if (adapter?.BondedDevices != null)
                        {
                            foreach (var device in adapter.BondedDevices)
                            {
                                if (device.Name != null && device.Address != null)
                                {
                                    entries.Add(device.Name);
                                    values.Add(device.Address);
                                }
                            }
                        }
                    }
                    relayName.SetEntries(entries.ToArray());
                    relayName.SetEntryValues(values.ToArray());
                    relayName.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
                    relayName.PreferenceChange += RelayName_PreferenceChange;
                    prefCat.AddPreference(relayName);
                }
            }

            var prefCat2 = new AndroidX.Preference.PreferenceCategory(Context);
            prefCat2.Title = "Advanced settings";
            PreferenceScreen.AddPreference(prefCat2);

            var workingMode = new AndroidX.Preference.ListPreference(Context);
            workingMode.Title = "Working mode";
            workingMode.Key = "workingMode";
            workingMode.SetDefaultValue("Auto");
            workingMode.SetEntries(new[] { "Auto", "Standalone" });
            workingMode.SetEntryValues(new[] { "Auto", "Standalone" });
            workingMode.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
            workingMode.PreferenceChange += WorkingMode_PreferenceChange;
            prefCat2.AddPreference(workingMode);

            var serviceOnBoot = new AndroidX.Preference.SwitchPreference(Context);
            serviceOnBoot.Title = "Start Service on boot";
            serviceOnBoot.Key = "serviceOnBoot";
            serviceOnBoot.SetDefaultValue(true);
            prefCat2.AddPreference(serviceOnBoot);

            if (Common.CurrentAppMode == Common.AppMode.Server)
            {
                var sendInternetStatus = new AndroidX.Preference.SwitchPreference(Context);
                sendInternetStatus.Title = "Send internet status to client";
                sendInternetStatus.Key = "sendInternetStatus";
                sendInternetStatus.SetDefaultValue(false);
                sendInternetStatus.PreferenceChange += SendInternetStatus_PreferenceChange;
                if (Context.CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
                    Xamarin.Essentials.Preferences.Set("sendInternetStatus", false);
                prefCat2.AddPreference(sendInternetStatus);
            }

            if (Common.CurrentAppMode != Common.AppMode.Server)
            {

                var updateIntervalPref = new AndroidX.Preference.ListPreference(Context);
                updateIntervalPref.Title = "Update Interval";
                updateIntervalPref.Key = "updateInterval";
                updateIntervalPref.SetDefaultValue("30");
                updateIntervalPref.SetEntries(new[] { "5 seconds", "10 secondes", "20 seconds", "30 seconds (default)", "1 minute", "2 minutes", "3 minutes", "5 minutes", "10 minutes" });
                updateIntervalPref.SetEntryValues(new[] { "5", "10", "20", "30", "60", "120", "180", "300", "600" });
                updateIntervalPref.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
                prefCat2.AddPreference(updateIntervalPref);

                var minDistance = new AndroidX.Preference.ListPreference(Context);
                minDistance.Title = "Locations min. distance";
                minDistance.Key = "minDistance";
                minDistance.SetDefaultValue("3");
                minDistance.SetEntries(new[] { "1 meter", "2 meters", "3 meters (default)", "4 meters", "5 meters" });
                minDistance.SetEntryValues(new[] { "1", "2", "3", "4", "5" });
                minDistance.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
                prefCat2.AddPreference(minDistance);

                var locProvider = new AndroidX.Preference.ListPreference(Context);
                locProvider.Title = "Locations provider";
                locProvider.Key = "locationsProvider";
                locProvider.SetDefaultValue(Common.IsKarooDevice ? "current" : "gps");
                locProvider.SetEntries(new[] { "Current Karoo acticity", "Device GPS", "Last Karoo activity (for testing)" });
                locProvider.SetEntryValues(new[] { "current", "gps", "last" });
                locProvider.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
                prefCat2.AddPreference(locProvider);

                if (Common.IsKarooDevice && Common.CurrentAppMode == Common.AppMode.Client)
                {
                    clientServerBehavior = new AndroidX.Preference.ListPreference(Context);
                    clientServerBehavior.Title = "Client/Server Behavior";
                    clientServerBehavior.Key = "clientServerBehavior";
                    clientServerBehavior.SetDefaultValue("always");
                    clientServerBehavior.SetEntries(new[] { "Always send data to server", "Try to send data direclty if server connection is weak", "Try to send data directly if server connection is lost" });
                    clientServerBehavior.SetEntryValues(new[] { "always", "onweak", "onlost" });
                    clientServerBehavior.SummaryProvider = AndroidX.Preference.ListPreference.SimpleSummaryProvider.Instance;
                    clientServerBehavior.PreferenceChange += ClientServerBehavior_PreferenceChange;
                    if (Context.CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
                    {
                        Xamarin.Essentials.Preferences.Set("clientServerBehavior", "always");
                    }
                    prefCat2.AddPreference(clientServerBehavior);
                }

            }

        }

        private void SendInternetStatus_PreferenceChange(object? sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs e)
        {
            if (Context.CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
            {
                Common.ShowAlert(Context, "Next request permission is requierd to get device internet status, you need to enable this permission on server device too", delegate
                {
                    RequestPermissions([Android.Manifest.Permission.ReadPhoneState], 1337);
                });
            }
        }

        AndroidX.Preference.ListPreference clientServerBehavior;

        private void ClientServerBehavior_PreferenceChange(object? sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs e)
        {
            if (e.NewValue.ToString() == "always")
                return;
            if (Context.CheckSelfPermission(Android.Manifest.Permission.ReadPhoneState) != Permission.Granted)
            {
                clientServerBehavior.Value = "always";
                Common.ShowAlert(Context, "Next request permission is requierd to get device internet status, you need to enable this permission on server device too", delegate
                {
                    RequestPermissions([Android.Manifest.Permission.ReadPhoneState], 1337);
                });
            }
        }

        private void RelayName_PreferenceChange(object? sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs e)
        {
            Console.WriteLine("Select relay : " + e.NewValue);
        }

        private void WorkingMode_PreferenceChange(object? sender, AndroidX.Preference.Preference.PreferenceChangeEventArgs e)
        {
            if (Activity != null)
            {

                Activity.Finish();
            }
        }
    }
}
