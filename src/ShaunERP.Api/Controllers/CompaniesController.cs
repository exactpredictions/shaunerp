using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ShaunErpDbContext _db;

    public CompaniesController(ShaunErpDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var companies = await _db.Companies.AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Domain,
                c.Description,
                Modules = c.CompanyModules.Select(m => m.ModuleName)
            })
            .ToListAsync(ct);

        return Ok(companies);
    }
}
