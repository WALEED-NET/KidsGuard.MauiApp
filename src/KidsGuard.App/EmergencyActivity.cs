using Android.Content;
using Android.Widget;
using KidsGuard.App.Logging;
using KidsGuard.App.Security;

namespace KidsGuard.App;

/// <summary>
/// الطبقة الثانية من مخرج الطوارئ: شاشة مستقلّة تماماً بأيقونة منفصلة في المشغّل.
///
/// تعمل حتى لو تعطّلت الشاشة الرئيسية — فلا تعتمد على أيّ شيء منها: لا قائمة
/// تطبيقات، ولا حالة VPN، ولا إعدادات. كلّ ما تحتاجه: رمز الحماية و ReleaseManager.
///
/// كلّ منطق الفكّ في ReleaseEverything نفسها (القاعدة الثالثة: نقطة خروج واحدة) —
/// هذه الشاشة مجرّد مستدعٍ آخر لها.
/// </summary>
[Activity(
    Label = "@string/emergency_label",
    Exported = true,
    Theme = "@style/AppTheme",
    ExcludeFromRecents = true)]
[IntentFilter(
    new[] { Intent.ActionMain },
    Categories = new[] { Intent.CategoryLauncher })]
public class EmergencyActivity : Activity
{
    private const string Tag = "KidsGuard.Emergency";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_emergency);
        AppLog.Warn(Tag, "emergency screen opened");

        FindViewById<Button>(Resource.Id.emergency_release)!.Click += (_, _) => Release();
    }

    private void Release()
    {
        var pinStore = new PinStore(this);
        var entered = FindViewById<EditText>(Resource.Id.emergency_pin)!.Text ?? string.Empty;

        // رمز محفوظ = يجب أن يطابق. لا رمز محفوظ = نمضي ونُسجّل التحذير،
        // فمنع الفكّ لعدم وجود رمز يقلب المخرج إلى قفل.
        if (pinStore.HasPin && !pinStore.Verify(entered))
        {
            AppLog.Warn(Tag, "release refused: wrong PIN");
            Toast.MakeText(this, Resource.String.pin_wrong, ToastLength.Long)!.Show();
            return;
        }

        if (!pinStore.HasPin)
        {
            AppLog.Warn(Tag, "no PIN stored — releasing without verification");
        }

        AppLog.Warn(Tag, "EMERGENCY RELEASE requested from standalone screen");
        var result = new ReleaseManager(this).ReleaseEverything();

        FindViewById<TextView>(Resource.Id.emergency_log)!.Text = result.ToLogText();
        Toast.MakeText(
            this,
            result.HasFailures ? Resource.String.release_partial : Resource.String.release_done,
            ToastLength.Long)!.Show();
    }
}
