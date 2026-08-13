using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services;

/// <summary>
/// Additive schema evolution for the Project Management module.
/// Mirrors <see cref="AccountingSchema"/>: EnsureCreated builds new databases, these statements
/// bring already-provisioned databases up to the current model without EF migrations.
/// </summary>
internal static class ProjectsSchema
{
    public static async Task EnsureAsync(ShaunErpDbContext db, ILogger logger, CancellationToken ct)
    {
        foreach (var sql in LegacyAlterSql)
        {
            await Exec(db, logger, sql, ct);
        }

        await BackfillProjectCompanyAsync(db, logger, ct);

        foreach (var sql in TableSql)
        {
            await Exec(db, logger, sql, ct);
        }

        foreach (var sql in IndexSql)
        {
            await Exec(db, logger, sql, ct);
        }
    }

    private static async Task Exec(ShaunErpDbContext db, ILogger logger, string sql, CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Projects schema statement skipped");
        }
    }

    /// <summary>
    /// Project.CompanyId is non-nullable in the model. Legacy rows may predate tenancy,
    /// so attach them to the oldest company before tightening the constraint.
    /// </summary>
    private static async Task BackfillProjectCompanyAsync(ShaunErpDbContext db, ILogger logger, CancellationToken ct)
    {
        await Exec(db, logger, """
            UPDATE "Projects"
            SET "CompanyId" = (SELECT MIN("Id") FROM "Companies")
            WHERE "CompanyId" IS NULL AND EXISTS (SELECT 1 FROM "Companies");
            """, ct);

        await Exec(db, logger, """
            DELETE FROM "Projects" WHERE "CompanyId" IS NULL;
            """, ct);

        await Exec(db, logger, """
            ALTER TABLE "Projects" ALTER COLUMN "CompanyId" SET NOT NULL;
            """, ct);

        await Exec(db, logger, """
            UPDATE "Projects"
            SET "ProjectCode" = 'PRJ-' || LPAD("Id"::text, 6, '0')
            WHERE "ProjectCode" IS NULL OR "ProjectCode" = '';
            """, ct);

        await Exec(db, logger, """
            UPDATE "Issues"
            SET "CompanyId" = COALESCE(
                (SELECT p."CompanyId" FROM "Projects" p WHERE p."Id" = "Issues"."ProjectId"),
                (SELECT MIN("Id") FROM "Companies"))
            WHERE "CompanyId" IS NULL OR "CompanyId" = 0;
            """, ct);
    }

    private static readonly string[] LegacyAlterSql =
    [
        // ——— Projects (pre-existing table) ———
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CompanyId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ProjectCode" text NOT NULL DEFAULT '';""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Description" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CustomerId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CustomerName" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "OpportunityId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "QuoteId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "SalesOrderId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ProjectType" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Department" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ManagerUserId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "OwnerUserId" integer NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Priority" integer NOT NULL DEFAULT 1;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ProjectStatus" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "HealthStatus" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "PlannedStartDate" timestamptz NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "PlannedEndDate" timestamptz NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualStartDate" timestamptz NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualEndDate" timestamptz NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "EstimatedHours" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualHours" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Budget" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "RevisedBudget" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "EstimatedCost" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualCost" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "EstimatedLabourCost" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualLabourCost" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "EstimatedExpense" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ActualExpense" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CustomerBilling" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Revenue" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "BillingType" integer NOT NULL DEFAULT 1;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "CurrencyCode" text NOT NULL DEFAULT 'INR';""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "ProgressPercent" numeric(18,2) NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "Notes" text NULL;""",
        """ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "TemplateId" integer NULL;""",

        // ——— Issues (pre-existing table, re-shaped as the project issue register) ———
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "CompanyId" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "ProjectId" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "IssueNumber" text NOT NULL DEFAULT '';""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Title" text NOT NULL DEFAULT '';""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Description" text NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Type" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Severity" integer NOT NULL DEFAULT 1;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Priority" integer NOT NULL DEFAULT 1;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "IssueStatus" integer NOT NULL DEFAULT 0;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "OwnerUserId" integer NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "DateRaised" timestamptz NOT NULL DEFAULT now();""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "TargetResolutionDate" timestamptz NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "ActualResolutionDate" timestamptz NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "RootCause" text NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "Resolution" text NULL;""",
        """ALTER TABLE "Issues" ADD COLUMN IF NOT EXISTS "DueDate" timestamptz NULL;"""
    ];

    private static readonly string[] TableSql =
    [
        """
        CREATE TABLE IF NOT EXISTS "ProjectMembers" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "UserId" integer NOT NULL,
            "ProjectRole" text NOT NULL DEFAULT 'Member',
            "AllocationPercent" numeric(18,2) NOT NULL DEFAULT 100,
            "StartDate" timestamptz NULL,
            "EndDate" timestamptz NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectPhases" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "Name" text NOT NULL DEFAULT '',
            "Description" text NULL,
            "SortOrder" integer NOT NULL DEFAULT 0,
            "OwnerUserId" integer NULL,
            "StartDate" timestamptz NULL,
            "DueDate" timestamptz NULL,
            "PhaseStatus" integer NOT NULL DEFAULT 0,
            "ProgressPercent" numeric(18,2) NOT NULL DEFAULT 0,
            "Notes" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectMilestones" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "PhaseId" integer NULL,
            "Name" text NOT NULL DEFAULT '',
            "Description" text NULL,
            "OwnerUserId" integer NULL,
            "PlannedDate" timestamptz NULL,
            "ActualCompletionDate" timestamptz NULL,
            "MilestoneStatus" integer NOT NULL DEFAULT 0,
            "ProgressPercent" numeric(18,2) NOT NULL DEFAULT 0,
            "CustomerApprovalRequired" boolean NOT NULL DEFAULT false,
            "CustomerApproved" boolean NOT NULL DEFAULT false,
            "ApprovalDate" timestamptz NULL,
            "Notes" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTasks" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "PhaseId" integer NULL,
            "MilestoneId" integer NULL,
            "ParentTaskId" integer NULL,
            "TaskNumber" text NOT NULL DEFAULT '',
            "Name" text NOT NULL DEFAULT '',
            "Description" text NULL,
            "AssigneeUserId" integer NULL,
            "Priority" integer NOT NULL DEFAULT 1,
            "TaskStatus" integer NOT NULL DEFAULT 0,
            "StartDate" timestamptz NULL,
            "DueDate" timestamptz NULL,
            "CompletedDate" timestamptz NULL,
            "EstimatedHours" numeric(18,2) NOT NULL DEFAULT 0,
            "ActualHours" numeric(18,2) NOT NULL DEFAULT 0,
            "IsBillable" boolean NOT NULL DEFAULT true,
            "ProgressPercent" numeric(18,2) NOT NULL DEFAULT 0,
            "Tags" text NULL,
            "SortOrder" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTaskCollaborators" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "TaskId" integer NOT NULL,
            "UserId" integer NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTaskChecklistItems" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "TaskId" integer NOT NULL,
            "Title" text NOT NULL DEFAULT '',
            "IsCompleted" boolean NOT NULL DEFAULT false,
            "SortOrder" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTaskDependencies" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PredecessorTaskId" integer NOT NULL,
            "SuccessorTaskId" integer NOT NULL,
            "DependencyType" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectRisks" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "RiskNumber" text NOT NULL DEFAULT '',
            "Description" text NOT NULL DEFAULT '',
            "Probability" integer NOT NULL DEFAULT 1,
            "Impact" integer NOT NULL DEFAULT 1,
            "OwnerUserId" integer NULL,
            "MitigationPlan" text NULL,
            "ContingencyPlan" text NULL,
            "RiskStatus" integer NOT NULL DEFAULT 0,
            "ReviewDate" timestamptz NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectChangeRequests" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "ChangeRequestNumber" text NOT NULL DEFAULT '',
            "CustomerId" integer NULL,
            "RequestedBy" text NULL,
            "RequestedDate" timestamptz NOT NULL DEFAULT now(),
            "Description" text NOT NULL DEFAULT '',
            "BusinessReason" text NULL,
            "EstimatedHours" numeric(18,2) NOT NULL DEFAULT 0,
            "EstimatedCost" numeric(18,2) NOT NULL DEFAULT 0,
            "ScheduleImpact" text NULL,
            "Priority" integer NOT NULL DEFAULT 1,
            "ApprovalStatus" integer NOT NULL DEFAULT 0,
            "ApprovedByUserId" integer NULL,
            "ApprovedDate" timestamptz NULL,
            "ImplementationStatus" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTimesheets" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "TaskId" integer NULL,
            "UserId" integer NOT NULL,
            "WorkDate" timestamptz NOT NULL DEFAULT now(),
            "StartTime" interval NULL,
            "EndTime" interval NULL,
            "Hours" numeric(18,2) NOT NULL DEFAULT 0,
            "BillableHours" numeric(18,2) NOT NULL DEFAULT 0,
            "NonBillableHours" numeric(18,2) NOT NULL DEFAULT 0,
            "Description" text NULL,
            "EntryStatus" integer NOT NULL DEFAULT 0,
            "SubmittedDate" timestamptz NULL,
            "ApprovedByUserId" integer NULL,
            "ApprovalDate" timestamptz NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTimeTrackers" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "UserId" integer NOT NULL,
            "ProjectId" integer NOT NULL,
            "TaskId" integer NULL,
            "StartedAt" timestamptz NOT NULL DEFAULT now(),
            "StoppedAt" timestamptz NULL,
            "IsActive" boolean NOT NULL DEFAULT true,
            "Description" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectComments" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "EntityType" integer NOT NULL DEFAULT 0,
            "EntityId" integer NOT NULL DEFAULT 0,
            "ProjectId" integer NULL,
            "AuthorUserId" integer NOT NULL,
            "Body" text NOT NULL DEFAULT '',
            "ParentCommentId" integer NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectAttachments" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "EntityType" integer NOT NULL DEFAULT 0,
            "EntityId" integer NOT NULL DEFAULT 0,
            "ProjectId" integer NULL,
            "FileName" text NOT NULL DEFAULT '',
            "ContentType" text NOT NULL DEFAULT 'application/octet-stream',
            "FileSizeBytes" bigint NOT NULL DEFAULT 0,
            "StoragePath" text NOT NULL DEFAULT '',
            "UploadedByUserId" integer NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTemplates" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL DEFAULT '',
            "Description" text NULL,
            "ProjectType" text NULL,
            "IsActive" boolean NOT NULL DEFAULT true
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTemplatePhases" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "TemplateId" integer NOT NULL,
            "Name" text NOT NULL DEFAULT '',
            "SortOrder" integer NOT NULL DEFAULT 0,
            "EstimatedDurationDays" integer NOT NULL DEFAULT 0
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectTemplateTasks" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "TemplatePhaseId" integer NOT NULL,
            "Name" text NOT NULL DEFAULT '',
            "Description" text NULL,
            "SortOrder" integer NOT NULL DEFAULT 0,
            "EstimatedHours" numeric(18,2) NOT NULL DEFAULT 0,
            "DefaultRole" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ProjectNumberSequences" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamptz NOT NULL DEFAULT now(),
            "UpdatedDate" timestamptz NULL,
            "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "SequenceKey" text NOT NULL DEFAULT 'PRJ',
            "Year" integer NOT NULL DEFAULT 0,
            "NextValue" integer NOT NULL DEFAULT 1
        );
        """
    ];

    private static readonly string[] IndexSql =
    [
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Projects_CompanyId_ProjectCode" ON "Projects" ("CompanyId", "ProjectCode");""",
        """CREATE INDEX IF NOT EXISTS "IX_Projects_CompanyId_ProjectStatus" ON "Projects" ("CompanyId", "ProjectStatus");""",
        """CREATE INDEX IF NOT EXISTS "IX_Issues_CompanyId_ProjectId" ON "Issues" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Issues_CompanyId_IssueNumber" ON "Issues" ("CompanyId", "IssueNumber");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectMembers_CompanyId_ProjectId_UserId" ON "ProjectMembers" ("CompanyId", "ProjectId", "UserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectPhases_CompanyId_ProjectId" ON "ProjectPhases" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectMilestones_CompanyId_ProjectId" ON "ProjectMilestones" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTasks_CompanyId_ProjectId" ON "ProjectTasks" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTasks_CompanyId_TaskStatus" ON "ProjectTasks" ("CompanyId", "TaskStatus");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTasks_CompanyId_AssigneeUserId" ON "ProjectTasks" ("CompanyId", "AssigneeUserId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTasks_CompanyId_DueDate" ON "ProjectTasks" ("CompanyId", "DueDate");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectTasks_CompanyId_TaskNumber" ON "ProjectTasks" ("CompanyId", "TaskNumber");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTaskChecklistItems_TaskId" ON "ProjectTaskChecklistItems" ("TaskId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTaskCollaborators_TaskId" ON "ProjectTaskCollaborators" ("TaskId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectTaskDependencies_Predecessor_Successor" ON "ProjectTaskDependencies" ("PredecessorTaskId", "SuccessorTaskId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectRisks_CompanyId_ProjectId" ON "ProjectRisks" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectChangeRequests_CompanyId_ProjectId" ON "ProjectChangeRequests" ("CompanyId", "ProjectId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTimesheets_CompanyId_UserId_WorkDate" ON "ProjectTimesheets" ("CompanyId", "UserId", "WorkDate");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTimesheets_CompanyId_EntryStatus" ON "ProjectTimesheets" ("CompanyId", "EntryStatus");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTimeTrackers_CompanyId_UserId_IsActive" ON "ProjectTimeTrackers" ("CompanyId", "UserId", "IsActive");""",
        // Database-level guarantee that a user can only ever have one running timer.
        """CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProjectTimeTrackers_ActivePerUser" ON "ProjectTimeTrackers" ("CompanyId", "UserId") WHERE "IsActive";""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectComments_CompanyId_Entity" ON "ProjectComments" ("CompanyId", "EntityType", "EntityId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectAttachments_CompanyId_Entity" ON "ProjectAttachments" ("CompanyId", "EntityType", "EntityId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectTemplates_CompanyId_Name" ON "ProjectTemplates" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTemplatePhases_TemplateId" ON "ProjectTemplatePhases" ("TemplateId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ProjectTemplateTasks_TemplatePhaseId" ON "ProjectTemplateTasks" ("TemplatePhaseId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProjectNumberSequences_CompanyId_Key_Year" ON "ProjectNumberSequences" ("CompanyId", "SequenceKey", "Year");"""
    ];
}
