using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Security;

/// <summary>
/// الطبقة الخامسة من مخرج الطوارئ: تفكّ الحماية تلقائياً إذا صار التطبيق ينهار
/// عند كلّ إقلاع.
///
/// المنطق: كلّ انهيار غير مُلتقَط يُسجَّل بختم وقته. عند بدء العملية نعدّ الانهيارات
/// داخل نافذة زمنية؛ فإن بلغت الحدّ، فالتطبيق في حلقة انهيار ولن يستطيع المستخدم
/// فتحه ليضغط أيّ زرّ — فنفكّ قسرياً بلا تدخّله.
/// </summary>
public sealed class CrashGuard
{
    private const string PrefsName = "kidsguard_prefs";
    private const string KeyCrashes = "crash_timestamps";
    private const string Tag = "KidsGuard.CrashGuard";

    /// <summary>عدد الانهيارات المتتالية التي تُعدّ «حلقة انهيار».</summary>
    public const int Threshold = 3;

    /// <summary>
    /// النافذة الزمنية. انهيارات أقدم منها لا تُحتسب، فلا تتراكم أعطال متفرّقة
    /// عبر أسابيع فتُفضي إلى فكّ غير مقصود.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ISharedPreferences _prefs;
    private readonly TimeProvider _timeProvider;

    public CrashGuard(Context context, TimeProvider? timeProvider = null)
    {
        var app = context.ApplicationContext ?? context;
        _prefs = app.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>يُسجّل انهياراً. يُستدعى من معالِجات الاستثناءات غير المُلتقَطة.</summary>
    public void RecordCrash()
    {
        try
        {
            var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
            var stamps = ReadStamps();
            stamps.Add(now);

            var editor = _prefs.Edit()!;
            editor.PutString(KeyCrashes, string.Join(",", stamps));
            // Commit لا Apply: العملية على وشك الموت، وApply غير متزامن فقد يضيع.
            editor.Commit();

            AppLog.Error(Tag, $"crash recorded — {stamps.Count} stamp(s) stored");
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, "failed to record crash: " + ex.Message);
        }
    }

    /// <summary>
    /// هل نحن في حلقة انهيار؟ تُستدعى مرّة عند بدء العملية. تمسح السجلّ حين تُرجع
    /// true حتى لا يتكرّر الفكّ عند كلّ إقلاع لاحق.
    /// </summary>
    public bool ShouldAutoRelease()
    {
        try
        {
            var cutoff = _timeProvider.GetUtcNow().Add(-Window).ToUnixTimeSeconds();
            var recent = ReadStamps().Where(t => t >= cutoff).ToList();

            // نُبقي الحديثة فقط — تنظيف تلقائيّ للقديمة.
            var editor = _prefs.Edit()!;
            editor.PutString(KeyCrashes, string.Join(",", recent));
            editor.Apply();

            if (recent.Count < Threshold)
            {
                if (recent.Count > 0)
                {
                    AppLog.Warn(Tag, $"{recent.Count} recent crash(es) — below threshold of {Threshold}");
                }
                return false;
            }

            AppLog.Error(Tag, $"CRASH LOOP DETECTED: {recent.Count} crashes within {Window.TotalMinutes:F0} minutes");
            Clear();
            return true;
        }
        catch (Exception ex)
        {
            // فشل الحارس لا يجوز أن يمنع الإقلاع.
            AppLog.Error(Tag, "crash-loop check failed: " + ex.Message);
            return false;
        }
    }

    public void Clear()
    {
        var editor = _prefs.Edit()!;
        editor.Remove(KeyCrashes);
        editor.Apply();
        AppLog.Info(Tag, "crash history cleared");
    }

    private List<long> ReadStamps()
    {
        var raw = _prefs.GetString(KeyCrashes, null);
        if (string.IsNullOrEmpty(raw))
        {
            return new List<long>();
        }

        var stamps = new List<long>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part, out var value))
            {
                stamps.Add(value);
            }
        }

        return stamps;
    }
}
