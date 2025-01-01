using Android.Content;
using IO.Hammerhead.Karooext.Extension;
using IO.Hammerhead.Karooext.Internal;
using IO.Hammerhead.Karooext.Models;
using karooext.dotnet.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace KTrackPlus
{
    //Data to show time span since last location send success
    internal class KTrackStatusData : DataTypeImpl
    {
        public KTrackStatusData() : base("ktrackplus", "ktrackStatus")
        {
        }

        public override void StartView(Context context, ViewConfig config, ViewEmitter emitter)
        {
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (object? sender, ElapsedEventArgs e) =>
            {
                var textColor = System.Drawing.Color.Black;
                if (context.Resources.Configuration.IsNightModeActive)
                    textColor = System.Drawing.Color.White;
                var txt = "-";
                if (KTrackService.UsedManager != null && KTrackService.UsedManager.LastSendPosSuccess != null)
                {
                    TimeSpan? duration = DateTime.Now - KTrackService.UsedManager.LastSendPosSuccess;
                    if (duration != null)
                        txt = $"{(int)duration.Value.TotalMinutes}:{duration.Value.Seconds:D2}";
                }
                var ss = new ShowCustomStreamState(txt, new Java.Lang.Integer(textColor.ToArgb()));
                emitter.OnNext(ss);
            };
            timer.Start();

            emitter.SetCancellable(new Func0(() =>
            {
                timer.Stop();
                timer.Dispose();
            }));
        }
    }
}
