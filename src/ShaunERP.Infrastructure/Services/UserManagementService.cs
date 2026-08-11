using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public record UserListItem(
    int Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    bool IsActive,
    bool IsSystemAdmin,
    IReadOnlyList<string> Companies,
    IReadOnlyList<string> Roles);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    int? CompanyId,
    string? RoleName,
    bool IsActive = true);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    int? CompanyId,
    string? RoleName);

public record ResetPasswordRequest(string NewPassword);

public record RoleDto(int Id, string Name, string? Description, int? CompanyId);

public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken ct = default);
    Task<UserListItem?> GetUserAsync(int id, CancellationToken ct = default);
    Task<(bool Ok, string? Error, UserListItem? User)> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error, UserListItem? User)> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(int? companyId = null, CancellationToken ct = default);
}

public class UserManagementService : IUserManagementService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;

    public UserManagementService(ShaunErpDbContext db, ITenantContext tenant, IModuleRegistry modules)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
    }

    public async Task<IReadOnlyList<UserListItem>> ListUsersAsync(CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.UsersView);
        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyUsers).ThenInclude(cu => cu.Company)
            .Include(u => u.CompanyUsers).ThenInclude(cu => cu.UserRoles).ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!_tenant.IsSystemAdmin && _tenant.CompanyId is int companyId)
        {
            query = query.Where(u => u.CompanyUsers.Any(cu => cu.CompanyId == companyId));
        }

        var users = await query.OrderBy(u => u.Username).ToListAsync(ct);
        return users.Select(Map).ToList();
    }

    public async Task<UserListItem?> GetUserAsync(int id, CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.UsersView);
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyUsers).ThenInclude(cu => cu.Company)
            .Include(u => u.CompanyUsers).ThenInclude(cu => cu.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return null;
        }

        if (!_tenant.IsSystemAdmin && _tenant.CompanyId is int companyId
            && user.CompanyUsers.All(cu => cu.CompanyId != companyId))
        {
            return null;
        }

        return Map(user);
    }

    public async Task<(bool Ok, string? Error, UserListItem? User)> CreateUserAsync(
        CreateUserRequest request, CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.UsersCreate);
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return (false, "Username, email and password are required.", null);
        }

        if (await _db.Users.AnyAsync(u => u.Username == username || u.Email == email, ct))
        {
            return (false, "Username or email already exists.", null);
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = request.IsActive,
            IsSystemAdmin = false
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var companyId = request.CompanyId
            ?? _tenant.CompanyId
            ?? await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

        if (companyId is not null)
        {
            var companyUser = new CompanyUser { UserId = user.Id, CompanyId = companyId.Value };
            _db.CompanyUsers.Add(companyUser);
            await _db.SaveChangesAsync(ct);

            var roleName = string.IsNullOrWhiteSpace(request.RoleName) ? "User" : request.RoleName.Trim();
            await AssignRoleAsync(companyUser.Id, companyId.Value, roleName, ct);
        }

        var created = await GetUserAsync(user.Id, ct);
        return (true, null, created);
    }

    public async Task<(bool Ok, string? Error, UserListItem? User)> UpdateUserAsync(
        int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.UsersUpdate);
        var user = await _db.Users
            .Include(u => u.CompanyUsers).ThenInclude(cu => cu.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null)
        {
            return (false, "User not found.", null);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Id != id && u.Email == email, ct))
        {
            return (false, "Email already in use.", null);
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedDate = DateTime.UtcNow;

        if (request.CompanyId is not null)
        {
            var companyUser = user.CompanyUsers.FirstOrDefault(cu => cu.CompanyId == request.CompanyId)
                ?? user.CompanyUsers.FirstOrDefault();

            if (companyUser is null)
            {
                companyUser = new CompanyUser { UserId = user.Id, CompanyId = request.CompanyId.Value };
                _db.CompanyUsers.Add(companyUser);
                await _db.SaveChangesAsync(ct);
            }

            if (!string.IsNullOrWhiteSpace(request.RoleName))
            {
                _db.UserRoles.RemoveRange(companyUser.UserRoles);
                await _db.SaveChangesAsync(ct);
                await AssignRoleAsync(companyUser.Id, companyUser.CompanyId, request.RoleName.Trim(), ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        var updated = await GetUserAsync(id, ct);
        return (true, null, updated);
    }

    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(
        int id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.UsersUpdate);
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return (false, "Password must be at least 8 characters.");
        }

        var user = await _db.Users.FindAsync([id], ct);
        if (user is null)
        {
            return (false, "User not found.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(int? companyId = null, CancellationToken ct = default)
    {
        EnsureUsersAccess(Permissions.RolesView);
        var q = _db.Roles.AsNoTracking().AsQueryable();
        companyId ??= _tenant.CompanyId;
        if (companyId is not null)
        {
            q = q.Where(r => r.CompanyId == null || r.CompanyId == companyId);
        }

        return await q.OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description, r.CompanyId))
            .ToListAsync(ct);
    }

    private void EnsureUsersAccess(string permission)
    {
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Users) || !_modules.HasPermission(_tenant, permission))
        {
            // System admins and first-run: allow if system admin
            if (_tenant.IsSystemAdmin)
            {
                return;
            }

            throw new UnauthorizedAccessException("User management access denied.");
        }
    }

    private async Task AssignRoleAsync(int companyUserId, int companyId, string roleName, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(
            r => r.Name == roleName && (r.CompanyId == null || r.CompanyId == companyId), ct);

        if (role is null)
        {
            role = new Role
            {
                Name = roleName,
                Description = $"{roleName} role",
                CompanyId = companyId
            };
            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }

        _db.UserRoles.Add(new UserRole { CompanyUserId = companyUserId, RoleId = role.Id });
        await _db.SaveChangesAsync(ct);
    }

    private static UserListItem Map(User user)
    {
        var companies = user.CompanyUsers
            .Select(cu => cu.Company?.Name ?? $"Company #{cu.CompanyId}")
            .Distinct()
            .ToList();

        var roles = user.CompanyUsers
            .SelectMany(cu => cu.UserRoles)
            .Select(ur => ur.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .Distinct()
            .ToList();

        return new UserListItem(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.IsActive,
            user.IsSystemAdmin,
            companies,
            roles);
    }
}
