using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;

    public AuthController(IAuthService auth, IHostEnvironment env, IConfiguration config)
    {
        _auth = auth;
        _env = env;
        _config = config;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var allowRegister = _env.IsDevelopment()
            || string.Equals(_config["ALLOW_PUBLIC_REGISTER"], "true", StringComparison.OrdinalIgnoreCase);
        if (!allowRegister)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Public registration is disabled in this environment." });
        }

        var result = await _auth.RegisterAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAt,
            user = result.User
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (!result.Success)
        {
            return Unauthorized(new { error = result.Error });
        }

        return Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAt,
            user = result.User,
            companyId = result.CompanyId,
            companyName = result.CompanyName
        });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me([FromServices] ITenantContext tenant)
    {
        return Ok(new
        {
            id = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            username = User.Identity?.Name,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? User.FindFirst("email")?.Value,
            isSystemAdmin = tenant.IsSystemAdmin,
            companyId = tenant.CompanyId,
            companyName = tenant.CompanyName,
            entitlements = tenant.Entitlements.OrderBy(x => x),
            permissions = tenant.Permissions.OrderBy(x => x)
        });
    }
}
