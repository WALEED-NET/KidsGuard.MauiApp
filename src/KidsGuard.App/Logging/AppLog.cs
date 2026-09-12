using Android.Content;

namespace KidsGuard.App.Logging;

/// <summary>
/// مسجّل مركزيّ للتطبيق كلّه. كلّ خطوة تكتب هنا، فيُعكَس السطر إلى ثلاثة أماكن:
///   1. logcat (عبر Android.Util.Log) — لمن يوصل adb.
///   2. buffer في الذاكرة — لعرض سريع في شاشة السجلّ.
///   3. ملفّ في FilesDir — يبقى بعد إعادة التشغيل ويُنسخ ويُرسَل.
///
/// الهدف: حين يعطل شيء على جهاز المستخدم، يفتح شاشة السجلّ وينسخ النصّ ويرسله
/// بلا حاجة إلى كمبيوتر ولا adb.
/// </summary>
public static class AppLog
{
    private const int MaxBufferLines = 2000;
    private const long MaxFileBytes = 2 * 1024 * 1024; // 2MB ثم يُدوَّر
    private const string LogFileName = "kidsguard.log";

    private static readonly object _gate = new();
    private static readonly List<string> _buffer = new(capacity: 512);
    private static string? _filePath;
    private static TimeProvider _clock = TimeProvider.System;

    /// <summary>يُستدعى مرّة واحدة من KidsGuardApp.OnCreate قبل أي شيء آخر.</summary>
    public static void Init(Context context, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;

        try
        {
            var dir = context.FilesDir?.AbsolutePath;
            if (!string.IsNullOrEmpty(dir))
            {
                _filePath = System.IO.Path.Combine(dir, LogFileName);
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AppLog", "cannot resolve log file: " + ex.Message);
        }

        Info("AppLog", "=========== logging session started ===========");
        WriteDeviceHeader(context);
    }

    private static void WriteDeviceHeader(Context context)
    {
        try
        {
            var version = context.PackageManager?
                .GetPackageInfo(context.PackageName!, 0)?.VersionName ?? "?";

            Info("Device", $"app={version} package={context.PackageName}");
            Info("Device", $"model={Android.OS.Build.Manufacturer} {Android.OS.Build.Model}");
            Info("Device", $"android=API {(int)Android.OS.Build.VERSION.SdkInt} ({Android.OS.Build.VERSION.Release})");
        }
        catch (Exception ex)
        {
            Warn("Device", "header failed: " + ex.Message);
        }
    }

    public static void Info(string tag, string message) => Write("I", tag, message);

    public static void Warn(string tag, string message) => Write("W", tag, message);

    public static void Error(string tag, string message) => Write("E", tag, message);

    public static void Error(string tag, string message, Exception ex) =>
        Write("E", tag, message + " :: " + ex);

    private static void Write(string level, string tag, string message)
    {
        // mirror إلى logcat أولاً — يبقى يعمل حتى لو تعطّل الملفّ.
        switch (level)
        {
            case "E": Android.Util.Log.Error(tag, message); break;
            case "W": Android.Util.Log.Warn(tag, message); break;
            default: Android.Util.Log.Info(tag, message); break;
        }

        var timestamp = _clock.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"{timestamp} {level}/{tag}: {message}";

        lock (_gate)
        {
            _buffer.Add(line);
            if (_buffer.Count > MaxBufferLines)
            {
                _buffer.RemoveRange(0, _buffer.Count - MaxBufferLines);
            }

            AppendToFile(line);
        }
    }

    private static void AppendToFile(string line)
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            var info = new System.IO.FileInfo(_filePath);
            if (info.Exists && info.Length > MaxFileBytes)
            {
                // تدوير بسيط: نبدأ ملفّاً جديداً بعلامة، لا نراكم بلا حدّ.
                System.IO.File.WriteAllText(_filePath,
                    "--- log rotated (exceeded 2MB) ---" + Environment.NewLine);
            }

            System.IO.File.AppendAllText(_filePath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("AppLog", "append failed: " + ex.Message);
        }
    }

    /// <summary>النصّ الكامل للعرض والنسخ. يفضّل الملفّ لأنّه يبقى بعد إعادة التشغيل.</summary>
    public static string GetLogText()
    {
        lock (_gate)
        {
            if (_filePath is not null && System.IO.File.Exists(_filePath))
            {
                try
                {
                    return System.IO.File.ReadAllText(_filePath);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("AppLog", "read failed: " + ex.Message);
                }
            }

            return _buffer.Count == 0 ? string.Empty : string.Join(Environment.NewLine, _buffer);
        }
    }

    public static void Clear()
    {
        lock (_gate)
        {
            _buffer.Clear();
            if (_filePath is not null)
            {
                try
                {
                    System.IO.File.Delete(_filePath);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("AppLog", "clear failed: " + ex.Message);
                }
            }
        }

        Info("AppLog", "log cleared by user");
    }
}
