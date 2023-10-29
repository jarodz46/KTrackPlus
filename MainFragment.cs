using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xamarin.Essentials;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;
using KTrackPlus.Helpers;

namespace KTrackPlus
{
    internal class MainFragment : AndroidX.Fragment.App.Fragment
    {
        public override View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            View layout;

            layout = inflater.Inflate(Resource.Layout.fragment_client, container, false);
            var maintActivity = Activity as MainActivity;
            if (layout != null && maintActivity != null)
            {
                var startBut = layout.FindViewById<Button>(Resource.Id.startLTbut);
                var startMailBut = layout.FindViewById<Button>(Resource.Id.startMailBut);
                var stopBut = layout.FindViewById<Button>(Resource.Id.stopBut);
                var resetBut = layout.FindViewById<Button>(Resource.Id.ResetBut);



                if (startBut != null && startMailBut != null && stopBut != null && resetBut != null)
                {
                    Action setVisibiliy = delegate
                    {
                        if (KTrackService.isRunning && ClientManager.Get.IsRunning)
                        {
                            startBut.Visibility = ViewStates.Gone;
                            stopBut.Visibility = ViewStates.Visible;
                        }
                        else
                        {
                            startBut.Visibility = ViewStates.Visible;
                            stopBut.Visibility = ViewStates.Gone;
                        }
                    };
                    setVisibiliy();

                    Action start = delegate
                    {
                        startBut.Enabled = false;
                        startMailBut.Enabled = false;
                        new Thread(() =>
                        {
                            if (maintActivity.SetService(true))
                            {                                
                                maintActivity.RunOnUiThread(() =>
                                {
                                    ClientManager.Get.Start();                                    
                                });
                            }
                            maintActivity.RunOnUiThread(() =>
                            {
                                startBut.Enabled = true;
                                startMailBut.Enabled = true;
                            });
                        }).Start();
                    };

                    startBut.Click += delegate
                    {
                        start();
                    };

                    startMailBut.Click += delegate
                    {
                        if (!KTrackService.isRunning)
                        {
                            maintActivity.ShowAlert("Start service before");
                            return;
                        }
                        var mailTo = Preferences.Get("sendMailTo", "");
                        if (mailTo.Length == 0 || !mailTo.Contains('@'))
                        {
                            maintActivity.ShowAlert("You must set at least one mail in settings");
                            return;
                        }
                        maintActivity.ShowConfirm("Are you sure to set send mail request ?", (bool val) =>
                        {
                            Console.WriteLine("Add send mail to pending list");
                            maintActivity.delayView(startMailBut);
                            if (val)
                                ClientManager.Get.AskSendMail = true;
                        });
                        
                    };

                    stopBut.Click += delegate
                    {
                        maintActivity.delayView(stopBut);                      
                        maintActivity.RunOnUiThread(() =>
                        {
                            ClientManager.Get.Stop();
                        });
                    };

                    resetBut.Click += delegate
                    {
                        if (!KTrackService.isRunning)
                        {
                            maintActivity.ShowAlert("Start service before");
                            return;
                        }
                        maintActivity.ShowConfirm("Are you sure to reset current tracking infos ?", (bool val) =>
                        {
                            if (val)
                            {
                                maintActivity.delayView(resetBut);
                                Console.WriteLine("Send reset request...");
                                ClientManager.Get.AskForReset = true;
                            }
                        });
                        
                    };
                }

                
            }



            return layout;
        }
    }
}
