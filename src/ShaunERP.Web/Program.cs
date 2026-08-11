using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ShaunERP.Infrastructure;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Web.Components;
using ShaunERP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("ShaunERP.Web");

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.AddScoped<ShellStateService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var tenant = context.RequestServices.GetRequiredService<ITenantContext>();
        var authz = context.RequestServices.GetRequiredService<IAuthorizationContextService>();
        if (tenant.UserId is null)
        {
            var snapshot = await authz.BuildFromPrincipalAsync(context.User);
            tenant.Set(snapshot);
        }
    }

    await next();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/login", async (HttpContext http, AuthSessionService auth) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var result = await auth.ValidateAsync(username, password);
    if (!result.Success || result.Principal is null)
    {
        return Results.Redirect("/login?error=1");
    }

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        result.Principal,
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });

    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/account/switch", async (HttpContext http, AuthSessionService auth) =>
{
    if (http.User.Identity?.IsAuthenticated != true)
    {
        return Results.Redirect("/login");
    }

    var form = await http.Request.ReadFormAsync();
    if (!int.TryParse(form["companyId"], out var companyId))
    {
        return Results.Redirect("/?error=account");
    }

    var result = await auth.SwitchAccountAsync(http.User, companyId);
    if (!result.Success || result.Principal is null)
    {
        return Results.Redirect("/?error=account");
    }

    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        result.Principal,
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });

    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/account/logout", async (HttpContext http, IAuditService audit) =>
{
    var userIdValue = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    int? userId = int.TryParse(userIdValue, out var id) ? id : null;
    int? companyId = int.TryParse(http.User.FindFirstValue("company_id"), out var cid) ? cid : null;
    await audit.WriteAsync("auth.logout", companyId: companyId, userId: userId, entityType: "User", entityId: userIdValue);
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.Run();

public partial class Program;
