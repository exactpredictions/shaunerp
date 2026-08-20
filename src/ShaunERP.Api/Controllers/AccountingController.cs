using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Core.Accounting;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounting")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accounting;
    private readonly IAccountingPostingService _posting;
    private readonly IAccountingReportService _reports;

    public AccountingController(
        IAccountingService accounting,
        IAccountingPostingService posting,
        IAccountingReportService reports)
    {
        _accounting = accounting;
        _posting = posting;
        _reports = reports;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AccountingDashboardStats>> Dashboard(CancellationToken ct)
        => Ok(await _accounting.GetDashboardAsync(ct));

    [HttpGet("ledgers")]
    public async Task<ActionResult<IEnumerable<Ledger>>> ListLedgers([FromQuery] string? search, CancellationToken ct)
        => Ok(await _accounting.ListLedgersAsync(search, ct));

    [HttpGet("ledgers/{id:int}")]
    public async Task<ActionResult<Ledger>> GetLedger(int id, CancellationToken ct)
    {
        var ledger = await _accounting.GetLedgerAsync(id, ct);
        return ledger is null ? NotFound() : Ok(ledger);
    }

    [HttpGet("vouchers")]
    public async Task<ActionResult<IEnumerable<AccountingVoucher>>> ListVouchers(
        [FromQuery] string? typeCode, CancellationToken ct)
        => Ok(await _accounting.ListVouchersAsync(typeCode, ct));

    [HttpGet("vouchers/{id:int}")]
    public async Task<ActionResult<AccountingVoucher>> GetVoucher(int id, CancellationToken ct)
    {
        var voucher = await _accounting.GetVoucherAsync(id, ct);
        return voucher is null ? NotFound() : Ok(voucher);
    }

    [HttpPost("vouchers")]
    public async Task<ActionResult<AccountingVoucher>> PostVoucher(
        [FromBody] AccountingPostingRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _posting.PostAsync(request, ct);
            return CreatedAtAction(nameof(GetVoucher), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("vouchers/{id:int}/post")]
    public async Task<ActionResult<AccountingVoucher>> PostDraft(int id, CancellationToken ct)
    {
        try
        {
            return Ok(await _posting.PostDraftAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("vouchers/{id:int}/reverse")]
    public async Task<ActionResult<AccountingVoucher>> Reverse(int id, [FromQuery] string? reason, CancellationToken ct)
    {
        try
        {
            return Ok(await _posting.ReverseAsync(id, reason, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("reports/trial-balance")]
    public async Task<ActionResult<TrialBalanceReport>> TrialBalance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _reports.GetTrialBalanceAsync(from, to, ct));

    [HttpGet("reports/profit-loss")]
    public async Task<ActionResult<ProfitLossReport>> ProfitLoss(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await _reports.GetProfitAndLossAsync(from, to, ct));

    [HttpGet("reports/balance-sheet")]
    public async Task<ActionResult<BalanceSheetReport>> BalanceSheet(
        [FromQuery] DateTime? asOf, CancellationToken ct)
        => Ok(await _reports.GetBalanceSheetAsync(asOf, ct));
}
