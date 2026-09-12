using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using KidsGuard.App.Logging;
using KidsGuard.App.Security;
using KidsGuard.App.Vpn;

namespace KidsGuard.App;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    private const string Tag = "KidsGuard.Main";
    private const int RequestVpnConsent = 1;
    private const int RequestPostNotifications = 2;

    private ReleaseManager? _releaseManager;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AppLog.Info(Tag, "OnCreate");
        SetContentView(Resource.Layout.activity_main);

        _releaseManager = new ReleaseManager(this);

        FindViewById<TextView>(Resource.Id.package_name)!.Text = PackageName;
        FindViewById<Button>(Resource.Id.vpn_toggle)!.Click += OnVpnToggleClicked;
        FindViewById<Button>(Resource.Id.open_log)!.Click += (_, _) =>
        {
            AppLog.Info(Tag, "open log viewer");
            StartActivity(new Intent(this, typeof(LogViewerActivity)));
        };

        RequestNotificationsIfNeeded();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppLog.Info(Tag, "OnResume");
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
        AppLog.Info(Tag, $"status: admin={isAdmin} owner={isOwner}");

        FindViewById<TextView>(Resource.Id.status_admin)!.Text =
            GetString(isAdmin ? Resource.String.status_admin_yes : Resource.String.status_admin_no);

        FindViewById<TextView>(Resource.Id.status_owner)!.Text =
            GetString(isOwner ? Resource.String.status_owner_yes : Resource.String.status_owner_no);

        RefreshVpnStatus();
    }

    private void RefreshVpnStatus()
    {
        var running = VpnController.IsRunning;
        AppLog.Info(Tag, $"vpn running={running}");

        FindViewById<TextView>(Resource.Id.vpn_status)!.Text =
            GetString(running ? Resource.String.vpn_status_on : Resource.String.vpn_status_off);

        FindViewById<Button>(Resource.Id.vpn_toggle)!.Text =
            GetString(running ? Resource.String.vpn_btn_unblock : Resource.String.vpn_btn_block);
    }

    private void OnVpnToggleClicked(object? sender, EventArgs e)
    {
        if (VpnController.IsRunning)
        {
            AppLog.Info(Tag, "user tapped: stop blocking");
            VpnController.Stop(this);
            RefreshVpnStatus();
            return;
        }

        AppLog.Info(Tag, "user tapped: start blocking");

        // موافقة النظام على الـ VPN. null تعني ممنوحة سلفاً فنبدأ مباشرة.
        var consent = VpnController.PrepareConsent(this);
        if (consent is null)
        {
            AppLog.Info(Tag, "vpn consent already granted");
            StartBlocking();
        }
        else
        {
            AppLog.Info(Tag, "requesting vpn consent dialog");
            StartActivityForResult(consent, RequestVpnConsent);
        }
    }

    private void StartBlocking()
    {
        AppLog.Info(Tag, "starting vpn service");
        VpnController.Start(this);
        RefreshVpnStatus();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        AppLog.Info(Tag, $"onActivityResult req={requestCode} result={resultCode}");

        if (requestCode != RequestVpnConsent)
        {
            return;
        }

        if (resultCode == Result.Ok)
        {
            AppLog.Info(Tag, "vpn consent granted");
            StartBlocking();
        }
        else
        {
            AppLog.Warn(Tag, "vpn consent denied");
            Toast.MakeText(this, Resource.String.vpn_consent_denied, ToastLength.Long)!.Show();
        }
    }

    private void RequestNotificationsIfNeeded()
    {
        // POST_NOTIFICATIONS مطلوب من API 33 لإظهار إشعار الخدمة (شفافية للطفل).
        // غيابه لا يمنع الحجب، لكنه يُخفي الإشعار الدالّ على أنّه فعّال.
        // OperatingSystem.IsAndroidVersionAtLeast صيغة يفهمها محلّل CA1416.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            AppLog.Info(Tag, "requesting POST_NOTIFICATIONS");
            RequestPermissions([Android.Manifest.Permission.PostNotifications], RequestPostNotifications);
        }
    }

    public override void OnRequestPermissionsResult(
        int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == RequestPostNotifications)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            AppLog.Info(Tag, $"POST_NOTIFICATIONS granted={granted}");
        }
    }
}
