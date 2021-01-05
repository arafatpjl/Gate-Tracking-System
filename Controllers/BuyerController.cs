using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Buyer maintenance (frmBuyer). The Razor <see cref="Index"/> page hosts a
/// React grid widget that talks to the JSON endpoints below.
/// </summary>
[RequireCompany]
public sealed class BuyerController : Controller
{
    private readonly BuyerService _buyers;
    private readonly LookupService _lookups;

    public BuyerController(BuyerService buyers, LookupService lookups)
    {
        _buyers = buyers;
        _lookups = lookups;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult List() => Json(_buyers.List());

    [HttpGet]
    public IActionResult MainBuyers() => Json(_lookups.MainBuyers());

    public sealed class BuyerForm
    {
        public int BuyerId { get; set; }
        public string MainBuyerName { get; set; } = "";
        public string BuyerName { get; set; } = "";
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create([FromBody] BuyerForm form)
    {
        var r = _buyers.Create(form.MainBuyerName, form.BuyerName);
        return Json(new { ok = r.Ok, message = r.Message });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update([FromBody] BuyerForm form)
    {
        var r = _buyers.Update(form.BuyerId, form.MainBuyerName, form.BuyerName);
        return Json(new { ok = r.Ok, message = r.Message });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete([FromBody] BuyerForm form)
    {
        var r = _buyers.Delete(form.BuyerId);
        return Json(new { ok = r.Ok, message = r.Message });
    }
}
