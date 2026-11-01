using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Projects;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.ImportExport;
using ShaunERP.Infrastructure.Services.ImportExport.Importers;

namespace ShaunERP.Tests;

public class ProjectCodeUniquenessTests
{
    [Fact]
    public async Task CreateProject_RejectsDuplicateCode_CaseInsensitive()
    {
        var (projects, _) = await CreateServiceAsync();
        await projects.CreateProjectAsync(new Project { Name = "Website", ProjectCode = "ALPHA" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            projects.CreateProjectAsync(new Project { Name = "Mobile", ProjectCode = "alpha" }));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateProject_RejectsCodeTakenByAnotherProject()
    {
        var (projects, _) = await CreateServiceAsync();
        var first = await projects.CreateProjectAsync(new Project { Name = "One", ProjectCode = "PRJ-X" });
        var second = await projects.CreateProjectAsync(new Project { Name = "Two", ProjectCode = "PRJ-Y" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            projects.UpdateProjectAsync(second.Id, new Project { Name = second.Name, ProjectCode = "prj-x" }));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);

        var kept = await projects.UpdateProjectAsync(first.Id, new Project { Name = "One renamed", ProjectCode = "PRJ-X" });
        Assert.NotNull(kept);
        Assert.Equal("One renamed", kept!.Name);
        Assert.Equal("PRJ-X", kept.ProjectCode);
    }

    [Fact]
    public async Task CreateProject_AllowsSameCodeInAnotherTenant()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
        db.Users.AddRange(
            new User { Id = 10, Username = "one", Email = "one@example.com", FirstName = "One", LastName = "User", PasswordHash = "x" },
            new User { Id = 20, Username = "two", Email = "two@example.com", FirstName = "Two", LastName = "User", PasswordHash = "x" });
        db.CompanyUsers.AddRange(
            new CompanyUser { Id = 1, UserId = 10, CompanyId = 1 },
            new CompanyUser { Id = 2, UserId = 20, CompanyId = 2 });
        await db.SaveChangesAsync();

        var one = NewService(db, companyId: 1, userId: 10);
        var two = NewService(db, companyId: 2, userId: 20);
        await one.CreateProjectAsync(new Project { Name = "A site", ProjectCode = "SHARED" });
        var other = await two.CreateProjectAsync(new Project { Name = "B site", ProjectCode = "shared" });

        Assert.Equal("shared", other.ProjectCode);
    }

    [Fact]
    public async Task NextProjectNumber_SkipsManualCodeAlreadyUsed()
    {
        var (projects, _) = await CreateServiceAsync();
        var year = DateTime.UtcNow.Year;
        await projects.CreateProjectAsync(new Project
        {
            Name = "Manual first",
            ProjectCode = $"PRJ-{year}-000001"
        });

        var auto = await projects.CreateProjectAsync(new Project { Name = "Auto second" });

        Assert.Equal($"PRJ-{year}-000002", auto.ProjectCode);
    }

    [Fact]
    public async Task ProjectsCsvPreview_DoesNotCrashWhenCaseVariantCodesAlreadyExist()
    {
        var (_, db) = await CreateServiceAsync();
        db.Projects.AddRange(
            new Project { CompanyId = 1, Name = "First", ProjectCode = "WEB-1", CreatedDate = DateTime.UtcNow },
            new Project { CompanyId = 1, Name = "Second", ProjectCode = "web-1", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var importer = new ProjectsCsvImporter(db);
        var parsed = CsvParser.Parse("ProjectCode,Name\nWEB-1,Updated\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.True(preview.CanApply);
        Assert.Equal("Update", preview.Rows[0].Action);
        Assert.NotNull(preview.Rows[0].ExistingId);
    }

    [Fact]
    public async Task ProjectsCsvPreview_RejectsDuplicateCodeInTheSameFile()
    {
        var (_, db) = await CreateServiceAsync();
        var importer = new ProjectsCsvImporter(db);
        var parsed = CsvParser.Parse("ProjectCode,Name\nNEW-1,First\nnew-1,Second\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.Equal("Create", preview.Rows[0].Action);
        Assert.True(preview.Rows[0].IsValid);
        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProjectTasksCsvPreview_DoesNotCrashWhenCaseVariantProjectCodesExist()
    {
        var (_, db) = await CreateServiceAsync();
        db.Projects.AddRange(
            new Project { CompanyId = 1, Name = "First", ProjectCode = "WEB-1", CreatedDate = DateTime.UtcNow },
            new Project { CompanyId = 1, Name = "Second", ProjectCode = "web-1", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var importer = new ProjectTasksCsvImporter(db);
        var parsed = CsvParser.Parse("ProjectCode,TaskNumber,Name\nWEB-1,TSK-1,Kickoff\n");

        var preview = await importer.PreviewAsync(parsed, companyId: 1);

        Assert.True(preview.CanApply);
        Assert.Equal("Create", preview.Rows[0].Action);
        Assert.True(preview.Rows[0].IsValid);
    }

    private static async Task<(ProjectManagementService Projects, ShaunErpDbContext Db)> CreateServiceAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.Users.Add(new User
        {
            Id = 10,
            Username = "one",
            Email = "one@example.com",
            FirstName = "One",
            LastName = "User",
            PasswordHash = "x"
        });
        db.CompanyUsers.Add(new CompanyUser { Id = 1, UserId = 10, CompanyId = 1 });
        await db.SaveChangesAsync();
        return (NewService(db, companyId: 1, userId: 10), db);
    }

    private static ProjectManagementService NewService(ShaunErpDbContext db, int companyId, int userId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = userId,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Projects, ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var storageRoot = Path.Combine(Path.GetTempPath(), "shaunerp-tests", Guid.NewGuid().ToString("N"));
        return new ProjectManagementService(
            db, tenant, new ModuleRegistry(), new NoopAudit(), new ProjectFileStorage(storageRoot));
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null,
            string? entityId = null, string? beforeValues = null, string? afterValues = null,
            string? correlationId = null, string? ipAddress = null, string? userAgent = null,
            CancellationToken ct = default) => Task.CompletedTask;
    }
}
