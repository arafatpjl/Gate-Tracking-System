using System.Security.Claims;

namespace GtrackWeb.Services;

/// <summary>
/// Session/claims-backed replacement for the desktop global state
/// (<c>Extra.call.UserInfo</c>, <c>CompanyInfo</c>, <c>SysConfigInfo</c>,
/// <c>Extra.call.Year</c>). Populated at login and after company selection.
/// </summary>
public sealed class CurrentUser
{
    private const string CompIdKey = "gtrack.compId";
    private const string CompNameKey = "gtrack.compName";
    private const string CompAddressKey = "gtrack.compAddress";
    private const string YearKey = "gtrack.year";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext Http =>
        _accessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");

    public bool IsAuthenticated => Http.User.Identity?.IsAuthenticated ?? false;

    public int UserId => int.TryParse(Http.User.FindFirstValue("UserId"), out var v) ? v : 0;

    public string UserName => Http.User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    /// <summary>Web analogue of the desktop PCName (machine name); we use the login name.</summary>
    public string PcName => $"WEB:{UserName}";

    public bool CompanySelected => Http.Session.GetInt32(CompIdKey).HasValue;

    public int CompId => Http.Session.GetInt32(CompIdKey) ?? 0;

    public string CompName => Http.Session.GetString(CompNameKey) ?? string.Empty;

    public string CompAddress => Http.Session.GetString(CompAddressKey) ?? string.Empty;

    public string Year => Http.Session.GetString(YearKey) ?? DateTime.Now.Year.ToString();

    public void SetCompany(int compId, string compName, string compAddress, string year)
    {
        Http.Session.SetInt32(CompIdKey, compId);
        Http.Session.SetString(CompNameKey, compName);
        Http.Session.SetString(CompAddressKey, compAddress);
        Http.Session.SetString(YearKey, year);
    }

    public void ClearCompany()
    {
        Http.Session.Remove(CompIdKey);
        Http.Session.Remove(CompNameKey);
        Http.Session.Remove(CompAddressKey);
        Http.Session.Remove(YearKey);
    }
}
