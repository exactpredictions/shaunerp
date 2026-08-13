using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public interface IProductionUserBootstrapService
{
    Task<ProductionUserBootstrapResult> BootstrapUsersAsync(
        ProductionUserBootstrapRequest request,
        CancellationToken ct = default);
}

public sealed class ProductionUserBootstrapService : IProductionUserBootstrapService
{
    private readonly ShaunErpDbContext _db;

    public ProductionUserBootstrapService(ShaunErpDbContext db) => _db = db;

    public async Task<ProductionUserBootstrapResult> BootstrapUsersAsync(
        ProductionUserBootstrapRequest request,
        CancellationToken ct = default)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || string.IsNullOrWhiteSpace(request.AccountantPassword))
        {
            return new ProductionUserBootstrapResult
            {
                Success = false,
                Messages = ["Admin and accountant passwords are required (pass via parameters/env; never hardcode)."]
            };
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await EnsureAccountantRoleAsync(ct);

            var company = await ResolveCompanyAsync(request.CompanyId, ct);
            if (company is null)
            {
                return new ProductionUserBootstrapResult
                {
                    Success = false,
                    Messages = ["No company found. Run production initialization first."]
                };
            }

            var admin = await UpsertUserAsync(
                request.AdminEmail,
                request.AdminFirstName,
                request.AdminLastName,
                request.AdminPassword,
                isSystemAdmin: true,
                ct);
            messages.Add($"Admin user ready: {admin.Email}");

            var accountant = await UpsertUserAsync(
                request.AccountantEmail,
                request.AccountantFirstName,
                request.AccountantLastName,
                request.AccountantPassword,
                isSystemAdmin: false,
                ct);
            messages.Add($"Accountant user ready: {accountant.Email}");

            await EnsureMembershipAsync(admin.Id, company.Id, "Admin", ct);
            await EnsureMembershipAsync(accountant.Id, company.Id, "Accountant", ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new ProductionUserBootstrapResult
            {
                Success = true,
                AdminUserId = admin.Id,
                AccountantUserId = accountant.Id,
                Messages = messages
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ProductionUserBootstrapResult
            {
                Success = false,
                Messages = [$"Bootstrap failed: {ex.Message}"]
            };
        }
    }

    private async Task EnsureAccountantRoleAsync(CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Accountant" && r.CompanyId == null, ct);
        if (role is null)
        {
            role = new Role
            {
                Name = "Accountant",
                Description = "Business module access for accounting operations (no platform tenant admin)",
                CompanyId = null
            };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }

        // ManagerDefaults minus platform-style admin; explicitly exclude Platform*
        var desired = Permissions.ManagerDefaults
            .Where(p => !p.StartsWith("platform.", StringComparison.OrdinalIgnoreCase))
            .Concat([Permissions.SettingsCompanyConfigManage, Permissions.SettingsImportExport])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var permissionName in desired)
        {
            if (!await _db.Permissions.AnyAsync(p => p.Name == permissionName, ct))
            {
                _db.Permissions.Add(new Permission { Name = permissionName, Description = permissionName });
            }
        }

        await _db.SaveChangesAsync(ct);

        var permissions = await _db.Permissions.Where(p => desired.Contains(p.Name)).ToListAsync(ct);
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync(ct);
        foreach (var permission in permissions)
        {
            if (existing.Contains(permission.Id)) continue;
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<User> UpsertUserAsync(
        string email,
        string firstName,
        string lastName,
        string password,
        bool isSystemAdmin,
        CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var username = email;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email || u.Username == username, ct);
        if (user is null)
        {
            user = new User
            {
                Email = email,
                Username = username,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                IsSystemAdmin = isSystemAdmin,
                MustChangePassword = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            user.Email = email;
            user.Username = username;
            user.FirstName = firstName.Trim();
            user.LastName = lastName.Trim();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.IsActive = true;
            user.IsSystemAdmin = isSystemAdmin;
            user.MustChangePassword = true;
            await _db.SaveChangesAsync(ct);
        }

        return user;
    }

    private async Task EnsureMembershipAsync(int userId, int companyId, string roleName, CancellationToken ct)
    {
        var membership = await _db.CompanyUsers.FirstOrDefaultAsync(cu => cu.UserId == userId && cu.CompanyId == companyId, ct);
        if (membership is null)
        {
            membership = new CompanyUser { UserId = userId, CompanyId = companyId };
            _db.CompanyUsers.Add(membership);
            await _db.SaveChangesAsync(ct);
        }

        var role = await _db.Roles.FirstAsync(r => r.Name == roleName && r.CompanyId == null, ct);
        var hasRole = await _db.UserRoles.AnyAsync(ur => ur.CompanyUserId == membership.Id && ur.RoleId == role.Id, ct);
        if (!hasRole)
        {
            _db.UserRoles.Add(new UserRole { CompanyUserId = membership.Id, RoleId = role.Id });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<Company?> ResolveCompanyAsync(int? companyId, CancellationToken ct)
    {
        if (companyId is int id)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        return await _db.Companies.FirstOrDefaultAsync(c => c.Name == ProductionInitializationService.ProductionCompanyName, ct)
               ?? await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
    }
}
