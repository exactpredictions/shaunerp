using ShaunERP.Core.Platform;

namespace ShaunERP.Infrastructure.Platform;

public interface IModuleRegistry
{
    IReadOnlyList<ModuleDefinition> GetAll();
    ModuleDefinition? GetByKey(string key);
    ModuleDefinition? ResolveFromPath(string? path);
    IReadOnlyList<ModuleDefinition> GetAccessible(ITenantContext tenant, bool includePlaceholders = false);
    IReadOnlyList<ModuleNavItem> GetNavigation(string moduleKey, ITenantContext tenant);
    bool CanAccessModule(ModuleDefinition module, ITenantContext tenant);
    bool HasPermission(ITenantContext tenant, string? permission);
    bool HasEntitlement(ITenantContext tenant, string? entitlementKey);
}

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly IReadOnlyList<ModuleDefinition> _modules = ModuleCatalog.All;

    public IReadOnlyList<ModuleDefinition> GetAll() => _modules;

    public ModuleDefinition? GetByKey(string key)
    {
        var normalized = ModuleKeys.Normalize(key);
        return _modules.FirstOrDefault(m => m.Key.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public ModuleDefinition? ResolveFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return GetByKey(ModuleKeys.Home);
        }

        var trimmed = path.TrimEnd('/');

        // Prefer longest BaseRoute match (/crm/leads → CRM).
        var byBase = _modules
            .Where(m => m.Key != ModuleKeys.Home)
            .OrderByDescending(m => m.BaseRoute.Length)
            .FirstOrDefault(m =>
                trimmed.Equals(m.BaseRoute, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(m.BaseRoute + "/", StringComparison.OrdinalIgnoreCase));
        if (byBase is not null)
        {
            return byBase;
        }

        // Routes that live outside BaseRoute (e.g. /roles under Users).
        return _modules
            .Where(m => m.Key != ModuleKeys.Home)
            .OrderBy(m => m.DisplayOrder)
            .FirstOrDefault(m => m.NavigationItems.Any(n =>
                trimmed.Equals(n.Route, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(n.Route) && n.Route != "/" &&
                 trimmed.StartsWith(n.Route.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))));
    }

    public bool HasPermission(ITenantContext tenant, string? permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return true;
        }

        if (tenant.IsSystemAdmin)
        {
            return true;
        }

        return tenant.Permissions.Contains(permission);
    }

    public bool HasEntitlement(ITenantContext tenant, string? entitlementKey)
    {
        if (string.IsNullOrWhiteSpace(entitlementKey))
        {
            return true;
        }

        if (tenant.IsSystemAdmin)
        {
            return true;
        }

        var key = ModuleKeys.Normalize(entitlementKey);
        return tenant.Entitlements.Contains(key);
    }

    public bool CanAccessModule(ModuleDefinition module, ITenantContext tenant)
    {
        if (!module.IsEnabled && !module.IsPlaceholder)
        {
            return false;
        }

        if (module.Key == ModuleKeys.Home)
        {
            return tenant.IsAuthenticated;
        }

        if (module.Key == ModuleKeys.Platform)
        {
            return tenant.IsSystemAdmin || HasPermission(tenant, Permissions.PlatformAccess);
        }

        if (module.IsPlaceholder)
        {
            return false;
        }

        if (!HasEntitlement(tenant, module.RequiredEntitlement))
        {
            return false;
        }

        return HasPermission(tenant, module.RequiredPermission);
    }

    public IReadOnlyList<ModuleDefinition> GetAccessible(ITenantContext tenant, bool includePlaceholders = false)
    {
        return _modules
            .Where(m => includePlaceholders ? (CanAccessModule(m, tenant) || (m.IsPlaceholder && HasEntitlement(tenant, m.RequiredEntitlement))) : CanAccessModule(m, tenant))
            .Where(m => m.Key != ModuleKeys.Home)
            .OrderBy(m => m.DisplayOrder)
            .ToList();
    }

    public IReadOnlyList<ModuleNavItem> GetNavigation(string moduleKey, ITenantContext tenant)
    {
        var module = GetByKey(moduleKey);
        if (module is null || !CanAccessModule(module, tenant))
        {
            return Array.Empty<ModuleNavItem>();
        }

        var items = module.NavigationItems
            .Where(n => HasPermission(tenant, n.RequiredPermission))
            .OrderBy(n => n.Order)
            .ToList();

        // Home sidebar should surface modules, not only the dashboard link.
        if (module.Key == ModuleKeys.Home)
        {
            var order = 10;
            foreach (var accessible in GetAccessible(tenant))
            {
                items.Add(new ModuleNavItem
                {
                    Key = $"home.module.{accessible.Key}",
                    Title = accessible.Name,
                    Route = accessible.DashboardRoute,
                    Icon = accessible.Icon,
                    Group = "Modules",
                    Order = order++
                });
            }
        }

        return items;
    }
}
