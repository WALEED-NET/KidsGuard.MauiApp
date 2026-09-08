using Android.Widget;
using KidsGuard.App.Security;

namespace KidsGuard.App;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    private ReleaseManager? _releaseManager;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _releaseManager = new ReleaseManager(this);

        FindViewById<TextView>(Resource.Id.package_name)!.Text = PackageName;
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshStatus();
    }

    /// <summary>
    /// تُقرأ الحالة من النظام في كل ظهور للشاشة لا مرّة واحدة عند الإنشاء،
    /// لأن dpm set-device-owner يُنفَّذ من adb والتطبيق يعمل — فلا حدث يُبلّغنا.
    /// </summary>
    private void RefreshStatus()
    {
        if (_releaseManager is null)
        {
            return;
        }

        var isAdmin = _releaseManager.IsAdminActive;
        var isOwner = _releaseManager.IsDeviceOwner;

        FindViewById<TextView>(Resource.Id.status_admin)!.Text =
            GetString(isAdmin ? Resource.String.status_admin_yes : Resource.String.status_admin_no);

        FindViewById<TextView>(Resource.Id.status_owner)!.Text =
            GetString(isOwner ? Resource.String.status_owner_yes : Resource.String.status_owner_no);
    }
}
