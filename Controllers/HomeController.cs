using GtrackWeb.Filters;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace GtrackWeb.Controllers;

/// <summary>Landing / dashboard — the web equivalent of <c>form.frmmenu</c>.</summary>
public sealed class HomeController : Controller
{
    [RequireCompany]
    public IActionResult Index()
    {
        ViewData["Menu"] = MenuProvider.Build();
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
