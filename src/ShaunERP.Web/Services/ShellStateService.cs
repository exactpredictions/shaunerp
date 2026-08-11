using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Web.Services;

public sealed class ShellStateService : IDisposable
{
    private readonly IModuleRegistry _registry;
    private readonly ITenantContext _tenant;
    private readonly IAuthorizationContextService _authz;
    private readonly IUserPreferenceService _prefs;
    private readonly IAccountAccessService _accounts;
    private readonly NavigationManager _nav;
    private readonly AuthenticationStateProvider _authState;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public ShellStateService(
        IModuleRegistry registry,
        ITenantContext tenant,
        IAuthorizationContextService authz,
        IUserPreferenceService prefs,
        IAccountAccessService accounts,
        NavigationManager nav,
        AuthenticationStateProvider authState)
    {
        _registry = registry;
        _tenant = tenant;
        _authz = authz;
        _prefs = prefs;
        _accounts = accounts;
        _nav = nav;
        _authState = authState;
        _nav.LocationChanged += OnLocationChanged;
    }

    public string? CurrentModuleKey { get; private set; } = ModuleKeys.Home;
    public ModuleDefinition? CurrentModule { get; private set; }
    public IReadOnlyList<ModuleNavItem> Navigation { get; private set; } = Array.Empty<ModuleNavItem>();
    public IReadOnlyList<ModuleDefinition> AccessibleModules { get; private set; } = Array.Empty<ModuleDefinition>();
    public IReadOnlyList<AccountSummary> Accounts { get; private set; } = Array.Empty<AccountSummary>();
    public UserCompanyPreference? Preferences { get; private set; }
    public bool SidebarCollapsed { get; private set; }
    public bool MobileNavOpen { get; private set; }
    public bool ModuleSwitcherOpen { get; private set; }
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            RefreshFromPath();
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                RefreshFromPath();
                return;
            }

            var state = await _authState.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated == true)
            {
                // Claims first (safe), then refresh from DB via factory-backed service.
                _tenant.Set(_authz.BuildFromClaims(state.User));
                var snapshot = await _authz.BuildFromPrincipalAsync(state.User);
                _tenant.Set(snapshot);
                if (snapshot.UserId is int userId)
                {
                    Accounts = await _accounts.ListMembershipsAsync(userId);
                    if (snapshot.CompanyId is int companyId)
                    {
                        Preferences = await _prefs.GetOrCreateAsync(userId, companyId);
                        SidebarCollapsed = Preferences.SidebarCollapsed;
                    }
                }
            }

            AccessibleModules = _registry.GetAccessible(_tenant);
            RefreshFromPath();
            _initialized = true;
            Notify();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void RefreshFromPath()
    {
        var path = new Uri(_nav.Uri).AbsolutePath;
        var module = _registry.ResolveFromPath(path) ?? _registry.GetByKey(ModuleKeys.Home);
        CurrentModule = module;
        CurrentModuleKey = module?.Key ?? ModuleKeys.Home;
        Navigation = module is null ? Array.Empty<ModuleNavItem>() : _registry.GetNavigation(module.Key, _tenant);
        AccessibleModules = _registry.GetAccessible(_tenant);
        Notify();
    }

    public async Task SelectModuleAsync(string moduleKey)
    {
        var module = _registry.GetByKey(moduleKey);
        if (module is null || !_registry.CanAccessModule(module, _tenant))
        {
            return;
        }

        CurrentModule = module;
        CurrentModuleKey = module.Key;
        Navigation = _registry.GetNavigation(module.Key, _tenant);
        ModuleSwitcherOpen = false;
        MobileNavOpen = false;

        if (_tenant.UserId is int userId && _tenant.CompanyId is int companyId)
        {
            await _prefs.RememberModuleAsync(userId, companyId, module.Key);
            Preferences = await _prefs.GetOrCreateAsync(userId, companyId);
        }

        _nav.NavigateTo(module.DashboardRoute);
        Notify();
    }

    public async Task ToggleSidebarAsync()
    {
        SidebarCollapsed = !SidebarCollapsed;
        Notify();

        try
        {
            if (_tenant.UserId is int userId && _tenant.CompanyId is int companyId)
            {
                await _prefs.SetSidebarCollapsedAsync(userId, companyId, SidebarCollapsed);
            }
        }
        catch
        {
            // Keep UI toggle even if preference persistence fails.
        }
    }

    public void ToggleMobileNav()
    {
        MobileNavOpen = !MobileNavOpen;
        Notify();
    }

    public void CloseMobileNav()
    {
        MobileNavOpen = false;
        Notify();
    }

    public void ToggleModuleSwitcher()
    {
        ModuleSwitcherOpen = !ModuleSwitcherOpen;
        Notify();
    }

    public void CloseModuleSwitcher()
    {
        ModuleSwitcherOpen = false;
        Notify();
    }

    public async Task ToggleFavouriteAsync(string moduleKey)
    {
        if (_tenant.UserId is not int userId || _tenant.CompanyId is not int companyId)
        {
            return;
        }

        await _prefs.ToggleFavouriteAsync(userId, companyId, moduleKey);
        Preferences = await _prefs.GetOrCreateAsync(userId, companyId);
        Notify();
    }

    public bool IsFavourite(string moduleKey)
    {
        var favs = Preferences?.FavouriteModules ?? string.Empty;
        return favs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(x => x.Equals(moduleKey, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> RecentModuleKeys()
    {
        var recent = Preferences?.RecentModules ?? string.Empty;
        return recent.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        MobileNavOpen = false;
        ModuleSwitcherOpen = false;
        RefreshFromPath();
    }

    private void Notify() => Changed?.Invoke();

    public void Dispose()
    {
        _nav.LocationChanged -= OnLocationChanged;
        _initLock.Dispose();
    }
}
