using ShaunERP.Core.Projects;

namespace ShaunERP.Web.Services;

/// <summary>Presentation helpers shared by the Projects pages (labels, pill classes, formatting).</summary>
public static class ProjectDisplay
{
    public static readonly ProjectTaskStatus[] KanbanColumns =
    [
        ProjectTaskStatus.Backlog,
        ProjectTaskStatus.ToDo,
        ProjectTaskStatus.InProgress,
        ProjectTaskStatus.Blocked,
        ProjectTaskStatus.Review,
        ProjectTaskStatus.Testing,
        ProjectTaskStatus.Completed,
        ProjectTaskStatus.Cancelled
    ];

    public static string Pct(decimal value) =>
        $"{Math.Clamp(value, 0, 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}%";

    public static string StatusClass(ProjectStatus status) => status switch
    {
        ProjectStatus.Active or ProjectStatus.Completed => "ok",
        ProjectStatus.AtRisk or ProjectStatus.Cancelled => "off",
        ProjectStatus.OnHold => "warn",
        _ => ""
    };

    public static string HealthClass(ProjectHealthStatus health) => health switch
    {
        ProjectHealthStatus.OnTrack => "ok",
        ProjectHealthStatus.AttentionRequired => "warn",
        _ => "off"
    };

    public static string TaskStatusClass(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.Completed => "ok",
        ProjectTaskStatus.Blocked or ProjectTaskStatus.Cancelled => "off",
        ProjectTaskStatus.InProgress or ProjectTaskStatus.Review or ProjectTaskStatus.Testing => "info",
        _ => ""
    };

    public static string PriorityClass(ProjectPriority priority) => priority switch
    {
        ProjectPriority.Critical => "off",
        ProjectPriority.High => "warn",
        _ => ""
    };

    public static string IssueStatusClass(ProjectIssueStatus status) => status switch
    {
        ProjectIssueStatus.Resolved or ProjectIssueStatus.Closed => "ok",
        ProjectIssueStatus.Cancelled => "off",
        _ => "warn"
    };

    public static string RiskStatusClass(ProjectRiskStatus status) => status switch
    {
        ProjectRiskStatus.Closed => "ok",
        ProjectRiskStatus.Realized => "off",
        _ => "warn"
    };

    public static string TimesheetStatusClass(TimesheetEntryStatus status) => status switch
    {
        TimesheetEntryStatus.Approved => "ok",
        TimesheetEntryStatus.Rejected => "off",
        TimesheetEntryStatus.Submitted => "info",
        _ => ""
    };

    public static string ChangeRequestStatusClass(ChangeRequestApprovalStatus status) => status switch
    {
        ChangeRequestApprovalStatus.Approved or ChangeRequestApprovalStatus.Implemented => "ok",
        ChangeRequestApprovalStatus.Rejected or ChangeRequestApprovalStatus.Cancelled => "off",
        _ => "info"
    };

    public static string Label(ProjectTaskStatus status) => status switch
    {
        ProjectTaskStatus.ToDo => "To do",
        ProjectTaskStatus.InProgress => "In progress",
        _ => status.ToString()
    };

    public static string Date(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "—";

    public static string UserName(Core.Identity.User? user)
    {
        if (user is null) return "—";
        var full = $"{user.FirstName} {user.LastName}".Trim();
        if (full.Length > 0) return full;
        return !string.IsNullOrWhiteSpace(user.Username) ? user.Username : user.Email;
    }

    public static bool IsOverdue(ProjectTask task) =>
        task.DueDate is { } due
        && due.Date < DateTime.UtcNow.Date
        && task.TaskStatus is not ProjectTaskStatus.Completed and not ProjectTaskStatus.Cancelled;
}
