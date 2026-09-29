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
    private const int RequestReceiveSms = 3;

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

        FindViewById<Button>(Resource.Id.set_pin)!.Click += (_, _) => ShowSetPinDialog();
        FindViewById<Button>(Resource.Id.release_now)!.Click += (_, _) => ConfirmAndRelease();

        // الرمز يُقرأ من المستقبِل نفسه لا يُكتب يدوياً، فلا ينحرف النصّ عمّا يستقبله فعلاً.
        FindViewById<TextView>(Resource.Id.layer_dialer_code)!.Text = SecretCodeReceiver.DialString;
        FindViewById<Button>(Resource.Id.enable_sms)!.Click += (_, _) => RequestSmsPermission();

        RequestNotificationsIfNeeded();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AppLog.Info(Tag, "OnResume");

        // الاشتراك أثناء الظهور فقط — الحدث ثابت (static) فبقاء الاشتراك بعد
        // الإخفاء يُبقي مرجعاً للـ Activity ويُسرّب الذاكرة.
        VpnController.RunningChanged += OnVpnRunningChanged;
        RefreshStatus();
    }

    protected override void OnPause()
    {
        VpnController.RunningChanged -= OnVpnRunningChanged;
        base.OnPause();
    }

    private void OnVpnRunningChanged() => RunOnUiThread(RefreshVpnStatus);

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

        RefreshPinState();
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
            // إيقاف صريح من الوالد = إلغاء النيّة، فلا يعود الحجب بعد الإقلاع.
            if (_settings is not null)
            {
                _settings.BlockingEnabled = false;
            }
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
        // تُحفظ النيّة على القرص ليقرأها BootReceiver بعد إعادة الإقلاع.
        if (_settings is not null)
        {
            _settings.BlockingEnabled = true;
        }
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

    private void RefreshPinState()
    {
        var hasPin = new PinStore(this).HasPin;

        var state = FindViewById<TextView>(Resource.Id.pin_state)!;
        state.Text = GetString(hasPin ? Resource.String.pin_present : Resource.String.pin_missing_warning);
        state.SetTextColor(hasPin ? Color.ParseColor("#16A34A") : Color.ParseColor("#DC2626"));

        FindViewById<Button>(Resource.Id.set_pin)!.Text =
            GetString(hasPin ? Resource.String.pin_change_btn : Resource.String.pin_set_btn);
    }

    private EditText BuildPinInput()
    {
        var input = new EditText(this)
        {
            InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberVariationPassword
        };
        input.SetHint(Resource.String.pin_hint);
        return input;
    }

    private void ShowSetPinDialog()
    {
        var input = BuildPinInput();

        new AlertDialog.Builder(this)!
            .SetTitle(Resource.String.pin_set_title)!
            .SetView(input)!
            .SetPositiveButton(Resource.String.btn_ok, (_, _) =>
            {
                var pin = input.Text ?? string.Empty;
                if (pin.Length < PinStore.MinLength)
                {
                    Toast.MakeText(this, Resource.String.pin_too_short, ToastLength.Long)!.Show();
                    return;
                }

                new PinStore(this).SetPin(pin);
                Toast.MakeText(this, Resource.String.pin_saved, ToastLength.Short)!.Show();
                RefreshPinState();
            })!
            .SetNegativeButton(Resource.String.btn_cancel, (_, _) => { })!
            .Show();
    }

    private void ConfirmAndRelease()
    {
        var pinStore = new PinStore(this);

        // لا رمز محفوظ = نمضي إلى التأكيد مباشرة. رفض الفكّ لغياب الرمز
        // يقلب مخرج الطوارئ إلى قفل، وهو عكس الغرض منه تماماً.
        if (!pinStore.HasPin)
        {
            AppLog.Warn(Tag, "release requested while no PIN is set");
            ShowReleaseConfirm();
            return;
        }

        var input = BuildPinInput();

        new AlertDialog.Builder(this)!
            .SetTitle(Resource.String.pin_enter_title)!
            .SetView(input)!
            .SetPositiveButton(Resource.String.btn_ok, (_, _) =>
            {
                if (!pinStore.Verify(input.Text ?? string.Empty))
                {
                    AppLog.Warn(Tag, "release refused: wrong PIN");
                    Toast.MakeText(this, Resource.String.pin_wrong, ToastLength.Long)!.Show();
                    return;
                }

                ShowReleaseConfirm();
            })!
            .SetNegativeButton(Resource.String.btn_cancel, (_, _) => { })!
            .Show();
    }

    private void ShowReleaseConfirm() =>
        new AlertDialog.Builder(this)!
            .SetTitle(Resource.String.release_confirm_title)!
            .SetMessage(Resource.String.release_confirm_msg)!
            .SetPositiveButton(Resource.String.btn_ok, (_, _) => PerformRelease())!
            .SetNegativeButton(Resource.String.btn_cancel, (_, _) => { })!
            .Show();

    private void PerformRelease()
    {
        AppLog.Warn(Tag, "RELEASE requested from main screen");

        var result = _releaseManager!.ReleaseEverything();
        FindViewById<TextView>(Resource.Id.release_log)!.Text = result.ToLogText();

        Toast.MakeText(
            this,
            result.HasFailures ? Resource.String.release_partial : Resource.String.release_done,
            ToastLength.Long)!.Show();

        RefreshStatus();
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
        else if (requestCode == RequestReceiveSms)
        {
            var granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
            AppLog.Info(Tag, $"RECEIVE_SMS granted={granted}");

            Toast.MakeText(
                this,
                granted ? Resource.String.sms_granted : Resource.String.sms_denied,
                ToastLength.Long)!.Show();
        }
    }

    /// <summary>
    /// إذن الرسائل خطر، فلا يُطلب عند أوّل فتح — يطلبه الوالد صراحةً حين يريد
    /// تفعيل هذه الطبقة. وعلى جهاز بلا شريحة اتّصال لا قيمة له أصلاً.
    /// </summary>
    private void RequestSmsPermission()
    {
        if (CheckSelfPermission(Android.Manifest.Permission.ReceiveSms) == Permission.Granted)
        {
            AppLog.Info(Tag, "RECEIVE_SMS already granted");
            Toast.MakeText(this, Resource.String.sms_granted, ToastLength.Short)!.Show();
            return;
        }

        AppLog.Info(Tag, "requesting RECEIVE_SMS");
        RequestPermissions([Android.Manifest.Permission.ReceiveSms], RequestReceiveSms);
    }
}
