namespace KidsGuard.App.Security;

/// <summary>خطوة واحدة من مسار الفكّ، بنتيجتها وتفصيلها — تُعرض للمستخدم وتُكتب في logcat.</summary>
/// <param name="Order">ترتيب التنفيذ الفعلي.</param>
/// <param name="Name">اسم الخطوة بالإنجليزية كما تظهر في logcat.</param>
/// <param name="Outcome">نتيجتها.</param>
/// <param name="Detail">تفصيل يُقرأ عند الفشل — رسالة الاستثناء أو سبب التخطّي.</param>
public sealed record ReleaseStep(int Order, string Name, StepOutcome Outcome, string Detail)
{
    public override string ToString() => $"[{Order:00}] {Name}: {Outcome}" +
        (string.IsNullOrEmpty(Detail) ? string.Empty : $" — {Detail}");
}

public enum StepOutcome
{
    /// <summary>نُفّذت ونجحت.</summary>
    Succeeded,

    /// <summary>لم تكن مطلوبة أصلاً (لا قيد مفروض، أو لسنا device owner).</summary>
    Skipped,

    /// <summary>حاولت وفشلت. وجود واحدة يمنع التخلّي عن الملكية.</summary>
    Failed
}

/// <summary>حصيلة استدعاء واحد لـ <see cref="ReleaseManager.ReleaseEverything"/>.</summary>
public sealed class ReleaseResult
{
    public required IReadOnlyList<ReleaseStep> Steps { get; init; }

    /// <summary>هل تخلّى التطبيق فعلاً عن ملكية الجهاز.</summary>
    public required bool OwnershipReleased { get; init; }

    /// <summary>
    /// هل توقّفنا قبل نقطة اللاعودة لأن خطوة سابقة فشلت.
    /// البقاء مالكاً يعني إمكانية إعادة المحاولة؛ التخلّي مع قيد عالق يعني بقاءه للأبد.
    /// </summary>
    public required bool AbortedBeforeOwnership { get; init; }

    public IEnumerable<ReleaseStep> FailedSteps => Steps.Where(s => s.Outcome == StepOutcome.Failed);

    public bool HasFailures => Steps.Any(s => s.Outcome == StepOutcome.Failed);

    /// <summary>السجلّ كاملاً سطراً بسطر — يُعرض في شاشة الطوارئ.</summary>
    public string ToLogText() => string.Join(Environment.NewLine, Steps);
}
