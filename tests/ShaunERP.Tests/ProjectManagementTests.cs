using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Projects;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class ProjectManagementTests
{
    [Fact]
    public async Task CreateProject_AssignsCodeAndTracksTaskStatus()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);
        var service = NewService(db, companyId: 1, userId: 10);

        var project = await service.CreateProjectAsync(new Project
        {
            Name = "Website revamp",
            PlannedStartDate = new DateTime(2026, 1, 5),
            PlannedEndDate = new DateTime(2026, 3, 5),
            EstimatedHours = 100
        });

        Assert.True(project.Id > 0);
        Assert.Equal(1, project.CompanyId);
        Assert.StartsWith("PRJ-", project.ProjectCode);

        var task = await service.CreateTaskAsync(new ProjectTask
        {
            ProjectId = project.Id,
            Name = "Draft sitemap",
            EstimatedHours = 8
        });

        Assert.StartsWith("TASK-", task.TaskNumber);
        Assert.Equal(ProjectTaskStatus.Backlog, task.TaskStatus);

        var moved = await service.UpdateTaskStatusAsync(task.Id, ProjectTaskStatus.Completed);
        Assert.NotNull(moved);
        Assert.Equal(ProjectTaskStatus.Completed, moved!.TaskStatus);
        Assert.NotNull(moved.CompletedDate);
        Assert.Equal(100, moved.ProgressPercent);

        var refreshed = await service.GetProjectAsync(project.Id);
        Assert.Equal(100, refreshed!.ProgressPercent);
    }

    [Fact]
    public async Task AddDependency_RejectsCyclesAndSelfLinks()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);
        var service = NewService(db, companyId: 1, userId: 10);

        var project = await service.CreateProjectAsync(new Project { Name = "Rollout" });
        var a = await service.CreateTaskAsync(new ProjectTask { ProjectId = project.Id, Name = "A" });
        var b = await service.CreateTaskAsync(new ProjectTask { ProjectId = project.Id, Name = "B" });
        var c = await service.CreateTaskAsync(new ProjectTask { ProjectId = project.Id, Name = "C" });

        await service.AddDependencyAsync(a.Id, b.Id);
        await service.AddDependencyAsync(b.Id, c.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddDependencyAsync(a.Id, a.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddDependencyAsync(c.Id, a.Id));

        var blockedBy = await service.GetBlockedByAsync(c.Id);
        Assert.Single(blockedBy);
        Assert.Equal(b.Id, blockedBy[0].Id);
    }

    [Fact]
    public async Task Projects_AreIsolatedPerCompany()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);

        var companyOne = NewService(db, companyId: 1, userId: 10);
        var companyTwo = NewService(db, companyId: 2, userId: 20);

        var first = await companyOne.CreateProjectAsync(new Project { Name = "Tenant one project" });
        var second = await companyTwo.CreateProjectAsync(new Project { Name = "Tenant two project" });

        var visibleToOne = await companyOne.ListProjectsAsync();
        Assert.Single(visibleToOne);
        Assert.Equal(first.Id, visibleToOne[0].Id);

        Assert.Null(await companyOne.GetProjectAsync(second.Id));
        Assert.Null(await companyTwo.GetProjectAsync(first.Id));

        await companyOne.CreateTaskAsync(new ProjectTask { ProjectId = first.Id, Name = "Only mine" });
        var tasksForTwo = await companyTwo.ListTasksAsync();
        Assert.Empty(tasksForTwo.Items);
    }

    [Fact]
    public async Task CreateProjectFromSalesOrder_PrefillsCommercials()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);
        db.CrmCustomers.Add(new Customer { Id = 7, CompanyId = 1, CompanyName = "Acme Ltd" });
        db.SalesOrders.Add(new SalesOrder
        {
            Id = 55,
            CompanyId = 1,
            OrderNumber = "SO-0001",
            CustomerId = 7,
            CustomerName = "Acme Ltd",
            CurrencyCode = "USD",
            GrandTotal = 12500m,
            OrderDate = new DateTime(2026, 2, 1),
            RequestedDeliveryDate = new DateTime(2026, 4, 1),
            OrderStatus = SalesOrderStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = NewService(db, companyId: 1, userId: 10);
        var project = await service.CreateProjectFromSalesOrderAsync(55);

        Assert.Equal(55, project.SalesOrderId);
        Assert.Equal(7, project.CustomerId);
        Assert.Equal("USD", project.CurrencyCode);
        Assert.Equal(12500m, project.Budget);
        Assert.Equal(ProjectBillingType.FixedPrice, project.BillingType);
        Assert.Equal(ProjectStatus.Planned, project.ProjectStatus);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateProjectFromSalesOrderAsync(55));
    }

    [Fact]
    public async Task Timesheet_ApprovalRollsUpHoursToTask()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);
        var service = NewService(db, companyId: 1, userId: 10);

        var project = await service.CreateProjectAsync(new Project { Name = "Support retainer" });
        var task = await service.CreateTaskAsync(new ProjectTask { ProjectId = project.Id, Name = "Triage" });

        var entry = await service.CreateTimesheetAsync(new ProjectTimesheet
        {
            ProjectId = project.Id,
            TaskId = task.Id,
            Hours = 3.5m,
            BillableHours = 3.5m,
            WorkDate = new DateTime(2026, 2, 10)
        });

        await service.SubmitTimesheetAsync(entry.Id);
        var approved = await service.ApproveTimesheetAsync(entry.Id);

        Assert.NotNull(approved);
        Assert.Equal(TimesheetEntryStatus.Approved, approved!.EntryStatus);

        var refreshedTask = await service.GetTaskAsync(task.Id);
        Assert.Equal(3.5m, refreshedTask!.ActualHours);
    }

    [Fact]
    public async Task MissingPermission_BlocksProjectCreation()
    {
        await using var db = NewDb();
        await SeedCompaniesAsync(db);
        var service = NewService(db, companyId: 1, userId: 10, permissions: [Permissions.ProjectsAccess, Permissions.ProjectsView]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateProjectAsync(new Project { Name = "Not allowed" }));
    }

    private static ShaunErpDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedCompaniesAsync(ShaunErpDbContext db)
    {
        db.Companies.AddRange(new Company { Id = 1, Name = "Tenant one" }, new Company { Id = 2, Name = "Tenant two" });
        db.Users.AddRange(
            new User { Id = 10, Username = "one", Email = "one@example.com", FirstName = "One", LastName = "User", PasswordHash = "x" },
            new User { Id = 20, Username = "two", Email = "two@example.com", FirstName = "Two", LastName = "User", PasswordHash = "x" });
        db.CompanyUsers.AddRange(
            new CompanyUser { Id = 1, UserId = 10, CompanyId = 1 },
            new CompanyUser { Id = 2, UserId = 20, CompanyId = 2 });
        await db.SaveChangesAsync();
    }

    private static ProjectManagementService NewService(
        ShaunErpDbContext db,
        int companyId,
        int userId,
        IEnumerable<string>? permissions = null)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = userId,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Projects, ModuleKeys.Crm },
            Permissions = new HashSet<string>(permissions ?? Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
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
