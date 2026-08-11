using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Core.Finance;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finance")]
public class FinanceController : ControllerBase
{
    private readonly IFinanceService _finance;

    public FinanceController(IFinanceService finance) => _finance = finance;

    [HttpGet("dashboard")]
    public async Task<ActionResult<FinanceDashboardStats>> Dashboard(CancellationToken ct)
        => Ok(await _finance.GetDashboardAsync(ct));

    [HttpGet("book")]
    public async Task<ActionResult<CompanyMaster>> GetBook(CancellationToken ct)
        => Ok(await _finance.GetOrCreateBillingBookAsync(ct));

    [HttpPut("book")]
    public async Task<ActionResult<CompanyMaster>> UpdateBook([FromBody] CompanyMaster update, CancellationToken ct)
    {
        var book = await _finance.UpdateBillingBookAsync(update, ct);
        return book is null ? NotFound() : Ok(book);
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers(CancellationToken ct)
        => Ok(await _finance.ListCrmCustomersAsync(ct));

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(CancellationToken ct)
        => Ok(await _finance.ListProductsAsync(ct));

    [HttpGet("invoices")]
    public async Task<ActionResult<IEnumerable<Invoice>>> GetInvoices(CancellationToken ct)
        => Ok(await _finance.ListInvoicesAsync(ct));

    [HttpGet("invoices/{id:int}")]
    public async Task<ActionResult<Invoice>> GetInvoice(int id, CancellationToken ct)
    {
        var invoice = await _finance.GetInvoiceAsync(id, ct);
        return invoice is null ? NotFound() : Ok(invoice);
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] Invoice invoice, CancellationToken ct)
    {
        var created = await _finance.CreateInvoiceAsync(invoice, ct);
        return CreatedAtAction(nameof(GetInvoice), new { id = created.Id }, created);
    }

    [HttpPut("invoices/{id:int}")]
    public async Task<IActionResult> UpdateInvoice(int id, [FromBody] Invoice update, CancellationToken ct)
    {
        try
        {
            var updated = await _finance.UpdateInvoiceAsync(id, update, ct);
            return updated is null ? NotFound() : NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("invoices/{id:int}")]
    public async Task<IActionResult> DeleteInvoice(int id, CancellationToken ct)
    {
        try
        {
            return await _finance.DeleteInvoiceAsync(id, ct) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("invoices/{id:int}/send")]
    public async Task<ActionResult<Invoice>> MarkSent(int id, CancellationToken ct)
    {
        try
        {
            var invoice = await _finance.MarkSentAsync(id, ct);
            return invoice is null ? NotFound() : Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("invoices/{id:int}/void")]
    public async Task<ActionResult<Invoice>> VoidInvoice(int id, CancellationToken ct)
    {
        try
        {
            var invoice = await _finance.VoidInvoiceAsync(id, ct);
            return invoice is null ? NotFound() : Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("invoices/{id:int}/mark-paid")]
    public async Task<ActionResult<Invoice>> MarkPaid(int id, CancellationToken ct)
    {
        try
        {
            var invoice = await _finance.MarkPaidAsync(id, ct);
            return invoice is null ? NotFound() : Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("payments")]
    public async Task<ActionResult<IEnumerable<InvoicePayment>>> GetPayments(CancellationToken ct)
        => Ok(await _finance.ListPaymentsAsync(ct));

    [HttpPost("invoices/{id:int}/payments")]
    public async Task<ActionResult<InvoicePayment>> RecordPayment(
        int id, [FromBody] InvoicePayment payment, CancellationToken ct)
    {
        var (ok, error, created) = await _finance.RecordPaymentAsync(id, payment, ct);
        if (!ok) return BadRequest(new { error });
        return Ok(created);
    }

    [HttpDelete("payments/{id:int}")]
    public async Task<IActionResult> DeletePayment(int id, CancellationToken ct)
        => await _finance.DeletePaymentAsync(id, ct) ? NoContent() : NotFound();
}
