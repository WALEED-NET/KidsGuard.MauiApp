using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Settings;

/// <summary>إعدادات التطبيق العامّة المحفوظة في SharedPreferences.</summary>
public sealed class AppSettings
{
    private const string PrefsName = "kidsguard_prefs";
    private const string KeyShowNotification = "show_blocking_notification";
    private const string Tag = "KidsGuard.Settings";

    private readonly ISharedPreferences _prefs;

    public AppSettings(Context context)
    {
        var app = context.ApplicationContext ?? context;
        _prefs = app.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

    /// <summary>
    /// هل يظهر إشعار الحجب. الافتراضي true التزاماً بقاعدة «الطفل يعلم بوجود التطبيق».
    /// عند false نستعمل قناة إشعار بأدنى أهمية فيختفي من شريط الحالة.
    /// ملاحظة: أيقونة VPN التي يرسمها Android نفسه تبقى ظاهرة مهما كان هذا الإعداد.
    /// </summary>
    public bool ShowBlockingNotification
    {
        get => _prefs.GetBoolean(KeyShowNotification, true);
        set
        {
            var editor = _prefs.Edit()!;
            editor.PutBoolean(KeyShowNotification, value);
            editor.Apply();
            AppLog.Info(Tag, $"showBlockingNotification = {value}");
        }
    }
}
