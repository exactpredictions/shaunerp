using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaunERP.Core.Projects;
using ShaunERP.Infrastructure.Services;

namespace ShaunERP.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectManagementService _projects;

    public ProjectsController(IProjectManagementService projects) => _projects = projects;

    [HttpGet("dashboard")]
    public async Task<ActionResult<ProjectDashboardStats>> Dashboard(CancellationToken ct)
        => Ok(await _projects.DashboardAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Project>>> List(
        [FromQuery] string? search,
        [FromQuery] ProjectStatus? status,
        [FromQuery] bool includeArchived,
        CancellationToken ct)
        => Ok(await _projects.ListProjectsAsync(
            new ProjectFilter { Search = search, Status = status, IncludeArchived = includeArchived }, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Project>> Get(int id, CancellationToken ct)
    {
        var project = await _projects.GetProjectAsync(id, ct);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create([FromBody] Project project, CancellationToken ct)
    {
        try
        {
            var created = await _projects.CreateProjectAsync(project, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Project>> Update(int id, [FromBody] Project update, CancellationToken ct)
    {
        try
        {
            var updated = await _projects.UpdateProjectAsync(id, update, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/archive")]
    public async Task<ActionResult<Project>> Archive(int id, CancellationToken ct)
    {
        var archived = await _projects.ArchiveProjectAsync(id, ct);
        return archived is null ? NotFound() : Ok(archived);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            return await _projects.DeleteProjectAsync(id, ct) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("from-sales-order/{salesOrderId:int}")]
    public async Task<ActionResult<Project>> CreateFromSalesOrder(int salesOrderId, CancellationToken ct)
    {
        try
        {
            var created = await _projects.CreateProjectFromSalesOrderAsync(salesOrderId, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<ProjectTaskPage>> Tasks(
        [FromQuery] int? projectId,
        [FromQuery] int? assigneeUserId,
        [FromQuery] ProjectTaskStatus? status,
        [FromQuery] bool onlyOverdue,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
        => Ok(await _projects.ListTasksAsync(new ProjectTaskFilter
        {
            ProjectId = projectId,
            AssigneeUserId = assigneeUserId,
            Status = status,
            OnlyOverdue = onlyOverdue,
            Search = search,
            Skip = skip,
            Take = take
        }, ct));

    [HttpPost("tasks")]
    public async Task<ActionResult<ProjectTask>> CreateTask([FromBody] ProjectTask task, CancellationToken ct)
    {
        try
        {
            return Ok(await _projects.CreateTaskAsync(task, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("tasks/{id:int}/status")]
    public async Task<ActionResult<ProjectTask>> UpdateTaskStatus(
        int id, [FromBody] ProjectTaskStatusRequest request, CancellationToken ct)
    {
        try
        {
            var updated = await _projects.UpdateTaskStatusAsync(id, request.Status, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-work")]
    public async Task<ActionResult<MyWorkBoard>> MyWork(CancellationToken ct)
        => Ok(await _projects.MyWorkAsync(null, ct));

    [HttpGet("timesheets")]
    public async Task<ActionResult<IEnumerable<ProjectTimesheet>>> Timesheets(
        [FromQuery] int? projectId,
        [FromQuery] TimesheetEntryStatus? status,
        [FromQuery] bool onlyMine,
        CancellationToken ct)
        => Ok(await _projects.ListTimesheetsAsync(
            new TimesheetFilter { ProjectId = projectId, Status = status, OnlyMine = onlyMine }, ct));

    [HttpPost("timesheets")]
    public async Task<ActionResult<ProjectTimesheet>> CreateTimesheet(
        [FromBody] ProjectTimesheet entry, CancellationToken ct)
    {
        try
        {
            return Ok(await _projects.CreateTimesheetAsync(entry, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("timesheets/{id:int}/submit")]
    public async Task<ActionResult<ProjectTimesheet>> SubmitTimesheet(int id, CancellationToken ct)
    {
        try
        {
            var row = await _projects.SubmitTimesheetAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("timesheets/{id:int}/approve")]
    public async Task<ActionResult<ProjectTimesheet>> ApproveTimesheet(int id, CancellationToken ct)
    {
        try
        {
            var row = await _projects.ApproveTimesheetAsync(id, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("timesheets/{id:int}/reject")]
    public async Task<ActionResult<ProjectTimesheet>> RejectTimesheet(
        int id, [FromBody] TimesheetRejectRequest? request, CancellationToken ct)
    {
        try
        {
            var row = await _projects.RejectTimesheetAsync(id, request?.Reason, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public sealed record ProjectTaskStatusRequest(ProjectTaskStatus Status);

public sealed record TimesheetRejectRequest(string? Reason);
