using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public record AuthResult(bool Success, string? Token, DateTime? ExpiresAt, string? Error, UserDto? User, int? CompanyId, string? CompanyName);

public record UserDto(int Id, string Username, string Email, string FirstName, string LastName, bool IsSystemAdmin);

public record RegisterRequest(string Username, string Email, string Password, string FirstName, string LastName, string? CompanyName);

public record LoginRequest(string UsernameOrEmail, string Password, int? CompanyId = null);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly ShaunErpDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuthorizationContextService _authz;
    private readonly IAuditService _audit;

    public AuthService(
        ShaunErpDbContext db,
        IConfiguration config,
        IAuthorizationContextService authz,
        IAuditService audit)
    {
        _db = db;
        _config = config;
        _authz = authz;
        _audit = audit;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var exists = await _db.Users.AnyAsync(
            u => u.Username == request.Username || u.Email == request.Email, ct);
        if (exists)
        {
            return new AuthResult(false, null, null, "Username or email already exists.", null, null, null);
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            IsSystemAdmin = !await _db.Users.AnyAsync(ct)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        int? companyId = null;
        string? companyName = null;
        if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var company = new Company { Name = request.CompanyName.Trim() };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);
            companyId = company.Id;
            companyName = company.Name;

            _db.CompanyUsers.Add(new CompanyUser { UserId = user.Id, CompanyId = company.Id });
            _db.CompanyModules.AddRange(
                CreateModule(company.Id, ModuleKeys.Crm, "CRM"),
                CreateModule(company.Id, ModuleKeys.Finance, "Finance"),
                CreateModule(company.Id, ModuleKeys.Orders, "Sales Orders"),
                CreateModule(company.Id, ModuleKeys.Accounting, "Accounting"),
                CreateModule(company.Id, ModuleKeys.Hrms, "HRMS"),
                CreateModule(company.Id, ModuleKeys.Projects, "Projects"),
                CreateModule(company.Id, ModuleKeys.Users, "Users"));
            await _db.SaveChangesAsync(ct);

            var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && r.CompanyId == null, ct);
            if (adminRole is not null)
            {
                var companyUser = await _db.CompanyUsers.FirstAsync(cu => cu.UserId == user.Id && cu.CompanyId == company.Id, ct);
                _db.UserRoles.Add(new UserRole { CompanyUserId = companyUser.Id, RoleId = adminRole.Id });
                await _db.SaveChangesAsync(ct);
            }
        }

        return await IssueTokenAsync(user, companyId, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var key = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == key || u.Email.ToLower() == key, ct);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthResult(false, null, null, "Invalid credentials.", null, null, null);
        }

        var result = await IssueTokenAsync(user, request.CompanyId, ct);
        if (result.Success)
        {
            await _audit.WriteAsync(
                "auth.login",
                companyId: result.CompanyId,
                userId: user.Id,
                entityType: "User",
                entityId: user.Id.ToString(),
                ct: ct);
        }

        return result;
    }

    private async Task<AuthResult> IssueTokenAsync(User user, int? preferredCompanyId, CancellationToken ct)
    {
        var snapshot = await _authz.BuildForUserAsync(user.Id, preferredCompanyId, ct);
        var jwt = _config.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
        var issuer = jwt["Issuer"] ?? "shaunerp";
        var audience = jwt["Audience"] ?? "shaunerp";
        var expires = DateTime.UtcNow.AddHours(12);

        var claims = _authz.ToClaims(snapshot, user).ToList();
        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, user.Username));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        _db.Sessions.Add(new Session
        {
            UserId = user.Id,
            CompanyId = snapshot.CompanyId,
            Token = encoded,
            ExpiryDate = expires,
            IsActive = true
        });
        await _db.SaveChangesAsync(ct);

        var dto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.IsSystemAdmin);
        return new AuthResult(true, encoded, expires, null, dto, snapshot.CompanyId, snapshot.CompanyName);
    }

    private static CompanyModule CreateModule(int companyId, string key, string name) => new()
    {
        CompanyId = companyId,
        ModuleKey = key,
        ModuleName = name,
        IsEnabled = true
    };
}
