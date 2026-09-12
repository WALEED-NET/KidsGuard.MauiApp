using Android.Views;
using Android.Widget;

namespace KidsGuard.App.Apps;

/// <summary>
/// محوّل ListView لقائمة التطبيقات مع بحث. حالة العلامة تُقرأ من مجموعة محفوظة في
/// الذاكرة (يمرّرها النشاط) لا من التخزين في كل bind — فالتمرير يبقى سلساً.
///
/// العلامة نفسها غير تفاعلية؛ النقر على الصفّ كلّه يبدّلها. هذا يتجنّب فخّ إعادة
/// استخدام صفوف ListView (تبديل حالة صفّ يُطلق حدث صفّ آخر مُعاد استخدامه)،
/// ويعطي مساحة نقر أوسع.
/// </summary>
public sealed class AppListAdapter : BaseAdapter<InstalledApp>
{
    private readonly LayoutInflater _inflater;
    private readonly List<InstalledApp> _all;
    private readonly HashSet<string> _blocked;
    private List<InstalledApp> _shown;

    public AppListAdapter(Activity activity, List<InstalledApp> apps, HashSet<string> blocked)
    {
        _inflater = activity.LayoutInflater!;
        _all = apps;
        _shown = apps;
        _blocked = blocked;
    }

    public void Filter(string? query)
    {
        _shown = string.IsNullOrWhiteSpace(query)
            ? _all
            : _all.Where(a =>
                a.Label.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                a.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        NotifyDataSetChanged();
    }

    public override InstalledApp this[int position] => _shown[position];
    public override int Count => _shown.Count;
    public override long GetItemId(int position) => position;

    public override View GetView(int position, View? convertView, ViewGroup? parent)
    {
        var view = convertView ?? _inflater.Inflate(Resource.Layout.item_app, parent, false)!;
        var app = _shown[position];

        view.FindViewById<ImageView>(Resource.Id.app_icon)!.SetImageDrawable(app.Icon);
        view.FindViewById<TextView>(Resource.Id.app_label)!.Text = app.Label;
        view.FindViewById<TextView>(Resource.Id.app_package)!.Text = app.PackageName;
        view.FindViewById<CheckBox>(Resource.Id.app_blocked)!.Checked = _blocked.Contains(app.PackageName);

        return view;
    }
}
