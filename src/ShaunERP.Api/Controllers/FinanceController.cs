using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Finance;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finance")]
public class FinanceController : ControllerBase
{
    private readonly ShaunErpDbContext _db;

    public FinanceController(ShaunErpDbContext db) => _db = db;

    [HttpGet("books")]
    public async Task<ActionResult<IEnumerable<CompanyMaster>>> GetBooks(CancellationToken ct)
        => Ok(await _db.CompanyMasters.AsNoTracking().ToListAsync(ct));

    [HttpGet("accounts")]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts([FromQuery] int? companyMasterId, CancellationToken ct)
    {
        var q = _db.Accounts.AsNoTracking().AsQueryable();
        if (companyMasterId is not null)
        {
            q = q.Where(a => a.CompanyMasterId == companyMasterId);
        }

        return Ok(await q.OrderBy(a => a.AccountNumber).ToListAsync(ct));
    }

    [HttpGet("customers")]
    public async Task<ActionResult<IEnumerable<FinanceCustomer>>> GetCustomers([FromQuery] int companyMasterId, CancellationToken ct)
        => Ok(await _db.FinanceCustomers.AsNoTracking()
            .Where(c => c.CompanyMasterId == companyMasterId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct));

    [HttpPost("customers")]
    public async Task<ActionResult<FinanceCustomer>> CreateCustomer([FromBody] FinanceCustomer customer, CancellationToken ct)
    {
        customer.Id = 0;
        _db.FinanceCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return Ok(customer);
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices([FromQuery] int? companyMasterId, CancellationToken ct)
    {
        var q = _db.Invoices.AsNoTracking().Include(i => i.FinanceCustomer).AsQueryable();
        if (companyMasterId is not null)
        {
            q = q.Where(i => i.CompanyMasterId == companyMasterId);
        }

        return Ok(await q.OrderByDescending(i => i.Id).Take(200).ToListAsync(ct));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] Invoice invoice, CancellationToken ct)
    {
        invoice.Id = 0;
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
        {
            invoice.InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return Ok(invoice);
    }

    [HttpPost("invoices/{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FindAsync([id], ct);
        if (invoice is null) return NotFound();
        invoice.IsPaid = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("vendors")]
    public async Task<ActionResult<IEnumerable<Vendor>>> GetVendors([FromQuery] int companyMasterId, CancellationToken ct)
        => Ok(await _db.Vendors.AsNoTracking()
            .Where(v => v.CompanyMasterId == companyMasterId)
            .OrderBy(v => v.Name)
            .ToListAsync(ct));

    [HttpPost("vendors")]
    public async Task<ActionResult<Vendor>> CreateVendor([FromBody] Vendor vendor, CancellationToken ct)
    {
        vendor.Id = 0;
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync(ct);
        return Ok(vendor);
    }

    [HttpPost("bills")]
    public async Task<ActionResult<Bill>> CreateBill([FromBody] Bill bill, CancellationToken ct)
    {
        bill.Id = 0;
        if (string.IsNullOrWhiteSpace(bill.BillNumber))
        {
            bill.BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(ct);
        return Ok(bill);
    }
}
