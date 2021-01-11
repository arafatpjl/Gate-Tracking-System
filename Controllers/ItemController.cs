using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Item maintenance (frmNewIteam).</summary>
[RequireCompany]
public sealed class ItemController : Controller
{
    private readonly ItemService _svc;
    private readonly ItemGroupService _groups;

    public ItemController(ItemService svc, ItemGroupService groups)
    {
        _svc = svc;
        _groups = groups;
    }

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        ViewData["Groups"] = _groups.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string itemName, string itemType) => Done(_svc.Create(itemName, itemType));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(int itemId, string itemName, string itemType) =>
        Done(_svc.Update(itemId, itemName, itemType));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int itemId) => Done(_svc.Delete(itemId));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
