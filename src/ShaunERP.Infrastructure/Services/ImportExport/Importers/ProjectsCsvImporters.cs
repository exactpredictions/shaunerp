using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Projects;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport.Importers;

public sealed class ProjectsCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;

    public ProjectsCsvImporter(ShaunErpDbContext db) => _db = db;

    public override string EntityKey => "projects";
    public override string DisplayName => "Projects";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "ProjectCode", Header = "Project Code", Required = true, Example = "SAMPLE-PRJ-001" },
        new() { Key = "Name", Header = "Name", Required = true, Example = "SAMPLE Website Rebuild" },
        new() { Key = "Description", Header = "Description", Example = "SAMPLE project description" },
        new() { Key = "CustomerName", Header = "Customer Name", Example = "SAMPLE Acme India Pvt Ltd" },
        new() { Key = "ProjectType", Header = "Project Type", Example = "Implementation" },
        new() { Key = "Priority", Header = "Priority", Example = "High" },
        new() { Key = "ProjectStatus", Header = "Project Status", Example = "Planned" },
        new() { Key = "HealthStatus", Header = "Health Status", Example = "OnTrack" },
        new() { Key = "PlannedStartDate", Header = "Planned Start Date", Example = "2026-04-01" },
        new() { Key = "PlannedEndDate", Header = "Planned End Date", Example = "2026-06-30" },
        new() { Key = "Budget", Header = "Budget", Example = "250000" },
        new() { Key = "CurrencyCode", Header = "Currency", Example = "INR" },
        new() { Key = "BillingType", Header = "Billing Type", Example = "FixedPrice" },
        new() { Key = "Tags", Header = "Tags", Example = "import" },
        new() { Key = "Notes", Header = "Notes", Example = "SAMPLE import note" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var existing = await _db.Projects.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.Id, p.ProjectCode })
            .ToListAsync(ct);
        var byCode = existing
            .Where(p => !string.IsNullOrWhiteSpace(p.ProjectCode))
            .GroupBy(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customers = await _db.CrmCustomers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && c.CompanyName != null && c.CompanyName != "")
            .Select(c => c.CompanyName!)
            .ToListAsync(ct);
        var customerNames = new HashSet<string>(customers, StringComparer.OrdinalIgnoreCase);

        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var code = CsvValidator.Get(values, "ProjectCode");
            if (!CsvValidator.TryRequired(code, "ProjectCode", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "Name"), "Name", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "PlannedStartDate"), out _, out var e3) && e3 is not null) errors.Add(e3);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "PlannedEndDate"), out _, out var e4) && e4 is not null) errors.Add(e4);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "Budget"), out _, out var e5) && e5 is not null) errors.Add(e5);

            TryEnum<ProjectPriority>(CsvValidator.Get(values, "Priority"), "Priority", errors);
            TryEnum<ProjectStatus>(CsvValidator.Get(values, "ProjectStatus"), "ProjectStatus", errors);
            TryEnum<ProjectHealthStatus>(CsvValidator.Get(values, "HealthStatus"), "HealthStatus", errors);
            TryEnum<ProjectBillingType>(CsvValidator.Get(values, "BillingType"), "BillingType", errors);

            var customerName = CsvValidator.Get(values, "CustomerName");
            if (!string.IsNullOrWhiteSpace(customerName) && !customerNames.Contains(customerName))
            {
                errors.Add($"Customer '{customerName}' not found.");
            }

            int? id = null;
            var action = "Create";
            if (!string.IsNullOrWhiteSpace(code) && byCode.TryGetValue(code, out var eid))
            {
                id = eid;
                action = "Update";
            }
            else if (!string.IsNullOrWhiteSpace(code) && !seenInFile.Add(code))
            {
                action = "Skip";
                errors.Add("Duplicate project code in import file.");
            }

            rows.Add(new CsvRowValidation
            {
                RowNumber = n,
                IsValid = errors.Count == 0,
                Action = action,
                Values = values,
                Errors = errors,
                MatchKey = code,
                ExistingId = id
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var customers = await _db.CrmCustomers
            .Where(c => c.CompanyId == companyId && c.CompanyName != null && c.CompanyName != "")
            .ToListAsync(ct);
        var byName = customers
            .GroupBy(c => c.CompanyName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "PlannedStartDate"), out var start, out _);
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "PlannedEndDate"), out var end, out _);
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "Budget"), out var budget, out _);

                var customerName = Null(CsvValidator.Get(row.Values, "CustomerName"));
                int? customerId = null;
                if (customerName is not null && byName.TryGetValue(customerName, out var customer))
                {
                    customerId = customer.Id;
                    customerName = customer.CompanyName;
                }

                var priority = ParseEnum(CsvValidator.Get(row.Values, "Priority"), ProjectPriority.Medium);
                var status = ParseEnum(CsvValidator.Get(row.Values, "ProjectStatus"), ProjectStatus.Draft);
                var health = ParseEnum(CsvValidator.Get(row.Values, "HealthStatus"), ProjectHealthStatus.OnTrack);
                var billing = ParseEnum(CsvValidator.Get(row.Values, "BillingType"), ProjectBillingType.TimeAndMaterial);

                if (row.ExistingId is int id)
                {
                    var project = await _db.Projects.FirstAsync(p => p.Id == id && p.CompanyId == companyId, ct);
                    project.Name = CsvValidator.Get(row.Values, "Name");
                    project.Description = Null(CsvValidator.Get(row.Values, "Description"));
                    project.CustomerId = customerId;
                    project.CustomerName = customerName;
                    project.ProjectType = Null(CsvValidator.Get(row.Values, "ProjectType"));
                    project.Priority = priority;
                    project.ProjectStatus = status;
                    project.HealthStatus = health;
                    project.PlannedStartDate = start;
                    project.PlannedEndDate = end;
                    project.Budget = budget ?? project.Budget;
                    project.CurrencyCode = Null(CsvValidator.Get(row.Values, "CurrencyCode")) ?? project.CurrencyCode;
                    project.BillingType = billing;
                    project.Tags = Null(CsvValidator.Get(row.Values, "Tags"));
                    project.Notes = Null(CsvValidator.Get(row.Values, "Notes"));
                    project.UpdatedDate = DateTime.UtcNow;
                    updated++;
                }
                else
                {
                    _db.Projects.Add(new Project
                    {
                        CompanyId = companyId,
                        ProjectCode = CsvValidator.Get(row.Values, "ProjectCode"),
                        Name = CsvValidator.Get(row.Values, "Name"),
                        Description = Null(CsvValidator.Get(row.Values, "Description")),
                        CustomerId = customerId,
                        CustomerName = customerName,
                        ProjectType = Null(CsvValidator.Get(row.Values, "ProjectType")),
                        Priority = priority,
                        ProjectStatus = status,
                        HealthStatus = health,
                        PlannedStartDate = start,
                        PlannedEndDate = end,
                        Budget = budget ?? 0,
                        CurrencyCode = Null(CsvValidator.Get(row.Values, "CurrencyCode")) ?? "INR",
                        BillingType = billing,
                        Tags = Null(CsvValidator.Get(row.Values, "Tags")),
                        Notes = Null(CsvValidator.Get(row.Values, "Notes")),
                        CreatedDate = DateTime.UtcNow
                    });
                    created++;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new CsvImportResult
            {
                EntityKey = EntityKey,
                Success = true,
                Created = created,
                Updated = updated,
                Skipped = preview.InvalidCount
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var items = await _db.Projects.AsNoTracking()
            .Where(p => p.CompanyId == request.CompanyId)
            .OrderBy(p => p.ProjectCode)
            .ToListAsync(ct);
        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(p => new[]
        {
            p.ProjectCode, p.Name, p.Description, p.CustomerName, p.ProjectType,
            p.Priority.ToString(), p.ProjectStatus.ToString(), p.HealthStatus.ToString(),
            p.PlannedStartDate?.ToString("yyyy-MM-dd"), p.PlannedEndDate?.ToString("yyyy-MM-dd"),
            p.Budget.ToString("0.##"), p.CurrencyCode, p.BillingType.ToString(), p.Tags, p.Notes
        }.AsEnumerable());
        return new CsvExportResult
        {
            FileName = "projects-export.csv",
            CsvContent = CsvParser.ToCsv(headers, rows),
            RowCount = items.Count
        };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["ProjectCode"] = "SAMPLE-PRJ-001",
            ["Name"] = "SAMPLE Website Rebuild",
            ["Description"] = "SAMPLE project description",
            ["CustomerName"] = "SAMPLE Acme India Pvt Ltd",
            ["ProjectType"] = "Implementation",
            ["Priority"] = "High",
            ["ProjectStatus"] = "Planned",
            ["HealthStatus"] = "OnTrack",
            ["PlannedStartDate"] = "2026-04-01",
            ["PlannedEndDate"] = "2026-06-30",
            ["Budget"] = "250000",
            ["CurrencyCode"] = "INR",
            ["BillingType"] = "FixedPrice",
            ["Tags"] = "import",
            ["Notes"] = "SAMPLE import note"
        },
        new Dictionary<string, string>
        {
            ["ProjectCode"] = "SAMPLE-PRJ-002",
            ["Name"] = "SAMPLE ERP Rollout",
            ["Description"] = "SAMPLE phase-1 rollout",
            ["CustomerName"] = "SAMPLE Beta Traders",
            ["ProjectType"] = "Consulting",
            ["Priority"] = "Medium",
            ["ProjectStatus"] = "Active",
            ["HealthStatus"] = "AttentionRequired",
            ["PlannedStartDate"] = "2026-05-01",
            ["PlannedEndDate"] = "2026-09-30",
            ["Budget"] = "500000",
            ["CurrencyCode"] = "INR",
            ["BillingType"] = "TimeAndMaterial",
            ["Tags"] = "import",
            ["Notes"] = "SAMPLE second project"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static void TryEnum<TEnum>(string? value, string field, List<string> errors) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Enum.TryParse<TEnum>(value.Trim(), true, out _))
            errors.Add($"Invalid {field} '{value}'.");
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), true, out var parsed)
            ? parsed
            : defaultValue;
}

