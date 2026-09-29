using System.Security.Cryptography;
using System.Text;
using Android.Content;
using KidsGuard.App.Logging;

namespace KidsGuard.App.Security;

/// <summary>
/// رمز الحماية الذي يحرس مخرج الطوارئ. يُخزَّن مُلبَّداً (salt) ومُشتقّاً بـ PBKDF2،
/// لا نصّاً صريحاً — فمن يقرأ ملفّ الإعدادات لا يحصل على الرمز نفسه.
///
/// الغرض منه منع الطفل من فكّ الحماية، لا مقاومة مهاجم يملك الجهاز وصلاحية root.
/// </summary>
public sealed class PinStore
{
    private const string PrefsName = "kidsguard_prefs";
    private const string KeyHash = "pin_hash";
    private const string KeySalt = "pin_salt";
    private const string Tag = "KidsGuard.Pin";

    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 20_000;

    /// <summary>أقصر رمز مقبول — أربعة أرقام.</summary>
    public const int MinLength = 4;

    private readonly ISharedPreferences _prefs;

    public PinStore(Context context)
    {
        var app = context.ApplicationContext ?? context;
        _prefs = app.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

    public bool HasPin => !string.IsNullOrEmpty(_prefs.GetString(KeyHash, null));

    public void SetPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(pin, salt);

        var editor = _prefs.Edit()!;
        editor.PutString(KeySalt, Convert.ToBase64String(salt));
        editor.PutString(KeyHash, Convert.ToBase64String(hash));
        editor.Apply();

        AppLog.Info(Tag, "protection PIN set");
    }

    /// <summary>يتحقّق من الرمز. يُرجع false بأمان إن لم يكن ثمّة رمز محفوظ.</summary>
    public bool Verify(string pin)
    {
        var storedHash = _prefs.GetString(KeyHash, null);
        var storedSalt = _prefs.GetString(KeySalt, null);

        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
        {
            AppLog.Warn(Tag, "verify called but no PIN is stored");
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(storedSalt);
            var expected = Convert.FromBase64String(storedHash);
            var actual = Derive(pin, salt);

            // مقارنة ثابتة الزمن — لا تُسرّب طول التطابق.
            var match = CryptographicOperations.FixedTimeEquals(actual, expected);
            AppLog.Info(Tag, $"PIN verification: {(match ? "accepted" : "rejected")}");
            return match;
        }
        catch (Exception ex)
        {
            AppLog.Error(Tag, "PIN verification failed", ex);
            return false;
        }
    }

    private static byte[] Derive(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes);
}
