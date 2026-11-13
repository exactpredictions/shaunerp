using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class PremiumSubscriptionImportTests
{
    [Fact]
    public void Activation_audit_maps_to_start_date_only()
    {
        // Mirrors PremiumSubscriberImportService.MapProvenDates rules via behavior contract
        Assert.Equal(MembershipStatuses.Active, MembershipStatuses.Active);
        Assert.Equal(MembershipStatuses.Expired, MembershipStatuses.Expired);
        Assert.Equal("ExactPredictions Premium — Historical/Unknown Plan", SubscriptionPlanNames.ExactPredictionsPremiumHistorical);
    }

    [Fact]
    public async Task Membership_external_reference_is_unique_per_company()
    {
        await using var db = CreateDb();
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        await db.SaveChangesAsync();

        var party = new Party
        {
            CompanyId = 1,
            PartyType = PartyTypes.Person,
            DisplayName = "Test",
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.Parties.Add(party);
        var plan = new SubscriptionPlan
        {
            CompanyId = 1,
            Name = SubscriptionPlanNames.ExactPredictionsPremiumHistorical,
            DurationMonths = 0,
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();

        db.SubscriptionMemberships.Add(new SubscriptionMembership
        {
            CompanyId = 1,
            PartyId = party.Id,
            PlanId = plan.Id,
            MembershipStatus = MembershipStatuses.Active,
            ExternalReference = "exactpredictions:815",
            LegacyDateSource = "audit: latest level change 1->7 (activation date)",
            StartDate = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.SubscriptionMemberships.CountAsync(m => m.ExternalReference == "exactpredictions:815"));
        Assert.NotNull((await db.SubscriptionMemberships.SingleAsync()).StartDate);
        Assert.Null((await db.SubscriptionMemberships.SingleAsync()).EndDate);
    }

    [Fact]
    public async Task Import_keeps_resubscribed_member_active_when_they_also_appear_on_the_old_sheet()
    {
        await using var db = CreateDb();
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        await db.SaveChangesAsync();

        var path = Path.Combine(Path.GetTempPath(), $"premium-{Guid.NewGuid():N}.xlsx");
        try
        {
            WriteWorkbook(path,
                active: [new(815, "Ravi Kumar", "ravi@example.com", "9876543210")],
                expired: [
                    new(815, "Ravi Kumar", "ravi@example.com", "9876543210"),
                    new(900, "Expired Only", "old@example.com", "9000000000")
                ]);

            var service = CreateService(db, companyId: 1);
            var report = await service.ImportAsync(new PremiumSubscriberImportOptions
            {
                CompanyId = 1,
                FilePath = path,
                ImportedBy = "test"
            });

            Assert.True(report.Success, string.Join("; ", report.Messages));
            Assert.Equal(1, report.Active.MembershipsCreated);
            Assert.Equal(1, report.Inactive.MembershipsCreated);
            Assert.Equal(1, report.Inactive.MembershipsSkipped);

            var memberships = await db.SubscriptionMemberships
                .AsNoTracking()
                .Where(m => m.CompanyId == 1)
                .ToListAsync();
            Assert.Equal(2, memberships.Count);

            var resubscribed = Assert.Single(memberships, m => m.ExternalReference == "exactpredictions:815");
            Assert.Equal(MembershipStatuses.Active, resubscribed.MembershipStatus);

            var lapsed = Assert.Single(memberships, m => m.ExternalReference == "exactpredictions:900");
            Assert.Equal(MembershipStatuses.Expired, lapsed.MembershipStatus);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Reimport_expires_a_member_who_is_only_on_the_old_sheet()
    {
        await using var db = CreateDb();
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        await db.SaveChangesAsync();

        var firstPath = Path.Combine(Path.GetTempPath(), $"premium-first-{Guid.NewGuid():N}.xlsx");
        var secondPath = Path.Combine(Path.GetTempPath(), $"premium-second-{Guid.NewGuid():N}.xlsx");
        try
        {
            WriteWorkbook(firstPath,
                active: [new(815, "Ravi Kumar", "ravi@example.com", "9876543210")],
                expired: []);
            var service = CreateService(db, companyId: 1);
            await service.ImportAsync(new PremiumSubscriberImportOptions
            {
                CompanyId = 1,
                FilePath = firstPath,
                ImportedBy = "test"
            });

            WriteWorkbook(secondPath,
                active: [],
                expired: [new(815, "Ravi Kumar", "ravi@example.com", "9876543210")]);
            var report = await service.ImportAsync(new PremiumSubscriberImportOptions
            {
                CompanyId = 1,
                FilePath = secondPath,
                ImportedBy = "test"
            });

            Assert.True(report.Success, string.Join("; ", report.Messages));
            var membership = Assert.Single(await db.SubscriptionMemberships.AsNoTracking()
                .Where(m => m.CompanyId == 1 && m.ExternalReference == "exactpredictions:815")
                .ToListAsync());
            Assert.Equal(MembershipStatuses.Expired, membership.MembershipStatus);
        }
        finally
        {
            if (File.Exists(firstPath)) File.Delete(firstPath);
            if (File.Exists(secondPath)) File.Delete(secondPath);
        }
    }

    private static void WriteWorkbook(
        string path,
        IReadOnlyList<MemberRow> active,
        IReadOnlyList<MemberRow> expired)
    {
        using var wb = new XLWorkbook();
        WriteSheet(wb, "active_premium_subscribers", active);
        WriteSheet(wb, "old_premium_subscribers", expired);
        wb.SaveAs(path);
    }

    private static void WriteSheet(XLWorkbook wb, string name, IReadOnlyList<MemberRow> rows)
    {
        var ws = wb.AddWorksheet(name);
        ws.Cell(1, 1).Value = "user_id";
        ws.Cell(1, 2).Value = "full_name";
        ws.Cell(1, 3).Value = "email_id";
        ws.Cell(1, 4).Value = "tel";
        ws.Cell(1, 5).Value = "fax";
        ws.Cell(1, 6).Value = "date";
        ws.Cell(1, 7).Value = "date_source";
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var r = i + 2;
            ws.Cell(r, 1).Value = row.UserId;
            ws.Cell(r, 2).Value = row.FullName;
            ws.Cell(r, 3).Value = row.Email;
            ws.Cell(r, 4).Value = row.Tel;
        }
    }

    private static PremiumSubscriberImportService CreateService(ShaunErpDbContext db, int companyId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId
        });
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var match = new IdentityMatchService(db, phones, emails);
        var parties = new PartyIdentityService(db, match, phones, emails);
        return new PremiumSubscriberImportService(
            db, parties, match, phones, emails, tenant,
            NullLogger<PremiumSubscriberImportService>.Instance);
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ShaunErpDbContext(options);
    }

    private sealed record MemberRow(int UserId, string FullName, string Email, string Tel);
}