public sealed class ProjectTasksCsvImporter : EntityCsvImporterBase
{
    private readonly ShaunErpDbContext _db;

    public ProjectTasksCsvImporter(ShaunErpDbContext db) => _db = db;

    public override string EntityKey => "project-tasks";
    public override string DisplayName => "Project tasks";

    public override IReadOnlyList<CsvColumnDefinition> Columns { get; } =
    [
        new() { Key = "ProjectCode", Header = "Project Code", Required = true, Example = "SAMPLE-PRJ-001" },
        new() { Key = "TaskNumber", Header = "Task Number", Required = true, Example = "SAMPLE-TSK-001" },
        new() { Key = "Name", Header = "Name", Required = true, Example = "SAMPLE Kickoff" },
        new() { Key = "Description", Header = "Description", Example = "SAMPLE task description" },
        new() { Key = "PhaseName", Header = "Phase Name", Example = "Discovery" },
        new() { Key = "MilestoneName", Header = "Milestone Name", Example = "Kickoff complete" },
        new() { Key = "ParentTaskNumber", Header = "Parent Task Number", Example = "" },
        new() { Key = "AssigneeEmail", Header = "Assignee Email", Example = "sample.owner@example.com" },
        new() { Key = "Priority", Header = "Priority", Example = "High" },
        new() { Key = "TaskStatus", Header = "Task Status", Example = "ToDo" },
        new() { Key = "StartDate", Header = "Start Date", Example = "2026-04-01" },
        new() { Key = "DueDate", Header = "Due Date", Example = "2026-04-10" },
        new() { Key = "EstimatedHours", Header = "Estimated Hours", Example = "8" },
        new() { Key = "IsBillable", Header = "Is Billable", Example = "true" },
        new() { Key = "Tags", Header = "Tags", Example = "import" }
    ];

