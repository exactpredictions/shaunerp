using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task CrmService_DoesNotLeakOtherTenantLeads()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "A" },
            new Company { Id = 2, Name = "B" });
        db.Leads.AddRange(
            new Lead { Id = 1, CompanyId = 1, FirstName = "A1", LastName = "Lead" },
            new Lead { Id = 2, CompanyId = 2, FirstName = "B1", LastName = "Lead" });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var crm = new CrmService(db, tenant, new ModuleRegistry(),
            new PartyIdentityService(db, new IdentityMatchService(db, new PhoneNormalizer(), new EmailNormalizer()),
                new PhoneNormalizer(), new EmailNormalizer()));
        var leads = await crm.ListLeadsAsync();
        Assert.Single(leads);
        Assert.Equal(1, leads[0].CompanyId);
        Assert.Null(await crm.GetLeadAsync(2));
    }

    [Fact]
    public async Task FinanceService_DoesNotLeakOtherTenantInvoices()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "A" },
            new Company { Id = 2, Name = "B" });
        db.CompanyMasters.AddRange(
            new CompanyMaster { Id = 10, TenantCompanyId = 1, Name = "Book A", InvoiceNumberPrefix = "A-", NextInvoiceSequence = 1 },
            new CompanyMaster { Id = 20, TenantCompanyId = 2, Name = "Book B", InvoiceNumberPrefix = "B-", NextInvoiceSequence = 1 });
        db.Invoices.AddRange(
            new Invoice
            {
                Id = 1, CompanyId = 1, CompanyMasterId = 10, InvoiceNumber = "A-0001",
                GrandTotal = 100, Amount = 100, InvoiceStatus = InvoiceStatus.Sent
            },
            new Invoice
            {
                Id = 2, CompanyId = 2, CompanyMasterId = 20, InvoiceNumber = "B-0001",
                GrandTotal = 200, Amount = 200, InvoiceStatus = InvoiceStatus.Sent
            });
        await db.SaveChangesAsync();

        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = 1,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Finance, ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });

        var registry = new ModuleRegistry();
        var audit = new NoopAudit();
        var posting = new AccountingPostingService(db, tenant, registry, audit);
        var accounting = new AccountingService(db, tenant, registry, posting, audit);
        var finance = new FinanceService(db, tenant, registry, posting, accounting);
        var invoices = await finance.ListInvoicesAsync();
        Assert.Single(invoices);
        Assert.Equal(1, invoices[0].CompanyId);
        Assert.Null(await finance.GetInvoiceAsync(2));
    }

    [Fact]
    public async Task AccountSwitch_RequiresMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var db = new ShaunErpDbContext(options))
        {
            db.Users.Add(new User
            {
                Id = 5,
                Username = "u",
                Email = "u@example.com",
                FirstName = "U",
                LastName = "Ser",
                PasswordHash = "x",
                IsActive = true
            });
            db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
            db.CompanyUsers.Add(new CompanyUser { Id = 1, UserId = 5, CompanyId = 1 });
            await db.SaveChangesAsync();
        }

        var access = new AccountAccessService(new TestDbFactory(options));
        Assert.True(await access.BelongsToAsync(5, 1));
        Assert.False(await access.BelongsToAsync(5, 2));
    }

    [Fact]
    public async Task CompanyAdmin_CannotResetPasswordOfOtherTenantUser()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error) = await users.ResetPasswordAsync(20, new ResetPasswordRequest("NewPassword1"));
        Assert.False(ok);
        Assert.Equal("User not found.", error);
        Assert.Equal("old-hash-b", (await db.Users.FindAsync(20))!.PasswordHash);
    }

    [Fact]
    public async Task CompanyAdmin_CannotUpdateOtherTenantUser()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error, updated) = await users.UpdateUserAsync(20, new UpdateUserRequest(
            "Hacked", "User", "bob@b.example", null, false, 2, "Admin"));
        Assert.False(ok);
        Assert.Equal("User not found.", error);
        Assert.Null(updated);
        var bob = await db.Users.FindAsync(20);
        Assert.Equal("Bob", bob!.FirstName);
        Assert.True(bob.IsActive);
    }

    [Fact]
    public async Task CompanyAdmin_CannotCreateUserInOtherCompany()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error, created) = await users.CreateUserAsync(new CreateUserRequest(
            "intruder", "intruder@b.example", "Password1!", "In", "Truder", null, 2, "Admin"));
        Assert.False(ok);
        Assert.Equal("Cannot create a user in another company.", error);
        Assert.Null(created);
        Assert.False(await db.Users.AnyAsync(u => u.Username == "intruder"));
    }

    [Fact]
    public async Task CompanyAdmin_CannotAssignLocalUserToOtherCompany()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error, _) = await users.UpdateUserAsync(10, new UpdateUserRequest(
            "Alice", "Admin", "alice@a.example", null, true, 2, "Admin"));
        Assert.False(ok);
        Assert.Equal("Cannot assign a user to another company.", error);
        Assert.False(await db.CompanyUsers.AnyAsync(cu => cu.UserId == 10 && cu.CompanyId == 2));
    }

    [Fact]
    public async Task CompanyAdmin_CreatesUserInOwnCompanyWhenCompanyIdOmitted()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error, created) = await users.CreateUserAsync(new CreateUserRequest(
            "newalice", "newalice@a.example", "Password1!", "New", "Alice", null, null, "User"));
        Assert.True(ok, error);
        Assert.NotNull(created);
        var membership = await db.CompanyUsers.SingleAsync(cu => cu.UserId == created!.Id);
        Assert.Equal(1, membership.CompanyId);
        Assert.False(await db.CompanyUsers.AnyAsync(cu => cu.UserId == created.Id && cu.CompanyId == 2));
    }

    [Fact]
    public async Task CompanyAdmin_CanResetPasswordOfOwnTenantUser()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: false);

        var (ok, error) = await users.ResetPasswordAsync(10, new ResetPasswordRequest("NewPassword1"));
        Assert.True(ok, error);
        var alice = await db.Users.FindAsync(10);
        Assert.NotEqual("old-hash-a", alice!.PasswordHash);
        Assert.True(alice.MustChangePassword);
    }

    [Fact]
    public async Task SystemAdmin_CanResetPasswordOfAnyTenantUser()
    {
        await using var db = await SeedTwoTenantUsersAsync();
        var users = CreateUserManagement(db, companyId: 1, systemAdmin: true);

        var (ok, error) = await users.ResetPasswordAsync(20, new ResetPasswordRequest("NewPassword1"));
        Assert.True(ok, error);
        Assert.NotEqual("old-hash-b", (await db.Users.FindAsync(20))!.PasswordHash);
    }

    private static UserManagementService CreateUserManagement(
        ShaunErpDbContext db, int companyId, bool systemAdmin)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            IsSystemAdmin = systemAdmin,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Users },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new UserManagementService(db, tenant, new ModuleRegistry());
    }

    private static async Task<ShaunErpDbContext> SeedTwoTenantUsersAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(new Company { Id = 1, Name = "A" }, new Company { Id = 2, Name = "B" });
        db.Users.AddRange(
            new User
            {
                Id = 10,
                Username = "alice",
                Email = "alice@a.example",
                FirstName = "Alice",
                LastName = "Admin",
                PasswordHash = "old-hash-a",
                IsActive = true
            },
            new User
            {
                Id = 20,
                Username = "bob",
                Email = "bob@b.example",
                FirstName = "Bob",
                LastName = "Admin",
                PasswordHash = "old-hash-b",
                IsActive = true
            });
        db.CompanyUsers.AddRange(
            new CompanyUser { Id = 100, UserId = 10, CompanyId = 1 },
            new CompanyUser { Id = 200, UserId = 20, CompanyId = 2 });
        await db.SaveChangesAsync();
        return db;
    }

    private sealed class TestDbFactory(DbContextOptions<ShaunErpDbContext> options) : IDbContextFactory<ShaunErpDbContext>
    {
        public ShaunErpDbContext CreateDbContext() => new(options);
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
