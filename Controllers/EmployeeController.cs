using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Employee browse / search (frmEmpSearching) over InfoEmp.</summary>
[RequireCompany]
public sealed class EmployeeController : Controller
{
    private readonly EmployeeService _svc;
    private readonly CurrentUser _user;

    public EmployeeController(EmployeeService svc, CurrentUser user)
    {
        _svc = svc;
        _user = user;
    }

    [HttpGet]
    public IActionResult Index(string? dept, string? section, string? code, string? name)
    {
        var compId = _user.CompId;
        dept ??= "";
        section ??= "";

        ViewData["Departments"] = _svc.Departments(compId);
        ViewData["Sections"] = _svc.Sections(compId, dept);   // sections for the currently-selected dept
        ViewData["Dept"] = dept;
        ViewData["Section"] = section;
        ViewData["Code"] = code ?? "";
        ViewData["Name"] = name ?? "";
        ViewData["Rows"] = _svc.Search(compId, dept, section, code ?? "", name ?? "");
        return View();
    }
}