    public override async Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default)
    {
        var projectRows = await _db.Projects.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.Id, p.ProjectCode })
            .ToListAsync(ct);
        var projects = projectRows
            .Where(p => !string.IsNullOrWhiteSpace(p.ProjectCode))
            .GroupBy(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.CompanyId == companyId)
            .Select(t => new { t.Id, t.ProjectId, t.TaskNumber })
            .ToListAsync(ct);
        var taskByProjectAndNumber = new Dictionary<(int ProjectId, string Number), int>(ProjectTaskKeyComparer.Instance);
        foreach (var t in tasks)
            taskByProjectAndNumber[(t.ProjectId, t.TaskNumber)] = t.Id;

        var phases = await _db.ProjectPhases.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.ProjectId, p.Name })
            .ToListAsync(ct);
        var phaseNames = phases
            .GroupBy(p => p.ProjectId)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.Name), StringComparer.OrdinalIgnoreCase));

        var milestones = await _db.ProjectMilestones.AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .Select(m => new { m.ProjectId, m.Name })
            .ToListAsync(ct);
        var milestoneNames = milestones
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.Name), StringComparer.OrdinalIgnoreCase));

        var users = await (
            from cu in _db.CompanyUsers.AsNoTracking()
            join u in _db.Users.AsNoTracking() on cu.UserId equals u.Id
            where cu.CompanyId == companyId
            select u.Email
        ).ToListAsync(ct);
        var emails = new HashSet<string>(users.Where(e => !string.IsNullOrWhiteSpace(e)), StringComparer.OrdinalIgnoreCase);

        var pendingParents = new HashSet<(int ProjectId, string Number)>(ProjectTaskKeyComparer.Instance);
        var rows = new List<CsvRowValidation>();
        var n = 1;
        foreach (var values in parsed.Rows)
        {
            n++;
            var errors = new List<string>();
            var projectCode = CsvValidator.Get(values, "ProjectCode");
            var taskNumber = CsvValidator.Get(values, "TaskNumber");
            if (!CsvValidator.TryRequired(projectCode, "ProjectCode", out var e1)) errors.Add(e1!);
            if (!CsvValidator.TryRequired(taskNumber, "TaskNumber", out var e2)) errors.Add(e2!);
            if (!CsvValidator.TryRequired(CsvValidator.Get(values, "Name"), "Name", out var e3)) errors.Add(e3!);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "StartDate"), out _, out var e4) && e4 is not null) errors.Add(e4);
            if (!CsvValidator.TryDate(CsvValidator.Get(values, "DueDate"), out _, out var e5) && e5 is not null) errors.Add(e5);
            if (!CsvValidator.TryDecimal(CsvValidator.Get(values, "EstimatedHours"), out _, out var e6) && e6 is not null) errors.Add(e6);
            if (!CsvValidator.TryEmail(CsvValidator.Get(values, "AssigneeEmail"), out var e7) && e7 is not null) errors.Add(e7);

            TryEnum<ProjectPriority>(CsvValidator.Get(values, "Priority"), "Priority", errors);
            TryEnum<ProjectTaskStatus>(CsvValidator.Get(values, "TaskStatus"), "TaskStatus", errors);

            int? projectId = null;
            if (!string.IsNullOrWhiteSpace(projectCode))
            {
                if (!projects.TryGetValue(projectCode, out var pid))
                    errors.Add($"Project '{projectCode}' not found.");
                else
                    projectId = pid;
            }

            var assignee = CsvValidator.Get(values, "AssigneeEmail");
            if (!string.IsNullOrWhiteSpace(assignee) && !emails.Contains(assignee))
                errors.Add($"Assignee '{assignee}' not found for this company.");

            if (projectId is int pId)
            {
                var phaseName = CsvValidator.Get(values, "PhaseName");
                if (!string.IsNullOrWhiteSpace(phaseName) &&
                    (!phaseNames.TryGetValue(pId, out var pset) || !pset.Contains(phaseName)))
                {
                    errors.Add($"Phase '{phaseName}' not found on project '{projectCode}'.");
                }

                var milestoneName = CsvValidator.Get(values, "MilestoneName");
                if (!string.IsNullOrWhiteSpace(milestoneName) &&
                    (!milestoneNames.TryGetValue(pId, out var mset) || !mset.Contains(milestoneName)))
                {
                    errors.Add($"Milestone '{milestoneName}' not found on project '{projectCode}'.");
                }

                var parentNumber = CsvValidator.Get(values, "ParentTaskNumber");
                if (!string.IsNullOrWhiteSpace(parentNumber) &&
                    !taskByProjectAndNumber.ContainsKey((pId, parentNumber)) &&
                    !pendingParents.Contains((pId, parentNumber)))
                {
                    errors.Add($"Parent task '{parentNumber}' not found on project '{projectCode}'.");
                }
            }

            int? existingId = null;
            var action = "Create";
            var matchKey = string.IsNullOrWhiteSpace(projectCode) || string.IsNullOrWhiteSpace(taskNumber)
                ? taskNumber
                : $"{projectCode}:{taskNumber}";
            if (projectId is int existingProjectId &&
                !string.IsNullOrWhiteSpace(taskNumber) &&
                taskByProjectAndNumber.TryGetValue((existingProjectId, taskNumber), out var tid))
            {
                existingId = tid;
                action = "Update";
            }

            if (errors.Count == 0 && projectId is int validProjectId && !string.IsNullOrWhiteSpace(taskNumber))
                pendingParents.Add((validProjectId, taskNumber));

            rows.Add(new CsvRowValidation
            {
                RowNumber = n,
                IsValid = errors.Count == 0,
                Action = action,
                Values = values,
                Errors = errors,
                MatchKey = matchKey,
                ExistingId = existingId
            });
        }

        return BuildPreview(EntityKey, parsed, rows);
    }

    public override async Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default)
    {
        var projectRows = await _db.Projects
            .Where(p => p.CompanyId == companyId)
            .ToListAsync(ct);
        var projects = projectRows
            .Where(p => !string.IsNullOrWhiteSpace(p.ProjectCode))
            .GroupBy(p => p.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var phases = await _db.ProjectPhases
            .Where(p => p.CompanyId == companyId)
            .ToListAsync(ct);
        var phaseByProjectName = new Dictionary<(int ProjectId, string Name), int>(PhaseNameKeyComparer.Instance);
        foreach (var phase in phases)
            phaseByProjectName[(phase.ProjectId, phase.Name)] = phase.Id;

        var milestones = await _db.ProjectMilestones
            .Where(m => m.CompanyId == companyId)
            .ToListAsync(ct);
        var milestoneByProjectName = new Dictionary<(int ProjectId, string Name), int>(PhaseNameKeyComparer.Instance);
        foreach (var milestone in milestones)
            milestoneByProjectName[(milestone.ProjectId, milestone.Name)] = milestone.Id;

        var tasks = await _db.ProjectTasks
            .Where(t => t.CompanyId == companyId)
            .ToListAsync(ct);
        var taskByProjectNumber = new Dictionary<(int ProjectId, string TaskNumber), ProjectTask>(ProjectTaskEntityKeyComparer.Instance);
        foreach (var task in tasks)
            taskByProjectNumber[(task.ProjectId, task.TaskNumber)] = task;

        var users = await (
            from cu in _db.CompanyUsers
            join u in _db.Users on cu.UserId equals u.Id
            where cu.CompanyId == companyId
            select u
        ).ToListAsync(ct);
        var userByEmail = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                var project = projects[CsvValidator.Get(row.Values, "ProjectCode")];
                var taskNumber = CsvValidator.Get(row.Values, "TaskNumber");
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "StartDate"), out var start, out _);
                CsvValidator.TryDate(CsvValidator.Get(row.Values, "DueDate"), out var due, out _);
                CsvValidator.TryDecimal(CsvValidator.Get(row.Values, "EstimatedHours"), out var hours, out _);

                int? phaseId = null;
                var phaseName = Null(CsvValidator.Get(row.Values, "PhaseName"));
                if (phaseName is not null && phaseByProjectName.TryGetValue((project.Id, phaseName), out var pid))
                    phaseId = pid;

                int? milestoneId = null;
                var milestoneName = Null(CsvValidator.Get(row.Values, "MilestoneName"));
                if (milestoneName is not null && milestoneByProjectName.TryGetValue((project.Id, milestoneName), out var mid))
                    milestoneId = mid;

                ProjectTask? parentTask = null;
                var parentNumber = Null(CsvValidator.Get(row.Values, "ParentTaskNumber"));
                if (parentNumber is not null)
                    taskByProjectNumber.TryGetValue((project.Id, parentNumber), out parentTask);

                int? assigneeId = null;
                var assigneeEmail = Null(CsvValidator.Get(row.Values, "AssigneeEmail"));
                if (assigneeEmail is not null && userByEmail.TryGetValue(assigneeEmail, out var uid))
                    assigneeId = uid;

                var priority = ParseEnum(CsvValidator.Get(row.Values, "Priority"), ProjectPriority.Medium);
                var status = ParseEnum(CsvValidator.Get(row.Values, "TaskStatus"), ProjectTaskStatus.Backlog);
                var isBillable = Bool(CsvValidator.Get(row.Values, "IsBillable"), true);

                if (row.ExistingId is int id)
                {
                    var task = await _db.ProjectTasks.FirstAsync(t => t.Id == id && t.CompanyId == companyId, ct);
                    task.Name = CsvValidator.Get(row.Values, "Name");
                    task.Description = Null(CsvValidator.Get(row.Values, "Description"));
                    task.PhaseId = phaseId;
                    task.MilestoneId = milestoneId;
                    task.ParentTaskId = parentTask?.Id;
                    task.AssigneeUserId = assigneeId;
                    task.Priority = priority;
                    task.TaskStatus = status;
                    task.StartDate = start;
                    task.DueDate = due;
                    task.EstimatedHours = hours ?? task.EstimatedHours;
                    task.IsBillable = isBillable;
                    task.Tags = Null(CsvValidator.Get(row.Values, "Tags"));
                    task.UpdatedDate = DateTime.UtcNow;
                    if (status == ProjectTaskStatus.Completed)
                    {
                        task.CompletedDate ??= DateTime.UtcNow;
                        task.ProgressPercent = 100;
                    }

                    taskByProjectNumber[(project.Id, task.TaskNumber)] = task;
                    updated++;
                }
                else
                {
                    var task = new ProjectTask
                    {
                        CompanyId = companyId,
                        ProjectId = project.Id,
                        TaskNumber = taskNumber,
                        Name = CsvValidator.Get(row.Values, "Name"),
                        Description = Null(CsvValidator.Get(row.Values, "Description")),
                        PhaseId = phaseId,
                        MilestoneId = milestoneId,
                        ParentTask = parentTask,
                        ParentTaskId = parentTask is { Id: > 0 } ? parentTask.Id : null,
                        AssigneeUserId = assigneeId,
                        Priority = priority,
                        TaskStatus = status,
                        StartDate = start,
                        DueDate = due,
                        EstimatedHours = hours ?? 0,
                        IsBillable = isBillable,
                        Tags = Null(CsvValidator.Get(row.Values, "Tags")),
                        CreatedDate = DateTime.UtcNow
                    };
                    if (status == ProjectTaskStatus.Completed)
                    {
                        task.CompletedDate = DateTime.UtcNow;
                        task.ProgressPercent = 100;
                    }

                    _db.ProjectTasks.Add(task);
                    await _db.SaveChangesAsync(ct);
                    taskByProjectNumber[(project.Id, taskNumber)] = task;
                    created++;
                }
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new CsvImportResult
            {
                EntityKey = EntityKey,
                Success = true,
                Created = created,
                Updated = updated,
                Skipped = preview.InvalidCount
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return new CsvImportResult { EntityKey = EntityKey, Success = false, Messages = [ex.Message] };
        }
    }

    public override async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        var companyId = request.CompanyId!.Value;
        var items = await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Phase)
            .Include(t => t.Milestone)
            .Include(t => t.ParentTask)
            .Include(t => t.AssigneeUser)
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.Project!.ProjectCode)
            .ThenBy(t => t.TaskNumber)
            .ToListAsync(ct);

        var headers = Columns.Select(c => c.Key);
        var rows = items.Select(t => new[]
        {
            t.Project?.ProjectCode, t.TaskNumber, t.Name, t.Description,
            t.Phase?.Name, t.Milestone?.Name, t.ParentTask?.TaskNumber,
            t.AssigneeUser?.Email, t.Priority.ToString(), t.TaskStatus.ToString(),
            t.StartDate?.ToString("yyyy-MM-dd"), t.DueDate?.ToString("yyyy-MM-dd"),
            t.EstimatedHours.ToString("0.##"), t.IsBillable.ToString(), t.Tags
        }.AsEnumerable());

        return new CsvExportResult
        {
            FileName = "project-tasks-export.csv",
            CsvContent = CsvParser.ToCsv(headers, rows),
            RowCount = items.Count
        };
    }

    public override string BuildSampleCsv() => BuildSampleCsv(
        new Dictionary<string, string>
        {
            ["ProjectCode"] = "SAMPLE-PRJ-001",
            ["TaskNumber"] = "SAMPLE-TSK-001",
            ["Name"] = "SAMPLE Kickoff",
            ["Description"] = "SAMPLE task description",
            ["AssigneeEmail"] = "sample.owner@example.com",
            ["Priority"] = "High",
            ["TaskStatus"] = "ToDo",
            ["StartDate"] = "2026-04-01",
            ["DueDate"] = "2026-04-10",
            ["EstimatedHours"] = "8",
            ["IsBillable"] = "true",
            ["Tags"] = "import"
        },
        new Dictionary<string, string>
        {
            ["ProjectCode"] = "SAMPLE-PRJ-001",
            ["TaskNumber"] = "SAMPLE-TSK-002",
            ["Name"] = "SAMPLE Requirements",
            ["Description"] = "SAMPLE gather requirements",
            ["ParentTaskNumber"] = "SAMPLE-TSK-001",
            ["AssigneeEmail"] = "sample.owner@example.com",
            ["Priority"] = "Medium",
            ["TaskStatus"] = "Backlog",
            ["StartDate"] = "2026-04-11",
            ["DueDate"] = "2026-04-20",
            ["EstimatedHours"] = "16",
            ["IsBillable"] = "true",
            ["Tags"] = "import"
        });

    private static string? Null(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static bool Bool(string? v, bool defaultValue) =>
        string.IsNullOrWhiteSpace(v)
            ? defaultValue
            : string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
              || v == "1"
              || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);

    private static void TryEnum<TEnum>(string? value, string field, List<string> errors) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Enum.TryParse<TEnum>(value.Trim(), true, out _))
            errors.Add($"Invalid {field} '{value}'.");
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), true, out var parsed)
            ? parsed
            : defaultValue;

    private sealed class ProjectTaskKeyComparer : IEqualityComparer<(int ProjectId, string Number)>
    {
        public static readonly ProjectTaskKeyComparer Instance = new();
        public bool Equals((int ProjectId, string Number) x, (int ProjectId, string Number) y) =>
            x.ProjectId == y.ProjectId && string.Equals(x.Number, y.Number, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((int ProjectId, string Number) obj) =>
            HashCode.Combine(obj.ProjectId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Number ?? string.Empty));
    }

    private sealed class ProjectTaskEntityKeyComparer : IEqualityComparer<(int ProjectId, string TaskNumber)>
    {
        public static readonly ProjectTaskEntityKeyComparer Instance = new();
        public bool Equals((int ProjectId, string TaskNumber) x, (int ProjectId, string TaskNumber) y) =>
            x.ProjectId == y.ProjectId && string.Equals(x.TaskNumber, y.TaskNumber, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((int ProjectId, string TaskNumber) obj) =>
            HashCode.Combine(obj.ProjectId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TaskNumber ?? string.Empty));
    }

    private sealed class PhaseNameKeyComparer : IEqualityComparer<(int ProjectId, string Name)>
    {
        public static readonly PhaseNameKeyComparer Instance = new();
        public bool Equals((int ProjectId, string Name) x, (int ProjectId, string Name) y) =>
            x.ProjectId == y.ProjectId && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((int ProjectId, string Name) obj) =>
            HashCode.Combine(obj.ProjectId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty));
    }
}
