using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Platform;

public interface IAuthorizationContextService
{
    Task<TenantContextSnapshot> BuildForUserAsync(int userId, int? preferredCompanyId = null, CancellationToken ct = default);
    Task<TenantContextSnapshot> BuildFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    TenantContextSnapshot BuildFromClaims(ClaimsPrincipal principal);
    IEnumerable<Claim> ToClaims(TenantContextSnapshot snapshot, User user);
}

public sealed class AuthorizationContextService : IAuthorizationContextService
{
    private readonly IDbContextFactory<ShaunErpDbContext> _dbFactory;

    public AuthorizationContextService(IDbContextFactory<ShaunErpDbContext> dbFactory) => _dbFactory = dbFactory;

    public TenantContextSnapshot BuildFromClaims(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new TenantContextSnapshot();
        }

        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        if (!int.TryParse(userIdValue, out var userId))
        {
            return new TenantContextSnapshot();
        }

        int? companyId = null;
        if (int.TryParse(principal.FindFirst("company_id")?.Value, out var parsedCompanyId))
        {
            companyId = parsedCompanyId;
        }

        var isAdmin = string.Equals(principal.FindFirst("is_system_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        var permissions = principal.FindAll("permission").Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entitlements = principal.FindAll("entitlement").Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (isAdmin)
        {
            foreach (var p in Permissions.All) permissions.Add(p);
            entitlements.Add(ModuleKeys.Users);
            entitlements.Add(ModuleKeys.Crm);
            entitlements.Add(ModuleKeys.Finance);
            entitlements.Add(ModuleKeys.Hrms);
            entitlements.Add(ModuleKeys.Projects);
            entitlements.Add(ModuleKeys.Platform);
        }

        return new TenantContextSnapshot
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyName = principal.FindFirst("company_name")?.Value,
            IsSystemAdmin = isAdmin,
            Permissions = permissions,
            Entitlements = entitlements
        };
    }

    public async Task<TenantContextSnapshot> BuildFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct = default)
    {
        var fromClaims = BuildFromClaims(principal);
        if (fromClaims.UserId is null)
        {
            return fromClaims;
        }

        // Prefer DB rebuild so entitlements stay current after admin changes.
        return await BuildForUserAsync(fromClaims.UserId.Value, fromClaims.CompanyId, ct);
    }

    public async Task<TenantContextSnapshot> BuildForUserAsync(int userId, int? preferredCompanyId = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive)
        {
            return new TenantContextSnapshot();
        }

        var memberships = await db.CompanyUsers.AsNoTracking()
            .Include(cu => cu.Company)
            .Include(cu => cu.UserRoles).ThenInclude(ur => ur.Role!).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Where(cu => cu.UserId == userId)
            .ToListAsync(ct);

        CompanyUser? membership = null;
        if (preferredCompanyId is not null)
        {
            membership = memberships.FirstOrDefault(m => m.CompanyId == preferredCompanyId);
        }

        membership ??= memberships.OrderBy(m => m.Id).FirstOrDefault();

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (user.IsSystemAdmin)
        {
            foreach (var p in Permissions.All)
            {
                permissions.Add(p);
            }

            entitlements.Add(ModuleKeys.Users);
            entitlements.Add(ModuleKeys.Crm);
            entitlements.Add(ModuleKeys.Finance);
            entitlements.Add(ModuleKeys.Hrms);
            entitlements.Add(ModuleKeys.Projects);
            entitlements.Add(ModuleKeys.Platform);
        }

        if (membership is not null)
        {
            var modules = await db.CompanyModules.AsNoTracking()
                .Where(m => m.CompanyId == membership.CompanyId && m.IsEnabled)
                .ToListAsync(ct);

            foreach (var module in modules)
            {
                var key = ModuleKeys.Normalize(string.IsNullOrWhiteSpace(module.ModuleKey) ? module.ModuleName : module.ModuleKey);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    entitlements.Add(key);
                }
            }

            foreach (var role in membership.UserRoles.Select(ur => ur.Role).Where(r => r is not null))
            {
                foreach (var rp in role!.RolePermissions)
                {
                    if (!string.IsNullOrWhiteSpace(rp.Permission?.Name))
                    {
                        permissions.Add(rp.Permission.Name);
                    }
                }

                if (role.RolePermissions.Count == 0)
                {
                    var defaults = role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase)
                        ? Permissions.AdminDefaults
                        : role.Name.Equals("Manager", StringComparison.OrdinalIgnoreCase)
                            ? Permissions.ManagerDefaults
                            : Permissions.UserDefaults;
                    foreach (var p in defaults)
                    {
                        permissions.Add(p);
                    }
                }
            }
        }

        return new TenantContextSnapshot
        {
            UserId = user.Id,
            CompanyId = membership?.CompanyId,
            CompanyName = membership?.Company?.Name,
            IsSystemAdmin = user.IsSystemAdmin,
            Permissions = permissions,
            Entitlements = entitlements
        };
    }

    public IEnumerable<Claim> ToClaims(TenantContextSnapshot snapshot, User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("full_name", $"{user.FirstName} {user.LastName}".Trim()),
            new("is_system_admin", user.IsSystemAdmin.ToString().ToLowerInvariant())
        };

        if (snapshot.CompanyId is not null)
        {
            claims.Add(new Claim("company_id", snapshot.CompanyId.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(snapshot.CompanyName))
            {
                claims.Add(new Claim("company_name", snapshot.CompanyName));
            }
        }

        foreach (var permission in snapshot.Permissions.OrderBy(p => p))
        {
            claims.Add(new Claim("permission", permission));
        }

        foreach (var entitlement in snapshot.Entitlements.OrderBy(e => e))
        {
            claims.Add(new Claim("entitlement", entitlement));
        }

        if (user.IsSystemAdmin || snapshot.Permissions.Contains(Permissions.PlatformAccess))
        {
            claims.Add(new Claim(ClaimTypes.Role, "PlatformAdmin"));
        }

        if (snapshot.Permissions.Contains(Permissions.UsersAccess))
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        return claims;
    }
}
