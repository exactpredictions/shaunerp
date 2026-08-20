using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Projects;
using ShaunERP.Core.Sales;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services;

public sealed record ProjectDashboardStats(
    int TotalProjects,
    int ActiveProjects,
    int OnHoldProjects,
    int AtRiskProjects,
    int CompletedProjects,
    int OpenTasks,
    int OverdueTasks,
    int TasksDueThisWeek,
    int OpenIssues,
    int OpenRisks,
    int PendingTimesheets,
    int MilestonesDueSoon,
    decimal TotalBudget,
    decimal ActualCost,
    decimal EstimatedHours,
    decimal ActualHours,
    IReadOnlyList<Project> RecentProjects);

public sealed class ProjectFilter
{
    public string? Search { get; set; }
    public ProjectStatus? Status { get; set; }
    public ProjectPriority? Priority { get; set; }
    public int? CustomerId { get; set; }
    public int? ManagerUserId { get; set; }
    public bool IncludeArchived { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}

public sealed record ProjectPage(IReadOnlyList<Project> Items, int TotalCount);

public sealed class ProjectTaskFilter
{
    public int? ProjectId { get; set; }
    public int? AssigneeUserId { get; set; }
    public ProjectTaskStatus? Status { get; set; }
    public ProjectPriority? Priority { get; set; }
    public DateTime? DueBefore { get; set; }
    public bool OnlyOverdue { get; set; }
    public bool ExcludeCompleted { get; set; }
    public string? Search { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

public sealed record ProjectTaskPage(IReadOnlyList<ProjectTask> Items, int TotalCount);

public sealed record MyWorkBoard(
    IReadOnlyList<ProjectTask> Today,
    IReadOnlyList<ProjectTask> Overdue,
    IReadOnlyList<ProjectTask> Upcoming,
    IReadOnlyList<ProjectTask> InProgress,
    IReadOnlyList<ProjectTask> Blocked,
    IReadOnlyList<ProjectTask> CompletedRecently);

public sealed class TimesheetFilter
{
    public int? ProjectId { get; set; }
    public int? UserId { get; set; }
    public TimesheetEntryStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool OnlyMine { get; set; }
    public int Take { get; set; } = 300;
}

public sealed class ProjectAttachmentUpload
{
    public ProjectCommentEntityType EntityType { get; set; } = ProjectCommentEntityType.Project;
    public int EntityId { get; set; }
    public int? ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed record ProjectAttachmentContent(ProjectAttachment Attachment, byte[] Content);

public interface IProjectManagementService
{
    Task<ProjectDashboardStats> DashboardAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Project>> ListProjectsAsync(ProjectFilter? filter = null, CancellationToken ct = default);
    Task<ProjectPage> QueryProjectsAsync(ProjectFilter? filter = null, CancellationToken ct = default);
    Task<Project?> GetProjectAsync(int id, CancellationToken ct = default);
    Task<Project> CreateProjectAsync(Project project, CancellationToken ct = default);
    Task<Project?> UpdateProjectAsync(int id, Project update, CancellationToken ct = default);
    Task<Project?> ArchiveProjectAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteProjectAsync(int id, CancellationToken ct = default);
    Task<Project> CreateProjectFromSalesOrderAsync(int salesOrderId, CancellationToken ct = default);
    Task<decimal> RecalculateProjectProgressAsync(int projectId, CancellationToken ct = default);

    Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListCompanyUsersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMember>> ListMembersAsync(int projectId, CancellationToken ct = default);
    Task<ProjectMember> AddMemberAsync(int projectId, ProjectMember member, CancellationToken ct = default);
    Task<ProjectMember?> UpdateMemberAsync(int memberId, ProjectMember update, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(int memberId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectPhase>> ListPhasesAsync(int projectId, CancellationToken ct = default);
    Task<ProjectPhase> CreatePhaseAsync(int projectId, ProjectPhase phase, CancellationToken ct = default);
    Task<ProjectPhase?> UpdatePhaseAsync(int phaseId, ProjectPhase update, CancellationToken ct = default);
    Task<bool> DeletePhaseAsync(int phaseId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMilestone>> ListMilestonesAsync(int projectId, CancellationToken ct = default);
    Task<ProjectMilestone> CreateMilestoneAsync(int projectId, ProjectMilestone milestone, CancellationToken ct = default);
    Task<ProjectMilestone?> UpdateMilestoneAsync(int milestoneId, ProjectMilestone update, CancellationToken ct = default);
    Task<bool> DeleteMilestoneAsync(int milestoneId, CancellationToken ct = default);

    Task<ProjectTaskPage> ListTasksAsync(ProjectTaskFilter? filter = null, CancellationToken ct = default);
    Task<ProjectTask?> GetTaskAsync(int id, CancellationToken ct = default);
    Task<ProjectTask> CreateTaskAsync(ProjectTask task, CancellationToken ct = default);
    Task<ProjectTask?> UpdateTaskAsync(int id, ProjectTask update, CancellationToken ct = default);
    Task<ProjectTask?> UpdateTaskStatusAsync(int id, ProjectTaskStatus status, CancellationToken ct = default);
    Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default);

    Task<ProjectTaskChecklistItem> AddChecklistItemAsync(int taskId, string title, CancellationToken ct = default);
    Task<ProjectTaskChecklistItem?> ToggleChecklistItemAsync(int itemId, CancellationToken ct = default);
    Task<bool> DeleteChecklistItemAsync(int itemId, CancellationToken ct = default);

    Task<ProjectTaskDependency> AddDependencyAsync(int predecessorTaskId, int successorTaskId, TaskDependencyType type = TaskDependencyType.FinishToStart, CancellationToken ct = default);
    Task<bool> RemoveDependencyAsync(int dependencyId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectTask>> GetBlockedByAsync(int taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectTask>> GetBlockingAsync(int taskId, CancellationToken ct = default);

    Task<MyWorkBoard> MyWorkAsync(int? userId = null, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectComment>> ListCommentsAsync(ProjectCommentEntityType entityType, int entityId, CancellationToken ct = default);
    Task<ProjectComment> AddCommentAsync(ProjectCommentEntityType entityType, int entityId, int? projectId, string body, int? parentCommentId = null, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectAttachment>> ListAttachmentsAsync(ProjectCommentEntityType entityType, int entityId, CancellationToken ct = default);
    Task<ProjectAttachment> UploadAttachmentAsync(ProjectAttachmentUpload upload, CancellationToken ct = default);
    Task<ProjectAttachmentContent?> DownloadAttachmentAsync(int attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(int attachmentId, CancellationToken ct = default);

    Task<IReadOnlyList<Issue>> ListIssuesAsync(int projectId, CancellationToken ct = default);
    Task<Issue> CreateIssueAsync(Issue issue, CancellationToken ct = default);
    Task<Issue?> UpdateIssueAsync(int id, Issue update, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectRisk>> ListRisksAsync(int projectId, CancellationToken ct = default);
    Task<ProjectRisk> CreateRiskAsync(ProjectRisk risk, CancellationToken ct = default);
    Task<ProjectRisk?> UpdateRiskAsync(int id, ProjectRisk update, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectChangeRequest>> ListChangeRequestsAsync(int projectId, CancellationToken ct = default);
    Task<ProjectChangeRequest> CreateChangeRequestAsync(ProjectChangeRequest request, CancellationToken ct = default);
    Task<ProjectChangeRequest?> UpdateChangeRequestAsync(int id, ProjectChangeRequest update, CancellationToken ct = default);
    Task<ProjectChangeRequest?> ApproveChangeRequestAsync(int id, bool approved, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTimesheet>> ListTimesheetsAsync(TimesheetFilter? filter = null, CancellationToken ct = default);
    Task<ProjectTimesheet> CreateTimesheetAsync(ProjectTimesheet entry, CancellationToken ct = default);
    Task<ProjectTimesheet?> SubmitTimesheetAsync(int id, CancellationToken ct = default);
    Task<ProjectTimesheet?> ApproveTimesheetAsync(int id, CancellationToken ct = default);
    Task<ProjectTimesheet?> RejectTimesheetAsync(int id, string? reason = null, CancellationToken ct = default);

    Task<ProjectTimeTracker?> GetActiveTimerAsync(CancellationToken ct = default);
    Task<ProjectTimeTracker> StartTimerAsync(int projectId, int? taskId = null, string? description = null, CancellationToken ct = default);
    Task<ProjectTimesheet?> StopTimerAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task<ProjectTemplate?> GetTemplateAsync(int id, CancellationToken ct = default);
    Task<ProjectTemplate> CreateTemplateAsync(ProjectTemplate template, CancellationToken ct = default);
    Task<bool> DeleteTemplateAsync(int id, CancellationToken ct = default);
    Task<Project> CloneTemplateToProjectAsync(int templateId, Project project, CancellationToken ct = default);

    Task<string> NextNumberAsync(int companyId, string key, CancellationToken ct = default);
}

public sealed partial class ProjectManagementService : IProjectManagementService
{
    private readonly ShaunErpDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IModuleRegistry _modules;
    private readonly IAuditService _audit;
    private readonly IProjectFileStorage _files;

    public ProjectManagementService(
        ShaunErpDbContext db,
        ITenantContext tenant,
        IModuleRegistry modules,
        IAuditService audit,
        IProjectFileStorage files)
    {
        _db = db;
        _tenant = tenant;
        _modules = modules;
        _audit = audit;
        _files = files;
    }

    // ——————————————————————————— Dashboard ———————————————————————————

    public async Task<ProjectDashboardStats> DashboardAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsDashboardView);
        var companyId = RequireCompanyId();
        var today = DateTime.UtcNow.Date;
        var weekEnd = today.AddDays(7);

        var projects = await VisibleProjects(_db.Projects.AsNoTracking()).ToListAsync(ct);
        var projectIds = projects.Select(p => p.Id).ToList();

        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.CompanyId == companyId && projectIds.Contains(t.ProjectId))
            .Select(t => new { t.TaskStatus, t.DueDate })
            .ToListAsync(ct);

        var openIssues = await _db.Issues.AsNoTracking()
            .CountAsync(i => i.CompanyId == companyId && projectIds.Contains(i.ProjectId)
                             && i.IssueStatus != ProjectIssueStatus.Closed
                             && i.IssueStatus != ProjectIssueStatus.Cancelled, ct);

        var openRisks = await _db.ProjectRisks.AsNoTracking()
            .CountAsync(r => r.CompanyId == companyId && projectIds.Contains(r.ProjectId)
                             && r.RiskStatus != ProjectRiskStatus.Closed, ct);

        var pendingTimesheets = await _db.ProjectTimesheets.AsNoTracking()
            .CountAsync(t => t.CompanyId == companyId && t.EntryStatus == TimesheetEntryStatus.Submitted, ct);

        var milestonesDueSoon = await _db.ProjectMilestones.AsNoTracking()
            .CountAsync(m => m.CompanyId == companyId && projectIds.Contains(m.ProjectId)
                             && m.MilestoneStatus != ProjectMilestoneStatus.Completed
                             && m.MilestoneStatus != ProjectMilestoneStatus.Cancelled
                             && m.PlannedDate != null && m.PlannedDate <= weekEnd, ct);

        var openStatuses = new[]
        {
            ProjectTaskStatus.Backlog, ProjectTaskStatus.ToDo, ProjectTaskStatus.InProgress,
            ProjectTaskStatus.Blocked, ProjectTaskStatus.Review, ProjectTaskStatus.Testing
        };

        var showMoney = CanSeeFinancials();

        return new ProjectDashboardStats(
            projects.Count,
            projects.Count(p => p.ProjectStatus == ProjectStatus.Active),
            projects.Count(p => p.ProjectStatus == ProjectStatus.OnHold),
            projects.Count(p => p.ProjectStatus == ProjectStatus.AtRisk || p.HealthStatus >= ProjectHealthStatus.AtRisk),
            projects.Count(p => p.ProjectStatus == ProjectStatus.Completed),
            tasks.Count(t => openStatuses.Contains(t.TaskStatus)),
            tasks.Count(t => openStatuses.Contains(t.TaskStatus) && t.DueDate != null && t.DueDate < today),
            tasks.Count(t => openStatuses.Contains(t.TaskStatus) && t.DueDate >= today && t.DueDate <= weekEnd),
            openIssues,
            openRisks,
            pendingTimesheets,
            milestonesDueSoon,
            showMoney ? projects.Sum(p => p.RevisedBudget > 0 ? p.RevisedBudget : p.Budget) : 0m,
            showMoney ? projects.Sum(p => p.ActualCost) : 0m,
            projects.Sum(p => p.EstimatedHours),
            projects.Sum(p => p.ActualHours),
            projects.OrderByDescending(p => p.UpdatedDate ?? p.CreatedDate).Take(8).ToList());
    }

    // ——————————————————————————— Projects ———————————————————————————

    public async Task<IReadOnlyList<Project>> ListProjectsAsync(ProjectFilter? filter = null, CancellationToken ct = default)
    {
        var page = await QueryProjectsAsync(filter, ct);
        return page.Items;
    }

    public async Task<ProjectPage> QueryProjectsAsync(ProjectFilter? filter = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        filter ??= new ProjectFilter();
        var take = ListQuery.NormalizePageSize(filter.Take <= 0 ? 50 : filter.Take);
        var skip = Math.Max(0, filter.Skip);

        var query = VisibleProjects(_db.Projects.AsNoTracking()).Include(p => p.Customer).AsQueryable();

        if (!filter.IncludeArchived)
            query = query.Where(p => p.ProjectStatus != ProjectStatus.Archived);
        if (filter.Status is { } status)
            query = query.Where(p => p.ProjectStatus == status);
        if (filter.Priority is { } priority)
            query = query.Where(p => p.Priority == priority);
        if (filter.CustomerId is { } customerId)
            query = query.Where(p => p.CustomerId == customerId);
        if (filter.ManagerUserId is { } managerId)
            query = query.Where(p => p.ManagerUserId == managerId);

        var pattern = PagingQueryableExtensions.ToILikePattern(filter.Search);
        if (pattern is not null)
        {
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.ProjectCode, pattern) ||
                (p.CustomerName != null && EF.Functions.ILike(p.CustomerName, pattern)) ||
                (p.Tags != null && EF.Functions.ILike(p.Tags, pattern)) ||
                (p.Department != null && EF.Functions.ILike(p.Department, pattern)));
        }

        var ordered = query
            .OrderByDescending(p => p.ProjectStatus == ProjectStatus.Active)
            .ThenByDescending(p => p.Id);
        var total = await ordered.CountAsync(ct);
        var items = total == 0
            ? new List<Project>()
            : await ordered.Skip(skip).Take(take).ToListAsync(ct);
        return new ProjectPage(items, total);
    }

    public async Task<Project?> GetProjectAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        var project = await _db.Projects.AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.ManagerUser)
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == companyId, ct);
        if (project is null) return null;
        await EnsureProjectVisibleAsync(project, ct);
        return project;
    }

    public async Task<Project> CreateProjectAsync(Project project, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsCreate);
        var companyId = RequireCompanyId();

        project.Id = 0;
        project.CompanyId = companyId;
        project.CreatedDate = DateTime.UtcNow;
        NormalizeProjectDates(project);
        Validate(project);

        project.ProjectCode = string.IsNullOrWhiteSpace(project.ProjectCode)
            ? await NextNumberAsync(companyId, ProjectSequenceKeys.Project, ct)
            : project.ProjectCode.Trim();

        await ResolveCustomerAsync(project, ct);
        project.OwnerUserId ??= _tenant.UserId;
        if (string.IsNullOrWhiteSpace(project.CurrencyCode)) project.CurrencyCode = "INR";

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        if (project.ManagerUserId is { } managerId)
            await EnsureMemberAsync(project, managerId, "Project manager", ct);
        if (project.OwnerUserId is { } ownerId)
            await EnsureMemberAsync(project, ownerId, "Owner", ct);
        await _db.SaveChangesAsync(ct);

        await AuditAsync("projects.project.created", "Project", project.Id, project.ProjectCode, ct);
        return project;
    }

    public async Task<Project?> UpdateProjectAsync(int id, Project update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var row = await RequireProjectAsync(id, ct);

        row.Name = update.Name?.Trim() ?? row.Name;
        row.Description = NullIfBlank(update.Description);
        row.CustomerId = update.CustomerId;
        row.OpportunityId = update.OpportunityId;
        row.QuoteId = update.QuoteId;
        row.SalesOrderId = update.SalesOrderId;
        row.ProjectType = NullIfBlank(update.ProjectType);
        row.Department = NullIfBlank(update.Department);
        row.ManagerUserId = update.ManagerUserId;
        row.OwnerUserId = update.OwnerUserId;
        row.Priority = update.Priority;
        row.ProjectStatus = update.ProjectStatus;
        row.HealthStatus = update.HealthStatus;
        row.PlannedStartDate = NormalizeUtc(update.PlannedStartDate);
        row.PlannedEndDate = NormalizeUtc(update.PlannedEndDate);
        row.ActualStartDate = NormalizeUtc(update.ActualStartDate);
        row.ActualEndDate = NormalizeUtc(update.ActualEndDate);
        row.EstimatedHours = Math.Max(0, update.EstimatedHours);
        row.BillingType = update.BillingType;
        row.CurrencyCode = string.IsNullOrWhiteSpace(update.CurrencyCode) ? row.CurrencyCode : update.CurrencyCode.Trim();
        row.Tags = NullIfBlank(update.Tags);
        row.Notes = NullIfBlank(update.Notes);

        if (CanEditBudget())
        {
            row.Budget = Math.Max(0, update.Budget);
            row.RevisedBudget = Math.Max(0, update.RevisedBudget);
            row.EstimatedCost = Math.Max(0, update.EstimatedCost);
            row.ActualCost = Math.Max(0, update.ActualCost);
            row.EstimatedLabourCost = Math.Max(0, update.EstimatedLabourCost);
            row.ActualLabourCost = Math.Max(0, update.ActualLabourCost);
            row.EstimatedExpense = Math.Max(0, update.EstimatedExpense);
            row.ActualExpense = Math.Max(0, update.ActualExpense);
            row.CustomerBilling = Math.Max(0, update.CustomerBilling);
            row.Revenue = Math.Max(0, update.Revenue);
        }

        if (!string.IsNullOrWhiteSpace(update.ProjectCode))
            row.ProjectCode = update.ProjectCode.Trim();

        Validate(row);
        await ResolveCustomerAsync(row, ct);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.project.updated", "Project", row.Id, row.ProjectCode, ct);
        return row;
    }

    public async Task<Project?> ArchiveProjectAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsArchive);
        var row = await RequireProjectAsync(id, ct);
        row.ProjectStatus = ProjectStatus.Archived;
        row.ActualEndDate ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.project.archived", "Project", row.Id, row.ProjectCode, ct);
        return row;
    }

    public async Task<bool> DeleteProjectAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsDelete);
        var row = await RequireProjectAsync(id, ct);

        var hasTimesheets = await _db.ProjectTimesheets
            .AnyAsync(t => t.ProjectId == row.Id && t.EntryStatus != TimesheetEntryStatus.Draft, ct);
        if (hasTimesheets)
            throw new InvalidOperationException("Projects with submitted time cannot be deleted. Archive instead.");

        var taskIds = await _db.ProjectTasks.Where(t => t.ProjectId == row.Id).Select(t => t.Id).ToListAsync(ct);
        if (taskIds.Count > 0)
        {
            var dependencies = await _db.ProjectTaskDependencies
                .Where(d => taskIds.Contains(d.PredecessorTaskId) || taskIds.Contains(d.SuccessorTaskId))
                .ToListAsync(ct);
            _db.ProjectTaskDependencies.RemoveRange(dependencies);
        }

        _db.Projects.Remove(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.project.deleted", "Project", id, row.ProjectCode, ct);
        return true;
    }

    public async Task<Project> CreateProjectFromSalesOrderAsync(int salesOrderId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsCreate);
        var companyId = RequireCompanyId();

        var order = await _db.SalesOrders.AsNoTracking()
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId && o.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Sales order not found for this account.");

        if (order.OrderStatus is SalesOrderStatus.Draft or SalesOrderStatus.Cancelled)
            throw new InvalidOperationException("Confirm the sales order before creating a delivery project.");

        var existing = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.SalesOrderId == order.Id, ct);
        if (existing is not null)
            throw new InvalidOperationException($"Project {existing.ProjectCode} already exists for this order.");

