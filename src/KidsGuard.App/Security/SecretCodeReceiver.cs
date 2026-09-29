using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Security;

/// <summary>
/// الطبقة الثالثة من مخرج الطوارئ: رمز سرّي يُطلب من لوحة الاتصال.
///
/// يطلب الوالد <c>*#*#54321#*#*</c> فيبثّ النظام إشعاراً يصل هذا المستقبِل
/// مباشرةً — بلا فتح التطبيق إطلاقاً. هذه هي قيمته: يعمل حتى لو كانت كلّ
/// شاشات التطبيق معطّلة.
///
/// ⚠️ مقايضة مقصودة: يفكّ بلا طلب رمز الحماية. لا واجهة للبثّ ليُدخَل فيها رمز،
/// واشتراطُ رمز يعني إسقاط الطبقة كلّها حين تتعطّل الواجهات — وهو ما وُجدت له.
/// من يعرف الرمز يفكّ؛ والمشروع يُغلّب ضمان المخرج على إحكام القفل.
///
/// 🔬 تحتاج إثباتاً عملياً: تسليم SECRET_CODE يختلف بين الإصدارات والمصنّعين،
/// ولهذا سُجّل كلا الفعلين (القديم وبديله من API 29).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(
    new[] { ActionLegacy, ActionModern },
    DataScheme = "android_secret_code",
    DataHost = SecretCode)]
public sealed class SecretCodeReceiver : BroadcastReceiver
{
    /// <summary>الرمز الذي يُطلب من لوحة الاتصال: *#*#54321#*#*</summary>
    public const string SecretCode = "54321";

    /// <summary>الفعل القديم — ما زال مستعملاً على أجهزة كثيرة.</summary>
    public const string ActionLegacy = "android.provider.Telephony.SECRET_CODE";

    /// <summary>بديله الرسميّ من API 29.</summary>
    public const string ActionModern = "android.telephony.action.SECRET_CODE";

    /// <summary>الصيغة التي يطلبها الوالد.</summary>
    public const string DialString = "*#*#" + SecretCode + "#*#*";

    private const string Tag = "KidsGuard.SecretCode";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null)
        {
            return;
        }

        AppLog.Warn(Tag, $"secret code received via {intent?.Action ?? "unknown action"}");

        try
        {
            var result = new ReleaseManager(context).ReleaseEverything();
            AppLog.Warn(Tag, $"release via secret code: released={result.OwnershipReleased} failures={result.HasFailures}");

            // نفتح شاشة الطوارئ لعرض النتيجة. الفكّ تمّ قبلها، فلو تعذّر الفتح
            // لا يضيع شيء — النتيجة محفوظة في السجلّ على أيّ حال.
            var show = new Intent(context, typeof(EmergencyActivity));
            show.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(show);
        }
        catch (Exception ex)
        {
            // لا نرمي من BroadcastReceiver أبداً.
            AppLog.Error(Tag, "secret code release failed", ex);
        }
    }
}
