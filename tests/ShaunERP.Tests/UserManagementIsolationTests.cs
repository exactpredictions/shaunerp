using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Tests;

public class UserManagementIsolationTests
{
    [Fact]
    public async Task Company_admin_cannot_create_user_in_another_company()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var users = CreateService(db, companyId: 1, isSystemAdmin: false);

        var result = await users.CreateUserAsync(new CreateUserRequest(
            "intruder",
            "intruder@example.com",
            "Password1!",
            "Other",
            "Tenant",
            null,
            CompanyId: 2,
            RoleName: "Admin"));

        Assert.False(result.Ok);
        Assert.Contains("own company", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.Users.AnyAsync(u => u.Username == "intruder"));
        Assert.False(await db.CompanyUsers.AnyAsync(cu =>
            cu.CompanyId == 2 && cu.User != null && cu.User.Username == "intruder"));
    }

    [Fact]
    public async Task Company_admin_create_defaults_to_current_company()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var users = CreateService(db, companyId: 1, isSystemAdmin: false);

        var result = await users.CreateUserAsync(new CreateUserRequest(
            "localuser",
            "local@example.com",
            "Password1!",
            "Local",
            "User",
            null,
            CompanyId: null,
            RoleName: "User"));

        Assert.True(result.Ok, result.Error);
        var createdUser = await db.Users.SingleAsync(u => u.Username == "localuser");
        var membership = await db.CompanyUsers.SingleAsync(cu => cu.UserId == createdUser.Id);
        Assert.Equal(1, membership.CompanyId);
    }

    [Fact]
    public async Task Company_admin_cannot_reset_password_of_other_tenant_user()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var victim = await db.Users.SingleAsync(u => u.Username == "b-admin");
        var originalHash = victim.PasswordHash;
        var users = CreateService(db, companyId: 1, isSystemAdmin: false);

        var result = await users.ResetPasswordAsync(victim.Id, new ResetPasswordRequest("HackedPass1!"));

        Assert.False(result.Ok);
        Assert.Equal("User not found.", result.Error);
        var reloaded = await db.Users.SingleAsync(u => u.Id == victim.Id);
        Assert.Equal(originalHash, reloaded.PasswordHash);
    }

    [Fact]
    public async Task Company_admin_cannot_deactivate_other_tenant_user()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var victim = await db.Users.SingleAsync(u => u.Username == "b-admin");
        var users = CreateService(db, companyId: 1, isSystemAdmin: false);

        var result = await users.UpdateUserAsync(victim.Id, new UpdateUserRequest(
            "Hacked",
            "User",
            "hacked@example.com",
            null,
            IsActive: false,
            CompanyId: 2,
            RoleName: "Admin"));

        Assert.False(result.Ok);
        Assert.Equal("User not found.", result.Error);
        var reloaded = await db.Users.SingleAsync(u => u.Id == victim.Id);
        Assert.True(reloaded.IsActive);
        Assert.Equal("b-admin@example.com", reloaded.Email);
        Assert.Equal("B", reloaded.FirstName);
    }

    [Fact]
    public async Task Company_admin_cannot_reassign_own_user_to_another_company()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var local = await db.Users.SingleAsync(u => u.Username == "a-user");
        var users = CreateService(db, companyId: 1, isSystemAdmin: false);

        var result = await users.UpdateUserAsync(local.Id, new UpdateUserRequest(
            "A",
            "User",
            "a-user@example.com",
            null,
            IsActive: true,
            CompanyId: 2,
            RoleName: "Admin"));

        Assert.False(result.Ok);
        Assert.Contains("own company", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.CompanyUsers.AnyAsync(cu => cu.UserId == local.Id && cu.CompanyId == 2));
    }

    [Fact]
    public async Task System_admin_can_still_manage_any_company()
    {
        await using var db = await SeedTwoCompaniesAsync();
        var users = CreateService(db, companyId: 1, isSystemAdmin: true);

        var created = await users.CreateUserAsync(new CreateUserRequest(
            "platform",
            "platform@example.com",
            "Password1!",
            "Plat",
            "Form",
            null,
            CompanyId: 2,
            RoleName: "User"));
        Assert.True(created.Ok, created.Error);
        Assert.Contains(created.User!.Companies, name => name == "Company B");

        var victim = await db.Users.SingleAsync(u => u.Username == "b-admin");
        var reset = await users.ResetPasswordAsync(victim.Id, new ResetPasswordRequest("ResetPass1!"));
        Assert.True(reset.Ok, reset.Error);
    }

    private static UserManagementService CreateService(ShaunErpDbContext db, int companyId, bool isSystemAdmin)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            CompanyName = companyId == 1 ? "Company A" : "Company B",
            IsSystemAdmin = isSystemAdmin,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Users },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new UserManagementService(db, tenant, new ModuleRegistry());
    }

    private static async Task<ShaunErpDbContext> SeedTwoCompaniesAsync()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "Company A" },
            new Company { Id = 2, Name = "Company B" });
        db.Users.AddRange(
            new User
            {
                Username = "a-user",
                Email = "a-user@example.com",
                FirstName = "A",
                LastName = "User",
                PasswordHash = "hash-a",
                IsActive = true
            },
            new User
            {
                Username = "b-admin",
                Email = "b-admin@example.com",
                FirstName = "B",
                LastName = "Admin",
                PasswordHash = "hash-b",
                IsActive = true
            });
        await db.SaveChangesAsync();

        var aUser = await db.Users.SingleAsync(u => u.Username == "a-user");
        var bAdmin = await db.Users.SingleAsync(u => u.Username == "b-admin");
        db.CompanyUsers.AddRange(
            new CompanyUser { UserId = aUser.Id, CompanyId = 1 },
            new CompanyUser { UserId = bAdmin.Id, CompanyId = 2 });
        await db.SaveChangesAsync();
        return db;
    }
}
