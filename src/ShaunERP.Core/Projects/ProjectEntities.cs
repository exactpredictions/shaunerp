using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Sales;

namespace ShaunERP.Core.Projects;

// ——— Enums ———

public enum ProjectStatus
{
    Draft = 0,
    Planned = 1,
    Active = 2,
    OnHold = 3,
    AtRisk = 4,
    Completed = 5,
    Cancelled = 6,
    Archived = 7
}

public enum ProjectHealthStatus
{
    OnTrack = 0,
    AttentionRequired = 1,
    AtRisk = 2,
    Critical = 3
}

public enum ProjectPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ProjectBillingType
{
    FixedPrice = 0,
    TimeAndMaterial = 1,
    Hourly = 2,
    MilestoneBased = 3,
    Retainer = 4,
    InternalNonBillable = 5
}

public enum ProjectPhaseStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public enum ProjectMilestoneStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Missed = 3,
    Cancelled = 4
}

public enum ProjectTaskStatus
{
    Backlog = 0,
    ToDo = 1,
    InProgress = 2,
    Blocked = 3,
    Review = 4,
    Testing = 5,
    Completed = 6,
    Cancelled = 7
}

public enum TaskDependencyType
{
    FinishToStart = 0,
    StartToStart = 1,
    FinishToFinish = 2
}

public enum ProjectIssueSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ProjectIssueStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3,
    Cancelled = 4
}

public enum ProjectRiskStatus
{
    Identified = 0,
    Monitoring = 1,
    Mitigating = 2,
    Closed = 3,
    Realized = 4
}

public enum ChangeRequestApprovalStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Implemented = 5,
    Cancelled = 6
}

public enum TimesheetEntryStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3
}

public enum ProjectCommentEntityType
{
    Project = 0,
    Task = 1,
    Milestone = 2,
    Issue = 3,
    Risk = 4,
    ChangeRequest = 5
}

// ——— Project master ———

public class Project : BaseEntity
{
    public int CompanyId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }
    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }
    public int? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }

    public string? ProjectType { get; set; }
    public string? Department { get; set; }
    public int? ManagerUserId { get; set; }
    public User? ManagerUser { get; set; }
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public ProjectStatus ProjectStatus { get; set; } = ProjectStatus.Draft;
    public ProjectHealthStatus HealthStatus { get; set; } = ProjectHealthStatus.OnTrack;

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public decimal Budget { get; set; }
    public decimal RevisedBudget { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public decimal EstimatedLabourCost { get; set; }
    public decimal ActualLabourCost { get; set; }
    public decimal EstimatedExpense { get; set; }
    public decimal ActualExpense { get; set; }
    public decimal CustomerBilling { get; set; }
    public decimal Revenue { get; set; }

    public ProjectBillingType BillingType { get; set; } = ProjectBillingType.TimeAndMaterial;
    public string CurrencyCode { get; set; } = "INR";
    public decimal ProgressPercent { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }

    public int? TemplateId { get; set; }
    public ProjectTemplate? Template { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectPhase> Phases { get; set; } = new List<ProjectPhase>();
    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    public ICollection<ProjectRisk> Risks { get; set; } = new List<ProjectRisk>();
    public ICollection<ProjectChangeRequest> ChangeRequests { get; set; } = new List<ProjectChangeRequest>();
    public ICollection<ProjectTimesheet> Timesheets { get; set; } = new List<ProjectTimesheet>();
}

public class ProjectMember : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string ProjectRole { get; set; } = "Member";
    public decimal AllocationPercent { get; set; } = 100;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class ProjectPhase : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public ProjectPhaseStatus PhaseStatus { get; set; } = ProjectPhaseStatus.Planned;
    public decimal ProgressPercent { get; set; }
    public string? Notes { get; set; }

    public ICollection<ProjectMilestone> Milestones { get; set; } = new List<ProjectMilestone>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
}

public class ProjectMilestone : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? PhaseId { get; set; }
    public ProjectPhase? Phase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public ProjectMilestoneStatus MilestoneStatus { get; set; } = ProjectMilestoneStatus.Planned;
    public decimal ProgressPercent { get; set; }
    public bool CustomerApprovalRequired { get; set; }
    public bool CustomerApproved { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? Notes { get; set; }
}

