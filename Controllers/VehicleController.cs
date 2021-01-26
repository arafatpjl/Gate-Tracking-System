using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Vehicle maintenance (frmVehicel).</summary>
[RequireCompany]
public sealed class VehicleController : Controller
{
    private readonly VehicleService _svc;

    public VehicleController(VehicleService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string vehicleNo) => Done(_svc.Create(vehicleNo));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Update(string oldVehicleNo, string vehicleNo) =>
        Done(_svc.Update(oldVehicleNo, vehicleNo));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Delete(string vehicleNo) => Done(_svc.Delete(vehicleNo));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
