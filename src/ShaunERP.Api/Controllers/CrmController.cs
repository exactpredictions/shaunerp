using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Core.Crm;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/crm")]
public class CrmController : ControllerBase
{
    private readonly ICrmService _crm;

    public CrmController(ICrmService crm) => _crm = crm;

    [HttpGet("dashboard")]
    public async Task<ActionResult<CrmDashboardStats>> Dashboard(CancellationToken ct)
        => Ok(await _crm.GetDashboardAsync(ct));

    [HttpGet("pipeline")]
    public async Task<ActionResult<CrmPipelineBoard>> Pipeline(CancellationToken ct)
        => Ok(await _crm.GetPipelineBoardAsync(ct));

    [HttpGet("forecast")]
    public async Task<ActionResult<CrmForecastStats>> Forecast(CancellationToken ct)
        => Ok(await _crm.GetForecastAsync(ct));

    // —— Leads ——
    [HttpGet("leads")]
    public async Task<ActionResult<IEnumerable<Lead>>> GetLeads(CancellationToken ct)
        => Ok(await _crm.ListLeadsAsync(ct));

    [HttpGet("leads/{id:int}")]
    public async Task<ActionResult<Lead>> GetLead(int id, CancellationToken ct)
    {
        var lead = await _crm.GetLeadAsync(id, ct);
        return lead is null ? NotFound() : Ok(lead);
    }

    [HttpPost("leads")]
    public async Task<ActionResult<Lead>> CreateLead([FromBody] Lead lead, CancellationToken ct)
    {
        var created = await _crm.CreateLeadAsync(lead, ct);
        return CreatedAtAction(nameof(GetLead), new { id = created.Id }, created);
    }

    [HttpPut("leads/{id:int}")]
    public async Task<IActionResult> UpdateLead(int id, [FromBody] Lead update, CancellationToken ct)
    {
        var updated = await _crm.UpdateLeadAsync(id, update, ct);
        return updated is null ? NotFound() : NoContent();
    }

