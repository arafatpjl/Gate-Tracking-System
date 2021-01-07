using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Driver maintenance (frmDriver).</summary>
[RequireCompany]
public sealed class DriverController : Controller
{
    private readonly DriverService _svc;

    public DriverController(DriverService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string name, string licence) => Done(_svc.Create(name, licence));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int did, string name, string licence) => Done(_svc.Update(did, name, licence));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int did) => Done(_svc.Delete(did));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
