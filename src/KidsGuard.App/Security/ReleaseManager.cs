using Android.App.Admin;
using KidsGuard.App.Logging;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace KidsGuard.App.Security;

/// <summary>
/// نقطة الخروج الوحيدة في التطبيق. كلّ طبقات الطوارئ الخمس تستدعي
/// <see cref="ReleaseEverything"/> نفسها — لا نسخة ثانية من هذا المنطق في أي مكان.
///
/// ثلاث قواعد تحكم هذا الملف:
///   1. الترتيب مُلزَم: القيود ثمّ فكّ التجميد ثمّ فتح الحذف ثمّ التخلّي عن الملكية أخيراً.
///      بعد ClearDeviceOwnerApp تُفقد الصلاحية اللازمة لكلّ ما قبلها.
///   2. لا يُرمى استثناء إلى المستدعي أبداً. الطبقة التي تنادينا قد تكون
///      BroadcastReceiver أو حارس انهيار — رمي استثناء هناك يقتل المخرج نفسه.
///   3. عند فشل أي خطوة قبل الملكية نتوقّف ونبقى مالكين. البقاء مالكاً يسمح
///      بإعادة المحاولة؛ التخلّي مع قيد عالق يُبقي القيد إلى الأبد.
/// </summary>
public sealed class ReleaseManager
{
    private const string Tag = "KidsGuard.Release";

    /// <summary>
    /// القيود التي نفرضها ونرفعها. DISALLOW_FACTORY_RESET غائب عمداً وإلى الأبد —
    /// هو الفرق بين جهاز نختبر عليه وجهاز نخسره.
    /// </summary>
    private static readonly string[] ManagedRestrictions =
    [
        UserManager.DisallowSafeBoot,
        UserManager.DisallowAddUser,
        UserManager.DisallowDebuggingFeatures,
        UserManager.DisallowInstallUnknownSources
    ];

    private readonly Context _context;
    private readonly TimeProvider _timeProvider;
    private readonly DevicePolicyManager? _dpm;
    private readonly ComponentName _admin;

    public ReleaseManager(Context context, TimeProvider? timeProvider = null)
    {
        _context = context.ApplicationContext ?? context;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dpm = _context.GetSystemService(Context.DevicePolicyService) as DevicePolicyManager;
        _admin = AppDeviceAdminReceiver.GetComponent(_context);
    }

