using Android.Content;
using Android.Net;

namespace KidsGuard.App.Vpn;

/// <summary>
/// واجهة رقيقة بين الشاشة وخدمة الحجب. تعزل الـ Activity عن تفاصيل بدء/إيقاف
/// <see cref="KidsGuardVpnService"/> وعن آلية موافقة النظام على الـ VPN.
/// </summary>
public static class VpnController
{
    // مصدر الحقيقة الوحيد لحالة التشغيل داخل هذه العملية.
    // ملاحظة صريحة: قيمة ثابتة على مستوى العملية، فإن أُعيد إنشاء العملية بينما
    // الـ VPN ما زال يعمل تُقرأ false خطأً. كافٍ لتجربة الجلسة الواحدة؛ يُحسَّن لاحقاً
    // بقراءة TRANSPORT_VPN من ConnectivityManager.
    private static volatile bool _running;

    public static bool IsRunning => _running;

    internal static void SetRunning(bool value) => _running = value;

    /// <summary>
    /// يطلب موافقة النظام على تشغيل VPN.
    /// يُرجع <c>Intent</c> يجب تشغيله بـ StartActivityForResult حين تكون الموافقة مطلوبة،
    /// أو <c>null</c> حين تكون ممنوحة سلفاً — فيُبدأ التشغيل مباشرة.
    /// </summary>
    public static Intent? PrepareConsent(Context context) => VpnService.Prepare(context);

    public static void Start(Context context)
    {
        var intent = new Intent(context, typeof(KidsGuardVpnService));
        intent.SetAction(KidsGuardVpnService.ActionStart);
        context.StartForegroundService(intent);
    }

    public static void Stop(Context context)
    {
        var intent = new Intent(context, typeof(KidsGuardVpnService));
        intent.SetAction(KidsGuardVpnService.ActionStop);
        // خدمة foreground تعمل أصلاً، فأمر الإيقاف يصل عبر StartService عادي.
        context.StartService(intent);
    }
}
