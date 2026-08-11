using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        // Factory avoids Blazor Server concurrent DbContext usage across components.
        services.AddDbContextFactory<ShaunErpDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<ShaunErpDbContext>>().CreateDbContext());

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IAuthorizationContextService, AuthorizationContextService>();
        services.AddSingleton<IModuleRegistry, ModuleRegistry>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ICrmService, CrmService>();
        services.AddScoped<IAccountAccessService, AccountAccessService>();
        services.AddScoped<DbSeeder>();

        return services;
    }
}
