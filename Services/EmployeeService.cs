using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Read-only employee browse (frmEmpSearching). Employees are imported from HR
/// into <c>InfoEmp</c> (active = <c>mleft = 0</c>) and organised by
/// <c>Dept_Name</c> → <c>Sub_Dept_Name</c>. This screen searches them by company,
/// department, section and code/name; it does not create employees.
/// </summary>
public sealed class EmployeeService
{
    private readonly ISqlDataAccess _db;

    public EmployeeService(ISqlDataAccess db) => _db = db;

    public List<LookupItem> Departments(int compId)
    {
        var dt = _db.Query(
            "SELECT DISTINCT DeptName FROM Dept_Name WHERE CompID = @c ORDER BY DeptName",
            Params.New("c", compId));
        return dt.AsEnumerable().Select(r => new LookupItem(r["DeptName"].ToString() ?? "", r["DeptName"].ToString() ?? "")).ToList();
    }

    public List<LookupItem> Sections(int compId, string dept)
    {
        if (string.IsNullOrWhiteSpace(dept)) return new List<LookupItem>();
        var dt = _db.Query(
            @"SELECT DISTINCT A.SubDeptName
              FROM Sub_Dept_Name A
              INNER JOIN Dept_Name B ON A.CompID = B.CompID AND A.DeptName = B.DeptName
              WHERE A.CompID = @c AND A.Status = 0 AND A.DeptName = @d
              ORDER BY A.SubDeptName",
            Params.New("c", compId).Add("d", dept));
        return dt.AsEnumerable().Select(r => new LookupItem(r["SubDeptName"].ToString() ?? "", r["SubDeptName"].ToString() ?? "")).ToList();
    }

    public List<EmployeeRow> Search(int compId, string dept, string section, string code, string name)
    {
        var sql =
            @"SELECT EmpCode, EMPName, ISNULL(Department,'') AS Department, ISNULL(Section,'') AS Section
              FROM InfoEmp
              WHERE ComPID = @c AND mleft = 0
                AND (@dept = '' OR Department = @dept)
                AND (@section = '' OR Section = @section)
                AND (@code = '' OR EmpCode LIKE @codeLike)
                AND (@name = '' OR EMPName LIKE @nameLike)
              ORDER BY Department, Section, EmpCode";

        var dt = _db.Query(sql,
            Params.New("c", compId)
                  .Add("dept", dept ?? "").Add("section", section ?? "")
                  .Add("code", code ?? "").Add("codeLike", (code ?? "") + "%")
                  .Add("name", name ?? "").Add("nameLike", (name ?? "") + "%"));

        return dt.AsEnumerable().Select(r => new EmployeeRow(
            r["EmpCode"].ToString() ?? "",
            r["EMPName"].ToString() ?? "",
            r["Department"].ToString() ?? "",
            r["Section"].ToString() ?? "")).ToList();
    }
}
