using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Security;

/// <summary>
/// الطبقة الرابعة من مخرج الطوارئ: أمر فكّ عبر رسالة نصّية — للحالة التي يكون
/// فيها الجهاز بعيداً عن يد الوالد.
///
/// صيغة الرسالة: <c>KIDSGUARD-RELEASE &lt;رمز الحماية&gt;</c>
///
/// الرمز نفسه المستعمل في الشاشات يحرس هذه الطبقة أيضاً — فلا تخزين جديد ولا
/// إعدادات إضافية، ولا تُقبل رسالة بلا رمز صحيح. وإن لم يكن ثمّة رمز محفوظ
/// فالطبقة معطّلة كلّياً: بلا رمز لا يمكن التحقّق، وقبولُ أيّ رسالة يجعل أيّ
/// مُرسِل قادراً على فكّ الحماية.
///
/// ⚠️ لا تعمل على جهاز بلا شريحة اتّصال — مثل MatePad الذي بين يديك (نسخة
/// Wi-Fi فقط). قيمتها تظهر على هواتف الأطفال لا الأجهزة اللوحية.
/// </summary>
[BroadcastReceiver(
    Enabled = true,
    Exported = true,
    // يقبل البثّ من النظام وحده — يمنع أيّ تطبيق من تزوير رسالة فكّ.
    Permission = "android.permission.BROADCAST_SMS")]
[IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" }, Priority = 999)]
public sealed class SmsReleaseReceiver : BroadcastReceiver
{
    /// <summary>البادئة التي تُميّز رسالة الفكّ عن أي رسالة أخرى.</summary>
    public const string CommandPrefix = "KIDSGUARD-RELEASE";

    private const string Tag = "KidsGuard.SmsRelease";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null)
        {
            return;
        }

        try
        {
            var body = ExtractBody(intent);
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            // لا نُسجّل نصّ الرسائل العادية — رسائل المستخدم الخاصّة ليست من شأننا.
            if (!body.TrimStart().StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            AppLog.Warn(Tag, "release command received by SMS");

            var pinStore = new PinStore(context);
            if (!pinStore.HasPin)
            {
                AppLog.Error(Tag, "SMS release refused: no PIN is set, so the command cannot be verified");
                return;
            }

            var pin = body.TrimStart()[CommandPrefix.Length..].Trim();
            if (!pinStore.Verify(pin))
            {
                AppLog.Error(Tag, "SMS release refused: wrong PIN");
                return;
            }

            var result = new ReleaseManager(context).ReleaseEverything();
            AppLog.Warn(Tag, $"release via SMS: released={result.OwnershipReleased} failures={result.HasFailures}");
        }
        catch (Exception ex)
        {
            // لا نرمي من BroadcastReceiver أبداً.
            AppLog.Error(Tag, "SMS release failed", ex);
        }
    }

    /// <summary>يجمع نصّ الرسالة، وقد تصل مقسّمة على عدّة أجزاء.</summary>
    private static string ExtractBody(Intent intent)
    {
        var messages = Android.Provider.Telephony.Sms.Intents.GetMessagesFromIntent(intent);
        if (messages is null || messages.Length == 0)
        {
            return string.Empty;
        }

        return string.Concat(messages.Select(m => m?.MessageBody ?? string.Empty));
    }
}
