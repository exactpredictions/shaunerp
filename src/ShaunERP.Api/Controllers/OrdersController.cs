using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ISalesOrderService _orders;

    public OrdersController(ISalesOrderService orders) => _orders = orders;

    [HttpGet("dashboard")]
    public async Task<ActionResult<SalesOrderDashboardStats>> Dashboard(CancellationToken ct)
        => Ok(await _orders.GetDashboardAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SalesOrder>>> List(CancellationToken ct)
        => Ok(await _orders.ListOrdersAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SalesOrder>> Get(int id, CancellationToken ct)
    {
        var order = await _orders.GetOrderAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<SalesOrder>> Create([FromBody] SalesOrder order, CancellationToken ct)
    {
        try
        {
            var created = await _orders.CreateOrderAsync(order, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SalesOrder>> Update(int id, [FromBody] SalesOrder update, CancellationToken ct)
    {
        try
        {
            var updated = await _orders.UpdateOrderAsync(id, update, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            return await _orders.DeleteOrderAsync(id, ct) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<SalesOrder>> Confirm(int id, CancellationToken ct)
    {
        var order = await _orders.ConfirmAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/fulfill")]
    public async Task<ActionResult<SalesOrder>> Fulfill(int id, CancellationToken ct)
    {
        var order = await _orders.FulfillAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<SalesOrder>> Cancel(int id, CancellationToken ct)
    {
        var order = await _orders.CancelAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{id:int}/invoice")]
    public async Task<ActionResult<Invoice>> CreateInvoice(int id, CancellationToken ct)
    {
        var (ok, error, invoice) = await _orders.CreateInvoiceFromOrderAsync(id, ct);
        if (!ok)
            return BadRequest(new { error });
        return Ok(invoice);
    }

    [HttpPost("from-quote/{quoteId:int}")]
    public async Task<ActionResult<SalesOrder>> CreateFromQuote(int quoteId, CancellationToken ct)
    {
        try
        {
            var order = await _orders.CreateFromQuoteAsync(quoteId, ct);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
