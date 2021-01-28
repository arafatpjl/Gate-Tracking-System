using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;

namespace GtrackWeb.Services;

/// <summary>
/// Authentication against <c>Sys_User_Name_UP</c>. Port of the login logic in
/// <c>frmlogin.btnlogin_Click</c> and the password change in
/// <c>frmchangepassword</c>. Active users are <c>YsnActive = 0</c> and passwords
/// are stored with the legacy Caesar cipher.
/// </summary>
public sealed class AuthService
{
    private readonly ISqlDataAccess _db;

    public AuthService(ISqlDataAccess db) => _db = db;

    public sealed record UserRecord(int UserId, string UserName);

    /// <summary>Returns the user when credentials match, otherwise null.</summary>
    public UserRecord? Validate(string userName, string password)
    {
        var encrypted = CipherHelper.Encrypt(password.Trim());

        var dt = _db.Query(
            @"SELECT * FROM [Sys_User_Name_UP]
              WHERE YsnActive = 0 AND UserName = @user AND UserPWord = @pwd",
            Params.New("user", userName.Trim()).Add("pwd", encrypted));

        if (dt.Rows.Count == 0) return null;

        var row = dt.Rows[0];

        // Mirror the desktop double-check that the stored value decrypts back.
        if (CipherHelper.Decrypt(row["UserPWord"].ToString() ?? string.Empty) != password.Trim())
            return null;

        var userId = row.Table.Columns.Contains("UserID") && row["UserID"] != DBNull.Value
            ? Convert.ToInt32(row["UserID"])
            : 0;

        return new UserRecord(userId, row["UserName"].ToString() ?? userName);
    }

    /// <summary>Port of <c>frmchangepassword</c>: verify old password then set new.</summary>
    public bool ChangePassword(string userName, string oldPassword, string newPassword)
    {
        var user = Validate(userName, oldPassword);
        if (user == null) return false;

        return _db.Execute(
            @"UPDATE [Sys_User_Name_UP] SET UserPWord = @pwd WHERE UserName = @user",
            Params.New("pwd", CipherHelper.Encrypt(newPassword.Trim())).Add("user", userName.Trim())) > 0;
    }
}
