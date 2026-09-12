using Android.Graphics.Drawables;

namespace KidsGuard.App.Apps;

/// <summary>تطبيق واحد قابل للحجب — اسمه المعروض واسم حزمته وأيقونته.</summary>
public sealed class InstalledApp(string packageName, string label, Drawable? icon)
{
    public string PackageName { get; } = packageName;
    public string Label { get; } = label;
    public Drawable? Icon { get; } = icon;
}
