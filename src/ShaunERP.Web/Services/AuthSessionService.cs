using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Web.Services;

public record AuthSessionResult(bool Success, ClaimsPrincipal? Principal, string? Error, int? CompanyId);

public class AuthSessionService
{
    private readonly ShaunErpDbContext _db;
    private readonly IAuthorizationContextService _authz;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;
    private readonly IAuthService _auth;

    public AuthSessionService(
        ShaunErpDbContext db,
        IAuthorizationContextService authz,
        IAuditService audit,
        ITenantContext tenant,
        IAuthService auth)
    {
        _db = db;
        _authz = authz;
        _audit = audit;
        _tenant = tenant;
        _auth = auth;
    }

    public async Task<AuthSessionResult> ValidateAsync(string usernameOrEmail, string password, int? companyId = null)
    {
        var login = await _auth.LoginAsync(new LoginRequest(usernameOrEmail, password, companyId));
        if (!login.Success || login.User is null)
        {
            return new AuthSessionResult(false, null, login.Error ?? "Invalid credentials.", null);
        }

        var user = await _db.Users.FirstAsync(u => u.Id == login.User.Id);
        var snapshot = await _authz.BuildForUserAsync(user.Id, companyId ?? login.CompanyId);
        _tenant.Set(snapshot);

        var claims = _authz.ToClaims(snapshot, user).ToList();
        if (user.MustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new AuthSessionResult(true, new ClaimsPrincipal(identity), null, snapshot.CompanyId);
    }

    public async Task<AuthSessionResult> SwitchAccountAsync(ClaimsPrincipal current, int companyId)
    {
        var userIdValue = current.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return new AuthSessionResult(false, null, "Not authenticated.", null);
        }

        var belongs = await _db.CompanyUsers.AnyAsync(cu => cu.UserId == userId && cu.CompanyId == companyId);
        if (!belongs)
        {
            return new AuthSessionResult(false, null, "You do not belong to that account.", null);
        }

        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        var snapshot = await _authz.BuildForUserAsync(userId, companyId);
        _tenant.Set(snapshot);

        var claims = _authz.ToClaims(snapshot, user).ToList();
        if (user.MustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await _audit.WriteAsync(
            "auth.account_switch",
            companyId: companyId,
            userId: userId,
            entityType: "Company",
            entityId: companyId.ToString(),
            afterValues: snapshot.CompanyName);

        return new AuthSessionResult(true, new ClaimsPrincipal(identity), null, companyId);
    }

    public async Task<AuthSessionResult> RefreshPrincipalAsync(ClaimsPrincipal current)
    {
        var userIdValue = current.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdValue, out var userId))
        {
            return new AuthSessionResult(false, null, "Not authenticated.", null);
        }

        int? companyId = int.TryParse(current.FindFirstValue("company_id"), out var cid) ? cid : null;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || !user.IsActive)
        {
            return new AuthSessionResult(false, null, "User not found.", null);
        }

        var snapshot = await _authz.BuildForUserAsync(userId, companyId);
        _tenant.Set(snapshot);
        var claims = _authz.ToClaims(snapshot, user).ToList();
        if (user.MustChangePassword)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new AuthSessionResult(true, new ClaimsPrincipal(identity), null, snapshot.CompanyId);
    }
}