    [HttpDelete("leads/{id:int}")]
    public async Task<IActionResult> DeleteLead(int id, CancellationToken ct)
        => await _crm.DeleteLeadAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("leads/{id:int}/convert")]
    public async Task<ActionResult<LeadConversionResult>> ConvertLead(
        int id, [FromBody] LeadConversionRequest? request, CancellationToken ct)
    {
        request ??= new LeadConversionRequest();
        var (ok, error, result) = await _crm.ConvertLeadAsync(id, request, ct);
        if (!ok) return BadRequest(new { error });
        return Ok(result);
    }

    [HttpPost("leads/{id:int}/convert-prospect")]
    public async Task<ActionResult<Prospect>> ConvertLeadToProspect(int id, CancellationToken ct)
    {
        var (ok, error, prospect) = await _crm.ConvertLeadToProspectAsync(id, ct);
        if (!ok) return BadRequest(new { error });
        return Ok(prospect);
    }

    // —— Prospects ——
    [HttpGet("prospects")]
    public async Task<ActionResult<IEnumerable<Prospect>>> GetProspects(CancellationToken ct)
        => Ok(await _crm.ListProspectsAsync(ct));

    [HttpGet("prospects/{id:int}")]
    public async Task<ActionResult<Prospect>> GetProspect(int id, CancellationToken ct)
    {
        var row = await _crm.GetProspectAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("prospects")]
    public async Task<ActionResult<Prospect>> CreateProspect([FromBody] Prospect prospect, CancellationToken ct)
    {
        var created = await _crm.CreateProspectAsync(prospect, ct);
        return CreatedAtAction(nameof(GetProspect), new { id = created.Id }, created);
    }

    [HttpPut("prospects/{id:int}")]
    public async Task<IActionResult> UpdateProspect(int id, [FromBody] Prospect update, CancellationToken ct)
        => await _crm.UpdateProspectAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("prospects/{id:int}")]
    public async Task<IActionResult> DeleteProspect(int id, CancellationToken ct)
        => await _crm.DeleteProspectAsync(id, ct) ? NoContent() : NotFound();

    // —— Accounts ——
    [HttpGet("accounts")]
    public async Task<ActionResult<IEnumerable<Account>>> GetAccounts(CancellationToken ct)
        => Ok(await _crm.ListAccountsAsync(ct));

    [HttpGet("accounts/{id:int}")]
    public async Task<ActionResult<Account>> GetAccount(int id, CancellationToken ct)
    {
        var row = await _crm.GetAccountAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<Account>> CreateAccount([FromBody] Account account, CancellationToken ct)
    {
        var created = await _crm.CreateAccountAsync(account, ct);
        return CreatedAtAction(nameof(GetAccount), new { id = created.Id }, created);
    }

    [HttpPut("accounts/{id:int}")]
    public async Task<IActionResult> UpdateAccount(int id, [FromBody] Account update, CancellationToken ct)
        => await _crm.UpdateAccountAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
        => await _crm.DeleteAccountAsync(id, ct) ? NoContent() : NotFound();

    // —— Contacts ——
    [HttpGet("contacts")]
    public async Task<ActionResult<IEnumerable<Contact>>> GetContacts(CancellationToken ct)
        => Ok(await _crm.ListContactsAsync(ct));

    [HttpGet("contacts/{id:int}")]
    public async Task<ActionResult<Contact>> GetContact(int id, CancellationToken ct)
    {
        var row = await _crm.GetContactAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("contacts")]
    public async Task<ActionResult<Contact>> CreateContact([FromBody] Contact contact, CancellationToken ct)
    {
        var created = await _crm.CreateContactAsync(contact, ct);
        return CreatedAtAction(nameof(GetContact), new { id = created.Id }, created);
    }

    [HttpPut("contacts/{id:int}")]
    public async Task<IActionResult> UpdateContact(int id, [FromBody] Contact update, CancellationToken ct)
        => await _crm.UpdateContactAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("contacts/{id:int}")]
    public async Task<IActionResult> DeleteContact(int id, CancellationToken ct)
        => await _crm.DeleteContactAsync(id, ct) ? NoContent() : NotFound();

    // —— Opportunities ——
    [HttpGet("opportunities")]
    public async Task<ActionResult<IEnumerable<Opportunity>>> GetOpportunities(CancellationToken ct)
        => Ok(await _crm.ListOpportunitiesAsync(ct));

    [HttpGet("opportunities/{id:int}")]
    public async Task<ActionResult<Opportunity>> GetOpportunity(int id, CancellationToken ct)
    {
        var row = await _crm.GetOpportunityAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("opportunities")]
    public async Task<ActionResult<Opportunity>> CreateOpportunity([FromBody] Opportunity deal, CancellationToken ct)
    {
        var created = await _crm.CreateOpportunityAsync(deal, ct);
        return CreatedAtAction(nameof(GetOpportunity), new { id = created.Id }, created);
    }

    [HttpPut("opportunities/{id:int}")]
    public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] Opportunity update, CancellationToken ct)
        => await _crm.UpdateOpportunityAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("opportunities/{id:int}")]
    public async Task<IActionResult> DeleteOpportunity(int id, CancellationToken ct)
        => await _crm.DeleteOpportunityAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("opportunities/{id:int}/win")]
    public async Task<ActionResult<Customer>> WinOpportunity(int id, [FromBody] WinLoseRequest? body, CancellationToken ct)
    {
        var (ok, error, customer) = await _crm.WinOpportunityAsync(id, body?.Reason, ct);
        if (!ok) return BadRequest(new { error });
        return Ok(customer);
    }

    [HttpPost("opportunities/{id:int}/lose")]
    public async Task<IActionResult> LoseOpportunity(int id, [FromBody] WinLoseRequest? body, CancellationToken ct)
    {
        var (ok, error) = await _crm.LoseOpportunityAsync(id, body?.Reason, ct);
        if (!ok) return BadRequest(new { error });
        return NoContent();
    }

    [HttpPost("opportunities/{id:int}/stage")]
    public async Task<ActionResult<Opportunity>> MoveStage(int id, [FromBody] StageMoveRequest body, CancellationToken ct)
    {
        var row = await _crm.MoveOpportunityStageAsync(id, body.Stage, ct);
        return row is null ? NotFound() : Ok(row);
    }

    // —— Customers ——
    [HttpGet("customers")]
    public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers(CancellationToken ct)
        => Ok(await _crm.ListCustomersAsync(ct));

    [HttpGet("customers/{id:int}")]
    public async Task<ActionResult<Customer>> GetCustomer(int id, CancellationToken ct)
    {
        var row = await _crm.GetCustomerAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("customers")]
    public async Task<ActionResult<Customer>> CreateCustomer([FromBody] Customer customer, CancellationToken ct)
    {
        var created = await _crm.CreateCustomerAsync(customer, ct);
        return CreatedAtAction(nameof(GetCustomer), new { id = created.Id }, created);
    }

    [HttpPut("customers/{id:int}")]
    public async Task<IActionResult> UpdateCustomer(int id, [FromBody] Customer update, CancellationToken ct)
        => await _crm.UpdateCustomerAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("customers/{id:int}")]
    public async Task<IActionResult> DeleteCustomer(int id, CancellationToken ct)
        => await _crm.DeleteCustomerAsync(id, ct) ? NoContent() : NotFound();

    // —— Campaigns ——
    [HttpGet("campaigns")]
    public async Task<ActionResult<IEnumerable<Campaign>>> GetCampaigns(CancellationToken ct)
        => Ok(await _crm.ListCampaignsAsync(ct));

    [HttpGet("campaigns/{id:int}")]
    public async Task<ActionResult<Campaign>> GetCampaign(int id, CancellationToken ct)
    {
        var row = await _crm.GetCampaignAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("campaigns")]
    public async Task<ActionResult<Campaign>> CreateCampaign([FromBody] Campaign campaign, CancellationToken ct)
    {
        var created = await _crm.CreateCampaignAsync(campaign, ct);
        return CreatedAtAction(nameof(GetCampaign), new { id = created.Id }, created);
    }

    [HttpPut("campaigns/{id:int}")]
    public async Task<IActionResult> UpdateCampaign(int id, [FromBody] Campaign update, CancellationToken ct)
        => await _crm.UpdateCampaignAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("campaigns/{id:int}")]
    public async Task<IActionResult> DeleteCampaign(int id, CancellationToken ct)
        => await _crm.DeleteCampaignAsync(id, ct) ? NoContent() : NotFound();

    // —— Products ——
    [HttpGet("products")]
    public async Task<ActionResult<IEnumerable<CrmProduct>>> GetProducts(CancellationToken ct)
        => Ok(await _crm.ListProductsAsync(ct));

    [HttpGet("products/{id:int}")]
    public async Task<ActionResult<CrmProduct>> GetProduct(int id, CancellationToken ct)
    {
        var row = await _crm.GetProductAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("products")]
    public async Task<ActionResult<CrmProduct>> CreateProduct([FromBody] CrmProduct product, CancellationToken ct)
    {
        var created = await _crm.CreateProductAsync(product, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = created.Id }, created);
    }

    [HttpPut("products/{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] CrmProduct update, CancellationToken ct)
        => await _crm.UpdateProductAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
        => await _crm.DeleteProductAsync(id, ct) ? NoContent() : NotFound();

    // —— Quotes ——
    [HttpGet("quotes")]
    public async Task<ActionResult<IEnumerable<Quote>>> GetQuotes(CancellationToken ct)
        => Ok(await _crm.ListQuotesAsync(ct));

    [HttpGet("quotes/{id:int}")]
    public async Task<ActionResult<Quote>> GetQuote(int id, CancellationToken ct)
    {
        var row = await _crm.GetQuoteAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("quotes")]
    public async Task<ActionResult<Quote>> CreateQuote([FromBody] Quote quote, CancellationToken ct)
    {
        var created = await _crm.CreateQuoteAsync(quote, ct);
        return CreatedAtAction(nameof(GetQuote), new { id = created.Id }, created);
    }

    [HttpPut("quotes/{id:int}")]
    public async Task<IActionResult> UpdateQuote(int id, [FromBody] Quote update, CancellationToken ct)
        => await _crm.UpdateQuoteAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("quotes/{id:int}")]
    public async Task<IActionResult> DeleteQuote(int id, CancellationToken ct)
        => await _crm.DeleteQuoteAsync(id, ct) ? NoContent() : NotFound();

    // —— Cases ——
    [HttpGet("cases")]
    public async Task<ActionResult<IEnumerable<CrmCase>>> GetCases(CancellationToken ct)
        => Ok(await _crm.ListCasesAsync(ct));

    [HttpGet("cases/{id:int}")]
    public async Task<ActionResult<CrmCase>> GetCase(int id, CancellationToken ct)
    {
        var row = await _crm.GetCaseAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("cases")]
    public async Task<ActionResult<CrmCase>> CreateCase([FromBody] CrmCase crmCase, CancellationToken ct)
    {
        var created = await _crm.CreateCaseAsync(crmCase, ct);
        return CreatedAtAction(nameof(GetCase), new { id = created.Id }, created);
    }

    [HttpPut("cases/{id:int}")]
    public async Task<IActionResult> UpdateCase(int id, [FromBody] CrmCase update, CancellationToken ct)
        => await _crm.UpdateCaseAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("cases/{id:int}")]
    public async Task<IActionResult> DeleteCase(int id, CancellationToken ct)
        => await _crm.DeleteCaseAsync(id, ct) ? NoContent() : NotFound();

    // —— Tasks ——
    [HttpGet("tasks")]
    public async Task<ActionResult<IEnumerable<CrmTask>>> GetTasks(CancellationToken ct)
        => Ok(await _crm.ListTasksAsync(ct));

    [HttpGet("tasks/{id:int}")]
    public async Task<ActionResult<CrmTask>> GetTask(int id, CancellationToken ct)
    {
        var row = await _crm.GetTaskAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<CrmTask>> CreateTask([FromBody] CrmTask task, CancellationToken ct)
    {
        var created = await _crm.CreateTaskAsync(task, ct);
        return CreatedAtAction(nameof(GetTask), new { id = created.Id }, created);
    }

    [HttpPut("tasks/{id:int}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] CrmTask update, CancellationToken ct)
        => await _crm.UpdateTaskAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("tasks/{id:int}")]
    public async Task<IActionResult> DeleteTask(int id, CancellationToken ct)
        => await _crm.DeleteTaskAsync(id, ct) ? NoContent() : NotFound();

    // —— Meetings ——
    [HttpGet("meetings")]
    public async Task<ActionResult<IEnumerable<Meeting>>> GetMeetings(CancellationToken ct)
        => Ok(await _crm.ListMeetingsAsync(ct));

    [HttpGet("meetings/{id:int}")]
    public async Task<ActionResult<Meeting>> GetMeeting(int id, CancellationToken ct)
    {
        var row = await _crm.GetMeetingAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("meetings")]
    public async Task<ActionResult<Meeting>> CreateMeeting([FromBody] Meeting meeting, CancellationToken ct)
    {
        var created = await _crm.CreateMeetingAsync(meeting, ct);
        return CreatedAtAction(nameof(GetMeeting), new { id = created.Id }, created);
    }

    [HttpPut("meetings/{id:int}")]
    public async Task<IActionResult> UpdateMeeting(int id, [FromBody] Meeting update, CancellationToken ct)
        => await _crm.UpdateMeetingAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("meetings/{id:int}")]
    public async Task<IActionResult> DeleteMeeting(int id, CancellationToken ct)
        => await _crm.DeleteMeetingAsync(id, ct) ? NoContent() : NotFound();

    // —— Calls ——
    [HttpGet("calls")]
    public async Task<ActionResult<IEnumerable<CrmCall>>> GetCalls(CancellationToken ct)
        => Ok(await _crm.ListCallsAsync(ct));

    [HttpGet("calls/{id:int}")]
    public async Task<ActionResult<CrmCall>> GetCall(int id, CancellationToken ct)
    {
        var row = await _crm.GetCallAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("calls")]
    public async Task<ActionResult<CrmCall>> CreateCall([FromBody] CrmCall call, CancellationToken ct)
    {
        var created = await _crm.CreateCallAsync(call, ct);
        return CreatedAtAction(nameof(GetCall), new { id = created.Id }, created);
    }

    [HttpPut("calls/{id:int}")]
    public async Task<IActionResult> UpdateCall(int id, [FromBody] CrmCall update, CancellationToken ct)
        => await _crm.UpdateCallAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("calls/{id:int}")]
    public async Task<IActionResult> DeleteCall(int id, CancellationToken ct)
        => await _crm.DeleteCallAsync(id, ct) ? NoContent() : NotFound();

    // —— Notes ——
    [HttpGet("notes")]
    public async Task<ActionResult<IEnumerable<CrmNote>>> GetNotes(
        [FromQuery] string? relatedType, [FromQuery] int? relatedId, CancellationToken ct)
        => Ok(await _crm.ListNotesAsync(relatedType, relatedId, ct));

    [HttpGet("notes/{id:int}")]
    public async Task<ActionResult<CrmNote>> GetNote(int id, CancellationToken ct)
    {
        var row = await _crm.GetNoteAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("notes")]
    public async Task<ActionResult<CrmNote>> CreateNote([FromBody] CrmNote note, CancellationToken ct)
    {
        var created = await _crm.CreateNoteAsync(note, ct);
        return CreatedAtAction(nameof(GetNote), new { id = created.Id }, created);
    }

    [HttpPut("notes/{id:int}")]
    public async Task<IActionResult> UpdateNote(int id, [FromBody] CrmNote update, CancellationToken ct)
        => await _crm.UpdateNoteAsync(id, update, ct) is null ? NotFound() : NoContent();

    [HttpDelete("notes/{id:int}")]
    public async Task<IActionResult> DeleteNote(int id, CancellationToken ct)
        => await _crm.DeleteNoteAsync(id, ct) ? NoContent() : NotFound();
}

public record WinLoseRequest(string? Reason);
public record StageMoveRequest(string Stage);
