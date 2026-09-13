using Android.Widget;
using KidsGuard.App.Apps;
using KidsGuard.App.Logging;

namespace KidsGuard.App;

/// <summary>
/// شاشة اختيار التطبيقات المحجوب عنها الإنترنت. تعرض كلّ تطبيق بأيقونته واسمه،
/// وتحفظ الاختيار فوراً. النقر على الصفّ يبدّل الحجب.
/// </summary>
[Activity(Label = "@string/apps_title", Exported = false)]
public class AppListActivity : Activity
{
    private const string Tag = "KidsGuard.AppList";

    private BlockedAppsStore? _store;
    private AppListAdapter? _adapter;
    private HashSet<string> _blocked = new();
    private int _totalApps;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_app_list);
        AppLog.Info(Tag, "OnCreate");

        _store = new BlockedAppsStore(this);
        _blocked = _store.GetBlocked();

        var search = FindViewById<EditText>(Resource.Id.app_search)!;
        search.TextChanged += (_, e) => _adapter?.Filter(e.Text?.ToString());

        var list = FindViewById<ListView>(Resource.Id.app_list)!;
        list.ItemClick += OnItemClicked;

        FindViewById<Button>(Resource.Id.select_all)!.Click += (_, _) => SetAllShown(true);
        FindViewById<Button>(Resource.Id.clear_all)!.Click += (_, _) => SetAllShown(false);

        SetCountText(GetString(Resource.String.apps_loading));
        LoadAppsAsync();
    }

    private void LoadAppsAsync()
    {
        // القراءة على خيط خلفيّ ثم الربط على خيط الواجهة.
        Task.Run(() =>
        {
            var apps = AppInventory.LoadBlockableApps(this);
            RunOnUiThread(() =>
            {
                _totalApps = apps.Count;
                _adapter = new AppListAdapter(this, apps, _blocked);
                FindViewById<ListView>(Resource.Id.app_list)!.Adapter = _adapter;
                UpdateCount();
            });
        });
    }

    private void OnItemClicked(object? sender, AdapterView.ItemClickEventArgs e)
    {
        if (_adapter is null || _store is null)
        {
            return;
        }

        var app = _adapter[e.Position];
        var nowBlocked = !_blocked.Contains(app.PackageName);

        if (nowBlocked)
        {
            _blocked.Add(app.PackageName);
        }
        else
        {
            _blocked.Remove(app.PackageName);
        }

        _store.SetBlocked(app.PackageName, nowBlocked);
        _adapter.NotifyDataSetChanged();
        UpdateCount();
    }

    /// <summary>
    /// يطبّق الاختيار على التطبيقات **الظاهرة حالياً** لا على الكلّ دائماً —
    /// فلو كان هناك بحث فعّال، «تحديد الكلّ» يشمل نتيجة البحث وحدها. هذا أقرب
    /// لتوقّع المستخدم: ما يراه هو ما يتغيّر.
    /// </summary>
    private void SetAllShown(bool blocked)
    {
        if (_adapter is null || _store is null)
        {
            return;
        }

        var changed = 0;
        foreach (var app in _adapter.ShownApps)
        {
            var already = _blocked.Contains(app.PackageName);
            if (already == blocked)
            {
                continue;
            }

            if (blocked)
            {
                _blocked.Add(app.PackageName);
            }
            else
            {
                _blocked.Remove(app.PackageName);
            }

            _store.SetBlocked(app.PackageName, blocked);
            changed++;
        }

        AppLog.Info(Tag, $"bulk {(blocked ? "select" : "clear")}: {changed} app(s) changed");
        _adapter.NotifyDataSetChanged();
        UpdateCount();

        Toast.MakeText(
            this,
            string.Format(GetString(Resource.String.apps_select_all_filtered), changed),
            ToastLength.Short)!.Show();
    }

    private void UpdateCount() =>
        SetCountText(string.Format(GetString(Resource.String.apps_count), _blocked.Count, _totalApps));

    private void SetCountText(string text) =>
        FindViewById<TextView>(Resource.Id.app_count)!.Text = text;
}
