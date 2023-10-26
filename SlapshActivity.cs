namespace KTrackPlus;
using Android.App;

[Activity(Label = "@string/app_name", MainLauncher = true, NoHistory = true, Theme = "@style/Theme.AppCompat")]
public class SlapshActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        SetContentView(Resource.Layout.splash_layout);

    }

    protected override void OnResume()
    {
        base.OnResume();
        new Task(() =>
        {
            StartMainActivity();
        }).Start();
    }

    void StartMainActivity()
    {
        //var nDialog = new ProgressDialog(this);
        //nDialog.SetMessage("Loading...");
        //nDialog.SetCancelable(false);
        //nDialog.Show();
        StartActivity(new Android.Content.Intent(Application.Context, typeof(MainActivity)));
    }
}