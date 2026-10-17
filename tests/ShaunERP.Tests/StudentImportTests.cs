using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class StudentImportTests
{
    private readonly IPhoneNormalizer _phones = new PhoneNormalizer();
    private readonly IEmailNormalizer _emails = new EmailNormalizer();

    [Fact]
    public async Task Shared_family_phone_does_not_enroll_sibling_on_first_party()
    {
        await using var db = CreateDb();
        var path = WriteWorkbook(
            ("Ravi Kumar", "9876543210", "ravi@gmail.com", "Math"),
            ("Raj Kumar", "9876543210", "raj@gmail.com", "Physics"));

        try
        {
            var report = await CreateImporter(db).ImportAsync(new StudentImportOptions
            {
                CompanyId = 1,
                FilePath = path,
                ImportedBy = "test"
            });

            Assert.True(report.Success, string.Join("; ", report.Messages));
            Assert.Equal(2, await db.Parties.CountAsync(p => p.CompanyId == 1));
            Assert.Equal(2, await db.CourseEnrollments.CountAsync());
            Assert.True(report.ReviewQueued > 0);

            var ravi = await db.Parties.SingleAsync(p => p.DisplayName == "Ravi Kumar");
            var raj = await db.Parties.SingleAsync(p => p.DisplayName == "Raj Kumar");
            Assert.NotEqual(ravi.Id, raj.Id);

            var math = await db.TrainingPrograms.SingleAsync(p => p.Name == "Math");
            var physics = await db.TrainingPrograms.SingleAsync(p => p.Name == "Physics");
            Assert.True(await db.CourseEnrollments.AnyAsync(e => e.PartyId == ravi.Id && e.ProgramId == math.Id));
            Assert.True(await db.CourseEnrollments.AnyAsync(e => e.PartyId == raj.Id && e.ProgramId == physics.Id));
            Assert.False(await db.CourseEnrollments.AnyAsync(e => e.PartyId == ravi.Id && e.ProgramId == physics.Id));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Same_phone_and_email_reuses_existing_student()
    {
        await using var db = CreateDb();
        var path = WriteWorkbook(
            ("Ravi Kumar", "9876543210", "ravi@gmail.com", "Math"),
            ("Ravi Kumar", "9876543210", "ravi@gmail.com", "Physics"));

        try
        {
            var report = await CreateImporter(db).ImportAsync(new StudentImportOptions
            {
                CompanyId = 1,
                FilePath = path,
                ImportedBy = "test"
            });

            Assert.True(report.Success, string.Join("; ", report.Messages));
            Assert.Equal(1, await db.Parties.CountAsync(p => p.CompanyId == 1));
            Assert.Equal(2, await db.CourseEnrollments.CountAsync());
            var partyId = await db.Parties.Select(p => p.Id).SingleAsync();
            Assert.Equal(2, await db.CourseEnrollments.CountAsync(e => e.PartyId == partyId));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private StudentImportService CreateImporter(ShaunErpDbContext db)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var match = new IdentityMatchService(db, _phones, _emails);
        var parties = new PartyIdentityService(db, match, _phones, _emails);
        return new StudentImportService(
            db, parties, match, _phones, _emails, tenant, NullLogger<StudentImportService>.Instance);
    }

    private static string WriteWorkbook(params (string Name, string Phone, string Email, string Courses)[] rows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"students-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Consolidated Registrations");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Phone";
        ws.Cell(1, 3).Value = "Email";
        ws.Cell(1, 4).Value = "Courses";
        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].Name;
            ws.Cell(i + 2, 2).Value = rows[i].Phone;
            ws.Cell(i + 2, 3).Value = rows[i].Email;
            ws.Cell(i + 2, 4).Value = rows[i].Courses;
        }

        wb.SaveAs(path);
        return path;
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "A" });
        db.SaveChanges();
        return db;
    }
}
