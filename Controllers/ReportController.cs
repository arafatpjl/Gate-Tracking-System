using System.Data;
using GtrackWeb.Filters;
using GtrackWeb.Helpers;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Reports (frmRpt*). Each action shows a filter form (date range + mode) and,
/// once run, a results table with a CSV export link.
/// </summary>
[RequireCompany]
public sealed class ReportController : Controller
{
    private readonly ReportService _reports;
    private readonly LookupService _lookups;

    public ReportController(ReportService reports, LookupService lookups)
    {
        _reports = reports;
        _lookups = lookups;
    }

    private static string DefaultFrom() => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd");
    private static string DefaultTo() => DateTime.Today.ToString("yyyy-MM-dd");

    public IActionResult Index() => View();

    // ---- Challan Auditing --------------------------------------------------

    [HttpGet]
    public IActionResult ChallanAuditing(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.ChallanModes;
        ViewData["Title"] = "Challan Auditing";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.ChallanAuditing(m, f, t), "challan-auditing");
    }

    // ---- Gate Auditing -----------------------------------------------------

    [HttpGet]
    public IActionResult GateAuditing(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.GateModes;
        ViewData["Title"] = "Gate Challan Auditing";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.GateAuditing(m, f, t), "gate-auditing");
    }

    // ---- Return Challan Auditing -------------------------------------------

    [HttpGet]
    public IActionResult ReturnAuditing(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.ReturnModes;
        ViewData["Title"] = "Return Challan Auditing";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.ReturnAuditing(m, f, t), "return-auditing");
    }

    // ---- Out-Company Challan Auditing --------------------------------------

    [HttpGet]
    public IActionResult OutCompanyAuditing(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.OutCompanyModes;
        ViewData["Title"] = "Out-Company Challan Auditing";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.OutCompanyAuditing(m, f, t), "outcompany-auditing");
    }

    // ---- Short / Excess Summary --------------------------------------------

    [HttpGet]
    public IActionResult ShortExcess(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.ShortExcessModes;
        ViewData["Title"] = "Short / Excess Summary";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.ShortExcess(m, f, t), "short-excess");
    }

    // ---- Returnable Challan Qty --------------------------------------------

    [HttpGet]
    public IActionResult ReturnableQty(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.ReturnableQtyModes;
        ViewData["Title"] = "Returnable Challan Qty";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.ReturnableQty(m, f, t), "returnable-qty");
    }

    // ---- Shipment Challan Auditing -----------------------------------------

    [HttpGet]
    public IActionResult ShipmentAuditing(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.ShipmentModes;
        ViewData["Title"] = "Shipment Challan Auditing";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.ShipmentAuditing(m, f, t), "shipment-auditing");
    }

    // ---- Out-Company User-wise Send ----------------------------------------

    [HttpGet]
    public IActionResult OutCompanyUserSend(int userId, string? from, string? to, string? export)
    {
        ViewData["Modes"] = new List<ReportService.ReportMode> { new("All", "All") };
        ViewData["Title"] = "Out-Company User-wise Send";
        return RunUserReport("All", userId, from, to, export,
            (u, f, t) => _reports.OutCompanyUserSend(u, f, t), "outcompany-user-send");
    }

    // ---- Company-wise All Challan ------------------------------------------

    [HttpGet]
    public IActionResult CompanyWise(string? mode, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.CompanyWiseModes;
        ViewData["Title"] = "Company Wise Challan";
        return RunOrForm(mode, from, to, export,
            (m, f, t) => _reports.CompanyWise(m, f, t), "company-wise");
    }

    private IActionResult RunOrForm(string? mode, string? from, string? to, string? export,
        Func<string, string, string, DataTable> run, string fileName)
    {
        var modes = (IReadOnlyList<ReportService.ReportMode>)ViewData["Modes"]!;
        mode ??= modes[0].Key;
        from ??= DefaultFrom();
        to ??= DefaultTo();
        ViewData["Mode"] = mode;
        ViewData["From"] = from;
        ViewData["To"] = to;

        // Only run once a date range is present (it always is here, defaults applied).
        var table = run(mode, from, to);

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
            return File(CsvExport.ToCsv(table), "text/csv", $"{fileName}-{from}_to_{to}.csv");

        ViewData["Table"] = table;
        return View("Auditing");
    }

    // ---- Department-wise Challan Auditing ----------------------------------

    [HttpGet]
    public IActionResult DeptAuditing(string? mode, string? dept, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.DeptModes;
        ViewData["Departments"] = _lookups.Departments();
        mode ??= ReportService.DeptModes[0].Key;
        from ??= DefaultFrom();
        to ??= DefaultTo();
        ViewData["Mode"] = mode;
        ViewData["Dept"] = dept ?? "";
        ViewData["From"] = from;
        ViewData["To"] = to;

        var table = _reports.DeptAuditing(mode, dept ?? "", from, to);

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
            return File(CsvExport.ToCsv(table), "text/csv", $"dept-auditing-{from}_to_{to}.csv");

        ViewData["Table"] = table;
        return View();
    }

    // ---- User-wise Challan -------------------------------------------------

    [HttpGet]
    public IActionResult UserWise(string? mode, int userId, string? from, string? to, string? export)
    {
        ViewData["Modes"] = ReportService.UserWiseModes;
        ViewData["Title"] = "User Wise Challan";
        mode ??= ReportService.UserWiseModes[0].Key;
        return RunUserReport(mode, userId, from, to, export,
            (u, f, t) => _reports.UserWise(mode, u, f, t), "user-wise");
    }

    [HttpGet]
    public IActionResult UserGpList(int userId, string? from, string? to, string? export)
    {
        ViewData["Modes"] = new List<ReportService.ReportMode> { new("All", "All") };
        ViewData["Title"] = "User GP List";
        return RunUserReport("All", userId, from, to, export,
            (u, f, t) => _reports.UserGpList(u, f, t), "user-gp-list");
    }

    private IActionResult RunUserReport(string mode, int userId, string? from, string? to, string? export,
        Func<int, string, string, DataTable> run, string fileName)
    {
        from ??= DefaultFrom();
        to ??= DefaultTo();
        ViewData["Users"] = _lookups.Users();
        ViewData["Mode"] = mode;
        ViewData["UserId"] = userId;
        ViewData["From"] = from;
        ViewData["To"] = to;

        var table = run(userId, from, to);
        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
            return File(CsvExport.ToCsv(table), "text/csv", $"{fileName}-{from}_to_{to}.csv");

        ViewData["Table"] = table;
        return View("UserAuditing");
    }

    // ---- Buyer-wise Challan ------------------------------------------------

    [HttpGet]
    public IActionResult BuyerWise(string? rcvr, string? buyer, string? from, string? to, string? export)
    {
        ViewData["Title"] = "Buyer Wise Challan";
        ViewData["OutCompanies"] = _lookups.OutCompanies();
        ViewData["MainBuyers"] = _lookups.MainBuyers();

        from ??= DefaultFrom();
        to ??= DefaultTo();
        ViewData["Rcvr"] = rcvr ?? "";
        ViewData["Buyer"] = buyer ?? "";
        ViewData["From"] = from;
        ViewData["To"] = to;

        var table = _reports.BuyerWise(rcvr ?? "", buyer ?? "", from, to);

        if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
            return File(CsvExport.ToCsv(table), "text/csv", $"buyer-wise-{from}_to_{to}.csv");

        ViewData["Table"] = table;
        return View();
    }
}
