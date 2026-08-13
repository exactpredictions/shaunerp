using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

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
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IAccountingPostingService, AccountingPostingService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IAccountingReportService, AccountingReportService>();
        services.AddScoped<IAccountAccessService, AccountAccessService>();
        services.AddScoped<IProjectManagementService, ProjectManagementService>();
        services.AddScoped<DbSeeder>();

        var projectFilesRoot = configuration["Storage:ProjectFilesRoot"];
        if (string.IsNullOrWhiteSpace(projectFilesRoot))
        {
            projectFilesRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "project-files");
        }
        services.AddSingleton<IProjectFileStorage>(_ => new ProjectFileStorage(projectFilesRoot));

        services.AddScoped<ICsvImportEngine, CsvImportEngine>();
        services.AddScoped<ICsvExportService, CsvExportService>();
        services.AddScoped<ICompanyConfigurationService, CompanyConfigurationService>();
        services.AddScoped<IProductionInitializationService, ProductionInitializationService>();
        services.AddScoped<IProductionUserBootstrapService, ProductionUserBootstrapService>();

        services.AddScoped<IEntityCsvImporter, CustomersCsvImporter>();
        services.AddScoped<IEntityCsvImporter, LeadsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, ContactsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, ProspectsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, OpportunitiesCsvImporter>();
        services.AddScoped<IEntityCsvImporter, ProductsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, VendorsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, LedgersCsvImporter>();
        services.AddScoped<IEntityCsvImporter, InvoicesCsvImporter>();
        services.AddScoped<IEntityCsvImporter, PaymentsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, SalesOrdersCsvImporter>();
        services.AddScoped<IEntityCsvImporter, InventoryItemsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, OpeningBalancesCsvImporter>();
        services.AddScoped<IEntityCsvImporter, ProjectsCsvImporter>();
        services.AddScoped<IEntityCsvImporter, ProjectTasksCsvImporter>();

        return services;
    }
}
