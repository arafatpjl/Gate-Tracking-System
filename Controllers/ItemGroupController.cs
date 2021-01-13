using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Item group maintenance (frmIteamGroup).</summary>
[RequireCompany]
public sealed class ItemGroupController : Controller
{
    private readonly ItemGroupService _svc;

    public ItemGroupController(ItemGroupService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string itemType) => Done(_svc.Create(itemType));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int id, string itemType) => Done(_svc.Update(id, itemType));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int id) => Done(_svc.Delete(id));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
