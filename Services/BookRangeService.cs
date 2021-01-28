using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Book-range maintenance (frmBookRange). Challan book number ranges are stored
/// per company + department + section in <c>BookRange</c>. The desktop form was
/// largely broken (leftover buyer code); this is a clean reconstruction of the
/// intended CRUD over the BookRange table.
/// </summary>
public sealed class BookRangeService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public BookRangeService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public List<BookRangeRow> List()
    {
        var dt = _db.Query(
            @"SELECT ISNULL(DeptName,'') AS DeptName, ISNULL(SubDeptName,'') AS SubDeptName,
                     ISNULL(CONVERT(varchar, startNo),'') AS startNo, ISNULL(CONVERT(varchar, EndNo),'') AS EndNo
              FROM BookRange WHERE CompID = @c ORDER BY DeptName, SubDeptName, startNo",
            Params.New("c", _user.CompId));
        return dt.AsEnumerable().Select(r => new BookRangeRow(
            r["DeptName"].ToString() ?? "", r["SubDeptName"].ToString() ?? "",
            r["startNo"].ToString() ?? "", r["EndNo"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string dept, string section, string startNo, string endNo)
    {
        if (string.IsNullOrWhiteSpace(dept)) return new OpResult(false, "Select Department");
        if (string.IsNullOrWhiteSpace(startNo)) return new OpResult(false, "Type Start No");
        if (string.IsNullOrWhiteSpace(endNo)) return new OpResult(false, "Type End No");

        var dup = _db.Scalar(
            "SELECT 1 FROM BookRange WHERE CompID=@c AND DeptName=@d AND ISNULL(SubDeptName,'')=@s AND startNo=@st",
            Params.New("c", _user.CompId).Add("d", dept).Add("s", section ?? "").Add("st", startNo));
        if (dup != null) return new OpResult(false, "Duplicate Book Range");

        _db.Execute(
            @"INSERT INTO BookRange (CompID, DeptName, SubDeptName, startNo, EndNo)
              VALUES (@c, @d, @s, @st, @en)",
            Params.New("c", _user.CompId).Add("d", dept).Add("s", section ?? "")
                  .Add("st", startNo).Add("en", endNo));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Delete(string dept, string section, string startNo)
    {
        _db.Execute(
            "DELETE FROM BookRange WHERE CompID=@c AND DeptName=@d AND ISNULL(SubDeptName,'')=@s AND startNo=@st",
            Params.New("c", _user.CompId).Add("d", dept).Add("s", section ?? "").Add("st", startNo));
        return new OpResult(true, "Data Delete Successfully");
    }
}
