namespace GtrackWeb.Helpers;

/// <summary>
/// Faithful port of <c>Gtrack.Extra.call.EncryptIt/DecryptIt</c> — a simple
/// Caesar (character-shift) cipher. Kept identical so existing password hashes
/// in <c>Sys_User_Name_UP.UserPWord</c> keep validating without a data migration.
///
/// NOTE: this is NOT real cryptography. It is preserved only for compatibility
/// with the legacy database. See README for the recommended hardening path.
/// </summary>
public static class CipherHelper
{
    public const int DefaultShift = 11;

    public static string Encrypt(string value, int shift = DefaultShift)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] + shift);
        return new string(chars);
    }

    public static string Decrypt(string value, int shift = DefaultShift)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            chars[i] = (char)(chars[i] - shift);
        return new string(chars);
    }
}
