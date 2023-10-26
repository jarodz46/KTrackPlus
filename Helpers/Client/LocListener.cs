using Android.Locations;
using Android.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers.Client
{
    class LocListener : Java.Lang.Object, ILocationListener
    {

        Action<Android.Locations.Location> action;
        public LocListener(Action<Android.Locations.Location> action)
        {
            this.action = action;
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            action(location);
        }

        public void OnProviderDisabled(string provider)
        {
        }

        public void OnProviderEnabled(string provider)
        {
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras)
        {
        }
    }
}
