using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>User administration (frm_user_information) over Sys_User_Name_UP.</summary>
[RequireCompany]
public sealed class UserController : Controller
{
    private readonly UserAdminService _svc;

    public UserController(UserAdminService svc) => _svc = svc;

    public IActionResult Index()
    {
        ViewData["Rows"] = _svc.List();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Create(string userName, string password) => Done(_svc.Create(userName, password));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ResetPassword(int userId, string password) => Done(_svc.ResetPassword(userId, password));

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetActive(int userId, bool active) => Done(_svc.SetActive(userId, active));

    private IActionResult Done(Models.OpResult r)
    {
        TempData[r.Ok ? "Message" : "Error"] = r.Message;
        return RedirectToAction(nameof(Index));
    }
}