        var project = new Project
        {
            Name = $"Delivery — {order.CustomerName ?? order.Customer?.CompanyName ?? order.OrderNumber}",
            Description = $"Delivery project created from sales order {order.OrderNumber}.",
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            SalesOrderId = order.Id,
            QuoteId = order.QuoteId,
            CurrencyCode = string.IsNullOrWhiteSpace(order.CurrencyCode) ? "INR" : order.CurrencyCode!,
            Budget = order.GrandTotal,
            RevisedBudget = order.GrandTotal,
            CustomerBilling = order.GrandTotal,
            BillingType = ProjectBillingType.FixedPrice,
            ProjectStatus = ProjectStatus.Planned,
            PlannedStartDate = NormalizeUtc(order.OrderDate),
            PlannedEndDate = NormalizeUtc(order.RequestedDeliveryDate),
            ManagerUserId = _tenant.UserId,
            OwnerUserId = _tenant.UserId
        };

        return await CreateProjectAsync(project, ct);
    }

    public async Task<decimal> RecalculateProjectProgressAsync(int projectId, CancellationToken ct = default)
    {
        var project = await RequireProjectAsync(projectId, ct);
        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && t.CompanyId == project.CompanyId)
            .Select(t => new { t.TaskStatus, t.ProgressPercent, t.EstimatedHours })
            .ToListAsync(ct);

