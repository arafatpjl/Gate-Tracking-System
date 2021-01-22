using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// External / receiving company maintenance (frmAddCompany). Implemented as a
/// classic server-rendered Razor MVC CRUD screen.
/// </summary>
[RequireCompany]
public sealed class OutCompanyController : Controller
{
    private readonly OutCompanyService _companies;

    public OutCompanyController(OutCompanyService companies) => _companies = companies;

    public IActionResult Index()
    {
        ViewData["Rows"] = _companies.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string compName, string address)
    {
        var r = _companies.Create(compName ?? "", address ?? "");
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int compId, string address)
    {
        var r = _companies.Update(compId, address ?? "");
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int compId)
    {
        var r = _companies.Delete(compId);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
