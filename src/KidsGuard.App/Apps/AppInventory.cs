using Android.Content;
using Android.Content.PM;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Apps;

/// <summary>
/// يقرأ التطبيقات القابلة للحجب: ما له أيقونة في المشغّل ويطلب إذن الإنترنت.
/// يُستدعى على خيط خلفيّ — قراءة الأيقونات والأسماء لمئة تطبيق تُجمّد الواجهة لو جرت عليها.
/// </summary>
public static class AppInventory
{
    private const string Tag = "KidsGuard.Inventory";

    public static List<InstalledApp> LoadBlockableApps(Context context)
    {
        var pm = context.PackageManager;
        var result = new List<InstalledApp>();
        if (pm is null)
        {
            AppLog.Error(Tag, "PackageManager unavailable");
            return result;
        }

        var ownPackage = context.PackageName;
        var seen = new HashSet<string>();

        var launcherIntent = new Intent(Intent.ActionMain);
        launcherIntent.AddCategory(Intent.CategoryLauncher);

        var launchables = pm.QueryIntentActivities(launcherIntent, 0);
        AppLog.Info(Tag, $"launchable activities: {launchables.Count}");

        foreach (var info in launchables)
        {
            var appInfo = info.ActivityInfo?.ApplicationInfo;
            var package = appInfo?.PackageName;
            if (string.IsNullOrEmpty(package) || package == ownPackage || !seen.Add(package))
            {
                continue;
            }

            // حجب تطبيق لا يطلب الإنترنت بلا معنى — نستبعده لتبقى القائمة ذات دلالة.
            if (!RequestsInternet(pm, package))
            {
                continue;
            }

            try
            {
                var label = appInfo!.LoadLabel(pm) ?? package;
                var icon = appInfo.LoadIcon(pm);
                result.Add(new InstalledApp(package, label, icon));
            }
            catch (Exception ex)
            {
                AppLog.Warn(Tag, $"load failed for {package}: {ex.Message}");
            }
        }

        result.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
        AppLog.Info(Tag, $"blockable apps: {result.Count}");
        return result;
    }

    private static bool RequestsInternet(PackageManager pm, string package)
    {
        try
        {
            var pkgInfo = pm.GetPackageInfo(package, PackageInfoFlags.Permissions);
            var requested = pkgInfo?.RequestedPermissions;
            return requested is not null && requested.Contains("android.permission.INTERNET");
        }
        catch (Exception ex)
        {
            AppLog.Warn(Tag, $"permission check failed for {package}: {ex.Message}");
            return false;
        }
    }
}
