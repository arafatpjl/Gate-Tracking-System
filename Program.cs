using GtrackWeb.Configuration;
using GtrackWeb.Data;
using GtrackWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----------------------------------------------------------
builder.Services.Configure<GtrackSettings>(
    builder.Configuration.GetSection(GtrackSettings.SectionName));

// ---- MVC + Razor ------------------------------------------------------------
// Every controller requires authentication by default; opt out with
// [AllowAnonymous] (login page). Mirrors the desktop app where nothing is
// reachable until you have logged in.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});

// ---- Data access (parameterized replacement for conn.Mssqlconnect) ----------
builder.Services.AddSingleton<ISqlDataAccess>(_ =>
    new SqlDataAccess(builder.Configuration.GetConnectionString("Gtrack")
                      ?? throw new InvalidOperationException("Missing 'Gtrack' connection string.")));

// ---- Domain services (one per desktop form group) ---------------------------
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<LookupService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BuyerService>();
builder.Services.AddScoped<OutCompanyService>();
builder.Services.AddScoped<ChallanService>();
builder.Services.AddScoped<ReturnChallanService>();
builder.Services.AddScoped<OutCompanyChallanService>();
builder.Services.AddScoped<ItemGroupService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<PurposeService>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<MerchandiserService>();
builder.Services.AddScoped<PfNoService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<BookRangeService>();
builder.Services.AddScoped<MailService>();

// ---- Auth (cookie) + session -----------------------------------------------
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Allow React widgets to send the anti-forgery token via a request header.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ---- Pipeline ---------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
