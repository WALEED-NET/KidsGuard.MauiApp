using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Apps;

/// <summary>
/// قائمة حزم التطبيقات المحجوب عنها الإنترنت، محفوظة في SharedPreferences.
/// مصدر الحقيقة الذي يقرأه KidsGuardVpnService عند إنشاء النفق.
/// </summary>
public sealed class BlockedAppsStore
{
    private const string PrefsName = "kidsguard_prefs";
    private const string Key = "blocked_packages";
    private const string Tag = "KidsGuard.BlockedApps";

    private readonly ISharedPreferences _prefs;

    public BlockedAppsStore(Context context)
    {
        var app = context.ApplicationContext ?? context;
        _prefs = app.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

    /// <summary>نسخة قابلة للتعديل من مجموعة الحزم المحجوبة.</summary>
    public HashSet<string> GetBlocked()
    {
        // GetStringSet قد يُرجع مجموعة غير قابلة للتعديل ومشتركة — ننسخها دائماً.
        var stored = _prefs.GetStringSet(Key, null);
        return stored is null ? new HashSet<string>() : new HashSet<string>(stored);
    }

    public bool IsBlocked(string packageName) => GetBlocked().Contains(packageName);

    public int Count => GetBlocked().Count;

    public void SetBlocked(string packageName, bool blocked)
    {
        var set = GetBlocked();
        var changed = blocked ? set.Add(packageName) : set.Remove(packageName);
        if (!changed)
        {
            return;
        }

        var editor = _prefs.Edit()!;
        editor.PutStringSet(Key, set);
        editor.Apply();
        AppLog.Info(Tag, $"{(blocked ? "blocked" : "unblocked")} {packageName} — total {set.Count}");
    }
}
