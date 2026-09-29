using Android.Runtime;
using KidsGuard.App.Logging;
using KidsGuard.App.Security;

namespace KidsGuard.App;

/// <summary>
/// نقطة إقلاع العملية. تُهيّئ المسجّل قبل أي Activity، وتصطاد الأعطال غير المُلتقَطة
/// فتكتبها في السجلّ — فحتى لو انهار التطبيق يبقى سبب الانهيار مكتوباً وقابلاً للإرسال.
///
/// وهنا أيضاً تعمل الطبقة الخامسة من مخرج الطوارئ: فحص حلقة الانهيار قبل أيّ شيء آخر.
/// </summary>
[Application]
public class KidsGuardApp : Application
{
    private const string Tag = "KidsGuard.App";

    public KidsGuardApp(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        AppLog.Init(this);
        AppLog.Info(Tag, "Application.OnCreate");

        var crashGuard = new CrashGuard(this);

        // أعطال جانب Java/Android الأصلي.
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            AppLog.Error("CRASH", "unhandled android exception", e.Exception);
            crashGuard.RecordCrash();
        };

        // أعطال جانب .NET.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLog.Error("CRASH", "unhandled domain exception: " + e.ExceptionObject);
            crashGuard.RecordCrash();
        };

        // مهامّ async لم يُنتظَر خطؤها — لا تُحتسب انهياراً، فهي لا تُسقط العملية.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("CRASH", "unobserved task exception", e.Exception);
            e.SetObserved();
        };

        CheckCrashLoop(crashGuard);
    }

    /// <summary>
    /// الطبقة الخامسة: إن كان التطبيق ينهار عند كلّ إقلاع فلن يصل المستخدم إلى أيّ
    /// زرّ ليفكّ الحماية بنفسه — فنفكّ قسرياً نيابةً عنه.
    ///
    /// التمرير هنا forceOwnershipRelease: true عن قصد. الجهاز في هذه الحالة غير
    /// صالح للاستعمال أصلاً، فبقاء قيدٍ عالق أهون من بقائه مملوكاً لتطبيق منهار.
    /// </summary>
    private void CheckCrashLoop(CrashGuard crashGuard)
    {
        try
        {
            if (!crashGuard.ShouldAutoRelease())
            {
                return;
            }

            AppLog.Error(Tag, "=== CRASH LOOP GUARD TRIGGERED — forcing release ===");
            var result = new ReleaseManager(this).ReleaseEverything(forceOwnershipRelease: true);
            AppLog.Error(Tag, $"auto-release: released={result.OwnershipReleased} failures={result.HasFailures}");
        }
        catch (Exception ex)
        {
            // الحارس نفسه لا يجوز أن يكون سبب انهيار جديد.
            AppLog.Error(Tag, "crash-loop guard failed", ex);
        }
    }
}
