using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class RelationshipDomainTests
{
    [Fact]
    public async Task Course_enrollment_links_party_without_creating_customer_or_lead()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@example.com");
        var program = new TrainingProgram
        {
            CompanyId = 1,
            Name = "KP Astrology",
            Status = "Active",
            CreatedDate = DateTime.UtcNow
        };
        db.TrainingPrograms.Add(program);
        await db.SaveChangesAsync();

        db.CourseEnrollments.Add(new CourseEnrollment
        {
            CompanyId = 1,
            PartyId = party.Id,
            ProgramId = program.Id,
            EnrollmentStatus = EnrollmentStatuses.Historical,
            Source = "test",
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.CourseEnrollments.CountAsync());
        Assert.Equal(0, await db.Leads.CountAsync());
        Assert.Equal(0, await db.CrmCustomers.CountAsync());
        Assert.True(await db.CourseEnrollments.AnyAsync(e => e.PartyId == party.Id && e.ProgramId == program.Id));
    }

    [Fact]
    public async Task Same_party_can_enroll_in_multiple_programs()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", null);
        var p1 = new TrainingProgram { CompanyId = 1, Name = "KP Astrology", Status = "Active", CreatedDate = DateTime.UtcNow };
        var p2 = new TrainingProgram { CompanyId = 1, Name = "Panchapakshi", Status = "Active", CreatedDate = DateTime.UtcNow };
        db.TrainingPrograms.AddRange(p1, p2);
        await db.SaveChangesAsync();

        db.CourseEnrollments.AddRange(
            new CourseEnrollment
            {
                CompanyId = 1, PartyId = party.Id, ProgramId = p1.Id,
                EnrollmentStatus = EnrollmentStatuses.Historical, CreatedDate = DateTime.UtcNow, Status = "Active"
            },
            new CourseEnrollment
            {
                CompanyId = 1, PartyId = party.Id, ProgramId = p2.Id,
                EnrollmentStatus = EnrollmentStatuses.Historical, CreatedDate = DateTime.UtcNow, Status = "Active"
            });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.CourseEnrollments.CountAsync(e => e.PartyId == party.Id));
        Assert.Equal(1, await db.Parties.CountAsync());
    }

    [Fact]
    public async Task Duplicate_enrollment_unique_index_is_enforced_in_memory_via_query()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Asha", "Devi", "9123456780", null);
        var program = new TrainingProgram { CompanyId = 1, Name = "Jamakol", Status = "Active", CreatedDate = DateTime.UtcNow };
        db.TrainingPrograms.Add(program);
        await db.SaveChangesAsync();

        db.CourseEnrollments.Add(new CourseEnrollment
        {
            CompanyId = 1, PartyId = party.Id, ProgramId = program.Id,
            EnrollmentStatus = EnrollmentStatuses.Historical, CreatedDate = DateTime.UtcNow, Status = "Active"
        });
        await db.SaveChangesAsync();

        var exists = await db.CourseEnrollments.AnyAsync(e =>
            e.CompanyId == 1 && e.PartyId == party.Id && e.ProgramId == program.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task Party_directory_derives_student_role_from_enrollment()
    {
        await using var db = CreateDb();
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 1,
            CompanyId = 1,
            IsSystemAdmin = true,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@example.com");
        var program = new TrainingProgram { CompanyId = 1, Name = "KP Astrology", Status = "Active", CreatedDate = DateTime.UtcNow };
        db.TrainingPrograms.Add(program);
        await db.SaveChangesAsync();
        db.CourseEnrollments.Add(new CourseEnrollment
        {
            CompanyId = 1, PartyId = party.Id, ProgramId = program.Id,
            EnrollmentStatus = EnrollmentStatuses.Historical, CreatedDate = DateTime.UtcNow, Status = "Active"
        });
        await db.SaveChangesAsync();

        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var parties = new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails);
        var directory = new PartyDirectoryService(db, tenant, new ModuleRegistry(), parties, phones, emails);

        var detail = await directory.GetPersonAsync(party.Id);
        Assert.NotNull(detail);
        Assert.Contains("Student", detail!.DerivedRoles);
        Assert.Single(detail.Courses);
        Assert.Equal("KP Astrology", detail.Courses[0].ProgramName);
    }

    [Fact]
    public async Task Subscription_plans_support_standard_durations()
    {
        await using var db = CreateDb();
        foreach (var (name, months) in new (string, int)[]
                 {
                     ("Monthly", 1), ("2 Months", 2), ("3 Months", 3), ("6 Months", 6), ("1 Year", 12)
                 })
        {
            db.SubscriptionPlans.Add(new SubscriptionPlan
            {
                CompanyId = 1,
                Name = name,
                DurationMonths = months,
                Status = "Active",
                CreatedDate = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        Assert.Equal(5, await db.SubscriptionPlans.CountAsync(p => p.CompanyId == 1));
        Assert.Equal(12, await db.SubscriptionPlans.Where(p => p.Name == "1 Year").Select(p => p.DurationMonths).FirstAsync());
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ShaunErpDbContext(options);
    }

    private static async Task<Party> SeedPartyAsync(
        ShaunErpDbContext db, int companyId, string first, string last, string phone, string? email)
    {
        var party = new Party
        {
            CompanyId = companyId,
            PartyType = PartyTypes.Person,
            DisplayName = $"{first} {last}".Trim(),
            Status = "Active",
            CreatedDate = DateTime.UtcNow,
            Person = new PartyPerson { FirstName = first, LastName = last, Status = "Active", CreatedDate = DateTime.UtcNow }
        };
        db.Parties.Add(party);
        await db.SaveChangesAsync();
        db.PartyPhones.Add(new PartyPhone
        {
            PartyId = party.Id,
            OriginalPhone = phone,
            NormalizedPhone = phone,
            E164Phone = "+91" + phone,
            PhoneType = "Mobile",
            IsPrimary = true,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
        if (!string.IsNullOrWhiteSpace(email))
        {
            db.PartyEmails.Add(new PartyEmail
            {
                PartyId = party.Id,
                OriginalEmail = email,
                NormalizedEmail = email.ToLowerInvariant(),
                EmailType = "Work",
                IsPrimary = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            });
        }

        await db.SaveChangesAsync();
        return party;
    }
}
