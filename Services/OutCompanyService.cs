using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmAddCompany</c> (external / receiving companies stored in
/// <c>Out_Company_Information</c>, soft-deleted via <c>DeleteRow</c>).
/// </summary>
public sealed class OutCompanyService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public OutCompanyService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public sealed record Result(bool Ok, string Message);

    public List<OutCompanyRow> List()
    {
        var dt = _db.Query(
            @"SELECT CompID, CompName, ISNULL(CompAdd, '') AS CompAdd
              FROM Out_Company_Information WHERE DeleteRow = 0 ORDER BY CompName");
        return dt.AsEnumerable().Select(r => new OutCompanyRow(
            Convert.ToInt32(r["CompID"]),
            r["CompName"].ToString() ?? "",
            r["CompAdd"].ToString() ?? "")).ToList();
    }

    public Result Create(string compName, string address)
    {
        compName = compName.Trim();
        if (string.IsNullOrEmpty(compName)) return new Result(false, "Type Company Name");

        var dup = _db.Scalar(
            "SELECT 1 FROM Out_Company_Information WHERE CompName = @n AND DeleteRow = 0",
            Params.New("n", compName));
        if (dup != null) return new Result(false, "This Company is Already Exist");

        _db.Execute(
            @"INSERT INTO Out_Company_Information
                (CompName, CompAdd, DeleteRow, UserID, PCName, EntryDate, EntryTime)
              VALUES (@name, @addr, 0, @uid, @pc, @date, @time)",
            Params.New("name", compName)
                  .Add("addr", address.Trim())
                  .Add("uid", _user.UserId)
                  .Add("pc", _user.PcName)
                  .Add("date", DateTime.Now.ToString("dd-MMM-yyyy"))
                  .Add("time", DateTime.Now.ToString("hh:mm:ss")));

        return new Result(true, "Data Successfully Save");
    }

    public Result Update(int compId, string address)
    {
        if (compId <= 0) return new Result(false, "Select a company");

        _db.Execute(
            "UPDATE Out_Company_Information SET CompAdd = @addr WHERE CompID = @id",
            Params.New("addr", address.Trim()).Add("id", compId));

        return new Result(true, "Data Update Successfully");
    }

    public Result Delete(int compId)
    {
        if (compId <= 0) return new Result(false, "Select a company");

        _db.Execute(
            "UPDATE Out_Company_Information SET DeleteRow = 1 WHERE CompID = @id",
            Params.New("id", compId));

        return new Result(true, "Data Delete Successfully");
    }
}
