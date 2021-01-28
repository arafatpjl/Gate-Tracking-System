using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// User administration against <c>Sys_User_Name_UP</c> (the authoritative login
/// table). This is a coherent replacement for <c>form.frm_user_information</c>,
/// which in the desktop also wrote a parallel <c>tblUserInfo</c> row and a
/// per-user menu-permission id. That legacy menu-permission system is NOT
/// replicated here — the web app currently grants the full menu after login.
///
/// Active users are <c>YsnActive = 0</c>; deactivating sets it to 1.
/// Passwords use the legacy Caesar cipher (see <see cref="CipherHelper"/>).
/// </summary>
public sealed class UserAdminService
{
    private readonly ISqlDataAccess _db;

    public UserAdminService(ISqlDataAccess db) => _db = db;

    public List<UserRow> List()
    {
        var dt = _db.Query(
            "SELECT UserID, UserName, YsnActive FROM Sys_User_Name_UP ORDER BY UserName");
        return dt.AsEnumerable().Select(r => new UserRow(
            r["UserID"] == DBNull.Value ? 0 : Convert.ToInt32(r["UserID"]),
            r["UserName"].ToString() ?? "",
            (r["YsnActive"].ToString() ?? "0") == "0")).ToList();
    }

    public OpResult Create(string userName, string password)
    {
        userName = (userName ?? "").Trim();
        if (string.IsNullOrEmpty(userName)) return new OpResult(false, "Type User Name");
        if (string.IsNullOrEmpty(password)) return new OpResult(false, "Type Password");

        if (_db.Scalar("SELECT 1 FROM Sys_User_Name_UP WHERE UserName = @u", Params.New("u", userName)) != null)
            return new OpResult(false, "User already exists");

        var maxId = _db.Scalar("SELECT MAX(UserID) FROM Sys_User_Name_UP");
        var nextId = (maxId == null ? 0 : Convert.ToInt32(maxId)) + 1;

        _db.Execute(
            @"INSERT INTO Sys_User_Name_UP (UserID, UserName, UserPWord, YsnActive)
              VALUES (@id, @u, @p, 0)",
            Params.New("id", nextId).Add("u", userName).Add("p", CipherHelper.Encrypt(password.Trim())));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult ResetPassword(int userId, string password)
    {
        if (userId <= 0) return new OpResult(false, "Select a user");
        if (string.IsNullOrWhiteSpace(password)) return new OpResult(false, "Type Password");

        _db.Execute("UPDATE Sys_User_Name_UP SET UserPWord = @p WHERE UserID = @id",
            Params.New("p", CipherHelper.Encrypt(password.Trim())).Add("id", userId));

        return new OpResult(true, "Password Updated Successfully");
    }

    public OpResult SetActive(int userId, bool active)
    {
        if (userId <= 0) return new OpResult(false, "Select a user");

        _db.Execute("UPDATE Sys_User_Name_UP SET YsnActive = @a WHERE UserID = @id",
            Params.New("a", active ? 0 : 1).Add("id", userId));

        return new OpResult(true, active ? "User Activated" : "User Deactivated");
    }
}
