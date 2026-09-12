using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class AccountingTests
{
    [Fact]
    public async Task BalancedJournal_IsAccepted_AndUnbalanced_IsRejected()
    {
        var (db, tenant, posting, accounting, reports) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();

        var bank = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.BankDefault);
        var capital = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Capital);

        var voucher = await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Journal,
            Narration = "Opening capital",
            Lines =
            [
                new() { LedgerId = bank.Id, Debit = 500000 },
                new() { LedgerId = capital.Id, Credit = 500000 }
            ]
        });

        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(500000m, voucher.TotalDebit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Journal,
            Lines =
            [
                new() { LedgerId = bank.Id, Debit = 100 },
                new() { LedgerId = capital.Id, Credit = 50 }
            ]
        }));

        var tb = await reports.GetTrialBalanceAsync(null, null);
        Assert.True(tb.IsBalanced);
    }

    [Fact]
    public async Task ClosedYear_And_LockedPeriod_Reject_Posting()
    {
        var (db, tenant, posting, accounting, _) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var fy = await db.FinancialYears.FirstAsync(y => y.CompanyId == 1);
        fy.IsClosed = true;
        await db.SaveChangesAsync();

        var bank = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.BankDefault);
        var rent = await db.Ledgers.FirstAsync(l => l.LedgerCode == "RENT");

        await Assert.ThrowsAsync<InvalidOperationException>(() => posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = fy.StartDate.AddDays(10),
            VoucherTypeCode = SystemVoucherTypeCodes.Payment,
            Lines =
            [
                new() { LedgerId = rent.Id, Debit = 100 },
                new() { LedgerId = bank.Id, Credit = 100 }
            ]
        }));

        fy.IsClosed = false;
        await db.SaveChangesAsync();
        await accounting.SetPeriodLockAsync(DateTime.UtcNow.Date.AddDays(30), "test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Payment,
            Lines =
            [
                new() { LedgerId = rent.Id, Debit = 100 },
                new() { LedgerId = bank.Id, Credit = 100 }
            ]
        }));
    }

    [Fact]
    public async Task Reversal_Creates_Opposite_Entries_And_Duplicate_Source_Is_Idempotent()
    {
        var (db, _, posting, accounting, _) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var bank = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.BankDefault);
        var rent = await db.Ledgers.FirstAsync(l => l.LedgerCode == "RENT");

        var first = await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Payment,
            SourceModule = AccountingSourceModules.Manual,
            SourceEntityType = "Test",
            SourceEntityId = "1",
            Lines =
            [
                new() { LedgerId = rent.Id, Debit = 25000 },
                new() { LedgerId = bank.Id, Credit = 25000 }
            ]
        });

        var again = await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Payment,
            SourceModule = AccountingSourceModules.Manual,
            SourceEntityType = "Test",
            SourceEntityId = "1",
            Lines =
            [
                new() { LedgerId = rent.Id, Debit = 25000 },
                new() { LedgerId = bank.Id, Credit = 25000 }
            ]
        });
        Assert.Equal(first.Id, again.Id);

        var reversal = await posting.ReverseAsync(first.Id);
        Assert.Equal(25000m, reversal.TotalDebit);
        Assert.Equal(VoucherStatus.Posted, reversal.VoucherStatus);
        var original = await db.AccountingVouchers.FirstAsync(v => v.Id == first.Id);
        Assert.Equal(VoucherStatus.Reversed, original.VoucherStatus);
    }

    [Fact]
    public async Task Posting_Drops_Rounded_Zero_Lines_And_Keeps_Balance()
    {
        var (db, _, posting, accounting, _) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var ar = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.AccountsReceivable);
        var sales = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Sales);
        var sgst = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.SgstOutput);

        var voucher = await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Sales,
            Narration = "One-cent tax split",
            Lines =
            [
                new() { LedgerId = ar.Id, Debit = 1.01m },
                new() { LedgerId = sales.Id, Credit = 1.00m },
                new() { LedgerId = sgst.Id, Credit = 0.00m },
                new() { LedgerId = sgst.Id, Credit = 0.01m }
            ]
        });

        Assert.Equal(VoucherStatus.Posted, voucher.VoucherStatus);
        Assert.Equal(1.01m, voucher.TotalDebit);
        Assert.Equal(1.01m, voucher.TotalCredit);
        Assert.Equal(3, voucher.Lines.Count);
        Assert.DoesNotContain(voucher.Lines, l => l.DebitAmount == 0 && l.CreditAmount == 0);
    }

    [Fact]
    public async Task ProfitAndLoss_And_BalanceSheet_Reflect_Posted_Activity()
    {
        var (db, _, posting, accounting, reports) = await CreateAsync();
        await accounting.EnsureBootstrapAsync();
        var bank = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.BankDefault);
        var capital = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Capital);
        var sales = await db.Ledgers.FirstAsync(l => l.SystemCode == SystemLedgerCodes.Sales);
        var rent = await db.Ledgers.FirstAsync(l => l.LedgerCode == "RENT");

        await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Journal,
            Lines =
            [
                new() { LedgerId = bank.Id, Debit = 500000 },
                new() { LedgerId = capital.Id, Credit = 500000 }
            ]
        });
        await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Payment,
            Lines =
            [
                new() { LedgerId = rent.Id, Debit = 25000 },
                new() { LedgerId = bank.Id, Credit = 25000 }
            ]
        });
        await posting.PostAsync(new AccountingPostingRequest
        {
            TransactionDate = DateTime.UtcNow.Date,
            VoucherTypeCode = SystemVoucherTypeCodes.Journal,
            Lines =
            [
                new() { LedgerId = bank.Id, Debit = 100000 },
                new() { LedgerId = sales.Id, Credit = 100000 }
            ]
        });

        var pl = await reports.GetProfitAndLossAsync(null, null);
        Assert.Equal(75000m, pl.NetProfit);

        var bs = await reports.GetBalanceSheetAsync(null);
        Assert.True(bs.IsBalanced);
    }

    private static async Task<(ShaunErpDbContext db, TenantContext tenant, AccountingPostingService posting, AccountingService accounting, AccountingReportService reports)> CreateAsync()
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
        var reports = new AccountingReportService(db, tenant, registry);
        return (db, tenant, posting, accounting, reports);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
