using Android.Runtime;
using KidsGuard.App.Logging;

namespace KidsGuard.App;

/// <summary>
/// نقطة إقلاع العملية. تُهيّئ المسجّل قبل أي Activity، وتصطاد الأعطال غير المُلتقَطة
/// فتكتبها في السجلّ — فحتى لو انهار التطبيق يبقى سبب الانهيار مكتوباً وقابلاً للإرسال.
/// </summary>
[Application]
public class KidsGuardApp : Application
{
    public KidsGuardApp(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        AppLog.Init(this);
        AppLog.Info("App", "Application.OnCreate");

        // أعطال جانب Java/Android الأصلي.
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            AppLog.Error("CRASH", "unhandled android exception", e.Exception);
            // Handled=false نتركه ليكمل النظام مساره المعتاد بعد التسجيل.
        };

        // أعطال جانب .NET.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLog.Error("CRASH", "unhandled domain exception: " + e.ExceptionObject);
        };

        // مهامّ async لم يُنتظَر خطؤها.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("CRASH", "unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }
}
