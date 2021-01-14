using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Employee mail-id maintenance (frmNewMail).</summary>
[RequireCompany]
public sealed class MailController : Controller
{
    private readonly MailService _svc;

    public MailController(MailService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string empCode, string mailId) => Done(_svc.Create(empCode, mailId));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string empCode, string mailId) => Done(_svc.Delete(empCode, mailId));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
