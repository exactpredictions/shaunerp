using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using System.Security.Claims;

namespace ShaunERP.Tests;

public class UsernameUniquenessTests
{
    [Fact]
    public async Task Register_rejects_username_that_differs_only_by_case()
    {
        await using var db = CreateDb();
        var auth = CreateAuth(db);

        var first = await auth.RegisterAsync(new RegisterRequest(
            "Admin", "a@example.com", "PasswordA1", "Ann", "Admin", null));
        Assert.True(first.Success, first.Error);

        var second = await auth.RegisterAsync(new RegisterRequest(
            "admin", "b@example.com", "PasswordB1", "Bob", "Admin", null));
        Assert.False(second.Success);
        Assert.Contains("already exists", second.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Login_still_matches_the_single_account_case_insensitively()
    {
        await using var db = CreateDb();
        var auth = CreateAuth(db);

        var registered = await auth.RegisterAsync(new RegisterRequest(
            "Admin", "a@example.com", "PasswordA1", "Ann", "Admin", null));
        Assert.True(registered.Success, registered.Error);

        var login = await auth.LoginAsync(new LoginRequest("admin", "PasswordA1"));
        Assert.True(login.Success, login.Error);
        Assert.Equal("Admin", login.User!.Username);
    }

    [Fact]
    public async Task Failed_login_on_case_variant_locks_the_existing_account_not_a_shadow()
    {
        await using var db = CreateDb();
        var auth = CreateAuth(db);

        var registered = await auth.RegisterAsync(new RegisterRequest(
            "Admin", "a@example.com", "PasswordA1", "Ann", "Admin", null));
        Assert.True(registered.Success, registered.Error);

        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            var fail = await auth.LoginAsync(new LoginRequest("admin", "wrong-password"));
            Assert.False(fail.Success);
        }

        var locked = await auth.LoginAsync(new LoginRequest("Admin", "PasswordA1"));
        Assert.False(locked.Success);
        Assert.Contains("locked", locked.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_rejects_username_that_differs_only_by_case()
    {
        await using var db = CreateDb();
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        await db.SaveChangesAsync();

        var users = CreateUsers(db);
        var first = await users.CreateUserAsync(new CreateUserRequest(
            "JSmith", "jsmith@example.com", "PasswordA1", "Jane", "Smith",
            null, 1, "User"));
        Assert.True(first.Ok, first.Error);

        var second = await users.CreateUserAsync(new CreateUserRequest(
            "jsmith", "other@example.com", "PasswordB1", "John", "Smith",
            null, 1, "User"));
        Assert.False(second.Ok);
        Assert.Contains("already exists", second.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ShaunErpDbContext(options);
    }

    private static AuthService CreateAuth(ShaunErpDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32CHARS",
                ["Jwt:Issuer"] = "shaunerp",
                ["Jwt:Audience"] = "shaunerp"
            })
            .Build();
        return new AuthService(db, config, new StubAuthz(), new NoopAudit());
    }

    private static UserManagementService CreateUsers(ShaunErpDbContext db)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 99,
            CompanyId = 1,
            IsSystemAdmin = true,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Users },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new UserManagementService(db, tenant, new ModuleRegistry());
    }

    private sealed class StubAuthz : IAuthorizationContextService
    {
        public Task<TenantContextSnapshot> BuildForUserAsync(int userId, int? preferredCompanyId = null, CancellationToken ct = default)
            => Task.FromResult(new TenantContextSnapshot { UserId = userId, CompanyId = preferredCompanyId });

        public Task<TenantContextSnapshot> BuildFromPrincipalAsync(ClaimsPrincipal principal, CancellationToken ct = default)
            => Task.FromResult(new TenantContextSnapshot());

        public TenantContextSnapshot BuildFromClaims(ClaimsPrincipal principal) => new();

        public IEnumerable<Claim> ToClaims(TenantContextSnapshot snapshot, User user)
            => [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())];
    }

    private sealed class NoopAudit : IAuditService
    {
        public Task WriteAsync(string action, int? companyId = null, int? userId = null, string? entityType = null, string? entityId = null, string? beforeValues = null, string? afterValues = null, string? correlationId = null, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
