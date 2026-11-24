using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.ImportExport;

namespace ShaunERP.Tests;

public class ProductionInitDemoRemovalTests
{
    [Fact]
    public async Task DemoLeadMatch_Ignores_Shared_Company_Name_And_Example_Tld()
    {
        await using var db = await SeedAsync();

        var matchedIds = await ProductionInitializationService.DemoLeads(db.Leads, 1)
            .Select(l => l.Id)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(new[] { 1, 2 }, matchedIds);
    }

    [Fact]
    public async Task DemoInvoiceMatch_Ignores_Notes_That_Only_Mention_A_Demo()
    {
        await using var db = await SeedAsync();

        var matchedNumbers = await ProductionInitializationService.DemoInvoices(db.Invoices, 1)
            .Select(i => i.InvoiceNumber)
            .OrderBy(n => n)
            .ToListAsync();

        Assert.Equal(new[] { "DEMO-INV-S01" }, matchedNumbers);
    }

    [Fact]
    public async Task Scan_Counts_Only_Tagged_Or_Prefixed_Demo_Leads_And_Invoices()
    {
        var (db, service) = CreateService();
        await SeedIntoAsync(db);

        var scan = await service.ScanDemoDataAsync();
        var leadCount = scan.Counts.Single(c => c.EntityType == "leads.tagged_or_prefix").Count;
        var invoiceCount = scan.Counts.Single(c => c.EntityType == "invoices.prefix_or_notes").Count;

        Assert.Equal(2, leadCount);
        Assert.Equal(1, invoiceCount);
        Assert.Equal(4, db.Leads.Count());
        Assert.Equal(2, db.Invoices.Count());
    }

    private static async Task<ShaunErpDbContext> SeedAsync()
    {
        var db = CreateDb();
        await SeedIntoAsync(db);
        return db;
    }

    private static (ShaunErpDbContext db, ProductionInitializationService service) CreateService()
    {
        var db = CreateDb();
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            IsSystemAdmin = true,
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var service = new ProductionInitializationService(db, tenant, new NoopAudit());
        return (db, service);
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ShaunErpDbContext(options);
    }

    private static async Task SeedIntoAsync(ShaunErpDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Name = "Shaun Infomedia OPC Private Limited" });
        db.CompanyMasters.Add(new CompanyMaster
        {
            Id = 10,
            TenantCompanyId = 1,
            Name = "Books",
            InvoiceNumberPrefix = "INV-",
            NextInvoiceSequence = 1001
        });
        db.Leads.AddRange(
            new Lead
            {
                Id = 1,
                CompanyId = 1,
                FirstName = "Priya",
                LastName = "Sharma",
                CompanyName = "Chennai Retail Pvt Ltd",
                Email = "demo.lead.new@chennairetail.example",
                Tags = "demo-seed"
            },
            new Lead
            {
                Id = 2,
                CompanyId = 1,
                FirstName = "Rahul",
                LastName = "Mehta",
                CompanyName = "NovaTech Systems",
                Email = "demo.lead.qualified@novatech.example",
                Tags = "demo-seed"
            },
            new Lead
            {
                Id = 3,
                CompanyId = 1,
                FirstName = "Lakshmi",
                LastName = "Iyer",
                CompanyName = "Chennai Retail Pvt Ltd",
                Email = "sales@chennairetail.in"
            },
            new Lead
            {
                Id = 4,
                CompanyId = 1,
                FirstName = "Arjun",
                LastName = "Nair",
                CompanyName = "Roja Combines",
                Email = "ops@roja.example"
            });
        db.Invoices.AddRange(
            new Invoice
            {
                Id = 1,
                CompanyId = 1,
                CompanyMasterId = 10,
                InvoiceNumber = "DEMO-INV-S01",
                Notes = "Demo invoice [demo-seed]",
                GrandTotal = 240000m,
                InvoiceStatus = InvoiceStatus.Sent
            },
            new Invoice
            {
                Id = 2,
                CompanyId = 1,
                CompanyMasterId = 10,
                InvoiceNumber = "INV-2044",
                Notes = "Sent demo invoice to prospect after the walkthrough",
                GrandTotal = 180000m,
                InvoiceStatus = InvoiceStatus.Sent
            });
        await db.SaveChangesAsync();
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
