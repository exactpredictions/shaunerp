using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Tests;

public class ModuleRegistryTests
{
    private readonly ModuleRegistry _registry = new();

    [Fact]
    public void ResolveFromPath_FindsCrmModule()
    {
        var module = _registry.ResolveFromPath("/crm/leads/12");
        Assert.NotNull(module);
        Assert.Equal(ModuleKeys.Crm, module!.Key);
    }

    [Fact]
    public void ResolveFromPath_FindsUsersModuleForRolesRoute()
    {
        var module = _registry.ResolveFromPath("/roles");
        Assert.NotNull(module);
        Assert.Equal(ModuleKeys.Users, module!.Key);
    }

    [Fact]
    public void GetNavigation_HomeIncludesAccessibleModules()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Demo",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm, ModuleKeys.Users },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Permissions.CrmAccess,
                Permissions.CrmDashboardView,
                Permissions.UsersAccess,
                Permissions.UsersView
            }
        });

        var nav = _registry.GetNavigation(ModuleKeys.Home, tenant);
        Assert.Contains(nav, n => n.Route == "/");
        Assert.Contains(nav, n => n.Route == "/crm" && n.Group == "Modules");
        Assert.Contains(nav, n => n.Route == "/users" && n.Group == "Modules");
    }

    [Fact]
    public void GetAccessible_IncludesFinanceWhenEntitled()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Demo",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm, ModuleKeys.Finance },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Permissions.CrmAccess,
                Permissions.CrmDashboardView,
                Permissions.FinanceAccess,
                Permissions.FinanceDashboardView,
                Permissions.FinanceInvoicesView
            }
        });

        var modules = _registry.GetAccessible(tenant);
        Assert.Contains(modules, m => m.Key == ModuleKeys.Finance);
        var finance = _registry.GetByKey(ModuleKeys.Finance)!;
        Assert.False(finance.IsPlaceholder);
        Assert.True(finance.IsEnabled);
        Assert.Contains(ModuleKeys.Crm, finance.Dependencies);

        var nav = _registry.GetNavigation(ModuleKeys.Finance, tenant);
        Assert.Contains(nav, n => n.Route == "/finance");
        Assert.Contains(nav, n => n.Route == "/finance/invoices");
        Assert.DoesNotContain(nav, n => n.Route == "/finance/settings");
    }

    [Fact]
    public void GetAccessible_HidesUnauthorizedModules()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Demo",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Permissions.CrmAccess,
                Permissions.CrmDashboardView,
                Permissions.CrmLeadsView
            }
        });

        var modules = _registry.GetAccessible(tenant);
        Assert.Contains(modules, m => m.Key == ModuleKeys.Crm);
        Assert.DoesNotContain(modules, m => m.Key == ModuleKeys.Users);
        Assert.DoesNotContain(modules, m => m.Key == ModuleKeys.Finance);
    }

    [Fact]
    public void GetNavigation_FiltersByPermission()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Permissions.CrmAccess,
                Permissions.CrmDashboardView,
                Permissions.CrmLeadsView
            }
        });

        var nav = _registry.GetNavigation(ModuleKeys.Crm, tenant);
        Assert.Contains(nav, n => n.Route == "/crm");
        Assert.Contains(nav, n => n.Route == "/crm/leads");
        Assert.DoesNotContain(nav, n => n.Route == "/crm/customers");
    }

    [Fact]
    public void EntitlementWithoutPermission_DeniesModule()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 2,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        });

        var crm = _registry.GetByKey(ModuleKeys.Crm)!;
        Assert.False(_registry.CanAccessModule(crm, tenant));
    }

    [Fact]
    public void PermissionWithoutEntitlement_DeniesModule()
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 3,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Permissions.CrmAccess,
                Permissions.CrmDashboardView
            }
        });

        var crm = _registry.GetByKey(ModuleKeys.Crm)!;
        Assert.False(_registry.CanAccessModule(crm, tenant));
    }

    [Theory]
    [InlineData("CRM", "crm")]
    [InlineData("Users", "users")]
    [InlineData("user-management", "users")]
    [InlineData("sales-orders", "orders")]
    [InlineData("Sales Orders", "orders")]
    [InlineData("accounting", "accounting")]
    [InlineData("books", "accounting")]
    public void ModuleKeys_Normalize(string input, string expected)
    {
        Assert.Equal(expected, ModuleKeys.Normalize(input));
    }

    [Fact]
    public void OrdersModule_IsEnabledInCatalog()
    {
        var module = _registry.GetByKey(ModuleKeys.Orders);
        Assert.NotNull(module);
        Assert.False(module!.IsPlaceholder);
        Assert.True(module.IsEnabled);
        Assert.Contains(module.NavigationItems, n => n.Route == "/orders/list");
    }

    [Fact]
    public void AccountingModule_IsEnabledInCatalog()
    {
        var module = _registry.GetByKey(ModuleKeys.Accounting);
        Assert.NotNull(module);
        Assert.False(module!.IsPlaceholder);
        Assert.True(module.IsEnabled);
        Assert.Contains(module.NavigationItems, n => n.Route == "/accounting/vouchers/journal");
    }
}
