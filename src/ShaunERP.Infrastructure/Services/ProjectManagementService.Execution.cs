using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Platform;
using ShaunERP.Core.Projects;

namespace ShaunERP.Infrastructure.Services;

public sealed partial class ProjectManagementService
{
    private static readonly ProjectTaskStatus[] OpenTaskStatuses =
    [
        ProjectTaskStatus.Backlog, ProjectTaskStatus.ToDo, ProjectTaskStatus.InProgress,
        ProjectTaskStatus.Blocked, ProjectTaskStatus.Review, ProjectTaskStatus.Testing
    ];

    // ——————————————————————————— Tasks ———————————————————————————

    public async Task<ProjectTaskPage> ListTasksAsync(ProjectTaskFilter? filter = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksView);
        filter ??= new ProjectTaskFilter();
        var companyId = RequireCompanyId();
        var today = DateTime.UtcNow.Date;

        var visibleProjectIds = VisibleProjectIds();
        var query = _db.ProjectTasks.AsNoTracking()
            .Where(t => t.CompanyId == companyId)
            .Where(t => visibleProjectIds.Contains(t.ProjectId));

        if (filter.ProjectId is { } projectId)
            query = query.Where(t => t.ProjectId == projectId);
        if (filter.AssigneeUserId is { } assignee)
            query = query.Where(t => t.AssigneeUserId == assignee);
        if (filter.Status is { } status)
            query = query.Where(t => t.TaskStatus == status);
        if (filter.Priority is { } priority)
            query = query.Where(t => t.Priority == priority);
        if (filter.DueBefore is { } dueBefore)
            query = query.Where(t => t.DueDate != null && t.DueDate <= dueBefore);
        if (filter.OnlyOverdue)
            query = query.Where(t => t.DueDate != null && t.DueDate < today && OpenTaskStatuses.Contains(t.TaskStatus));
        if (filter.ExcludeCompleted)
            query = query.Where(t => t.TaskStatus != ProjectTaskStatus.Completed && t.TaskStatus != ProjectTaskStatus.Cancelled);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.TaskNumber.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(t => t.Project)
            .Include(t => t.AssigneeUser)
            .OrderBy(t => t.TaskStatus == ProjectTaskStatus.Completed || t.TaskStatus == ProjectTaskStatus.Cancelled)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.Priority)
            .Skip(Math.Max(0, filter.Skip))
            .Take(ListQuery.NormalizePageSize(filter.Take <= 0 ? 50 : filter.Take))
            .ToListAsync(ct);

        return new ProjectTaskPage(items, total);
    }

    public async Task<ProjectTask?> GetTaskAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksView);
        var companyId = RequireCompanyId();
        var task = await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.AssigneeUser)
            .Include(t => t.ChecklistItems)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (task is null) return null;
        await RequireProjectAsync(task.ProjectId, ct);
        return task;
    }

    public async Task<ProjectTask> CreateTaskAsync(ProjectTask task, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksCreate);
        var project = await RequireProjectAsync(task.ProjectId, ct);
        if (string.IsNullOrWhiteSpace(task.Name))
            throw new InvalidOperationException("Task name is required.");
        if (task.AssigneeUserId is { } assignee && !await IsCompanyUserAsync(assignee, project.CompanyId, ct))
            throw new InvalidOperationException("Assignee does not belong to this account.");

        var row = new ProjectTask
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            PhaseId = task.PhaseId,
            MilestoneId = task.MilestoneId,
            ParentTaskId = task.ParentTaskId,
            TaskNumber = string.IsNullOrWhiteSpace(task.TaskNumber)
                ? await NextNumberAsync(project.CompanyId, ProjectSequenceKeys.Task, ct)
                : task.TaskNumber.Trim(),
            Name = task.Name.Trim(),
            Description = NullIfBlank(task.Description),
            AssigneeUserId = task.AssigneeUserId,
            Priority = task.Priority,
            TaskStatus = task.TaskStatus,
            StartDate = NormalizeUtc(task.StartDate),
            DueDate = NormalizeUtc(task.DueDate),
            EstimatedHours = Math.Max(0, task.EstimatedHours),
            IsBillable = task.IsBillable,
            ProgressPercent = Math.Clamp(task.ProgressPercent, 0, 100),
            Tags = NullIfBlank(task.Tags),
            SortOrder = task.SortOrder
        };
        if (row.TaskStatus == ProjectTaskStatus.Completed)
        {
            row.CompletedDate = DateTime.UtcNow;
            row.ProgressPercent = 100;
        }

        _db.ProjectTasks.Add(row);
        await _db.SaveChangesAsync(ct);
        await RecalculateProjectProgressAsync(project.Id, ct);
        await AuditAsync("projects.task.created", "ProjectTask", row.Id, row.TaskNumber, ct);
        return row;
    }

    public async Task<ProjectTask?> UpdateTaskAsync(int id, ProjectTask update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        if (update.AssigneeUserId != row.AssigneeUserId)
        {
            EnsureAccess(Permissions.ProjectsTasksAssign);
            if (update.AssigneeUserId is { } assignee && !await IsCompanyUserAsync(assignee, companyId, ct))
                throw new InvalidOperationException("Assignee does not belong to this account.");
            row.AssigneeUserId = update.AssigneeUserId;
        }

        row.PhaseId = update.PhaseId;
        row.MilestoneId = update.MilestoneId;
        row.Name = string.IsNullOrWhiteSpace(update.Name) ? row.Name : update.Name.Trim();
        row.Description = NullIfBlank(update.Description);
        row.Priority = update.Priority;
        row.StartDate = NormalizeUtc(update.StartDate);
        row.DueDate = NormalizeUtc(update.DueDate);
        row.EstimatedHours = Math.Max(0, update.EstimatedHours);
        row.IsBillable = update.IsBillable;
        row.ProgressPercent = Math.Clamp(update.ProgressPercent, 0, 100);
        row.Tags = NullIfBlank(update.Tags);
        row.SortOrder = update.SortOrder;

        if (update.TaskStatus != row.TaskStatus)
        {
            await ApplyTaskStatusAsync(row, update.TaskStatus, ct);
        }

        await _db.SaveChangesAsync(ct);
        await RecalculateProjectProgressAsync(row.ProjectId, ct);
        await AuditAsync("projects.task.updated", "ProjectTask", row.Id, row.TaskNumber, ct);
        return row;
    }

    public async Task<ProjectTask?> UpdateTaskStatusAsync(int id, ProjectTaskStatus status, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        await ApplyTaskStatusAsync(row, status, ct);
        await _db.SaveChangesAsync(ct);
        await RecalculateProjectProgressAsync(row.ProjectId, ct);
        await AuditAsync("projects.task.status_changed", "ProjectTask", row.Id, $"{row.TaskNumber}:{status}", ct);
        return row;
    }

    public async Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksDelete);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireProjectAsync(row.ProjectId, ct);

        var hasTime = await _db.ProjectTimesheets.AnyAsync(t => t.TaskId == row.Id, ct);
        if (hasTime)
            throw new InvalidOperationException("Tasks with logged time cannot be deleted. Cancel the task instead.");
        if (await _db.ProjectTasks.AnyAsync(t => t.ParentTaskId == row.Id, ct))
            throw new InvalidOperationException("Delete or reparent the subtasks first.");

        var dependencies = await _db.ProjectTaskDependencies
            .Where(d => d.PredecessorTaskId == row.Id || d.SuccessorTaskId == row.Id)
            .ToListAsync(ct);
        _db.ProjectTaskDependencies.RemoveRange(dependencies);
        _db.ProjectTasks.Remove(row);
        await _db.SaveChangesAsync(ct);
        await RecalculateProjectProgressAsync(row.ProjectId, ct);
        await AuditAsync("projects.task.deleted", "ProjectTask", id, row.TaskNumber, ct);
        return true;
    }

    /// <summary>Applies status side effects. A task cannot complete while an incomplete predecessor blocks it.</summary>
    private async Task ApplyTaskStatusAsync(ProjectTask row, ProjectTaskStatus status, CancellationToken ct)
    {
        if (status == ProjectTaskStatus.Completed)
        {
            var blockingIds = await _db.ProjectTaskDependencies.AsNoTracking()
                .Where(d => d.SuccessorTaskId == row.Id && d.DependencyType == TaskDependencyType.FinishToStart)
                .Select(d => d.PredecessorTaskId)
                .ToListAsync(ct);
            if (blockingIds.Count > 0)
            {
                var openPredecessor = await _db.ProjectTasks.AsNoTracking()
                    .Where(t => blockingIds.Contains(t.Id))
                    .Where(t => t.TaskStatus != ProjectTaskStatus.Completed && t.TaskStatus != ProjectTaskStatus.Cancelled)
                    .Select(t => t.TaskNumber)
                    .FirstOrDefaultAsync(ct);
                if (openPredecessor is not null)
                    throw new InvalidOperationException($"Predecessor task {openPredecessor} must be completed first.");
            }

            row.CompletedDate = DateTime.UtcNow;
            row.ProgressPercent = 100;
        }
        else
        {
            row.CompletedDate = null;
            if (row.ProgressPercent >= 100) row.ProgressPercent = 0;
            if (status == ProjectTaskStatus.InProgress && row.ProgressPercent <= 0) row.ProgressPercent = 10;
        }

        row.TaskStatus = status;
    }

    // ——————————————————————————— Checklist ———————————————————————————

    public async Task<ProjectTaskChecklistItem> AddChecklistItemAsync(int taskId, string title, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var task = await RequireTaskAsync(taskId, ct);
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Checklist item text is required.");

        var order = await _db.ProjectTaskChecklistItems.CountAsync(c => c.TaskId == task.Id, ct);
        var row = new ProjectTaskChecklistItem
        {
            CompanyId = task.CompanyId,
            TaskId = task.Id,
            Title = title.Trim(),
            SortOrder = order
        };
        _db.ProjectTaskChecklistItems.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectTaskChecklistItem?> ToggleChecklistItemAsync(int itemId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTaskChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireTaskAsync(row.TaskId, ct);

        row.IsCompleted = !row.IsCompleted;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> DeleteChecklistItemAsync(int itemId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTaskChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireTaskAsync(row.TaskId, ct);

        _db.ProjectTaskChecklistItems.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ——————————————————————————— Dependencies ———————————————————————————

    public async Task<ProjectTaskDependency> AddDependencyAsync(
        int predecessorTaskId,
        int successorTaskId,
        TaskDependencyType type = TaskDependencyType.FinishToStart,
        CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        if (predecessorTaskId == successorTaskId)
            throw new InvalidOperationException("A task cannot depend on itself.");

        var predecessor = await RequireTaskAsync(predecessorTaskId, ct);
        var successor = await RequireTaskAsync(successorTaskId, ct);
        if (predecessor.ProjectId != successor.ProjectId)
            throw new InvalidOperationException("Dependencies must stay within the same project.");

        var duplicate = await _db.ProjectTaskDependencies.AnyAsync(
            d => d.PredecessorTaskId == predecessorTaskId && d.SuccessorTaskId == successorTaskId, ct);
        if (duplicate)
            throw new InvalidOperationException("That dependency already exists.");

        if (await CreatesCycleAsync(predecessorTaskId, successorTaskId, successor.ProjectId, ct))
            throw new InvalidOperationException("That dependency would create a circular chain.");

        var row = new ProjectTaskDependency
        {
            CompanyId = successor.CompanyId,
            PredecessorTaskId = predecessorTaskId,
            SuccessorTaskId = successorTaskId,
            DependencyType = type
        };
        _db.ProjectTaskDependencies.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> RemoveDependencyAsync(int dependencyId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTaskDependencies.FirstOrDefaultAsync(d => d.Id == dependencyId && d.CompanyId == companyId, ct);
        if (row is null) return false;
        await RequireTaskAsync(row.SuccessorTaskId, ct);

        _db.ProjectTaskDependencies.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ProjectTask>> GetBlockedByAsync(int taskId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksView);
        var task = await RequireTaskAsync(taskId, ct);
        var ids = await _db.ProjectTaskDependencies.AsNoTracking()
            .Where(d => d.SuccessorTaskId == task.Id)
            .Select(d => d.PredecessorTaskId)
            .ToListAsync(ct);
        return await _db.ProjectTasks.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectTask>> GetBlockingAsync(int taskId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksView);
        var task = await RequireTaskAsync(taskId, ct);
        var ids = await _db.ProjectTaskDependencies.AsNoTracking()
            .Where(d => d.PredecessorTaskId == task.Id)
            .Select(d => d.SuccessorTaskId)
            .ToListAsync(ct);
        return await _db.ProjectTasks.AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync(ct);
    }

    /// <summary>
    /// Depth-first walk forward from the proposed successor. If the proposed predecessor is
    /// reachable, adding the edge would close a loop.
    /// </summary>
    private async Task<bool> CreatesCycleAsync(int predecessorTaskId, int successorTaskId, int projectId, CancellationToken ct)
    {
        var taskIds = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var edges = await _db.ProjectTaskDependencies.AsNoTracking()
            .Where(d => taskIds.Contains(d.PredecessorTaskId))
            .Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId })
            .ToListAsync(ct);

        var adjacency = edges
            .GroupBy(e => e.PredecessorTaskId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.SuccessorTaskId).ToList());

        var visited = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(successorTaskId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == predecessorTaskId) return true;
            if (!visited.Add(current)) continue;
            if (!adjacency.TryGetValue(current, out var next)) continue;
            foreach (var n in next) stack.Push(n);
        }

        return false;
    }

    // ——————————————————————————— My work ———————————————————————————

    public async Task<MyWorkBoard> MyWorkAsync(int? userId = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTasksView);
        var companyId = RequireCompanyId();
        var targetUserId = userId ?? RequireUserId();
        if (targetUserId != _tenant.UserId)
            EnsureAccess(Permissions.ProjectsViewAll);

        var today = DateTime.UtcNow.Date;
        var weekEnd = today.AddDays(7);
        var recentCutoff = today.AddDays(-7);

        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.CompanyId == companyId && t.AssigneeUserId == targetUserId)
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .Take(500)
            .ToListAsync(ct);

        var open = tasks.Where(t => OpenTaskStatuses.Contains(t.TaskStatus)).ToList();

        return new MyWorkBoard(
            open.Where(t => t.DueDate != null && t.DueDate.Value.Date == today).ToList(),
            open.Where(t => t.DueDate != null && t.DueDate.Value.Date < today).ToList(),
            open.Where(t => t.DueDate != null && t.DueDate.Value.Date > today && t.DueDate.Value.Date <= weekEnd).ToList(),
            open.Where(t => t.TaskStatus == ProjectTaskStatus.InProgress).ToList(),
            open.Where(t => t.TaskStatus == ProjectTaskStatus.Blocked).ToList(),
            tasks.Where(t => t.TaskStatus == ProjectTaskStatus.Completed && t.CompletedDate >= recentCutoff)
                .OrderByDescending(t => t.CompletedDate)
                .ToList());
    }

    // ——————————————————————————— Comments ———————————————————————————

    public async Task<IReadOnlyList<ProjectComment>> ListCommentsAsync(
        ProjectCommentEntityType entityType, int entityId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        await EnsureEntityVisibleAsync(entityType, entityId, ct);
        return await _db.ProjectComments.AsNoTracking()
            .Include(c => c.AuthorUser)
            .Where(c => c.CompanyId == companyId && c.EntityType == entityType && c.EntityId == entityId)
            .OrderBy(c => c.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<ProjectComment> AddCommentAsync(
        ProjectCommentEntityType entityType,
        int entityId,
        int? projectId,
        string body,
        int? parentCommentId = null,
        CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Comment text is required.");

        var resolvedProjectId = await EnsureEntityVisibleAsync(entityType, entityId, ct) ?? projectId;
        var row = new ProjectComment
        {
            CompanyId = companyId,
            EntityType = entityType,
            EntityId = entityId,
            ProjectId = resolvedProjectId,
            AuthorUserId = RequireUserId(),
            Body = body.Trim(),
            ParentCommentId = parentCommentId
        };
        _db.ProjectComments.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    // ——————————————————————————— Attachments ———————————————————————————

    public async Task<IReadOnlyList<ProjectAttachment>> ListAttachmentsAsync(
        ProjectCommentEntityType entityType, int entityId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        await EnsureEntityVisibleAsync(entityType, entityId, ct);
        return await _db.ProjectAttachments.AsNoTracking()
            .Include(a => a.UploadedByUser)
            .Where(a => a.CompanyId == companyId && a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Id)
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<ProjectAttachment> UploadAttachmentAsync(ProjectAttachmentUpload upload, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        _files.Validate(upload.FileName, upload.Content.LongLength);

        var resolvedProjectId = await EnsureEntityVisibleAsync(upload.EntityType, upload.EntityId, ct) ?? upload.ProjectId;
        var storagePath = await _files.SaveAsync(companyId, upload.FileName, upload.Content, ct);

        var row = new ProjectAttachment
        {
            CompanyId = companyId,
            EntityType = upload.EntityType,
            EntityId = upload.EntityId,
            ProjectId = resolvedProjectId,
            FileName = Path.GetFileName(upload.FileName),
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? "application/octet-stream" : upload.ContentType,
            FileSizeBytes = upload.Content.LongLength,
            StoragePath = storagePath,
            UploadedByUserId = RequireUserId()
        };
        _db.ProjectAttachments.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.attachment.uploaded", "ProjectAttachment", row.Id, row.FileName, ct);
        return row;
    }

    public async Task<ProjectAttachmentContent?> DownloadAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CompanyId == companyId, ct);
        if (row is null) return null;
        await EnsureEntityVisibleAsync(row.EntityType, row.EntityId, ct);

        var content = await _files.ReadAsync(row.StoragePath, ct);
        return content is null ? null : new ProjectAttachmentContent(row, content);
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsUpdate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId && a.CompanyId == companyId, ct);
        if (row is null) return false;
        await EnsureEntityVisibleAsync(row.EntityType, row.EntityId, ct);

        _db.ProjectAttachments.Remove(row);
        await _db.SaveChangesAsync(ct);
        _files.Delete(row.StoragePath);
        await AuditAsync("projects.attachment.deleted", "ProjectAttachment", attachmentId, row.FileName, ct);
        return true;
    }

    // ——————————————————————————— Issues ———————————————————————————

    public async Task<IReadOnlyList<Issue>> ListIssuesAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        return await _db.Issues.AsNoTracking()
            .Include(i => i.OwnerUser)
            .Where(i => i.ProjectId == project.Id && i.CompanyId == project.CompanyId)
            .OrderByDescending(i => i.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<Issue> CreateIssueAsync(Issue issue, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsIssuesManage);
        var project = await RequireProjectAsync(issue.ProjectId, ct);
        if (string.IsNullOrWhiteSpace(issue.Title))
            throw new InvalidOperationException("Issue title is required.");

        var row = new Issue
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            IssueNumber = string.IsNullOrWhiteSpace(issue.IssueNumber)
                ? await NextNumberAsync(project.CompanyId, ProjectSequenceKeys.Issue, ct)
                : issue.IssueNumber.Trim(),
            Title = issue.Title.Trim(),
            Description = NullIfBlank(issue.Description),
            Type = issue.Type,
            Severity = issue.Severity,
            Priority = issue.Priority,
            IssueStatus = issue.IssueStatus,
            OwnerUserId = issue.OwnerUserId,
            DateRaised = NormalizeUtc(issue.DateRaised == default ? DateTime.UtcNow : issue.DateRaised),
            TargetResolutionDate = NormalizeUtc(issue.TargetResolutionDate),
            DueDate = NormalizeUtc(issue.DueDate)
        };
        _db.Issues.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.issue.created", "Issue", row.Id, row.IssueNumber, ct);
        return row;
    }

    public async Task<Issue?> UpdateIssueAsync(int id, Issue update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsIssuesManage);
        var companyId = RequireCompanyId();
        var row = await _db.Issues.FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        row.Title = string.IsNullOrWhiteSpace(update.Title) ? row.Title : update.Title.Trim();
        row.Description = NullIfBlank(update.Description);
        row.Type = update.Type;
        row.Severity = update.Severity;
        row.Priority = update.Priority;
        row.OwnerUserId = update.OwnerUserId;
        row.TargetResolutionDate = NormalizeUtc(update.TargetResolutionDate);
        row.DueDate = NormalizeUtc(update.DueDate);
        row.RootCause = NullIfBlank(update.RootCause);
        row.Resolution = NullIfBlank(update.Resolution);

        if (update.IssueStatus != row.IssueStatus)
        {
            row.IssueStatus = update.IssueStatus;
            row.ActualResolutionDate = update.IssueStatus is ProjectIssueStatus.Resolved or ProjectIssueStatus.Closed
                ? DateTime.UtcNow
                : null;
        }

        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.issue.updated", "Issue", row.Id, row.IssueNumber, ct);
        return row;
    }

    // ——————————————————————————— Risks ———————————————————————————

    public async Task<IReadOnlyList<ProjectRisk>> ListRisksAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        var risks = await _db.ProjectRisks.AsNoTracking()
            .Include(r => r.OwnerUser)
            .Where(r => r.ProjectId == project.Id && r.CompanyId == project.CompanyId)
            .ToListAsync(ct);
        return risks.OrderByDescending(r => r.RiskScore).ThenByDescending(r => r.Id).ToList();
    }

    public async Task<ProjectRisk> CreateRiskAsync(ProjectRisk risk, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsRisksManage);
        var project = await RequireProjectAsync(risk.ProjectId, ct);
        if (string.IsNullOrWhiteSpace(risk.Description))
            throw new InvalidOperationException("Risk description is required.");

        var row = new ProjectRisk
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            RiskNumber = string.IsNullOrWhiteSpace(risk.RiskNumber)
                ? await NextNumberAsync(project.CompanyId, ProjectSequenceKeys.Risk, ct)
                : risk.RiskNumber.Trim(),
            Description = risk.Description.Trim(),
            Probability = Math.Clamp(risk.Probability, 1, 5),
            Impact = Math.Clamp(risk.Impact, 1, 5),
            OwnerUserId = risk.OwnerUserId,
            MitigationPlan = NullIfBlank(risk.MitigationPlan),
            ContingencyPlan = NullIfBlank(risk.ContingencyPlan),
            RiskStatus = risk.RiskStatus,
            ReviewDate = NormalizeUtc(risk.ReviewDate)
        };
        _db.ProjectRisks.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.risk.created", "ProjectRisk", row.Id, row.RiskNumber, ct);
        return row;
    }

    public async Task<ProjectRisk?> UpdateRiskAsync(int id, ProjectRisk update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsRisksManage);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectRisks.FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);

        row.Description = string.IsNullOrWhiteSpace(update.Description) ? row.Description : update.Description.Trim();
        row.Probability = Math.Clamp(update.Probability, 1, 5);
        row.Impact = Math.Clamp(update.Impact, 1, 5);
        row.OwnerUserId = update.OwnerUserId;
        row.MitigationPlan = NullIfBlank(update.MitigationPlan);
        row.ContingencyPlan = NullIfBlank(update.ContingencyPlan);
        row.RiskStatus = update.RiskStatus;
        row.ReviewDate = NormalizeUtc(update.ReviewDate);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    // ——————————————————————————— Change requests ———————————————————————————

    public async Task<IReadOnlyList<ProjectChangeRequest>> ListChangeRequestsAsync(int projectId, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsView);
        var project = await RequireProjectAsync(projectId, ct);
        return await _db.ProjectChangeRequests.AsNoTracking()
            .Where(c => c.ProjectId == project.Id && c.CompanyId == project.CompanyId)
            .OrderByDescending(c => c.Id)
            .Take(300)
            .ToListAsync(ct);
    }

    public async Task<ProjectChangeRequest> CreateChangeRequestAsync(ProjectChangeRequest request, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsChangeRequestsCreate);
        var project = await RequireProjectAsync(request.ProjectId, ct);
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("Change description is required.");

        var row = new ProjectChangeRequest
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            ChangeRequestNumber = string.IsNullOrWhiteSpace(request.ChangeRequestNumber)
                ? await NextNumberAsync(project.CompanyId, ProjectSequenceKeys.ChangeRequest, ct)
                : request.ChangeRequestNumber.Trim(),
            CustomerId = request.CustomerId ?? project.CustomerId,
            RequestedBy = NullIfBlank(request.RequestedBy),
            RequestedDate = NormalizeUtc(request.RequestedDate == default ? DateTime.UtcNow : request.RequestedDate),
            Description = request.Description.Trim(),
            BusinessReason = NullIfBlank(request.BusinessReason),
            EstimatedHours = Math.Max(0, request.EstimatedHours),
            EstimatedCost = Math.Max(0, request.EstimatedCost),
            ScheduleImpact = NullIfBlank(request.ScheduleImpact),
            Priority = request.Priority,
            ApprovalStatus = request.ApprovalStatus
        };
        _db.ProjectChangeRequests.Add(row);
        await _db.SaveChangesAsync(ct);
        await AuditAsync("projects.change_request.created", "ProjectChangeRequest", row.Id, row.ChangeRequestNumber, ct);
        return row;
    }

    public async Task<ProjectChangeRequest?> UpdateChangeRequestAsync(int id, ProjectChangeRequest update, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsChangeRequestsCreate);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);
        if (row.ApprovalStatus is ChangeRequestApprovalStatus.Approved or ChangeRequestApprovalStatus.Rejected)
            throw new InvalidOperationException("Decided change requests can no longer be edited.");

        row.Description = string.IsNullOrWhiteSpace(update.Description) ? row.Description : update.Description.Trim();
        row.BusinessReason = NullIfBlank(update.BusinessReason);
        row.EstimatedHours = Math.Max(0, update.EstimatedHours);
        row.EstimatedCost = Math.Max(0, update.EstimatedCost);
        row.ScheduleImpact = NullIfBlank(update.ScheduleImpact);
        row.Priority = update.Priority;
        row.RequestedBy = NullIfBlank(update.RequestedBy);
        row.ImplementationStatus = NullIfBlank(update.ImplementationStatus);
        if (update.ApprovalStatus is ChangeRequestApprovalStatus.Draft
            or ChangeRequestApprovalStatus.Submitted
            or ChangeRequestApprovalStatus.UnderReview
            or ChangeRequestApprovalStatus.Cancelled)
        {
            row.ApprovalStatus = update.ApprovalStatus;
        }

        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectChangeRequest?> ApproveChangeRequestAsync(int id, bool approved, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsChangeRequestsApprove);
        var companyId = RequireCompanyId();
        var row = await _db.ProjectChangeRequests.FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == companyId, ct);
        if (row is null) return null;
        if (row.ApprovalStatus is ChangeRequestApprovalStatus.Approved
            or ChangeRequestApprovalStatus.Rejected
            or ChangeRequestApprovalStatus.Implemented)
        {
            throw new InvalidOperationException("This change request has already been decided.");
        }

        var project = await RequireProjectAsync(row.ProjectId, ct);

        row.ApprovalStatus = approved ? ChangeRequestApprovalStatus.Approved : ChangeRequestApprovalStatus.Rejected;
        row.ApprovedByUserId = RequireUserId();
        row.ApprovedDate = DateTime.UtcNow;

        if (approved && row.EstimatedCost > 0)
        {
            var baseline = project.RevisedBudget > 0 ? project.RevisedBudget : project.Budget;
            project.RevisedBudget = baseline + row.EstimatedCost;
        }
        if (approved && row.EstimatedHours > 0)
        {
            project.EstimatedHours += row.EstimatedHours;
        }

        await _db.SaveChangesAsync(ct);
        await AuditAsync(
            approved ? "projects.change_request.approved" : "projects.change_request.rejected",
            "ProjectChangeRequest", row.Id, row.ChangeRequestNumber, ct);
        return row;
    }

    // ——————————————————————————— Timesheets ———————————————————————————

    public async Task<IReadOnlyList<ProjectTimesheet>> ListTimesheetsAsync(TimesheetFilter? filter = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsCreate);
        filter ??= new TimesheetFilter();
        var companyId = RequireCompanyId();

        var query = _db.ProjectTimesheets.AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Task)
            .Include(t => t.User)
            .Where(t => t.CompanyId == companyId);

        var canSeeOthers = _tenant.IsSystemAdmin
            || _modules.HasPermission(_tenant, Permissions.ProjectsTimesheetsApprove)
            || _modules.HasPermission(_tenant, Permissions.ProjectsViewAll);
        if (filter.OnlyMine || !canSeeOthers)
        {
            var me = RequireUserId();
            query = query.Where(t => t.UserId == me);
        }
        else if (filter.UserId is { } userId)
        {
            query = query.Where(t => t.UserId == userId);
        }

        if (filter.ProjectId is { } projectId)
            query = query.Where(t => t.ProjectId == projectId);
        if (filter.Status is { } status)
            query = query.Where(t => t.EntryStatus == status);
        if (filter.From is { } from)
        {
            var fromUtc = NormalizeUtc(from);
            query = query.Where(t => t.WorkDate >= fromUtc);
        }
        if (filter.To is { } to)
        {
            var toUtc = NormalizeUtc(to);
            query = query.Where(t => t.WorkDate <= toUtc);
        }

        return await query
            .OrderByDescending(t => t.WorkDate).ThenByDescending(t => t.Id)
            .Take(Math.Clamp(filter.Take, 1, 1000))
            .ToListAsync(ct);
    }

    public async Task<ProjectTimesheet> CreateTimesheetAsync(ProjectTimesheet entry, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsCreate);
        var project = await RequireProjectAsync(entry.ProjectId, ct);
        var userId = entry.UserId > 0 ? entry.UserId : RequireUserId();
        if (userId != _tenant.UserId)
            EnsureAccess(Permissions.ProjectsTimesheetsApprove);
        if (entry.Hours <= 0)
            throw new InvalidOperationException("Log more than zero hours.");
        if (entry.Hours > 24)
            throw new InvalidOperationException("A single entry cannot exceed 24 hours.");
        if (entry.TaskId is { } taskId &&
            !await _db.ProjectTasks.AnyAsync(t => t.Id == taskId && t.ProjectId == project.Id, ct))
            throw new InvalidOperationException("Task does not belong to this project.");

        var billable = Math.Clamp(entry.BillableHours, 0, entry.Hours);
        var row = new ProjectTimesheet
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            TaskId = entry.TaskId,
            UserId = userId,
            WorkDate = NormalizeUtc(entry.WorkDate == default ? DateTime.UtcNow.Date : entry.WorkDate.Date),
            StartTime = entry.StartTime,
            EndTime = entry.EndTime,
            Hours = entry.Hours,
            BillableHours = billable,
            NonBillableHours = entry.Hours - billable,
            Description = NullIfBlank(entry.Description),
            EntryStatus = TimesheetEntryStatus.Draft
        };
        _db.ProjectTimesheets.Add(row);
        await _db.SaveChangesAsync(ct);
        await SyncTaskAndProjectHoursAsync(row.ProjectId, row.TaskId, ct);
        return row;
    }

    public async Task<ProjectTimesheet?> SubmitTimesheetAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsSubmit);
        var row = await RequireTimesheetAsync(id, ct);
        if (row is null) return null;
        if (row.UserId != _tenant.UserId && !_tenant.IsSystemAdmin)
            EnsureAccess(Permissions.ProjectsTimesheetsApprove);
        if (row.EntryStatus == TimesheetEntryStatus.Approved)
            throw new InvalidOperationException("Approved entries cannot be resubmitted.");

        row.EntryStatus = TimesheetEntryStatus.Submitted;
        row.SubmittedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectTimesheet?> ApproveTimesheetAsync(int id, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsApprove);
        var row = await RequireTimesheetAsync(id, ct);
        if (row is null) return null;
        if (row.EntryStatus != TimesheetEntryStatus.Submitted)
            throw new InvalidOperationException("Only submitted entries can be approved.");

        row.EntryStatus = TimesheetEntryStatus.Approved;
        row.ApprovedByUserId = RequireUserId();
        row.ApprovalDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SyncTaskAndProjectHoursAsync(row.ProjectId, row.TaskId, ct);
        await AuditAsync("projects.timesheet.approved", "ProjectTimesheet", row.Id, row.Hours.ToString("0.##"), ct);
        return row;
    }

    public async Task<ProjectTimesheet?> RejectTimesheetAsync(int id, string? reason = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsApprove);
        var row = await RequireTimesheetAsync(id, ct);
        if (row is null) return null;
        if (row.EntryStatus != TimesheetEntryStatus.Submitted)
            throw new InvalidOperationException("Only submitted entries can be rejected.");

        row.EntryStatus = TimesheetEntryStatus.Rejected;
        row.ApprovedByUserId = RequireUserId();
        row.ApprovalDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
            row.Description = $"{row.Description} · Rejected: {reason.Trim()}".Trim(' ', '·');
        await _db.SaveChangesAsync(ct);
        await SyncTaskAndProjectHoursAsync(row.ProjectId, row.TaskId, ct);
        await AuditAsync("projects.timesheet.rejected", "ProjectTimesheet", row.Id, reason, ct);
        return row;
    }

    // ——————————————————————————— Timer ———————————————————————————

    public async Task<ProjectTimeTracker?> GetActiveTimerAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsCreate);
        var companyId = RequireCompanyId();
        var userId = RequireUserId();
        return await _db.ProjectTimeTrackers.AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Task)
            .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.UserId == userId && t.IsActive, ct);
    }

    public async Task<ProjectTimeTracker> StartTimerAsync(
        int projectId, int? taskId = null, string? description = null, CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsCreate);
        var project = await RequireProjectAsync(projectId, ct);
        var userId = RequireUserId();

        var running = await _db.ProjectTimeTrackers
            .FirstOrDefaultAsync(t => t.CompanyId == project.CompanyId && t.UserId == userId && t.IsActive, ct);
        if (running is not null)
            throw new InvalidOperationException("A timer is already running. Stop it before starting another.");

        if (taskId is { } tid && !await _db.ProjectTasks.AnyAsync(t => t.Id == tid && t.ProjectId == project.Id, ct))
            throw new InvalidOperationException("Task does not belong to this project.");

        var row = new ProjectTimeTracker
        {
            CompanyId = project.CompanyId,
            UserId = userId,
            ProjectId = project.Id,
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
            IsActive = true,
            Description = NullIfBlank(description)
        };
        _db.ProjectTimeTrackers.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ProjectTimesheet?> StopTimerAsync(CancellationToken ct = default)
    {
        EnsureAccess(Permissions.ProjectsTimesheetsCreate);
        var companyId = RequireCompanyId();
        var userId = RequireUserId();

        var running = await _db.ProjectTimeTrackers
            .FirstOrDefaultAsync(t => t.CompanyId == companyId && t.UserId == userId && t.IsActive, ct);
        if (running is null) return null;

        var stoppedAt = DateTime.UtcNow;
        running.StoppedAt = stoppedAt;
        running.IsActive = false;

        var hours = Math.Round((decimal)(stoppedAt - running.StartedAt).TotalHours, 2, MidpointRounding.AwayFromZero);
        if (hours <= 0) hours = 0.01m;
        await _db.SaveChangesAsync(ct);

        return await CreateTimesheetAsync(new ProjectTimesheet
        {
            ProjectId = running.ProjectId,
            TaskId = running.TaskId,
            UserId = userId,
            WorkDate = running.StartedAt.Date,
            Hours = hours,
            BillableHours = hours,
            Description = running.Description ?? "Tracked with timer"
        }, ct);
    }

    // ——————————————————————————— Execution helpers ———————————————————————————

    private IQueryable<int> VisibleProjectIds() => VisibleProjects(_db.Projects.AsNoTracking()).Select(p => p.Id);

    private async Task<ProjectTask> RequireTaskAsync(int taskId, CancellationToken ct)
    {
        var companyId = RequireCompanyId();
        var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Task not found for this account.");
        await RequireProjectAsync(task.ProjectId, ct);
        return task;
    }

    private async Task<ProjectTimesheet?> RequireTimesheetAsync(int id, CancellationToken ct)
    {
        var companyId = RequireCompanyId();
        var row = await _db.ProjectTimesheets.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId, ct);
        if (row is null) return null;
        await RequireProjectAsync(row.ProjectId, ct);
        return row;
    }

    /// <summary>Resolves the owning project of a commentable/attachable entity and enforces visibility.</summary>
    private async Task<int?> EnsureEntityVisibleAsync(ProjectCommentEntityType entityType, int entityId, CancellationToken ct)
    {
        var companyId = RequireCompanyId();
        int? projectId = entityType switch
        {
            ProjectCommentEntityType.Project => entityId,
            ProjectCommentEntityType.Task => await _db.ProjectTasks.AsNoTracking()
                .Where(t => t.Id == entityId && t.CompanyId == companyId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync(ct),
            ProjectCommentEntityType.Milestone => await _db.ProjectMilestones.AsNoTracking()
                .Where(m => m.Id == entityId && m.CompanyId == companyId).Select(m => (int?)m.ProjectId).FirstOrDefaultAsync(ct),
            ProjectCommentEntityType.Issue => await _db.Issues.AsNoTracking()
                .Where(i => i.Id == entityId && i.CompanyId == companyId).Select(i => (int?)i.ProjectId).FirstOrDefaultAsync(ct),
            ProjectCommentEntityType.Risk => await _db.ProjectRisks.AsNoTracking()
                .Where(r => r.Id == entityId && r.CompanyId == companyId).Select(r => (int?)r.ProjectId).FirstOrDefaultAsync(ct),
            ProjectCommentEntityType.ChangeRequest => await _db.ProjectChangeRequests.AsNoTracking()
                .Where(c => c.Id == entityId && c.CompanyId == companyId).Select(c => (int?)c.ProjectId).FirstOrDefaultAsync(ct),
            _ => null
        };

        if (projectId is null or 0)
            throw new InvalidOperationException("The related record was not found for this account.");

        await RequireProjectAsync(projectId.Value, ct);
        return projectId;
    }

    /// <summary>Rolls approved time up onto the task and the project.</summary>
    private async Task SyncTaskAndProjectHoursAsync(int projectId, int? taskId, CancellationToken ct)
    {
        if (taskId is { } id)
        {
            var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (task is not null)
            {
                task.ActualHours = await _db.ProjectTimesheets
                    .Where(t => t.TaskId == id && t.EntryStatus != TimesheetEntryStatus.Rejected)
                    .SumAsync(t => t.Hours, ct);
            }
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is not null)
        {
            project.ActualHours = await _db.ProjectTimesheets
                .Where(t => t.ProjectId == projectId && t.EntryStatus != TimesheetEntryStatus.Rejected)
                .SumAsync(t => t.Hours, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
