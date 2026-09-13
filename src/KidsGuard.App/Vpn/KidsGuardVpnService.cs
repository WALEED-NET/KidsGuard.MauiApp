using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Java.IO;
using KidsGuard.App.Apps;
using KidsGuard.App.Logging;
using KidsGuard.App.Settings;

namespace KidsGuard.App.Vpn;

/// <summary>
/// حجب الإنترنت عن تطبيقات مختارة عبر VpnService محلّي. الحزم المحجوبة وحدها تُوجَّه
/// إلى نفق TUN ثمّ تُسقَط حزمها، فينقطع إنترنتها ويبقى باقي الجهاز طبيعياً.
///
/// لا تحتاج Device Owner: الموافقة من المستخدم عبر VpnService.Prepare. سلبيّتها أنّ
/// المستخدم يقدر فصلها من إعدادات النظام؛ منعُ ذلك يأتي لاحقاً بـ setAlwaysOnVpnPackage
/// مع lockdown من Device Owner (المرحلة 2).
/// </summary>
[Service(
    Permission = "android.permission.BIND_VPN_SERVICE",
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeSpecialUse)]
[IntentFilter(new[] { "android.net.VpnService" })]
public sealed class KidsGuardVpnService : VpnService
{
    public const string ActionStart = "com.kidsguard.app.action.VPN_START";
    public const string ActionStop = "com.kidsguard.app.action.VPN_STOP";

    private const string Tag = "KidsGuard.Vpn";

    // قناتان لأنّ أهمية القناة لا تتغيّر بعد إنشائها برمجياً — فنحتاج واحدة ظاهرة
    // وأخرى بأدنى أهمية، ونختار بينهما حسب إعداد المستخدم.
    private const string ChannelVisible = "kidsguard_vpn_status";
    private const string ChannelSilent = "kidsguard_vpn_silent";
    private const int NotificationId = 1001;

    // عنوان خاصّ داخل واجهة TUN — لا يخرج إلى الشبكة، مجرّد طرف للنفق.
    private const string TunAddress = "10.111.222.1";

