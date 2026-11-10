using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class StudentImportTests
{
    [Fact]
    public async Task Second_workbook_does_not_enroll_new_row_against_leftover_student_row_party()
    {
        await using var db = CreateDb();
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var parties = new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails);

        var leftover = await parties.EnsurePersonPartyAsync(new EnsurePartyRequest
        {
            CompanyId = 1,
            FirstName = "Ravi",
            LastName = "Kumar",
            Phone = "9876543210",
            Mobile = "9876543210",
            Email = "ravi@example.com",
            CreateReviewOnAmbiguous = false
        });

        var priorBatch = new ImportBatch
        {
            CompanyId = 1,
            SourceName = "course-registrations",
            FileName = "old-students.xlsx",
            StartedAt = DateTime.UtcNow.AddDays(-1),
            BatchStatus = ImportBatchStatuses.Completed,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        };
        db.ImportBatches.Add(priorBatch);
        await db.SaveChangesAsync();
        db.ImportSourceRows.Add(new ImportSourceRow
        {
            ImportBatchId = priorBatch.Id,
            SourceRowNumber = 1,
            ExternalId = "student-row:1",
            MatchedPartyId = leftover.PartyId,
            MatchStatus = ImportMatchStatuses.Linked,
            ProcessedAt = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var path = WriteWorkbook("Meera Shah", "9123456789", "meera@example.com", "Numerology Foundation");
        try
        {
            var tenant = new TenantContext();
            tenant.Set(new TenantContextSnapshot
            {
                UserId = 10,
                CompanyId = 1,
                Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
                Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
            });

            var importer = new StudentImportService(
                db, parties, new IdentityMatchService(db, phones, emails),
                phones, emails, tenant, NullLogger<StudentImportService>.Instance);

            var report = await importer.ImportAsync(new StudentImportOptions
            {
                CompanyId = 1,
                FilePath = path
            });

            Assert.True(report.Success, string.Join("; ", report.Messages));
            Assert.Equal(1, report.EnrollmentsCreated);

            var enrollments = await db.CourseEnrollments.ToListAsync();
            Assert.Single(enrollments);
            Assert.NotEqual(leftover.PartyId, enrollments[0].PartyId);

            var meera = await db.Parties.SingleAsync(p => p.Id == enrollments[0].PartyId);
            Assert.Equal("Meera Shah", meera.DisplayName);
            Assert.Equal(0, await db.CourseEnrollments.CountAsync(e => e.PartyId == leftover.PartyId));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Reimport_of_same_student_reuses_party_by_phone_and_skips_duplicate_enrollment()
    {
        await using var db = CreateDb();
        var phones = new PhoneNormalizer();
        var emails = new EmailNormalizer();
        var parties = new PartyIdentityService(db, new IdentityMatchService(db, phones, emails), phones, emails);
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        var importer = new StudentImportService(
            db, parties, new IdentityMatchService(db, phones, emails),
            phones, emails, tenant, NullLogger<StudentImportService>.Instance);

        var path = WriteWorkbook("Meera Shah", "9123456789", "meera@example.com", "Numerology Foundation");
        try
        {
            var first = await importer.ImportAsync(new StudentImportOptions { CompanyId = 1, FilePath = path });
            Assert.True(first.Success);
            Assert.Equal(1, first.NewPartiesCreated);
            Assert.Equal(1, first.EnrollmentsCreated);

            var second = await importer.ImportAsync(new StudentImportOptions { CompanyId = 1, FilePath = path });
            Assert.True(second.Success);
            Assert.Equal(1, second.ExistingPartiesReused);
            Assert.Equal(0, second.NewPartiesCreated);
            Assert.Equal(1, second.EnrollmentsSkipped);
            Assert.Equal(1, await db.Parties.CountAsync(p => p.CompanyId == 1 && p.PartyType == PartyTypes.Person));
            Assert.Equal(1, await db.CourseEnrollments.CountAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteWorkbook(string name, string phone, string email, string courses)
    {
        var path = Path.Combine(Path.GetTempPath(), $"students-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Consolidated Registrations");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Phone";
        ws.Cell(1, 3).Value = "Email";
        ws.Cell(1, 4).Value = "Courses";
        ws.Cell(2, 1).Value = name;
        ws.Cell(2, 2).Value = phone;
        ws.Cell(2, 3).Value = email;
        ws.Cell(2, 4).Value = courses;
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
