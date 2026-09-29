using Android.Content;
using KidsGuard.App.Apps;
using KidsGuard.App.Logging;
using KidsGuard.App.Settings;

namespace KidsGuard.App.Vpn;

/// <summary>
/// يُعيد تشغيل الحجب تلقائياً بعد إقلاع الجهاز.
///
/// بدونه ينقطع الحجب مع كلّ إطفاء وتشغيل، لأن حالة التشغيل كانت تعيش في الذاكرة
/// فقط (VpnController.IsRunning) وتموت مع العملية — فيستيقظ الجهاز بلا حجب حتى
/// يفتح الوالد التطبيق ويضغط الزرّ بيده.
///
/// ثلاثة شروط قبل التشغيل، وكلّ فشل منها يُسجَّل بسببه لا صامتاً.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public sealed class BootReceiver : BroadcastReceiver
{
    private const string Tag = "KidsGuard.Boot";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        if (intent?.Action != Intent.ActionBootCompleted)
        {
            return;
        }

        AppLog.Info(Tag, "BOOT_COMPLETED received");

        try
        {
            // 1) هل طلب الوالد الحجب أصلاً؟
            if (!new AppSettings(context).BlockingEnabled)
            {
                AppLog.Info(Tag, "blocking was not enabled before reboot — nothing to restore");
                return;
            }

            // 2) هل بقيت قائمة محجوبة؟ قائمة فارغة تعني لا شيء نحجبه،
            //    وتشغيل الخدمة بلا حزم يُسقطها فوراً بحارس الانقلاب.
            var blockedCount = new BlockedAppsStore(context).Count;
            if (blockedCount == 0)
            {
                AppLog.Warn(Tag, "no blocked apps stored — skipping restore");
                return;
            }

            // 3) هل ما زالت موافقة الـ VPN ممنوحة؟ الموافقة تبقى عادةً بعد الإقلاع،
            //    لكن إن سُحبت فلا سبيل لطلبها من BroadcastReceiver (لا واجهة له).
            if (VpnController.PrepareConsent(context) is not null)
            {
                AppLog.Error(Tag, "VPN consent no longer granted — parent must open the app once");
                return;
            }

            VpnController.Start(context);
            AppLog.Warn(Tag, $"blocking restored after boot for {blockedCount} app(s)");
        }
        catch (Exception ex)
        {
            // لا نرمي من BroadcastReceiver إطلاقاً — رمي استثناء هنا يُسقط العملية
            // عند كلّ إقلاع، فيصير الجهاز ينهار دورياً بلا سبب ظاهر.
            AppLog.Error(Tag, "boot restore failed", ex);
        }
    }
}
