using System.Security.Claims;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>
/// Login / logout / change-password (desktop: frmlogin, frmchangepassword) and
/// the company + year selector (desktop: frmsplash).
/// </summary>
public sealed class AccountController : Controller
{
    private readonly AuthService _auth;
    private readonly LookupService _lookups;
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public AccountController(AuthService auth, LookupService lookups, ISqlDataAccess db, CurrentUser user)
    {
        _auth = auth;
        _lookups = lookups;
        _db = db;
        _user = user;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = _auth.Validate(model.UserName, model.Password);
        if (user == null)
        {
            model.Error = "Wrong Username And Password";
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new("UserId", user.UserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToAction(nameof(SelectCompany));
    }

    [HttpGet]
    public IActionResult SelectCompany() =>
        View(new SelectCompanyViewModel
        {
            Companies = _lookups.OwnCompanies(),
            Year = DateTime.Now.Year.ToString()
        });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SelectCompany(SelectCompanyViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Companies = _lookups.OwnCompanies();
            return View(model);
        }

        var dt = _db.Query(
            "SELECT CompName, ISNULL(CompAdd, '') AS CompAdd FROM Company_Information WHERE CompID = @id",
            Params.New("id", model.CompId));

        if (dt.Rows.Count == 0)
        {
            ModelState.AddModelError(nameof(model.CompId), "Company not found");
            model.Companies = _lookups.OwnCompanies();
            return View(model);
        }

        _user.SetCompany(
            model.CompId,
            dt.Rows[0]["CompName"].ToString() ?? "",
            dt.Rows[0]["CompAdd"].ToString() ?? "",
            model.Year);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ChangePassword() =>
        View(new ChangePasswordViewModel { UserName = _user.UserName });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        if (!_auth.ChangePassword(model.UserName, model.OldPassword, model.NewPassword))
        {
            ModelState.AddModelError(string.Empty, "Old password is incorrect");
            return View(model);
        }

        TempData["Message"] = "Password changed successfully";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _user.ClearCompany();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
