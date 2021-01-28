using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmDriver</c>. Drivers live in <c>InfoDriver</c>
/// (soft-deleted via <c>sign</c>). A driver referenced by
/// <c>Challan_Main.DriverID</c> cannot be edited or deleted.
/// </summary>
public sealed class DriverService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public DriverService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public List<DriverRow> List()
    {
        var dt = _db.Query(
            "SELECT DID, Dname, ISNULL(Dlicence, '') AS Dlicence FROM InfoDriver WHERE sign = 1 ORDER BY Dname");
        return dt.AsEnumerable().Select(r => new DriverRow(
            Convert.ToInt32(r["DID"]),
            r["Dname"].ToString() ?? "",
            r["Dlicence"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string name, string licence)
    {
        name = (name ?? "").Trim();
        licence = (licence ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return new OpResult(false, "Type Driver Name");
        if (string.IsNullOrEmpty(licence)) return new OpResult(false, "Type Licence No");

        if (_db.Scalar("SELECT 1 FROM InfoDriver WHERE Dname = @n AND sign = 1",
                Params.New("n", name)) != null)
            return new OpResult(false, "Duplicate Driver");

        var maxId = _db.Scalar("SELECT MAX(DID) FROM InfoDriver");
        var nextId = (maxId == null ? 0 : Convert.ToInt32(maxId)) + 1;

        _db.Execute(
            @"INSERT INTO InfoDriver (DID, Dname, Dlicence, sign, UserID, PCName, EntryDate, EntryTime)
              VALUES (@id, @n, @l, 1, @uid, @pc,
                      CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108))",
            Params.New("id", nextId).Add("n", name).Add("l", licence)
                  .Add("uid", _user.UserId).Add("pc", _user.PcName));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(int did, string name, string licence)
    {
        name = (name ?? "").Trim();
        licence = (licence ?? "").Trim();
        if (did <= 0) return new OpResult(false, "Select a driver");
        if (string.IsNullOrEmpty(name)) return new OpResult(false, "Type Driver Name");

        if (InUse(did))
            return new OpResult(false, "ID Exist In Challan. Data can not Update");

        _db.Execute(
            "UPDATE InfoDriver SET Dname = @n, Dlicence = @l, UserID = @uid, PCName = @pc WHERE DID = @id",
            Params.New("n", name).Add("l", licence).Add("uid", _user.UserId).Add("pc", _user.PcName).Add("id", did));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(int did)
    {
        if (did <= 0) return new OpResult(false, "Select a driver");
        if (InUse(did))
            return new OpResult(false, "ID Exist In Challan. Data can not delete");

        _db.Execute("UPDATE InfoDriver SET sign = 0 WHERE DID = @id", Params.New("id", did));
        return new OpResult(true, "Data Delete Successfully");
    }

    private bool InUse(int did) =>
        _db.Scalar("SELECT 1 FROM Challan_Main WHERE DriverID = @id", Params.New("id", did)) != null;
}
