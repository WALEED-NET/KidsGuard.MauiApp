using Android.Content;
using Android.Widget;
using KidsGuard.App.Logging;

namespace KidsGuard.App;

/// <summary>
/// شاشة السجلّ: تعرض كلّ ما كُتب، وتتيح نسخه إلى الحافظة أو مشاركته عبر أي تطبيق
/// (واتساب، بريد، ...) لإرساله. الغرض تشخيص العطل بلا كمبيوتر ولا adb.
/// </summary>
[Activity(Label = "@string/log_title", Exported = false)]
public class LogViewerActivity : Activity
{
    private TextView? _logText;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_log);

        _logText = FindViewById<TextView>(Resource.Id.log_text);

        FindViewById<Button>(Resource.Id.log_refresh)!.Click += (_, _) => Render();
        FindViewById<Button>(Resource.Id.log_copy)!.Click += (_, _) => CopyToClipboard();
        FindViewById<Button>(Resource.Id.log_share)!.Click += (_, _) => Share();
        FindViewById<Button>(Resource.Id.log_clear)!.Click += (_, _) => ClearLog();

        AppLog.Info("LogViewer", "opened");
    }

    protected override void OnResume()
    {
        base.OnResume();
        Render();
    }

    private void Render()
    {
        var text = AppLog.GetLogText();
        _logText!.Text = string.IsNullOrEmpty(text) ? GetString(Resource.String.log_empty) : text;
    }

    private void CopyToClipboard()
    {
        var clipboard = (ClipboardManager?)GetSystemService(ClipboardService);
        if (clipboard is null)
        {
            return;
        }

        clipboard.PrimaryClip = ClipData.NewPlainText("KidsGuard logs", AppLog.GetLogText());
        Toast.MakeText(this, Resource.String.log_copied, ToastLength.Short)!.Show();
    }

    private void Share()
    {
        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraSubject, "KidsGuard logs");
        intent.PutExtra(Intent.ExtraText, AppLog.GetLogText());
        StartActivity(Intent.CreateChooser(intent, GetString(Resource.String.log_share)));
    }

    private void ClearLog()
    {
        AppLog.Clear();
        Render();
    }
}
