using GtrackWeb.Filters;
using GtrackWeb.Models;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Return-challan flow: Sender → Sender Gate → Receiver Gate → Receiver over the
/// Return_* tables (frmReturnChallanSender / gates / receiver).
/// </summary>
[RequireCompany]
public sealed class ReturnChallanController : Controller
{
    private readonly ReturnChallanService _svc;

    public ReturnChallanController(ReturnChallanService svc) => _svc = svc;

    // ---- Return Sender -----------------------------------------------------

    [HttpGet]
    public IActionResult Sender(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var info = _svc.FindReturnable(gpNo);
            ViewData["Info"] = info;
            ViewData["NotReturnable"] = info == null;
            if (info != null) ViewData["Lines"] = _svc.OriginalLines(info.GpId);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Sender(int gpId, string gpDate, string returnDate,
                               int[] sl, decimal[] recQty, string[] remarks)
    {
        var r = _svc.CreateReturn(gpId, gpDate, returnDate, BuildLines(sl, recQty, remarks));
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Sender));
    }

    // ---- Return Edit / Delete (latest installment) -------------------------

    [HttpGet]
    public IActionResult Edit(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var st = _svc.FindLatestReturn(gpNo);
            ViewData["Info"] = st;
            if (st != null) ViewData["Lines"] = _svc.ReturnLines(st.GpId, st.RowSl);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int gpId, int rowSl, string gpNo, int[] sl, decimal[] qty,
                              string[] unit, string[] description, string[] remarks)
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
        var r = _svc.UpdateLines(gpId, rowSl, lines);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult DeleteLine(int gpId, int rowSl, int sl, string gpNo)
    {
        var r = _svc.DeleteLine(gpId, rowSl, sl);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Edit), new { gpNo });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(int gpId, int rowSl)
    {
        var r = _svc.DeleteReturn(gpId, rowSl);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Sender));
    }

    // ---- Return Sender Gate ------------------------------------------------

    [HttpGet]
    public IActionResult SenderGate(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo)) ViewData["Info"] = _svc.FindLatestReturn(gpNo);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var r = _svc.SenderGate(gpNo, date, time, remark);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(SenderGate));
    }

    // ---- Return Receiver Gate ----------------------------------------------

    [HttpGet]
    public IActionResult ReceiverGate(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo)) ViewData["Info"] = _svc.FindLatestReturn(gpNo);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ReceiverGate(string gpNo, string date, string time, string remark)
    {
        var r = _svc.ReceiverGate(gpNo, date, time, remark);
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(ReceiverGate));
    }

    // ---- Return Receiver ---------------------------------------------------

    [HttpGet]
    public IActionResult Receiver(string? gpNo)
    {
        if (!string.IsNullOrWhiteSpace(gpNo))
        {
            var st = _svc.FindLatestReturn(gpNo);
            ViewData["Info"] = st;
            if (st != null) ViewData["Lines"] = _svc.ReturnLines(st.GpId, st.RowSl);
        }
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Receiver(int gpId, int rowSl, string date, string time,
                                 int[] sl, decimal[] recQty, string[] remarks)
    {
        var r = _svc.Receive(gpId, rowSl, date, time, BuildLines(sl, recQty, remarks));
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Receiver), new { gpNo = Request.Form["gpNo"].ToString() });
    }

    private static List<ReceiveLineInput> BuildLines(int[] sl, decimal[] recQty, string[] remarks)
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
        return lines;
    }
}
