using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Purpose maintenance (frmPurpuse).</summary>
[RequireCompany]
public sealed class PurposeController : Controller
{
    private readonly PurposeService _svc;

    public PurposeController(PurposeService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string purpose) => Done(_svc.Create(purpose));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int pid, string purpose) => Done(_svc.Update(pid, purpose));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int pid) => Done(_svc.Delete(pid));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
