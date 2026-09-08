using Android.App.Admin;
using Android.Content;

namespace KidsGuard.App.Security;

/// <summary>
/// نقطة دخول النظام إلى التطبيق كـ device admin / device owner.
/// وجود هذا المستقبِل شرط لنجاح <c>dpm set-device-owner</c>.
/// </summary>
[BroadcastReceiver(
    Name = ComponentName,
    Label = "@string/device_admin_label",
    Permission = Android.Manifest.Permission.BindDeviceAdmin,
    Exported = true)]
[MetaData("android.app.device_admin", Resource = "@xml/device_admin")]
[IntentFilter(new[] { "android.app.action.DEVICE_ADMIN_ENABLED" })]
public sealed class AppDeviceAdminReceiver : DeviceAdminReceiver
{
    /// <summary>الاسم الكامل كما يراه النظام — يُستخدم حرفياً في أمر <c>dpm set-device-owner</c>.</summary>
    public const string ComponentName = "com.kidsguard.app.Security.AppDeviceAdminReceiver";

    private const string Tag = "KidsGuard.Admin";

    /// <summary>الـ <see cref="ComponentName"/> جاهزاً للتمرير إلى <see cref="DevicePolicyManager"/>.</summary>
    public static ComponentName GetComponent(Context context) =>
        new(context.PackageName!, ComponentName);

    public override void OnEnabled(Context context, Intent intent)
    {
        base.OnEnabled(context, intent);
        Android.Util.Log.Info(Tag, "device admin enabled");
    }

    public override void OnDisabled(Context context, Intent intent)
    {
        base.OnDisabled(context, intent);
        Android.Util.Log.Info(Tag, "device admin disabled");
    }
}
