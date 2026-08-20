using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using System.Security.Claims;

namespace ShaunERP.Tests;

public class AuthLockoutTests
{
    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        await using var db = CreateDb();
        var auth = CreateAuth(db);
        db.Users.Add(new User
        {
            Username = "lockme",
            Email = "lockme@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-horse"),
            IsActive = true
        });
        await db.SaveChangesAsync();

        for (var i = 0; i < AuthService.MaxFailedLogins; i++)
        {
            var fail = await auth.LoginAsync(new LoginRequest("lockme", "wrong"));
            Assert.False(fail.Success);
        }

        var locked = await auth.LoginAsync(new LoginRequest("lockme", "correct-horse"));
        Assert.False(locked.Success);
        Assert.Contains("locked", locked.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Successful_login_resets_failed_count()
    {
        await using var db = CreateDb();
        var auth = CreateAuth(db);
        db.Users.Add(new User
        {
            Username = "okuser",
            Email = "ok@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct-horse"),
            IsActive = true,
            FailedLoginCount = 3
        });
        await db.SaveChangesAsync();

        var ok = await auth.LoginAsync(new LoginRequest("okuser", "correct-horse"));
        Assert.True(ok.Success, ok.Error);
        var user = await db.Users.SingleAsync();
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEnd);
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
