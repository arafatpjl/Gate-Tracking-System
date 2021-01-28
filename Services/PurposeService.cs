using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmPurpuse</c>. Purposes live in <c>new_purpose</c>
/// (soft-deleted via <c>DeleteRow</c>). A purpose referenced by
/// <c>Challan_Main.pid</c> cannot be deleted.
/// </summary>
public sealed class PurposeService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public PurposeService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public List<PurposeRow> List()
    {
        var dt = _db.Query(
            "SELECT pid, purpose FROM new_purpose WHERE DeleteRow = 0 ORDER BY purpose");
        return dt.AsEnumerable()
            .Select(r => new PurposeRow(Convert.ToInt32(r["pid"]), r["purpose"].ToString() ?? ""))
            .ToList();
    }

    public OpResult Create(string purpose)
    {
        purpose = (purpose ?? "").Trim();
        if (string.IsNullOrEmpty(purpose)) return new OpResult(false, "Type Purpose Name");

        if (_db.Scalar("SELECT 1 FROM new_purpose WHERE purpose = @p AND DeleteRow = 0",
                Params.New("p", purpose)) != null)
            return new OpResult(false, "Duplicate Type of Purpose");

        _db.Execute(
            @"INSERT INTO new_purpose (purpose, UserID, CompId, PCName, DeleteRow, EntryDate, EntryTime)
              VALUES (@p, @uid, @cid, @pc, 0,
                      CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108))",
            Params.New("p", purpose).Add("uid", _user.UserId).Add("cid", _user.CompId).Add("pc", _user.PcName));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(int pid, string purpose)
    {
        purpose = (purpose ?? "").Trim();
        if (pid <= 0) return new OpResult(false, "Select a purpose");
        if (string.IsNullOrEmpty(purpose)) return new OpResult(false, "Type Purpose Name");

        _db.Execute("UPDATE new_purpose SET purpose = @p, UserID = @uid, PCName = @pc WHERE pid = @id",
            Params.New("p", purpose).Add("uid", _user.UserId).Add("pc", _user.PcName).Add("id", pid));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(int pid)
    {
        if (pid <= 0) return new OpResult(false, "Select a purpose");

        if (_db.Scalar("SELECT 1 FROM Challan_Main WHERE pid = @id", Params.New("id", pid)) != null)
            return new OpResult(false, "Purpose Exist In Challan. Data can not Delete");

        _db.Execute("UPDATE new_purpose SET DeleteRow = 1 WHERE pid = @id", Params.New("id", pid));
        return new OpResult(true, "Data Delete Successfully");
    }
}