    private ParcelFileDescriptor? _tunInterface;
    private Thread? _drainThread;
    private volatile bool _running;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopBlocking();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        StartBlocking();
        // Sticky: لو قتل النظام العملية أعاد تشغيل الخدمة فيعود الحجب.
        return StartCommandResult.Sticky;
    }

    private void StartBlocking()
    {
        var showNotification = new AppSettings(this).ShowBlockingNotification;
        CreateNotificationChannels();
        StartForeground(NotificationId, BuildNotification(showNotification));
        AppLog.Info(Tag, $"foreground started (notification visible = {showNotification})");

        // نُعيد الإنشاء دائماً عند ActionStart لتُطبَّق أي تغييرات في قائمة الحجب.
        CloseInterface();

        try
        {
            var blocked = new BlockedAppsStore(this).GetBlocked();
            if (blocked.Count == 0)
            {
                AppLog.Warn(Tag, "no apps selected — nothing to block, stopping");
                StopBlocking();
                StopSelf();
                return;
            }

            var builder = new Builder(this)
                .SetSession("KidsGuard")
                .AddAddress(TunAddress, 32)
                // 0.0.0.0/0 و ::/0 يوجّهان كلّ حركة IPv4 و IPv6 إلى النفق.
                // إغفال IPv6 يترك ثغرة يتسرّب منها الإنترنت.
                .AddRoute("0.0.0.0", 0)
                .AddRoute("::", 0);

            // كلّ حزمة مُضافة إلى allowed توجَّه وحدها إلى النفق ثمّ تُسقَط،
            // فيُحجب الإنترنت عنها وحدها ويبقى الباقي طبيعياً.
            var added = 0;
            foreach (var package in blocked)
            {
                try
                {
                    builder.AddAllowedApplication(package);
                    added++;
                    AppLog.Info(Tag, "blocking app: " + package);
                }
                catch (Exception ex)
                {
                    // حزمة أُلغي تثبيتها — نتخطّاها.
                    AppLog.Warn(Tag, $"skip {package}: {ex.Message}");
                }
            }

            // حارس ضدّ الانقلاب: لو لم تُضَف أي حزمة، سيُوجَّه كلّ الجهاز إلى النفق
            // فينحجب الإنترنت كلّياً — عكس المقصود تماماً. نتوقّف بدل ذلك.
            if (added == 0)
            {
                AppLog.Error(Tag, "none of the blocked apps are installed — aborting to avoid full block");
                StopBlocking();
                StopSelf();
                return;
            }

            _tunInterface = builder.Establish();
            if (_tunInterface is null)
            {
                AppLog.Error(Tag, "Establish() returned null - consent missing or another VPN active");
                StopBlocking();
                StopSelf();
                return;
            }

            _running = true;
            VpnController.SetRunning(true);
            VpnController.LastAppliedSignature = VpnController.Signature(blocked);

            _drainThread = new Thread(DrainLoop) { IsBackground = true, Name = "kidsguard-vpn-drain" };
            _drainThread.Start();

            AppLog.Warn(Tag, $"per-app blocking STARTED for {added} app(s)");
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, "StartBlocking failed: " + ex);
            StopBlocking();
            StopSelf();
        }
    }

    /// <summary>
    /// تقرأ الحزم الواردة من واجهة TUN وتُسقطها دون تمرير. القراءة تحجب الخيط
    /// حتى يُغلَق الواصف عند الإيقاف، فتخرج الحلقة بأمان.
    /// </summary>
    private void DrainLoop()
    {
        try
        {
            var descriptor = _tunInterface?.FileDescriptor;
            if (descriptor is null)
            {
                return;
            }

            using var input = new FileInputStream(descriptor);
            var packet = new byte[32767];

            while (_running)
            {
                var read = input.Read(packet);
                if (read < 0)
                {
                    break;
                }
                // نُسقط الحزمة: لا تمرير ولا ردّ = لا إنترنت.
            }
        }
        catch (Exception ex)
        {
            // الإغلاق أثناء القراءة يرمي استثناءً متوقّعاً — ليس خطأً.
            AppLog.Info(Tag, "drain loop ended: " + ex.Message);
        }
    }

    /// <summary>يُغلق واجهة TUN الحالية ويوقف خيط التصريف — تمهيداً لإعادة الإنشاء أو للإيقاف.</summary>
    private void CloseInterface()
    {
        _running = false;

        try
        {
            _tunInterface?.Close();
        }
        catch (Exception ex)
        {
            AppLog.Warn(Tag, "closing tun failed: " + ex.Message);
        }
        finally
        {
            _tunInterface = null;
        }

        try
        {
            _drainThread?.Join(500);
        }
        catch (Exception ex)
        {
            AppLog.Warn(Tag, "join drain thread failed: " + ex.Message);
        }
        finally
        {
            _drainThread = null;
        }
    }

    private void StopBlocking()
    {
        CloseInterface();
        VpnController.SetRunning(false);
        StopForeground(StopForegroundFlags.Remove);
        AppLog.Warn(Tag, "per-app blocking STOPPED");
    }

    public override void OnDestroy()
    {
        StopBlocking();
        base.OnDestroy();
    }

    /// <summary>يُستدعى حين يفصل المستخدم الـ VPN من إعدادات النظام.</summary>
    public override void OnRevoke()
    {
        AppLog.Warn(Tag, "VPN revoked from system settings");
        StopBlocking();
        StopSelf();
        base.OnRevoke();
    }

    private void CreateNotificationChannels()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null)
        {
            return;
        }

        var visible = new NotificationChannel(
            ChannelVisible,
            GetString(Resource.String.vpn_channel_name),
            NotificationImportance.Low);
        manager.CreateNotificationChannel(visible);

        // Min: بلا صوت، ولا إشعار منبثق، ولا أيقونة في شريط الحالة.
        var silent = new NotificationChannel(
            ChannelSilent,
            GetString(Resource.String.vpn_channel_silent_name),
            NotificationImportance.Min);
        silent.SetShowBadge(false);
        manager.CreateNotificationChannel(silent);
    }

    private Notification BuildNotification(bool visible)
    {
        // النقر على الإشعار يفتح الشاشة الرئيسية.
        var launch = new Intent(this, typeof(MainActivity));
        launch.AddFlags(ActivityFlags.SingleTop);
        var pending = PendingIntent.GetActivity(
            this, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // الإسكات كلّه محكوم بأهمية القناة (Min) لا بخصائص الإشعار:
        // من API 26 فأعلى، القناة هي التي تقرّر الصوت والظهور المنبثق وأيقونة شريط
        // الحالة — وminSdk عندنا 26، فاختيار القناة وحده كافٍ.
        return new Notification.Builder(this, visible ? ChannelVisible : ChannelSilent)
            .SetContentTitle(GetString(Resource.String.vpn_notif_title))
            .SetContentText(GetString(Resource.String.vpn_notif_text))
            .SetSmallIcon(Android.Resource.Drawable.IcLockLock)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
    }
}
