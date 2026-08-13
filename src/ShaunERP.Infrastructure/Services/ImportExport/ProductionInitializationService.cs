using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public interface IProductionInitializationService
{
    Task<DemoDataScanResult> ScanDemoDataAsync(CancellationToken ct = default);
    Task<DemoDataPreview> PreviewDemoRecordsAsync(CancellationToken ct = default);
    Task<DemoDataRemovalResult> RemoveDemoDataAsync(CancellationToken ct = default);
    Task<ProductionInitResult> InitializeProductionDatasetAsync(CancellationToken ct = default);
}

public sealed class ProductionInitializationService : IProductionInitializationService
{
    public const string ProductionCompanyName = "Shaun Infomedia OPC Private Limited";
    private const string DemoTag = "demo-seed";
    private const string DemoAdminEmail = "admin@shaunerp.local";

    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public ProductionInitializationService(ShaunErpDbContext db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<DemoDataScanResult> ScanDemoDataAsync(CancellationToken ct = default)
    {
        EnsureSystemAdmin();
        var company = await ResolveTargetCompanyAsync(ct);
        var counts = await CollectCountsAsync(company?.Id, ct);
        return new DemoDataScanResult
        {
            CompanyId = company?.Id ?? 0,
            CompanyName = company?.Name,
            DemoTaggedRecords = counts.Where(c => c.EntityType.Contains("tagged", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Count),
            DemoPrefixedRecords = counts.Where(c => c.EntityType.Contains("prefix", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Count),
            HasDemoAdmin = await _db.Users.AnyAsync(u => u.Email == DemoAdminEmail, ct),
            HasDemoCompany = await _db.Companies.AnyAsync(c => c.Name == "Demo Company", ct),
            Counts = counts
        };
    }

    public async Task<DemoDataPreview> PreviewDemoRecordsAsync(CancellationToken ct = default)
    {
        EnsureSystemAdmin();
        var scan = await ScanDemoDataAsync(ct);
        var samples = new List<string>();
        if (scan.CompanyId > 0)
        {
            var companyId = scan.CompanyId;
            samples.AddRange(await _db.CrmCustomers.AsNoTracking()
                .Where(c => c.CompanyId == companyId && (c.Tags!.Contains(DemoTag) || (c.AccountNumber != null && c.AccountNumber.StartsWith("CUST-DEMO-"))))
                .Select(c => $"Customer:{c.AccountNumber}:{c.CompanyName}")
                .Take(10).ToListAsync(ct));
            samples.AddRange(await _db.Leads.AsNoTracking()
                .Where(l => l.CompanyId == companyId && (l.Tags!.Contains(DemoTag) || (l.Email != null && l.Email.StartsWith("demo.lead."))))
                .Select(l => $"Lead:{l.Email}")
                .Take(10).ToListAsync(ct));
            samples.AddRange(await _db.Invoices.AsNoTracking()
                .Where(i => i.CompanyId == companyId && (i.InvoiceNumber.StartsWith("DEMO-INV-") || (i.Notes != null && i.Notes.Contains(DemoTag))))
                .Select(i => $"Invoice:{i.InvoiceNumber}")
                .Take(10).ToListAsync(ct));
            samples.AddRange(await _db.CrmProducts.AsNoTracking()
                .Where(p => p.CompanyId == companyId && p.ProductCode.StartsWith("DEMO-"))
                .Select(p => $"Product:{p.ProductCode}")
                .Take(10).ToListAsync(ct));
        }

        var warnings = new List<string>
        {
            "Removal deletes demo CRM/finance/orders/vouchers in FK-safe order.",
            "Roles, permissions, modules, and system CoA templates are preserved.",
            "Demo admin user admin@shaunerp.local is deactivated, not deleted."
        };

        return new DemoDataPreview { Scan = scan, SampleKeys = samples, Warnings = warnings };
    }

    public async Task<DemoDataRemovalResult> RemoveDemoDataAsync(CancellationToken ct = default)
    {
        EnsureSystemAdmin();
        var company = await ResolveTargetCompanyAsync(ct);
        if (company is null)
        {
            return new DemoDataRemovalResult { Success = false, Messages = ["No company found."] };
        }

        var companyId = company.Id;
        var deleted = 0;
        var messages = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Payments -> invoice lines -> invoices linked to demo
            var demoInvoiceIds = await _db.Invoices
                .Where(i => i.CompanyId == companyId &&
                            (i.InvoiceNumber.StartsWith("DEMO-INV-") ||
                             i.InvoiceNumber.StartsWith("DEMO-") ||
                             (i.Notes != null && (
                                 i.Notes.Contains(DemoTag) ||
                                 i.Notes.ToLower().Contains("seeded demo") ||
                                 i.Notes.ToLower().Contains("demo invoice")))))
                .Select(i => i.Id)
                .ToListAsync(ct);
            if (demoInvoiceIds.Count > 0)
            {
                deleted += await _db.InvoicePayments.Where(p => demoInvoiceIds.Contains(p.InvoiceId)).ExecuteDeleteAsync(ct);
                deleted += await _db.InvoiceLines.Where(l => demoInvoiceIds.Contains(l.InvoiceId)).ExecuteDeleteAsync(ct);
                deleted += await _db.Invoices.Where(i => demoInvoiceIds.Contains(i.Id)).ExecuteDeleteAsync(ct);
            }

            var demoOrderIds = await _db.SalesOrders
                .Where(o => o.CompanyId == companyId &&
                            (o.OrderNumber.StartsWith("DEMO-") || (o.Notes != null && o.Notes.Contains(DemoTag))))
                .Select(o => o.Id)
                .ToListAsync(ct);
            if (demoOrderIds.Count > 0)
            {
                deleted += await _db.SalesOrderLines.Where(l => demoOrderIds.Contains(l.SalesOrderId)).ExecuteDeleteAsync(ct);
                deleted += await _db.SalesOrders.Where(o => demoOrderIds.Contains(o.Id)).ExecuteDeleteAsync(ct);
            }

            // Accounting vouchers sourced from demo (non-system)
            var demoVoucherIds = await _db.AccountingVouchers
                .Where(v => v.CompanyId == companyId &&
                            ((v.Narration != null && v.Narration.Contains(DemoTag)) ||
                             (v.SourceEntityId != null && v.SourceEntityId.StartsWith("DEMO"))))
                .Select(v => v.Id)
                .ToListAsync(ct);
            if (demoVoucherIds.Count > 0)
            {
                var lineIds = await _db.AccountingVoucherLines
                    .Where(l => demoVoucherIds.Contains(l.VoucherId))
                    .Select(l => l.Id)
                    .ToListAsync(ct);
                if (lineIds.Count > 0)
                {
                    deleted += await _db.AccountingAllocations.Where(a => lineIds.Contains(a.VoucherLineId)).ExecuteDeleteAsync(ct);
                }

                deleted += await _db.AccountingVoucherLines.Where(l => demoVoucherIds.Contains(l.VoucherId)).ExecuteDeleteAsync(ct);
                deleted += await _db.AccountingVouchers.Where(v => demoVoucherIds.Contains(v.Id)).ExecuteDeleteAsync(ct);
            }

            deleted += await DeleteTaggedCrmAsync(companyId, ct);

            // Non-system DEMO- ledgers only
            deleted += await _db.Ledgers
                .Where(l => l.CompanyId == companyId && !l.IsSystemLedger && l.LedgerCode.StartsWith("DEMO-"))
                .ExecuteDeleteAsync(ct);

            var demoAdmin = await _db.Users.FirstOrDefaultAsync(u => u.Email == DemoAdminEmail, ct);
            if (demoAdmin is not null)
            {
                demoAdmin.IsActive = false;
                messages.Add("Deactivated demo admin admin@shaunerp.local.");
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.WriteAsync(
                "production.demo.remove",
                companyId: companyId,
                userId: _tenant.UserId,
                entityType: "DemoData",
                entityId: companyId.ToString(),
                afterValues: $"deleted={deleted}",
                ct: ct);

            messages.Add($"Removed {deleted} demo-linked record(s).");
            return new DemoDataRemovalResult { Success = true, DeletedRecords = deleted, Messages = messages };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new DemoDataRemovalResult { Success = false, Messages = [$"Removal failed: {ex.Message}"] };
        }
    }

    public async Task<ProductionInitResult> InitializeProductionDatasetAsync(CancellationToken ct = default)
    {
        EnsureSystemAdmin();
        var messages = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var demo = await _db.Companies.FirstOrDefaultAsync(c => c.Name == "Demo Company", ct);
            Company company;
            if (demo is not null)
            {
                // Safe rename when still named Demo Company
                demo.Name = ProductionCompanyName;
                demo.Domain = "shauninfomedia.com";
                demo.Description = "Production company shell";
                company = demo;
                messages.Add("Renamed Demo Company to Shaun Infomedia OPC Private Limited.");
            }
            else
            {
                company = await _db.Companies.FirstOrDefaultAsync(c => c.Name == ProductionCompanyName, ct)
                          ?? new Company
                          {
                              Name = ProductionCompanyName,
                              Domain = "shauninfomedia.com",
                              Description = "Production company shell"
                          };
                if (company.Id == 0)
                {
                    _db.Companies.Add(company);
                    await _db.SaveChangesAsync(ct);
                    messages.Add("Created Shaun Infomedia OPC Private Limited company shell.");
                }
                else
                {
                    messages.Add("Using existing Shaun Infomedia OPC Private Limited company.");
                }
            }

            await EnsureEntitlementsAsync(company.Id, ct);

            var profile = await _db.CompanyProfiles.FirstOrDefaultAsync(p => p.CompanyId == company.Id, ct);
            if (profile is null)
            {
                profile = new Core.Accounting.CompanyProfile
                {
                    CompanyId = company.Id,
                    LegalName = ProductionCompanyName,
                    TradingName = "Shaun Infomedia",
                    Country = "India",
                    PrimaryGstin = null,
                    PrimaryPan = null
                };
                _db.CompanyProfiles.Add(profile);
            }
            else
            {
                profile.LegalName = ProductionCompanyName;
                profile.TradingName ??= "Shaun Infomedia";
                profile.Country ??= "India";
                // Keep statutory blank for production fill-in
                profile.PrimaryGstin = null;
                profile.PrimaryPan = null;
            }

            var gst = await _db.CompanyGstSettings.FirstOrDefaultAsync(g => g.CompanyId == company.Id, ct);
            if (gst is null)
            {
                _db.CompanyGstSettings.Add(new Core.Accounting.CompanyGstSettings
                {
                    CompanyId = company.Id,
                    GstRegistered = false,
                    RegistrationType = Core.Accounting.GstRegistrationType.Unregistered
                });
            }
            else
            {
                gst.Gstin = null;
                gst.GstRegistered = false;
            }

            var book = await _db.CompanyMasters.FirstOrDefaultAsync(b => b.TenantCompanyId == company.Id, ct);
            if (book is null)
            {
                _db.CompanyMasters.Add(new Core.Finance.CompanyMaster
                {
                    TenantCompanyId = company.Id,
                    Name = "Shaun Infomedia",
                    LegalName = ProductionCompanyName,
                    Currency = "INR",
                    InvoiceNumberPrefix = "INV-",
                    NextInvoiceSequence = 1001,
                    OrderNumberPrefix = "SO-",
                    NextOrderSequence = 1001,
                    DefaultPaymentTerms = "Net 30",
                    DefaultDueDays = 30,
                    BillingCountry = "India"
                });
            }
            else
            {
                book.Name = "Shaun Infomedia";
                book.LegalName = ProductionCompanyName;
                book.Currency = "INR";
                book.TaxId = null;
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _audit.WriteAsync(
                "production.init",
                companyId: company.Id,
                userId: _tenant.UserId,
                entityType: "Company",
                entityId: company.Id.ToString(),
                afterValues: ProductionCompanyName,
                ct: ct);

            messages.Add("Production company shell ready with blank statutory fields.");
            return new ProductionInitResult
            {
                Success = true,
                CompanyId = company.Id,
                CompanyName = company.Name,
                Messages = messages
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new ProductionInitResult { Success = false, Messages = [$"Initialization failed: {ex.Message}"] };
        }
    }

    private async Task EnsureEntitlementsAsync(int companyId, CancellationToken ct)
    {
        var developed = new (string Key, string Name)[]
        {
            (ModuleKeys.Users, "Users"),
            (ModuleKeys.Crm, "CRM"),
            (ModuleKeys.Finance, "Finance"),
            (ModuleKeys.Orders, "Sales Orders"),
            (ModuleKeys.Accounting, "Accounting"),
            (ModuleKeys.Settings, "Settings")
        };

        var existing = await _db.CompanyModules.Where(m => m.CompanyId == companyId).ToListAsync(ct);
        foreach (var (key, name) in developed)
        {
            var row = existing.FirstOrDefault(m => m.ModuleKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                _db.CompanyModules.Add(new CompanyModule
                {
                    CompanyId = companyId,
                    ModuleKey = key,
                    ModuleName = name,
                    IsEnabled = true
                });
            }
            else
            {
                row.IsEnabled = true;
                row.ModuleKey = key;
                row.ModuleName = name;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int> DeleteTaggedCrmAsync(int companyId, CancellationToken ct)
    {
        var deleted = 0;
        deleted += await _db.CrmNotes.Where(x => x.CompanyId == companyId && ((x.Body != null && x.Body.Contains(DemoTag)) || x.Title.Contains("DEMO"))).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmCalls.Where(x => x.CompanyId == companyId && ((x.Description != null && x.Description.Contains(DemoTag)) || x.Subject.StartsWith("DEMO"))).ExecuteDeleteAsync(ct);
        deleted += await _db.Meetings.Where(x => x.CompanyId == companyId && ((x.Agenda != null && x.Agenda.Contains(DemoTag)) || (x.Title != null && x.Title.StartsWith("DEMO")))).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmTasks.Where(x => x.CompanyId == companyId && ((x.Description != null && x.Description.Contains(DemoTag)) || x.Subject.StartsWith("DEMO"))).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmCases.Where(x => x.CompanyId == companyId && (x.CaseNumber.StartsWith("DEMO-CAS-") || (x.Tags != null && x.Tags.Contains(DemoTag)))).ExecuteDeleteAsync(ct);

        var quoteIds = await _db.Quotes
            .Where(q => q.CompanyId == companyId && (q.QuoteNumber.StartsWith("DEMO-Q-") || (q.Description != null && q.Description.Contains(DemoTag))))
            .Select(q => q.Id).ToListAsync(ct);
        if (quoteIds.Count > 0)
        {
            deleted += await _db.QuoteLines.Where(l => quoteIds.Contains(l.QuoteId)).ExecuteDeleteAsync(ct);
            deleted += await _db.Quotes.Where(q => quoteIds.Contains(q.Id)).ExecuteDeleteAsync(ct);
        }

        deleted += await _db.Opportunities.Where(x => x.CompanyId == companyId && ((x.DealName != null && x.DealName.StartsWith("DEMO:")) || (x.Tags != null && x.Tags.Contains(DemoTag)))).ExecuteDeleteAsync(ct);
        deleted += await _db.Contacts.Where(x => x.CompanyId == companyId && x.Tags != null && x.Tags.Contains(DemoTag)).ExecuteDeleteAsync(ct);
        deleted += await _db.Prospects.Where(x => x.CompanyId == companyId && ((x.Email != null && x.Email.StartsWith("demo.prospect.")) || (x.Tags != null && x.Tags.Contains(DemoTag)))).ExecuteDeleteAsync(ct);
        deleted += await _db.Leads.Where(x => x.CompanyId == companyId && (
            (x.Email != null && x.Email.StartsWith("demo.lead.")) ||
            (x.Email != null && x.Email.EndsWith(".example")) ||
            (x.Tags != null && x.Tags.Contains(DemoTag)) ||
            x.CompanyName == "Chennai Retail Pvt Ltd" ||
            x.CompanyName == "Roja Combines")).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmCustomers.Where(x => x.CompanyId == companyId && ((x.AccountNumber != null && x.AccountNumber.StartsWith("CUST-DEMO-")) || (x.Tags != null && x.Tags.Contains(DemoTag)))).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmAccounts.Where(x => x.CompanyId == companyId && ((x.AccountNumber != null && x.AccountNumber.StartsWith("DEMO-ACC-")) || (x.Tags != null && x.Tags.Contains(DemoTag)))).ExecuteDeleteAsync(ct);
        deleted += await _db.Campaigns.Where(x => x.CompanyId == companyId && (x.Name.StartsWith("DEMO:") || (x.Description != null && x.Description.Contains(DemoTag)))).ExecuteDeleteAsync(ct);
        deleted += await _db.CrmProducts.Where(x => x.CompanyId == companyId && x.ProductCode.StartsWith("DEMO-")).ExecuteDeleteAsync(ct);
        return deleted;
    }

    private async Task<IReadOnlyList<DemoDataCount>> CollectCountsAsync(int? companyId, CancellationToken ct)
    {
        if (companyId is null) return Array.Empty<DemoDataCount>();
        var id = companyId.Value;
        return
        [
            new DemoDataCount { EntityType = "customers.tagged_or_prefix", Count = await _db.CrmCustomers.CountAsync(c => c.CompanyId == id && ((c.Tags != null && c.Tags.Contains(DemoTag)) || (c.AccountNumber != null && c.AccountNumber.StartsWith("CUST-DEMO-"))), ct) },
            new DemoDataCount { EntityType = "leads.tagged_or_prefix", Count = await _db.Leads.CountAsync(l => l.CompanyId == id && ((l.Tags != null && l.Tags.Contains(DemoTag)) || (l.Email != null && l.Email.StartsWith("demo.lead."))), ct) },
            new DemoDataCount { EntityType = "products.prefix", Count = await _db.CrmProducts.CountAsync(p => p.CompanyId == id && p.ProductCode.StartsWith("DEMO-"), ct) },
            new DemoDataCount { EntityType = "invoices.prefix_or_notes", Count = await _db.Invoices.CountAsync(i => i.CompanyId == id && (i.InvoiceNumber.StartsWith("DEMO-INV-") || (i.Notes != null && i.Notes.Contains(DemoTag))), ct) },
            new DemoDataCount { EntityType = "opportunities.tagged_or_prefix", Count = await _db.Opportunities.CountAsync(o => o.CompanyId == id && ((o.DealName != null && o.DealName.StartsWith("DEMO:")) || (o.Tags != null && o.Tags.Contains(DemoTag))), ct) }
        ];
    }

    private async Task<Company?> ResolveTargetCompanyAsync(CancellationToken ct)
    {
        if (_tenant.CompanyId is int cid)
        {
            return await _db.Companies.FirstOrDefaultAsync(c => c.Id == cid, ct);
        }

        return await _db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
    }

    private void EnsureSystemAdmin()
    {
        if (!_tenant.IsSystemAdmin && !_tenant.Permissions.Contains(Permissions.SettingsProductionInit))
        {
            throw new UnauthorizedAccessException("System admin / production initialization permission required.");
        }

        if (!_tenant.IsSystemAdmin)
        {
            // Require system admin explicitly for destructive ops
            throw new UnauthorizedAccessException("System administrator required for production initialization.");
        }
    }
}