        var counted = tasks.Where(t => t.TaskStatus != ProjectTaskStatus.Cancelled).ToList();
        decimal progress = 0;
        if (counted.Count > 0)
        {
            var weighted = counted.Sum(t => t.EstimatedHours);
            if (weighted > 0)
            {
                progress = counted.Sum(t => t.EstimatedHours * EffectiveProgress(t.TaskStatus, t.ProgressPercent)) / weighted;
            }
            else
            {
                progress = counted.Sum(t => EffectiveProgress(t.TaskStatus, t.ProgressPercent)) / counted.Count;
            }
        }

        project.ProgressPercent = Math.Round(Math.Clamp(progress, 0, 100), 2);
        await _db.SaveChangesAsync(ct);
        return project.ProgressPercent;

        static decimal EffectiveProgress(ProjectTaskStatus status, decimal declared) =>
            status == ProjectTaskStatus.Completed ? 100m : Math.Clamp(declared, 0, 100);
    }

    // ——————————————————————————— Lookups ———————————————————————————

    public async Task<IReadOnlyList<Customer>> ListCustomersAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsAccess);
        var companyId = RequireCompanyId();
        return await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && (c.Status == null || c.Status == "Active"))
            .OrderBy(c => c.CompanyName)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<User>> ListCompanyUsersAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsAccess);
        var companyId = RequireCompanyId();
        return await _db.CompanyUsers.AsNoTracking()
            .Where(cu => cu.CompanyId == companyId && cu.User != null && cu.User.IsActive)
            .Select(cu => cu.User!)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Take(500)
            .ToListAsync(ct);
    }

    // ——————————————————————————— Members ———————————————————————————

    public async Task<IReadOnlyList<ProjectMember>> ListMembersAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        return await _db.ProjectMembers.AsNoTracking()
            .Include(m => m.User)
            .Where(m => m.ProjectId == project.Id && m.CompanyId == project.CompanyId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<ProjectMember> AddMemberAsync(int projectId, ProjectMember member, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var project = await RequireProjectAsync(projectId, ct);

        if (member.UserId <= 0)
            throw new InvalidOperationException("Select a team member.");
        if (!await IsCompanyUserAsync(member.UserId, project.CompanyId, ct))
            throw new InvalidOperationException("That user does not belong to this account.");

        var existing = await _db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == project.Id && m.UserId == member.UserId, ct);
        if (existing is not null)
            throw new InvalidOperationException("That user is already on the project team.");

        var row = new ProjectMember
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            UserId = member.UserId,
            ProjectRole = string.IsNullOrWhiteSpace(member.ProjectRole) ? "Member" : member.ProjectRole.Trim(),
            AllocationPercent = Math.Clamp(member.AllocationPercent, 0, 100),
            StartDate = NormalizeUtc(member.StartDate),
            EndDate = NormalizeUtc(member.EndDate)
        };
        _db.ProjectMembers.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.member.added", "ProjectMember", row.Id, $"{project.ProjectCode}:{row.UserId}", ct);
        return row;
    }

    public async Task<ProjectMember?> UpdateMemberAsync(int memberId, ProjectMember update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        row.ProjectRole = string.IsNullOrWhiteSpace(update.ProjectRole) ? row.ProjectRole : update.ProjectRole.Trim();
        row.AllocationPercent = Math.Clamp(update.AllocationPercent, 0, 100);
        row.StartDate = NormalizeUtc(update.StartDate);
        row.EndDate = NormalizeUtc(update.EndDate);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> RemoveMemberAsync(int memberId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireProjectAsync(row.ProjectId, ct);

        _db.ProjectMembers.Remove(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.member.removed", "ProjectMember", memberId, row.UserId.ToString(), ct);
        return true;
    }

    // ——————————————————————————— Phases ———————————————————————————

    public async Task<IReadOnlyList<ProjectPhase>> ListPhasesAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        return await _db.ProjectPhases.AsNoTracking()
            .Where(p => p.ProjectId == project.Id && p.CompanyId == project.CompanyId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task<ProjectPhase> CreatePhaseAsync(int projectId, ProjectPhase phase, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var project = await RequireProjectAsync(projectId, ct);
        if (string.IsNullOrWhiteSpace(phase.Name))
            throw new InvalidOperationException("Phase name is required.");

        var row = new ProjectPhase
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            Name = phase.Name.Trim(),
            Description = NullIfBlank(phase.Description),
            SortOrder = phase.SortOrder,
            OwnerUserId = phase.OwnerUserId,
            StartDate = NormalizeUtc(phase.StartDate),
            DueDate = NormalizeUtc(phase.DueDate),
            PhaseStatus = phase.PhaseStatus,
            Notes = NullIfBlank(phase.Notes)
        };
        _db.ProjectPhases.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectPhase?> UpdatePhaseAsync(int phaseId, ProjectPhase update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        row.Name = string.IsNullOrWhiteSpace(update.Name) ? row.Name : update.Name.Trim();
        row.Description = NullIfBlank(update.Description);
        row.SortOrder = update.SortOrder;
        row.OwnerUserId = update.OwnerUserId;
        row.StartDate = NormalizeUtc(update.StartDate);
        row.DueDate = NormalizeUtc(update.DueDate);
        row.PhaseStatus = update.PhaseStatus;
        row.ProgressPercent = Math.Clamp(update.ProgressPercent, 0, 100);
        row.Notes = NullIfBlank(update.Notes);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeletePhaseAsync(int phaseId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectPhases.FirstOrDefaultAsync(p => p.Id == phaseId && p.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireProjectAsync(row.ProjectId, ct);

        var tasks = await _db.ProjectTasks.Where(t => t.PhaseId == row.Id).ToListAsync(ct);
        foreach (var task in tasks) task.PhaseId = null;
        var milestones = await _db.ProjectMilestones.Where(m => m.PhaseId == row.Id).ToListAsync(ct);
        foreach (var milestone in milestones) milestone.PhaseId = null;

        _db.ProjectPhases.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ——————————————————————————— Milestones ———————————————————————————

    public async Task<IReadOnlyList<ProjectMilestone>> ListMilestonesAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        return await _db.ProjectMilestones.AsNoTracking()
            .Where(m => m.ProjectId == project.Id && m.CompanyId == project.CompanyId)
            .OrderBy(m => m.PlannedDate).ThenBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<ProjectMilestone> CreateMilestoneAsync(int projectId, ProjectMilestone milestone, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var project = await RequireProjectAsync(projectId, ct);
        if (string.IsNullOrWhiteSpace(milestone.Name))
            throw new InvalidOperationException("Milestone name is required.");

        var row = new ProjectMilestone
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            PhaseId = milestone.PhaseId,
            Name = milestone.Name.Trim(),
            Description = NullIfBlank(milestone.Description),
            OwnerUserId = milestone.OwnerUserId,
            PlannedDate = NormalizeUtc(milestone.PlannedDate),
            MilestoneStatus = milestone.MilestoneStatus,
            CustomerApprovalRequired = milestone.CustomerApprovalRequired,
            Notes = NullIfBlank(milestone.Notes)
        };
        _db.ProjectMilestones.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectMilestone?> UpdateMilestoneAsync(int milestoneId, ProjectMilestone update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectMilestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        row.PhaseId = update.PhaseId;
        row.Name = string.IsNullOrWhiteSpace(update.Name) ? row.Name : update.Name.Trim();
        row.Description = NullIfBlank(update.Description);
        row.OwnerUserId = update.OwnerUserId;
        row.PlannedDate = NormalizeUtc(update.PlannedDate);
        row.ProgressPercent = Math.Clamp(update.ProgressPercent, 0, 100);
        row.CustomerApprovalRequired = update.CustomerApprovalRequired;
        row.CustomerApproved = update.CustomerApproved;
        row.Notes = NullIfBlank(update.Notes);

        if (update.MilestoneStatus != row.MilestoneStatus)
        {
            row.MilestoneStatus = update.MilestoneStatus;
            row.ActualCompletionDate = update.MilestoneStatus == ProjectMilestoneStatus.Completed
                ? DateTime.UtcNow
                : null;
        }

        if (row.CustomerApproved && row.ApprovalDate is null)
            row.ApprovalDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteMilestoneAsync(int milestoneId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectMilestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireProjectAsync(row.ProjectId, ct);

        var tasks = await _db.ProjectTasks.Where(t => t.MilestoneId == row.Id).ToListAsync(ct);
        foreach (var task in tasks) task.MilestoneId = null;

        _db.ProjectMilestones.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ——————————————————————————— Templates ———————————————————————————

    public async Task<IReadOnlyList<ProjectTemplate>> ListTemplatesAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        return await _db.ProjectTemplates.AsNoTracking()
            .Include(t => t.Phases).ThenInclude(p => p.Tasks)
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<ProjectTemplate?> GetTemplateAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        return await _db.ProjectTemplates.AsNoTracking()
            .Include(t => t.Phases).ThenInclude(p => p.Tasks)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
    }

    public async Task<ProjectTemplate> CreateTemplateAsync(ProjectTemplate template, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTemplatesManage);
        var companyId = RequireCompanyId();
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new InvalidOperationException("Template name is required.");

        var duplicate = await _db.ProjectTemplates
            .AnyAsync(t => t.CompanyId == companyId && t.Name == template.Name.Trim(), ct);
        if (duplicate)
            throw new InvalidOperationException("A template with that name already exists.");

        var row = new ProjectTemplate
        {
            CompanyId = companyId,
            Name = template.Name.Trim(),
            Description = NullIfBlank(template.Description),
            ProjectType = NullIfBlank(template.ProjectType),
            IsActive = template.IsActive
        };

        var phaseOrder = 0;
        foreach (var phase in template.Phases)
        {
            var phaseRow = new ProjectTemplatePhase
            {
                CompanyId = companyId,
                Name = string.IsNullOrWhiteSpace(phase.Name) ? $"Phase {phaseOrder + 1}" : phase.Name.Trim(),
                SortOrder = phaseOrder++,
                EstimatedDurationDays = Math.Max(0, phase.EstimatedDurationDays)
            };
            var taskOrder = 0;
            foreach (var task in phase.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Name)) continue;
                phaseRow.Tasks.Add(new ProjectTemplateTask
                {
                    CompanyId = companyId,
                    Name = task.Name.Trim(),
                    Description = NullIfBlank(task.Description),
                    SortOrder = taskOrder++,
                    EstimatedHours = Math.Max(0, task.EstimatedHours),
                    DefaultRole = NullIfBlank(task.DefaultRole)
                });
            }
            row.Phases.Add(phaseRow);
        }

        _db.ProjectTemplates.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.template.created", "ProjectTemplate", row.Id, row.Name, ct);
        return row;
    }

    public async Task<bool> DeleteTemplateAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTemplatesManage);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTemplates
            .Include(t => t.Phases).ThenInclude(p => p.Tasks)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (row is null) return false;

        _db.ProjectTemplates.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Project> CloneTemplateToProjectAsync(int templateId, Project project, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsCreate);
        var companyId = RequireCompanyId();
        var template = await _db.ProjectTemplates.AsNoTracking()
            .Include(t => t.Phases).ThenInclude(p => p.Tasks)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Template not found.");

        project.TemplateId = template.Id;
        if (string.IsNullOrWhiteSpace(project.ProjectType))
            project.ProjectType = template.ProjectType;
        var created = await CreateProjectAsync(project, ct);

        var cursor = created.PlannedStartDate ?? DateTime.UtcNow.Date;
        foreach (var templatePhase in template.Phases.OrderBy(p => p.SortOrder))
        {
            var phase = new ProjectPhase
            {
                CompanyId = companyId,
                ProjectId = created.Id,
                Name = templatePhase.Name,
                SortOrder = templatePhase.SortOrder,
                StartDate = NormalizeUtc(cursor),
                DueDate = NormalizeUtc(cursor.AddDays(Math.Max(0, templatePhase.EstimatedDurationDays)))
            };
            _db.ProjectPhases.Add(phase);
            await _db.SaveChangesAsync(ct);

            foreach (var templateTask in templatePhase.Tasks.OrderBy(t => t.SortOrder))
            {
                _db.ProjectTasks.Add(new ProjectTask
                {
                    CompanyId = companyId,
                    ProjectId = created.Id,
                    PhaseId = phase.Id,
                    TaskNumber = await NextNumberAsync(companyId, ProjectSequenceKeys.Task, ct),
                    Name = templateTask.Name,
                    Description = templateTask.Description,
                    EstimatedHours = templateTask.EstimatedHours,
                    SortOrder = templateTask.SortOrder,
                    StartDate = phase.StartDate,
                    DueDate = phase.DueDate,
                    TaskStatus = ProjectTaskStatus.ToDo
                });
            }
            await _db.SaveChangesAsync(ct);
            cursor = cursor.AddDays(Math.Max(0, templatePhase.EstimatedDurationDays));
        }

        if (created.PlannedEndDate is null)
        {
            created.PlannedEndDate = NormalizeUtc(cursor);
        }
        created.EstimatedHours = template.Phases.SelectMany(p => p.Tasks).Sum(t => t.EstimatedHours);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.template.applied", "Project", created.Id, template.Name, ct);
        return created;
    }

    // ——————————————————————————— Numbering ———————————————————————————

    public async Task<string> NextNumberAsync(int companyId, string key, CancellationToken ct = default)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? ProjectSequenceKeys.Project : key.Trim().ToUpperInvariant();
        var year = DateTime.UtcNow.Year;

        var sequence = await _db.ProjectNumberSequences
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.SequenceKey == normalizedKey && s.Year == year, ct);
        if (sequence is null)
        {
            sequence = new ProjectNumberSequence
            {
                CompanyId = companyId,
                SequenceKey = normalizedKey,
                Year = year,
                NextValue = 1
            };
            _db.ProjectNumberSequences.Add(sequence);
        }

        var value = sequence.NextValue;
        sequence.NextValue = value + 1;
        await _db.SaveChangesAsync(ct);
        return $"{normalizedKey}-{year}-{value:000000}";
    }

    // ——————————————————————————— Shared helpers ———————————————————————————

    private IQueryable<Project> VisibleProjects(IQueryable<Project> query)
    {
        var companyId = RequireCompanyId();
        query = query.Where(p => p.CompanyId == companyId);
        if (_tenant.IsSystemAdmin || _modules.HasPermission(_tenant, Permissions.ProjectsViewAll))
            return query;

        var userId = _tenant.UserId ?? 0;
        return query.Where(p =>
            p.OwnerUserId == userId ||
            p.ManagerUserId == userId ||
            _db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId));
    }

    /// <summary>Loads a tracked project inside the tenant and enforces per-project visibility.</summary>
    private async Task<Project> RequireProjectAsync(int projectId, CancellationToken ct)
    {
        var companyId = RequireCompanyId();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Project not found for this account.");
        await EnsureProjectVisibleAsync(project, ct);
        return project;
    }

    private async Task EnsureProjectVisibleAsync(Project project, CancellationToken ct)
    {
        if (_tenant.IsSystemAdmin) return;
        if (_modules.HasPermission(_tenant, Permissions.ProjectsViewAll)) return;

        var userId = _tenant.UserId
            ?? throw new UnauthorizedAccessException("Sign in required.");
        if (project.OwnerUserId == userId || project.ManagerUserId == userId) return;

        var isMember = await _db.ProjectMembers.AsNoTracking()
            .AnyAsync(m => m.ProjectId == project.Id && m.UserId == userId, ct);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this project.");
    }

    private async Task EnsureMemberAsync(Project project, int userId, string role, CancellationToken ct)
    {
        var exists = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == userId, ct);
        if (exists) return;
        _db.ProjectMembers.Add(new ProjectMember
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            UserId = userId,
            ProjectRole = role
        });
    }

    private Task<bool> IsCompanyUserAsync(int userId, int companyId, CancellationToken ct) =>
        _db.CompanyUsers.AsNoTracking().AnyAsync(cu => cu.UserId == userId && cu.CompanyId == companyId, ct);

    private async Task ResolveCustomerAsync(Project project, CancellationToken ct)
    {
        if (project.CustomerId is not int customerId)
        {
            return;
        }

        var customer = await _db.CrmCustomers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == project.CompanyId, ct)
            ?? throw new InvalidOperationException("Customer not found for this account.");
        project.CustomerName = customer.CompanyName;
    }

    private static void Validate(Project project)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
            throw new InvalidOperationException("Project name is required.");
        if (project.PlannedStartDate is { } start && project.PlannedEndDate is { } end && end.Date < start.Date)
            throw new InvalidOperationException("Planned end date cannot be before the planned start date.");
        project.Name = project.Name.Trim();
    }

    private static void NormalizeProjectDates(Project project)
    {
        project.PlannedStartDate = NormalizeUtc(project.PlannedStartDate);
        project.PlannedEndDate = NormalizeUtc(project.PlannedEndDate);
        project.ActualStartDate = NormalizeUtc(project.ActualStartDate);
        project.ActualEndDate = NormalizeUtc(project.ActualEndDate);
    }

    private bool CanSeeFinancials() =>
        _tenant.IsSystemAdmin ||
        _modules.HasPermission(_tenant, Permissions.ProjectsFinancialsView) ||
        _modules.HasPermission(_tenant, Permissions.ProjectsBudgetView);

    private bool CanEditBudget() =>
        _tenant.IsSystemAdmin || _modules.HasPermission(_tenant, Permissions.ProjectsBudgetEdit);

    private int RequireCompanyId() =>
        _tenant.CompanyId ?? throw new InvalidOperationException("No company context.");

    private int RequireUserId() =>
        _tenant.UserId ?? throw new UnauthorizedAccessException("Sign in required.");

    private void EnsureAccess(string permission)
    {
        if (!_tenant.IsAuthenticated)
            throw new UnauthorizedAccessException("Sign in required.");
        if (_tenant.IsSystemAdmin) return;
        if (!_modules.HasEntitlement(_tenant, ModuleKeys.Projects))
            throw new UnauthorizedAccessException("Projects module is not enabled for this account.");
        if (!_modules.HasPermission(_tenant, permission))
            throw new UnauthorizedAccessException("You do not have permission for this action.");
    }

    private Task AuditAsync(string action, string entityType, int entityId, string? description, CancellationToken ct) =>
        _audit.WriteAsync(
            action,
            companyId: _tenant.CompanyId,
            userId: _tenant.UserId,
            entityType: entityType,
            entityId: entityId.ToString(),
            afterValues: description,
            ct: ct);

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null) return null;
        var d = value.Value;
        return d.Kind switch
        {
            DateTimeKind.Utc => d,
            DateTimeKind.Local => d.ToUniversalTime(),
            _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
        };
    }

    private static DateTime NormalizeUtc(DateTime value) => NormalizeUtc((DateTime?)value)!.Value;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class ProjectSequenceKeys
{
    public const string Project = "PRJ";
    public const string Task = "TASK";
    public const string Issue = "ISS";
    public const string Risk = "RSK";
    public const string ChangeRequest = "CR";
}