    /// <summary>هل التطبيق مالك الجهاز الآن. تُرجع false بأمان إن غابت الخدمة.</summary>
    public bool IsDeviceOwner
    {
        get
        {
            try
            {
                return _dpm?.IsDeviceOwnerApp(_context.PackageName) == true;
            }
            catch (Exception ex)
            {
                AppLog.Warn(Tag, "IsDeviceOwner check failed: " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>هل المستقبِل مفعّل كـ device admin (أضعف من device owner).</summary>
    public bool IsAdminActive
    {
        get
        {
            try
            {
                return _dpm?.IsAdminActive(_admin) == true;
            }
            catch (Exception ex)
            {
                AppLog.Warn(Tag, "IsAdminActive check failed: " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// يفكّ كلّ ما فرضه التطبيق ثمّ يتخلّى عن ملكية الجهاز — بالترتيب الملزم.
    /// آمنة للاستدعاء ولو لم نكن device owner إطلاقاً: تُرجع نتيجة كلّ خطواتها Skipped.
    /// </summary>
    /// <param name="forceOwnershipRelease">
    /// تجاوز حارس القاعدة 3 والتخلّي عن الملكية رغم فشل خطوة سابقة.
    /// لا يُمرَّر true إلا من حارس حلقة الانهيار، حيث الجهاز غير صالح للاستعمال أصلاً
    /// فيصير بقاء قيد عالق أهون من بقاء التطبيق مالكاً وهو ينهار عند كل إقلاع.
    /// </param>
    public ReleaseResult ReleaseEverything(bool forceOwnershipRelease = false)
    {
        var steps = new List<ReleaseStep>();
        var order = 0;
        var startedAt = _timeProvider.GetUtcNow();

        AppLog.Warn(Tag, FormattableString.Invariant(
            $"=== ReleaseEverything START at {startedAt:O} (force={forceOwnershipRelease}) ==="));

        if (_dpm is null)
        {
            steps.Add(new ReleaseStep(++order, "resolve-device-policy-service", StepOutcome.Failed,
                "DevicePolicyManager unavailable"));
            return Finish(steps, ownershipReleased: false, aborted: true, startedAt);
        }

        if (!IsDeviceOwner)
        {
            steps.Add(new ReleaseStep(++order, "verify-device-owner", StepOutcome.Skipped,
                "app is not device owner - nothing to release"));
            return Finish(steps, ownershipReleased: false, aborted: false, startedAt);
        }

        steps.Add(new ReleaseStep(++order, "verify-device-owner", StepOutcome.Succeeded, string.Empty));

        // 1) القيود أوّلاً
        foreach (var restriction in ManagedRestrictions)
        {
            steps.Add(ClearRestriction(++order, restriction));
        }

        // 2) فكّ تجميد كل التطبيقات.
        // نُعدّد من النظام لا من ملفّ إعداداتنا: مسار الطوارئ يُستدعى غالباً
        // لأن شيئاً في التطبيق تعطّل، فقد تكون قائمتنا المحفوظة هي العطل نفسه.
        steps.Add(UnsuspendAllPackages(++order));

        // 3) فتح قفل الحذف
        steps.Add(AllowUninstall(++order));

        // 4) نقطة اللاعودة
        var failedBefore = steps.Count(s => s.Outcome == StepOutcome.Failed);
        if (failedBefore > 0 && !forceOwnershipRelease)
        {
            steps.Add(new ReleaseStep(++order, "clear-device-owner", StepOutcome.Skipped,
                FormattableString.Invariant(
                    $"aborted: {failedBefore} earlier step(s) failed - staying owner so this can be retried")));
            return Finish(steps, ownershipReleased: false, aborted: true, startedAt);
        }

        var released = ClearOwnership(++order, out var ownershipStep);
        steps.Add(ownershipStep);

        return Finish(steps, released, aborted: false, startedAt);
    }

    private ReleaseStep ClearRestriction(int order, string restriction)
    {
        var name = "clear-restriction:" + restriction;
        try
        {
            _dpm!.ClearUserRestriction(_admin, restriction);
            return new ReleaseStep(order, name, StepOutcome.Succeeded, string.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, name + " failed: " + ex);
            return new ReleaseStep(order, name, StepOutcome.Failed, ex.Message);
        }
    }

    private ReleaseStep UnsuspendAllPackages(int order)
    {
        const string name = "unsuspend-all-packages";
        try
        {
            var suspended = FindSuspendedPackages();
            if (suspended.Count == 0)
            {
                return new ReleaseStep(order, name, StepOutcome.Skipped, "no suspended packages found");
            }

            // تُرجع أسماء ما تعذّر فكّه — مصفوفة فارغة تعني نجاحاً كاملاً.
            var stillSuspended = _dpm!.SetPackagesSuspended(_admin, suspended.ToArray(), false)
                                 ?? Array.Empty<string>();

            return stillSuspended.Length == 0
                ? new ReleaseStep(order, name, StepOutcome.Succeeded,
                    FormattableString.Invariant($"released {suspended.Count} package(s)"))
                : new ReleaseStep(order, name, StepOutcome.Failed,
                    "still suspended: " + string.Join(", ", stillSuspended));
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, name + " failed: " + ex);
            return new ReleaseStep(order, name, StepOutcome.Failed, ex.Message);
        }
    }

    private List<string> FindSuspendedPackages()
    {
        var result = new List<string>();
        var pm = _context.PackageManager;
        if (pm is null)
        {
            return result;
        }

        var installed = pm.GetInstalledApplications(PackageInfoFlags.MatchUninstalledPackages);
        if (installed is null)
        {
            return result;
        }

        foreach (var app in installed)
        {
            var packageName = app.PackageName;
            if (string.IsNullOrEmpty(packageName) || packageName == _context.PackageName)
            {
                continue;
            }

            try
            {
                if (_dpm!.IsPackageSuspended(_admin, packageName))
                {
                    result.Add(packageName);
                }
            }
            catch (Exception ex)
            {
                // حزمة واحدة تعذّر فحصها لا تُسقط المسح كلّه.
                AppLog.Warn(Tag, "suspend-check failed for " + packageName + ": " + ex.Message);
            }
        }

        return result;
    }

    private ReleaseStep AllowUninstall(int order)
    {
        const string name = "allow-uninstall";
        try
        {
            _dpm!.SetUninstallBlocked(_admin, _context.PackageName, false);
            return new ReleaseStep(order, name, StepOutcome.Succeeded, string.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, name + " failed: " + ex);
            return new ReleaseStep(order, name, StepOutcome.Failed, ex.Message);
        }
    }

    private bool ClearOwnership(int order, out ReleaseStep step)
    {
        const string name = "clear-device-owner";
        try
        {
            // CA1422: ClearDeviceOwnerApp مهجورة منذ API 26، ووثائق Android تصفها
            // بأنها "for testing purposes only". أبقيناها لأنها الوسيلة الوحيدة:
            // مسحُ سطح الـ API في Mono.Android 36.1.30 أظهر أن كل ما يخصّ الملكية هو
            // IsDeviceOwnerApp و ClearDeviceOwnerApp و SetDeviceOwnerLockScreenInfo — لا بديل.
            // ⚠️ خطر مفتوح: يجب إثبات أنها ما زالت تعمل فعلياً على API 35/36 في المهمّة 1.4
            // قبل تفعيل Device Owner على أي جهاز حقيقي.
#pragma warning disable CA1422
            _dpm!.ClearDeviceOwnerApp(_context.PackageName!);
#pragma warning restore CA1422
            step = new ReleaseStep(order, name, StepOutcome.Succeeded, "ownership relinquished");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, name + " failed: " + ex);
            step = new ReleaseStep(order, name, StepOutcome.Failed, ex.Message);
            return false;
        }
    }

    private ReleaseResult Finish(List<ReleaseStep> steps, bool ownershipReleased, bool aborted, DateTimeOffset startedAt)
    {
        var elapsed = _timeProvider.GetUtcNow() - startedAt;

        foreach (var step in steps)
        {
            AppLog.Info(Tag, step.ToString());
        }

        AppLog.Warn(Tag, FormattableString.Invariant(
            $"=== ReleaseEverything END in {elapsed.TotalMilliseconds:F0}ms (released={ownershipReleased}, aborted={aborted}) ==="));

        return new ReleaseResult
        {
            Steps = steps,
            OwnershipReleased = ownershipReleased,
            AbortedBeforeOwnership = aborted
        };
    }
}
