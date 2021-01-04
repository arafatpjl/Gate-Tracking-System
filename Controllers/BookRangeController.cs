using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Book-range maintenance (frmBookRange).</summary>
[RequireCompany]
public sealed class BookRangeController : Controller
{
    private readonly BookRangeService _svc;
    private readonly LookupService _lookups;

    public BookRangeController(BookRangeService svc, LookupService lookups)
    {
        _svc = svc;
        _lookups = lookups;
    }

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        ViewData["Departments"] = _lookups.Departments();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string dept, string section, string startNo, string endNo) =>
        Done(_svc.Create(dept, section, startNo, endNo));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string dept, string section, string startNo) =>
        Done(_svc.Delete(dept, section, startNo));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
