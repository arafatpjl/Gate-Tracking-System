using GtrackWeb.Filters;
using GtrackWeb.Models;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Out-Company challan flow (frmOutCompanyChallanSender / gate / receiver).
/// The Sender screen reuses the same React master-detail widget as the in-company
/// sender, pointed at this controller's Lookups/Save endpoints.
/// </summary>
[RequireCompany]
public sealed class OutCompanyChallanController : Controller
{
    private readonly OutCompanyChallanService _svc;
    private readonly LookupService _lookups;

    public OutCompanyChallanController(OutCompanyChallanService svc, LookupService lookups)
    {
        _svc = svc;
        _lookups = lookups;
    }

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.Recent();
        return View();
    }

    public IActionResult Sender() => View();

    [HttpGet]
    public IActionResult Lookups() => Json(new
    {
        itemGroups = _lookups.ItemTypes(),
        items = _lookups.Items(),
        pfNos = _lookups.PfNos(),
        // Out-company challans go to an external company (Out_Company_Information).
        receiverCompanies = _lookups.OutCompanies(),
        drivers = _lookups.Drivers(),
        purposes = _lookups.Purposes(),
        employees = _lookups.Employees()
    });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save([FromBody] ChallanSenderInput input)
    {
        var r = _svc.CreateSender(input);
        return Json(new { ok = r.Ok, message = r.Message, gpNo = r.GpNo });
    }

    // ---- Edit / Delete -----------------------------------------------------

    [HttpGet]
    public IActionResult Edit(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var info = _svc.FindByGpNo(gpNo);
            ViewData["Info"] = info;
            if (info != null) ViewData["Lines"] = _svc.Lines(info.GpId);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int gpId, string gpNo, int[] sl, decimal[] qty, string[] unit,
                              string[] description, string[] remarks)
    {
        var lines = new List<ChallanLineView>();
        for (var i = 0; i < (sl?.Length ?? 0); i++)
        {
            lines.Add(new ChallanLineView
            {
                Sl = sl![i],
                GpQty = qty != null && i < qty.Length ? qty[i] : 0,
                Unit = unit != null && i < unit.Length ? unit[i] : "",
                Description = description != null && i < description.Length ? description[i] : "",
                Remarks = remarks != null && i < remarks.Length ? remarks[i] : "",
            });
        }
        var r = _svc.UpdateLines(gpId, lines);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteLine(int gpId, int sl, string gpNo)
    {
        var r = _svc.DeleteLine(gpId, sl);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int gpId)
    {
        var r = _svc.DeleteChallan(gpId);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // ---- Sender Gate -------------------------------------------------------

    [HttpGet]
    public IActionResult SenderGate(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo)) ViewData["Info"] = _svc.FindByGpNo(gpNo);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var r = _svc.SenderGate(gpNo, date, time, remark);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(SenderGate));
    }

    // ---- Receiver ----------------------------------------------------------

    [HttpGet]
    public IActionResult Receiver(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var info = _svc.FindByGpNo(gpNo);
            ViewData["Info"] = info;
            if (info != null) ViewData["Lines"] = _svc.Lines(info.GpId);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Receiver(int gpId, string date, string time,
                                  int[] sl, decimal[] recQty, string[] remarks)
    {
        var lines = new List<ReceiveLineInput>();
        for (var i = 0; i < (sl?.Length ?? 0); i++)
        {
            lines.Add(new ReceiveLineInput
            {
                Sl = sl![i],
                RecQty = recQty != null && i < recQty.Length ? recQty[i] : 0,
                Remarks = remarks != null && i < remarks.Length ? remarks[i] : "",
            });
        }

        var r = _svc.Receive(gpId, date, time, lines);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
