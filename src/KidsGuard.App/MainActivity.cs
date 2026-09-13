using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Widget;
using KidsGuard.App.Apps;
using KidsGuard.App.Logging;
using KidsGuard.App.Security;
using KidsGuard.App.Settings;
using KidsGuard.App.Vpn;

namespace KidsGuard.App;

[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    private const string Tag = "KidsGuard.Main";
    private const int RequestVpnConsent = 1;
    private const int RequestPostNotifications = 2;

    private ReleaseManager? _releaseManager;
    private BlockedAppsStore? _blockedStore;
    private AppSettings? _settings;

    // تعيين Checked برمجياً يُطلق CheckedChange — نكتمه أثناء التحديث.
    private bool _suppressSwitchEvent;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AppLog.Info(Tag, "OnCreate");
        SetContentView(Resource.Layout.activity_main);

        _releaseManager = new ReleaseManager(this);
        _blockedStore = new BlockedAppsStore(this);
        _settings = new AppSettings(this);

        FindViewById<TextView>(Resource.Id.package_name)!.Text = PackageName;
        FindViewById<Button>(Resource.Id.vpn_toggle)!.Click += OnVpnToggleClicked;

        FindViewById<Button>(Resource.Id.open_apps)!.Click += (_, _) =>
        {
            AppLog.Info(Tag, "open app list");
            StartActivity(new Intent(this, typeof(AppListActivity)));
        };

        FindViewById<Button>(Resource.Id.open_log)!.Click += (_, _) =>
        {
            AppLog.Info(Tag, "open log viewer");
            StartActivity(new Intent(this, typeof(LogViewerActivity)));
        };

        FindViewById<Switch>(Resource.Id.notif_switch)!.CheckedChange += OnNotificationSwitchChanged;

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

        SetPill(Resource.Id.status_admin, isAdmin);
        SetPill(Resource.Id.status_owner, isOwner);

        RefreshVpnStatus();
    }

    /// <summary>يضبط شارة الحالة: خضراء لـ«نعم» ورمادية لـ«لا».</summary>
    private void SetPill(int viewId, bool value)
    {
        var pill = FindViewById<TextView>(viewId)!;
        pill.Text = GetString(value ? Resource.String.status_yes : Resource.String.status_no);
        pill.SetBackgroundResource(value ? Resource.Drawable.pill_yes : Resource.Drawable.pill_no);
        pill.SetTextColor(value
            ? Color.ParseColor("#16A34A")
            : Color.ParseColor("#64748B"));
    }

    private void RefreshVpnStatus()
    {
        var running = VpnController.IsRunning;
        var count = _blockedStore?.Count ?? 0;
        AppLog.Info(Tag, $"vpn running={running} blockedCount={count}");

        FindViewById<TextView>(Resource.Id.vpn_status)!.Text =
            GetString(running ? Resource.String.vpn_status_on : Resource.String.vpn_status_off);

        FindViewById<TextView>(Resource.Id.vpn_blocked_count)!.Text =
            string.Format(GetString(Resource.String.vpn_blocked_count), count);

        var toggle = FindViewById<Button>(Resource.Id.vpn_toggle)!;
        toggle.Text = GetString(running ? Resource.String.vpn_btn_unblock : Resource.String.vpn_btn_block);
        toggle.SetBackgroundResource(running ? Resource.Drawable.btn_danger : Resource.Drawable.btn_primary);
        toggle.SetTextColor(Color.White);

        var notifSwitch = FindViewById<Switch>(Resource.Id.notif_switch)!;
        _suppressSwitchEvent = true;
        notifSwitch.Checked = _settings?.ShowBlockingNotification ?? true;
        _suppressSwitchEvent = false;

        // لو تغيّرت القائمة بينما الحجب يعمل، نُعيد تطبيقها (الموافقة ممنوحة سلفاً).
        if (running && _blockedStore is not null)
        {
            var signature = VpnController.Signature(_blockedStore.GetBlocked());
            if (signature != VpnController.LastAppliedSignature)
            {
                AppLog.Info(Tag, "blocked list changed while running — re-applying");
                VpnController.Start(this);
            }
        }
    }

    private void OnNotificationSwitchChanged(object? sender, CompoundButton.CheckedChangeEventArgs e)
    {
        if (_suppressSwitchEvent || _settings is null)
        {
            return;
        }

        _settings.ShowBlockingNotification = e.IsChecked;

        // إعادة بناء الإشعار تحتاج إعادة تشغيل الخدمة — فقط إن كان الحجب يعمل.
        if (VpnController.IsRunning)
        {
            AppLog.Info(Tag, "notification setting changed while running — re-applying");
            VpnController.Start(this);
        }
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

        if ((_blockedStore?.Count ?? 0) == 0)
        {
            AppLog.Warn(Tag, "start refused: no apps selected");
            Toast.MakeText(this, Resource.String.vpn_need_selection, ToastLength.Long)!.Show();
            return;
        }

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
        // POST_NOTIFICATIONS مطلوب من API 33 لإظهار إشعار الخدمة.
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
