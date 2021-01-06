using GtrackWeb.Filters;
using GtrackWeb.Models;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Challan Sender entry + list (frmChallanSender). The Sender screen hosts a
/// React master-detail widget; saving posts the whole challan as JSON to
/// <see cref="Save"/>, which persists it in one transaction.
/// </summary>
[RequireCompany]
public sealed class ChallanController : Controller
{
    private readonly ChallanService _challans;
    private readonly LookupService _lookups;

    public ChallanController(ChallanService challans, LookupService lookups)
    {
        _challans = challans;
        _lookups = lookups;
    }

    public IActionResult Index()
    {
        ViewData["Rows"] = _challans.Recent();
        return View();
    }

    public IActionResult Sender() => View();

    /// <summary>Reference data for the sender screen's dropdowns/autocompletes.</summary>
    [HttpGet]
    public IActionResult Lookups() => Json(new
    {
        itemGroups = _lookups.ItemTypes(),
        items = _lookups.Items(),
        pfNos = _lookups.PfNos(),
        // In-company challans go to another company in the group (Company_Information).
        receiverCompanies = _lookups.OwnCompanies(),
        drivers = _lookups.Drivers(),
        purposes = _lookups.Purposes(),
        employees = _lookups.Employees()
    });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save([FromBody] ChallanSenderInput input)
    {
        var r = _challans.CreateSender(input);
        return Json(new { ok = r.Ok, message = r.Message, gpNo = r.GpNo });
    }

    // ---- Edit / Delete -----------------------------------------------------

    [HttpGet]
    public IActionResult Edit(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var info = _challans.FindByGpNo(gpNo);
            ViewData["Info"] = info;
            if (info != null) ViewData["Lines"] = _challans.Lines(info.GpId);
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

        var r = _challans.UpdateLines(gpId, lines);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteLine(int gpId, int sl, string gpNo)
    {
        var r = _challans.DeleteLine(gpId, sl);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int gpId)
    {
        var r = _challans.DeleteChallan(gpId);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }

    // ---- Lifecycle: Sender Gate -------------------------------------------

    [HttpGet]
    public IActionResult SenderGate(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo)) ViewData["Info"] = _challans.FindByGpNo(gpNo);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var r = _challans.SenderGate(gpNo, date, time, remark);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(SenderGate));
    }

    // ---- Lifecycle: Receiver Gate -----------------------------------------

    [HttpGet]
    public IActionResult ReceiverGate(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo)) ViewData["Info"] = _challans.FindByGpNo(gpNo);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReceiverGate(string gpNo, string date, string time, string remark)
    {
        var r = _challans.ReceiverGate(gpNo, date, time, remark);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(ReceiverGate));
    }

    // ---- Lifecycle: Receiver (confirm receipt) ----------------------------

    [HttpGet]
    public IActionResult Receiver(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var info = _challans.FindByGpNo(gpNo);
            ViewData["Info"] = info;
            if (info != null) ViewData["Lines"] = _challans.Lines(info.GpId);
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

        var r = _challans.Receive(gpId, date, time, lines);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
