using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users) => _users = users;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserListItem>>> List(CancellationToken ct)
        => Ok(await _users.ListUsersAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserListItem>> Get(int id, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var (ok, error, user) = await _users.CreateUserAsync(request, ct);
        if (!ok)
        {
            return BadRequest(new { error });
        }

        return CreatedAtAction(nameof(Get), new { id = user!.Id }, user);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var (ok, error, user) = await _users.UpdateUserAsync(id, request, ct);
        if (!ok)
        {
            return error == "User not found." ? NotFound(new { error }) : BadRequest(new { error });
        }

        return Ok(user);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var (ok, error) = await _users.ResetPasswordAsync(id, request, ct);
        if (!ok)
        {
            return error == "User not found." ? NotFound(new { error }) : BadRequest(new { error });
        }

        return NoContent();
    }
}
