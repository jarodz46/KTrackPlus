using Android.Content;
using Android.OS;
using Android.Preferences;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Client
{
    [Activity(Label = "Settings", Theme = "@style/Theme.AppCompat", ParentActivity = typeof(MainActivity))]
    public class SettingsActivity : AppCompatActivity
    {

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (item.ItemId == Resource.Id.home)
                NavUtils.NavigateUpFromSameTask(this);
            return base.OnOptionsItemSelected(item);
        }


        protected override void OnCreate(Bundle? savedInstanceState)
        {

            base.OnCreate(savedInstanceState);
            ActionBar?.SetDisplayHomeAsUpEnabled(true);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            }
            Window.DecorView.SetOnApplyWindowInsetsListener(new WindowInsetsListener());

            if (savedInstanceState == null)
            {
                SupportFragmentManager.BeginTransaction().
                    Replace(Android.Resource.Id.Content, new SettingsFragment()).Commit();
            }
            
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
        {

            if (requestCode == 654)
            {
                Xamarin.Essentials.Preferences.Set("showStartAsk", Android.Provider.Settings.CanDrawOverlays(this));                
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }
    }

}
