using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;

namespace ShaunERP.Tests;

public class CompanyConfigurationTests
{
    [Fact]
    public async Task UpsertCurrency_WithoutId_UpdatesExistingCode_InsteadOfInsertingDuplicate()
    {
        var (db, _, _, accounting, _) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var before = await db.Currencies.CountAsync(c => c.CompanyId == 1 && c.CurrencyCode == "INR");
        Assert.Equal(1, before);

        var updated = await accounting.UpsertCurrencyAsync(new CurrencyMaster
        {
            CurrencyCode = "inr",
            CurrencyName = "Indian Rupee (updated)",
            Symbol = "₹",
            DecimalPlaces = 2,
            IsBaseCurrency = true,
            IsActive = true
        });

        Assert.Equal(1, await db.Currencies.CountAsync(c => c.CompanyId == 1 && c.CurrencyCode == "INR"));
        Assert.Equal("Indian Rupee (updated)", updated.CurrencyName);
        Assert.True(updated.Id > 0);
    }

    [Fact]
    public async Task UpsertStatutory_WithoutId_UpdatesMatchingTypeAndNumber()
    {
        var (_, _, _, accounting, _) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var first = await accounting.UpsertStatutoryAsync(new CompanyStatutoryRegistration
        {
            RegistrationType = "GSTIN",
            RegistrationNumber = "33AAAAA0000A1Z5",
            State = "Tamil Nadu",
            IsActive = true
        });
        var second = await accounting.UpsertStatutoryAsync(new CompanyStatutoryRegistration
        {
            RegistrationType = "gstin",
            RegistrationNumber = "33AAAAA0000A1Z5",
            State = "Karnataka",
            Notes = "re-applied from export",
            IsActive = true
        });

        var rows = await accounting.ListStatutoryAsync();
        var gstin = Assert.Single(rows, s => s.RegistrationType.Equals("GSTIN", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.Id, gstin.Id);
        Assert.Equal("Karnataka", gstin.State);
        Assert.Equal("re-applied from export", gstin.Notes);
    }

    [Fact]
    public async Task CompanyConfig_ExportThenApply_DoesNotDuplicateCurrenciesOrStatutoryRows()
    {
        var (db, tenant, _, accounting, config) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        await accounting.UpsertStatutoryAsync(new CompanyStatutoryRegistration
        {
            RegistrationType = "GSTIN",
            RegistrationNumber = "33AAAAA0000A1Z5",
            IssuingAuthority = "GSTN",
            IsActive = true
        });

        var currencyCount = await db.Currencies.CountAsync(c => c.CompanyId == 1);
        Assert.True(currencyCount >= 1);

        var exported = await config.ExportCurrentAsync();
        exported.Identity.Phone = "+91-9000000000";
        exported.Identity.LegalName = "Shaun Infomedia OPC Pvt Ltd";

        var result = await config.ApplyAsync(exported);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        Assert.Equal(currencyCount, await db.Currencies.CountAsync(c => c.CompanyId == 1));
        Assert.Equal(1, await db.Currencies.CountAsync(c => c.CompanyId == 1 && c.CurrencyCode == "INR"));
        Assert.Equal(1, await db.CompanyStatutoryRegistrations.CountAsync(s => s.CompanyId == 1));

        var profile = await accounting.GetOrCreateProfileAsync();
        Assert.Equal("+91-9000000000", profile.Phone);
        Assert.Equal(tenant.CompanyId, profile.CompanyId);
    }

    private static async Task<(ShaunErpDbContext db, TenantContext tenant, AccountingPostingService posting, AccountingService accounting, CompanyConfigurationService config)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Shaun Infomedia OPC Pvt Ltd" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            CompanyName = "Shaun Infomedia OPC Pvt Ltd",
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ModuleKeys.Accounting, ModuleKeys.Finance
            },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var config = new CompanyConfigurationService(db, tenant, audit, accounting, finance);
        return (db, tenant, posting, accounting, config);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
