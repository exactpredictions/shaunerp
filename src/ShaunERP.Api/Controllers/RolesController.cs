using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IUserManagementService _users;

    public RolesController(IUserManagementService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> List([FromQuery] int? companyId, CancellationToken ct)
        => Ok(await _users.ListRolesAsync(companyId, ct));
}
