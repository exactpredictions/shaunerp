namespace ShaunERP.Core.Projects;

public class Project : BaseEntity
{
    public int? CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
}

public class Issue : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IssueType Type { get; set; } = IssueType.Task;
    public IssueStatus IssueStatus { get; set; } = IssueStatus.New;
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public DateTime? DueDate { get; set; }
}

public enum IssueType { Bug, Feature, Task, Improvement }
public enum IssueStatus { New, Open, InProgress, Resolved, Closed }
public enum IssuePriority { Low, Medium, High, Critical }