public class ProjectTask : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? PhaseId { get; set; }
    public ProjectPhase? Phase { get; set; }
    public int? MilestoneId { get; set; }
    public ProjectMilestone? Milestone { get; set; }
    public int? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }

    public string TaskNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssigneeUserId { get; set; }
    public User? AssigneeUser { get; set; }
    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public ProjectTaskStatus TaskStatus { get; set; } = ProjectTaskStatus.Backlog;
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public bool IsBillable { get; set; } = true;
    public decimal ProgressPercent { get; set; }
    public string? Tags { get; set; }
    public int SortOrder { get; set; }

    public ICollection<ProjectTask> Subtasks { get; set; } = new List<ProjectTask>();
    public ICollection<ProjectTaskChecklistItem> ChecklistItems { get; set; } = new List<ProjectTaskChecklistItem>();
    public ICollection<ProjectTaskCollaborator> Collaborators { get; set; } = new List<ProjectTaskCollaborator>();
    public ICollection<ProjectTaskDependency> DependenciesAsSuccessor { get; set; } = new List<ProjectTaskDependency>();
    public ICollection<ProjectTaskDependency> DependenciesAsPredecessor { get; set; } = new List<ProjectTaskDependency>();
}

public class ProjectTaskCollaborator : BaseEntity
{
    public int CompanyId { get; set; }
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
}

public class ProjectTaskChecklistItem : BaseEntity
{
    public int CompanyId { get; set; }
    public int TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectTaskDependency : BaseEntity
{
    public int CompanyId { get; set; }
    public int PredecessorTaskId { get; set; }
    public ProjectTask? PredecessorTask { get; set; }
    public int SuccessorTaskId { get; set; }
    public ProjectTask? SuccessorTask { get; set; }
    public TaskDependencyType DependencyType { get; set; } = TaskDependencyType.FinishToStart;
}

/// <summary>Issue register entry (extends legacy Issue table).</summary>
public class Issue : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string IssueNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; } = IssueType.Bug;
    public ProjectIssueSeverity Severity { get; set; } = ProjectIssueSeverity.Medium;
    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public ProjectIssueStatus IssueStatus { get; set; } = ProjectIssueStatus.Open;
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public DateTime DateRaised { get; set; } = DateTime.UtcNow;
    public DateTime? TargetResolutionDate { get; set; }
    public DateTime? ActualResolutionDate { get; set; }
    public string? RootCause { get; set; }
    public string? Resolution { get; set; }

    // Legacy compatibility fields
    public DateTime? DueDate { get; set; }
}

public enum IssueType { Bug = 0, Feature = 1, Task = 2, Improvement = 3 }

public class ProjectRisk : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string RiskNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Probability { get; set; } = 1; // 1-5
    public int Impact { get; set; } = 1; // 1-5
    public int RiskScore => Probability * Impact;
    public int? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }
    public string? MitigationPlan { get; set; }
    public string? ContingencyPlan { get; set; }
    public ProjectRiskStatus RiskStatus { get; set; } = ProjectRiskStatus.Identified;
    public DateTime? ReviewDate { get; set; }
}

public class ProjectChangeRequest : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string ChangeRequestNumber { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public string? BusinessReason { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ScheduleImpact { get; set; }
    public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    public ChangeRequestApprovalStatus ApprovalStatus { get; set; } = ChangeRequestApprovalStatus.Draft;
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ImplementationStatus { get; set; }
}

public class ProjectTimesheet : BaseEntity
{
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime WorkDate { get; set; } = DateTime.UtcNow.Date;
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public decimal Hours { get; set; }
    public decimal BillableHours { get; set; }
    public decimal NonBillableHours { get; set; }
    public string? Description { get; set; }
    public TimesheetEntryStatus EntryStatus { get; set; } = TimesheetEntryStatus.Draft;
    public DateTime? SubmittedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovalDate { get; set; }
}

public class ProjectTimeTracker : BaseEntity
{
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? TaskId { get; set; }
    public ProjectTask? Task { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StoppedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
}

public class ProjectComment : BaseEntity
{
    public int CompanyId { get; set; }
    public ProjectCommentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public int? ProjectId { get; set; }
    public int AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public string Body { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    public ProjectComment? ParentComment { get; set; }
}

public class ProjectAttachment : BaseEntity
{
    public int CompanyId { get; set; }
    public ProjectCommentEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public int? ProjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
}

public class ProjectTemplate : BaseEntity
{
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProjectType { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProjectTemplatePhase> Phases { get; set; } = new List<ProjectTemplatePhase>();
}

public class ProjectTemplatePhase : BaseEntity
{
    public int CompanyId { get; set; }
    public int TemplateId { get; set; }
    public ProjectTemplate? Template { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int EstimatedDurationDays { get; set; }
    public ICollection<ProjectTemplateTask> Tasks { get; set; } = new List<ProjectTemplateTask>();
}

public class ProjectTemplateTask : BaseEntity
{
    public int CompanyId { get; set; }
    public int TemplatePhaseId { get; set; }
    public ProjectTemplatePhase? TemplatePhase { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public decimal EstimatedHours { get; set; }
    public string? DefaultRole { get; set; }
}

public class ProjectNumberSequence : BaseEntity
{
    public int CompanyId { get; set; }
    public string SequenceKey { get; set; } = "PRJ"; // PRJ, TASK, ISS, RSK, CR
    public int Year { get; set; }
    public int NextValue { get; set; } = 1;
}
