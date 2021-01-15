using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Merchandiser maintenance (frmMerchandiser).</summary>
[RequireCompany]
public sealed class MerchandiserController : Controller
{
    private readonly MerchandiserService _svc;

    public MerchandiserController(MerchandiserService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string name) => Done(_svc.Create(name));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(string oldName, string name) => Done(_svc.Update(oldName, name));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string name) => Done(_svc.Delete(name));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
