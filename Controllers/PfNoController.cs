using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>PF (file) number maintenance (frmNewpfnoauto).</summary>
[RequireCompany]
public sealed class PfNoController : Controller
{
    private readonly PfNoService _svc;
    private readonly LookupService _lookups;

    public PfNoController(PfNoService svc, LookupService lookups)
    {
        _svc = svc;
        _lookups = lookups;
    }

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        ViewData["Buyers"] = _lookups.AllBuyers();
        ViewData["Merchandisers"] = _lookups.Merchandisers();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string date, string qty, string buyerName, string reference,
                                string merchandiser, string description) =>
        Done(_svc.Create(date, qty, buyerName, reference, merchandiser, description));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(string pfNo, string qty, string buyerName, string reference,
                                string merchandiser, string description) =>
        Done(_svc.Update(pfNo, qty, buyerName, reference, merchandiser, description));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
