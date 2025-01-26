using Android.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus
{
    internal class WindowInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            // Ajoutez du padding selon le notch / la barre d'état
            v.SetPadding(insets.SystemWindowInsetLeft, insets.SystemWindowInsetTop,
                         insets.SystemWindowInsetRight, insets.SystemWindowInsetBottom);
            return insets.ConsumeSystemWindowInsets();
        }
    }

}
