using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;

    public CompaniesController(ShaunErpDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var query = _db.Companies.AsNoTracking().AsQueryable();
        if (!_tenant.IsSystemAdmin)
        {
            if (_tenant.CompanyId is not int companyId)
            {
                return Ok(Array.Empty<object>());
            }

            query = query.Where(c => c.Id == companyId);
        }

        var companies = await query
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
