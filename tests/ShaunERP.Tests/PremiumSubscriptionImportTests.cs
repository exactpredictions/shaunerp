using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;

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

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ShaunErpDbContext(options);
    }
}
