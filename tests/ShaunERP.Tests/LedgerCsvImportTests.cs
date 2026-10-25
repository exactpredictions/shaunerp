using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class LedgerCsvImportTests
{
    [Fact]
    public async Task Export_then_reimport_keeps_customer_ledger_link()
    {
        var (db, accounting, importer) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        db.CrmCustomers.Add(new Customer
        {
            Id = 42,
            CompanyId = 1,
            CompanyName = "BrightPath Education"
        });
        await db.SaveChangesAsync();

        var customer = await db.CrmCustomers.FirstAsync(c => c.Id == 42);
        var ar = await accounting.EnsureCustomerLedgerAsync(customer);
        ar = await db.Ledgers.FirstAsync(l => l.Id == ar.Id);
        ar.GstApplicable = true;
        ar.CreditLimit = 250000;
        ar.CreditDays = 45;
        ar.BankAccountNumber = "501001234567";
        ar.BankIfsc = "HDFC0001234";
        ar.BankName = "HDFC Bank";
        ar.OpeningBalance = 15000;
        ar.OpeningBalanceType = BalanceSide.Debit;
        await db.SaveChangesAsync();

        var exported = await importer.ExportAsync(new CsvExportRequest { CompanyId = 1 });
        var parsed = CsvParser.Parse(exported.CsvContent);
        Assert.True(parsed.Success);

        var preview = await importer.PreviewAsync(parsed, 1);
        var arRow = preview.Rows.Single(r => r.MatchKey == ar.LedgerCode);
        Assert.Equal("Update", arRow.Action);
        Assert.Equal(ar.Id, arRow.ExistingId);

        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var reloaded = await db.Ledgers.AsNoTracking().FirstAsync(l => l.Id == ar.Id);
        Assert.Equal(42, reloaded.CustomerId);
        Assert.True(reloaded.GstApplicable);
        Assert.Equal(250000m, reloaded.CreditLimit);
        Assert.Equal(45, reloaded.CreditDays);
        Assert.Equal("501001234567", reloaded.BankAccountNumber);
        Assert.Equal("HDFC0001234", reloaded.BankIfsc);
        Assert.Equal("HDFC Bank", reloaded.BankName);
        Assert.Equal(15000m, reloaded.OpeningBalance);

        var again = await accounting.EnsureCustomerLedgerAsync(customer);
        Assert.Equal(ar.Id, again.Id);
        Assert.Equal(1, await db.Ledgers.CountAsync(l => l.CompanyId == 1 && l.LedgerCode == ar.LedgerCode));
    }

    [Fact]
    public async Task Header_only_update_does_not_zero_opening_or_flip_side()
    {
        var (db, accounting, importer) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var arGroup = await db.AccountGroups.FirstAsync(g => g.CompanyId == 1 && g.GroupCode == SystemGroupCodes.AccountsReceivable);
        var ledger = new Ledger
        {
            CompanyId = 1,
            LedgerCode = "AR-ADV",
            LedgerName = "Advance receivable",
            AccountGroupId = arGroup.Id,
            AccountType = AccountNature.Asset,
            OpeningBalance = 8000,
            OpeningBalanceType = BalanceSide.Credit,
            CustomerId = 9,
            SupplierId = 3,
            GstApplicable = true,
            IsActive = true
        };
        db.Ledgers.Add(ledger);
        await db.SaveChangesAsync();

        var parsed = new CsvParseResult
        {
            Headers = ["LedgerCode", "LedgerName", "GroupCode"],
            Rows =
            [
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LedgerCode"] = "AR-ADV",
                    ["LedgerName"] = "Customer advances",
                    ["GroupCode"] = SystemGroupCodes.AccountsReceivable,
                    ["OpeningBalance"] = "",
                    ["OpeningBalanceType"] = "",
                    ["Gstin"] = "",
                    ["Pan"] = "",
                    ["IsCashLedger"] = "",
                    ["IsBankLedger"] = ""
                }
            ]
        };

        var preview = await importer.PreviewAsync(parsed, 1);
        Assert.True(preview.CanApply);
        var result = await importer.ApplyAsync(preview, 1);
        Assert.True(result.Success, string.Join("; ", result.Messages));

        var reloaded = await db.Ledgers.AsNoTracking().FirstAsync(l => l.Id == ledger.Id);
        Assert.Equal("Customer advances", reloaded.LedgerName);
        Assert.Equal(8000m, reloaded.OpeningBalance);
        Assert.Equal(BalanceSide.Credit, reloaded.OpeningBalanceType);
        Assert.Equal(9, reloaded.CustomerId);
        Assert.Equal(3, reloaded.SupplierId);
        Assert.True(reloaded.GstApplicable);
    }

    private static async Task<(ShaunErpDbContext db, AccountingService accounting, LedgersCsvImporter importer)> CreateAsync()
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
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Accounting, ModuleKeys.Finance },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var importer = new LedgersCsvImporter(db, accounting);
        return (db, accounting, importer);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
