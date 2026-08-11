using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Platform;

public record AccountSummary(int Id, string Name, string? Domain);

public interface IAccountAccessService
{
    Task<IReadOnlyList<AccountSummary>> ListMembershipsAsync(int userId, CancellationToken ct = default);
    Task<bool> BelongsToAsync(int userId, int companyId, CancellationToken ct = default);
}

public sealed class AccountAccessService : IAccountAccessService
{
    private readonly IDbContextFactory<ShaunErpDbContext> _dbFactory;

    public AccountAccessService(IDbContextFactory<ShaunErpDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<AccountSummary>> ListMembershipsAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CompanyUsers.AsNoTracking()
            .Where(cu => cu.UserId == userId)
            .OrderBy(cu => cu.Company!.Name)
            .Select(cu => new AccountSummary(cu.CompanyId, cu.Company!.Name, cu.Company.Domain))
            .ToListAsync(ct);
    }

    public async Task<bool> BelongsToAsync(int userId, int companyId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.CompanyUsers.AsNoTracking().AnyAsync(cu => cu.UserId == userId && cu.CompanyId == companyId, ct);
    }
}

public interface IAuditService
{
    Task WriteAsync(
        string action,
        int? companyId = null,
        int? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? beforeValues = null,
        string? afterValues = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly IDbContextFactory<ShaunErpDbContext> _dbFactory;

    public AuditService(IDbContextFactory<ShaunErpDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task WriteAsync(
        string action,
        int? companyId = null,
        int? userId = null,
        string? entityType = null,
        string? entityId = null,
        string? beforeValues = null,
        string? afterValues = null,
        string? correlationId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            CompanyId = companyId,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            CorrelationId = correlationId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}

public interface IUserPreferenceService
{
    Task<UserCompanyPreference> GetOrCreateAsync(int userId, int companyId, CancellationToken ct = default);
    Task RememberModuleAsync(int userId, int companyId, string moduleKey, CancellationToken ct = default);
    Task SetSidebarCollapsedAsync(int userId, int companyId, bool collapsed, CancellationToken ct = default);
    Task ToggleFavouriteAsync(int userId, int companyId, string moduleKey, CancellationToken ct = default);
}

public sealed class UserPreferenceService : IUserPreferenceService
{
    private readonly IDbContextFactory<ShaunErpDbContext> _dbFactory;

    public UserPreferenceService(IDbContextFactory<ShaunErpDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task<UserCompanyPreference> GetOrCreateAsync(int userId, int companyId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var pref = await db.UserCompanyPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct);
        if (pref is not null)
        {
            return pref;
        }

        pref = new UserCompanyPreference
        {
            UserId = userId,
            CompanyId = companyId
        };
        db.UserCompanyPreferences.Add(pref);
        await db.SaveChangesAsync(ct);
        return pref;
    }

    public async Task RememberModuleAsync(int userId, int companyId, string moduleKey, CancellationToken ct = default)
    {
        var key = ModuleKeys.Normalize(moduleKey);
        if (string.IsNullOrWhiteSpace(key) || key == ModuleKeys.Home)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var pref = await db.UserCompanyPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct)
            ?? new UserCompanyPreference { UserId = userId, CompanyId = companyId };

        if (pref.Id == 0)
        {
            db.UserCompanyPreferences.Add(pref);
        }

        pref.LastModuleKey = key;
        var recent = Split(pref.RecentModules);
        recent.RemoveAll(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, key);
        pref.RecentModules = string.Join(',', recent.Take(8));
        pref.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetSidebarCollapsedAsync(int userId, int companyId, bool collapsed, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var pref = await db.UserCompanyPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct)
            ?? new UserCompanyPreference { UserId = userId, CompanyId = companyId };
        if (pref.Id == 0)
        {
            db.UserCompanyPreferences.Add(pref);
        }

        pref.SidebarCollapsed = collapsed;
        pref.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ToggleFavouriteAsync(int userId, int companyId, string moduleKey, CancellationToken ct = default)
    {
        var key = ModuleKeys.Normalize(moduleKey);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var pref = await db.UserCompanyPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.CompanyId == companyId, ct)
            ?? new UserCompanyPreference { UserId = userId, CompanyId = companyId };
        if (pref.Id == 0)
        {
            db.UserCompanyPreferences.Add(pref);
        }

        var favs = Split(pref.FavouriteModules);
        if (favs.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase)))
        {
            favs.RemoveAll(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            favs.Add(key);
        }

        pref.FavouriteModules = string.Join(',', favs);
        pref.UpdatedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static List<string> Split(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
