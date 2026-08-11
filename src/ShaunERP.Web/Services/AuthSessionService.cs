using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Web.Services;

public record AuthSessionResult(bool Success, ClaimsPrincipal? Principal, string? Error, int? CompanyId);

public class AuthSessionService
{
    private readonly ShaunErpDbContext _db;
    private readonly IAuthorizationContextService _authz;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public AuthSessionService(
        ShaunErpDbContext db,
        IAuthorizationContextService authz,
        IAuditService audit,
        ITenantContext tenant)
    {
        _db = db;
        _authz = authz;
        _audit = audit;
        _tenant = tenant;
    }

    public async Task<AuthSessionResult> ValidateAsync(string usernameOrEmail, string password, int? companyId = null)
    {
        var key = usernameOrEmail.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == key || u.Email.ToLower() == key);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return new AuthSessionResult(false, null, "Invalid credentials.", null);
        }

        var snapshot = await _authz.BuildForUserAsync(user.Id, companyId);
        _tenant.Set(snapshot);

        var claims = _authz.ToClaims(snapshot, user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await _audit.WriteAsync(
            "auth.login",
            companyId: snapshot.CompanyId,
            userId: user.Id,
            entityType: "User",
            entityId: user.Id.ToString());

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

        var claims = _authz.ToClaims(snapshot, user);
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
}
