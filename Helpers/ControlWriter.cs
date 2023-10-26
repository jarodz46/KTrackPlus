using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTrackPlus.Helpers
{
    public class ControlWriter : TextWriter
    {
        public TextView? textView;
        public ScrollView? scrollview;
        public MainActivity? mainActivity;
        public StringBuilder stringBuilder;
        public ControlWriter()
        {
            stringBuilder = new StringBuilder();
        }

        public void Set(MainActivity mainActivity, TextView textView, ScrollView scrollview)
        {
            this.mainActivity = mainActivity;
            this.textView = textView;
            this.scrollview = scrollview;
        }

        void checkLenght()
        {
            if (stringBuilder.Length > 10000)
            {
                stringBuilder.Remove(0, 200);
                if (mainActivity != null)
                {
                    mainActivity.RunOnUiThread(() =>
                    {
                        textView.Text = stringBuilder.ToString();
                        scrollview.FullScroll(Android.Views.FocusSearchDirection.Down);
                    });
                }
            }
        }

        public override void Write(char value)
        {
            try
            {
                checkLenght();
                stringBuilder.Append(value);
                if (mainActivity != null)
                {
                    mainActivity.RunOnUiThread(() =>
                    {
                        textView.Text += value;
                        scrollview.FullScroll(Android.Views.FocusSearchDirection.Down);
                    });
                }
            }
            catch { }
        }

        public override void Write(string? value)
        {
            try
            {
                checkLenght();
                stringBuilder.Append(value);
                if (mainActivity != null)
                {
                    mainActivity.RunOnUiThread(() =>
                    {
                        textView.Text += value;
                        scrollview.FullScroll(Android.Views.FocusSearchDirection.Down);
                    });
                }
            }
            catch { }
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
