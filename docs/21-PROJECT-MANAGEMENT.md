# 21 — Project Management Module

Delivery management for ShaunERP: projects, phases, milestones, tasks, dependencies, issues,
risks, change requests, timesheets, time tracking, attachments and reusable templates.

The module follows the same conventions as CRM / Sales Orders / Accounting: entities in
`ShaunERP.Core`, business logic in a single Infrastructure service, thin API controllers,
and Blazor Server pages that only bind and display.

---

## 1. Architecture

| Layer | Location | Responsibility |
| --- | --- | --- |
| Domain | `src/ShaunERP.Core/Projects/ProjectEntities.cs` | Entities and enums |
| Permissions | `src/ShaunERP.Core/Platform/PermissionCatalog.cs` | `projects.*` permission constants and role defaults |
| Module metadata | `src/ShaunERP.Core/Platform/ModuleCatalog.cs` | Navigation, entitlement, dependencies |
| Persistence | `src/ShaunERP.Infrastructure/Persistence/ShaunErpDbContext.cs` | `DbSet`s, relationships, indexes |
| Schema evolution | `src/ShaunERP.Infrastructure/Services/ProjectsSchema.cs` | Additive `ALTER`/`CREATE TABLE IF NOT EXISTS` SQL |
| Business logic | `ProjectManagementService.cs` + `ProjectManagementService.Execution.cs` | All rules, tenant filtering, permission checks, audit |
| File storage | `ProjectFileStorage.cs` | Attachment bytes on disk |
| API | `src/ShaunERP.Api/Controllers/ProjectsController.cs` | `api/projects/*` |
| UI | `src/ShaunERP.Web/Components/Pages/Projects/*` | Dashboard, list, editor, workspace, board, my work, timesheets, templates |

The service is split into two partial class files purely for readability:

* `ProjectManagementService.cs` — dashboard, projects, members, phases, milestones, templates,
  numbering, and the shared access/validation helpers.
* `ProjectManagementService.Execution.cs` — tasks, checklists, dependencies, my work, comments,
  attachments, issues, risks, change requests, timesheets and the timer.

### Schema evolution

The application uses `EnsureCreated` plus additive SQL rather than EF migrations.
`DbSeeder` calls `ProjectsSchema.EnsureAsync` immediately after `AccountingSchema.EnsureAsync`.
`ProjectsSchema`:

1. Adds every new column to the pre-existing `Projects` and `Issues` tables with
   `ADD COLUMN IF NOT EXISTS`.
2. Backfills `CompanyId` (from the first company) and `ProjectCode` for legacy rows, then
   promotes `Projects.CompanyId` to `NOT NULL`. This is what makes the previously nullable
   `CompanyId` safe to require in the model.
3. Creates the new tables and indexes with `IF NOT EXISTS`.
4. Creates the partial unique index `UX_ProjectTimeTrackers_ActivePerUser`
   (`CompanyId, UserId WHERE "IsActive"`) so a user can only ever have one running timer.

`DbSeeder.NormalizeCompanyModulesAsync` also grants the `projects` entitlement to every company,
in the same way Orders / Accounting / Settings are granted.

---

## 2. Tables

| Table | Purpose |
| --- | --- |
| `Projects` | Project master: code, customer, sales order link, dates, budget, progress, health |
| `ProjectMembers` | Team allocation (`UserId`, role, allocation %) — also drives project visibility |
| `ProjectPhases` | Ordered delivery stages |
| `ProjectMilestones` | Dated deliverables, optional customer approval |
| `ProjectTasks` | Work items: number, assignee, status, priority, dates, hours, progress |
| `ProjectTaskCollaborators` | Additional watchers/contributors on a task |
| `ProjectTaskChecklistItems` | Sub-steps inside a task |
| `ProjectTaskDependencies` | Predecessor → successor links with dependency type |
| `Issues` | Issue register (extends the legacy `Issues` table) |
| `ProjectRisks` | Risk register with probability/impact (score is computed, not stored) |
| `ProjectChangeRequests` | Scope changes with approval workflow |
| `ProjectTimesheets` | Time entries with draft → submitted → approved/rejected lifecycle |
| `ProjectTimeTrackers` | Running timers (one active per user) |
| `ProjectComments` | Polymorphic discussion on project/task/milestone/issue/risk/change request |
| `ProjectAttachments` | File metadata; bytes live on disk |
| `ProjectTemplates`, `ProjectTemplatePhases`, `ProjectTemplateTasks` | Reusable structures |
| `ProjectNumberSequences` | Per company + key + year counters |

Every table carries `CompanyId` and every query filters on it.

### Numbering

`NextNumberAsync(companyId, key)` produces `KEY-yyyy-000001` from `ProjectNumberSequences`.
Keys: `PRJ` (projects), `TASK` (tasks), `ISS` (issues), `RSK` (risks), `CR` (change requests).
Counters reset per calendar year.

---

## 3. Permissions

All permissions are registered in `Permissions.All`.

| Permission | Meaning |
| --- | --- |
| `projects.access` | Module entry — required by the module itself |
| `projects.dashboard.view` | KPI dashboard |
| `projects.view` | Read projects the user can see |
| `projects.view_all` | See every project in the account, not just owned/managed/member projects |
| `projects.create` / `projects.update` / `projects.delete` / `projects.archive` | Project lifecycle |
| `projects.tasks.view` / `.create` / `.update` / `.assign` / `.delete` | Task lifecycle; `assign` is required to set another user as assignee |
| `projects.timesheets.create` / `.submit` / `.approve` | Timesheet lifecycle |
| `projects.budget.view` / `projects.budget.edit` | Budget fields |
| `projects.financials.view` | Cost/revenue roll-ups |
| `projects.issues.manage` / `projects.risks.manage` | Registers |
| `projects.change_requests.create` / `.approve` | Change control |
| `projects.templates.manage` | Templates |

Role defaults:

* **Admin** and **Manager** — the full set.
* **User** — `projects.access`, `projects.dashboard.view`, `projects.view`,
  `projects.tasks.view`, `projects.tasks.create`, `projects.tasks.update`,
  `projects.timesheets.create`, `projects.timesheets.submit`.

### Visibility rules

A project is visible when any of the following is true:

* the caller holds `projects.view_all`, or is a system admin;
* the caller is the project manager or owner;
* the caller is a `ProjectMembers` row on that project.

Tasks, timesheets, issues, risks, change requests, comments and attachments inherit visibility
from their project. Budget and financial figures additionally require
`projects.budget.view` / `projects.financials.view`, and budget edits require
`projects.budget.edit`.

Every mutation writes an audit entry through `IAuditService` (`projects.create`,
`projects.task.status`, `projects.timesheet.approve`, and so on).

---

## 4. Service surface

`IProjectManagementService` (registered scoped in `DependencyInjection.AddInfrastructure`):

* **Dashboard** — `DashboardAsync` returns `ProjectDashboardStats` (counts by status, open/overdue
  tasks, milestones due, open issues/risks, pending timesheets, budget vs. actual, recent projects).
* **Projects** — `ListProjectsAsync(ProjectFilter)`, `GetProjectAsync`, `CreateProjectAsync`,
  `UpdateProjectAsync`, `ArchiveProjectAsync`, `DeleteProjectAsync`,
  `CreateProjectFromSalesOrderAsync`, `RecalculateProjectProgressAsync`.
* **Team / structure** — members, phases and milestones CRUD.
* **Tasks** — `ListTasksAsync(ProjectTaskFilter)` (project, assignee, status, priority, due,
  overdue, search, paging) returning `ProjectTaskPage`, plus get/create/update/delete and
  `UpdateTaskStatusAsync` for the board.
* **Checklists** — add / toggle / delete.
* **Dependencies** — `AddDependencyAsync` rejects self-links, duplicates, cross-project links and
  cycles (depth-first search over existing predecessor edges); `RemoveDependencyAsync`,
  `GetBlockedByAsync`, `GetBlockingAsync`. A task cannot be completed while an unfinished
  finish-to-start predecessor exists.
* **My work** — `MyWorkAsync(userId?)` returns Today / Overdue / Upcoming / In progress / Blocked /
  Completed recently.
* **Collaboration** — comments and attachments on any `ProjectCommentEntityType`.
* **Registers** — issues, risks, change requests (with `ApproveChangeRequestAsync`).
* **Time** — timesheets list/create/submit/approve/reject; `StartTimerAsync`, `StopTimerAsync`
  (converts the elapsed time into a draft timesheet entry), `GetActiveTimerAsync`.
* **Templates** — list/get/create/delete and `CloneTemplateToProjectAsync`.

Progress and hours roll up automatically: approving or editing a timesheet syncs
`ProjectTask.ActualHours` and `Project.ActualHours`; task status changes recompute
`Project.ProgressPercent` weighted by estimated hours.

### Attachments

`ProjectFileStorage` writes to `{ContentRoot}/App_Data/project-files/{companyId}/` using a random
UUID filename (the original name is only metadata). Configure a different root with
`Storage:ProjectFilesRoot`.

* Maximum size: 10 MB.
* Allowed extensions: `.pdf .png .jpg .jpeg .gif .docx .xlsx .txt .csv .zip`.
* Paths are resolved and verified to stay inside the storage root.
* Bytes are only ever served through the authorised endpoint
  `GET /projects/attachments/{id}`, which runs the same visibility checks as the service.

---

## 5. API

`ProjectsController`, `[Authorize]`, base route `api/projects`:

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/projects/dashboard` | KPI snapshot |
| GET | `/api/projects` | List (`search`, `status`, `includeArchived`) |
| GET | `/api/projects/{id}` | Single project |
| POST | `/api/projects` | Create |
| PUT | `/api/projects/{id}` | Update |
| POST | `/api/projects/{id}/archive` | Archive |
| DELETE | `/api/projects/{id}` | Delete (blocked once time has been submitted) |
| POST | `/api/projects/from-sales-order/{salesOrderId}` | Create delivery project from an order |
| GET | `/api/projects/tasks` | Filtered, paged task list |
| POST | `/api/projects/tasks` | Create task |
| PUT | `/api/projects/tasks/{id}/status` | Move task (kanban) |
| GET | `/api/projects/my-work` | Personal work board |
| GET / POST | `/api/projects/timesheets` | List / create entries |
| POST | `/api/projects/timesheets/{id}/submit\|approve\|reject` | Timesheet workflow |

Permission failures surface as `UnauthorizedAccessException`; validation failures return
`400` with `{ "error": "..." }`.

---

## 6. Pages

| Route | Page | Notes |
| --- | --- | --- |
| `/projects` | `Index.razor` | KPI dashboard and recent projects |
| `/projects/list` | `List.razor` | Filterable project table |
| `/projects/new`, `/projects/{id}` | `Edit.razor` | Create / edit project |
| `/projects/{id}/workspace` | `Detail.razor` | Tabs: Overview, Tasks, Board, Milestones, Team, Issues, Risks, Changes, Timesheets, Activity |
| `/projects/tasks` | `Tasks.razor` | Cross-project task list with paging |
| `/projects/kanban` | `Kanban.razor` | Board across projects |
| `/projects/my-work` | `MyWork.razor` | Personal board with start/stop timer |
| `/projects/timesheets` | `Timesheets.razor` | Log, submit and approve time |
| `/projects/templates` | `Templates.razor` | Manage templates, spawn projects from them |

`Components/Shared/ProjectKanbanBoard.razor` is the reusable board — used standalone at
`/projects/kanban` and embedded in the project workspace via `ProjectId`.

Board columns are the eight `ProjectTaskStatus` values. Moving work uses a status selector on
each card that calls `UpdateTaskStatusAsync` immediately; HTML5 drag and drop is deliberately not
used (see remaining work below).

### Sales order integration

`Orders/Edit.razor` shows a **Create project** action for orders that are Confirmed, Fulfilled,
Partially invoiced or Invoiced. It calls `CreateProjectFromSalesOrderAsync`, which prefills the
customer, sales order and quote links, currency, description, planned dates (order date →
requested delivery date) and budget/billing from `GrandTotal`, then navigates to the new
workspace. A second attempt on the same order is rejected with the existing project code.

---

## 7. Tests

`tests/ShaunERP.Tests/ProjectManagementTests.cs`:

* project + task creation, numbering, status change and progress roll-up;
* circular and self dependencies are rejected, `GetBlockedByAsync` returns predecessors;
* tenant isolation across two companies for projects and tasks;
* create-from-sales-order prefill and duplicate protection;
* timesheet approval rolling hours up to the task;
* missing permission blocks project creation.

`ModuleRegistryTests.ProjectsModule_IsEnabledInCatalog` asserts the catalog entry is no longer a
placeholder and exposes the expected navigation.

---

## 8. Status

### Delivered (P0 + P1 foundations)

Projects, phases, milestones, team, tasks with checklists and dependencies, kanban, my work,
issues, risks, change requests with approval, timesheets with approval workflow, running timers,
comments, attachments with authorised download, templates and cloning, project numbering,
dashboard KPIs, sales order → project creation, API, permissions and tenant isolation.

### Remaining (P1 / P2)

| Item | Status |
| --- | --- |
| Gantt chart | Deferred — needs a charting library decision; dependencies and dates are already stored, so it is a UI-only addition |
| Resource capacity planning / utilisation heatmap | Not started; `ProjectMembers.AllocationPercent` is captured for it |
| Calendar view of milestones and due dates | Not started |
| Baseline vs. actual schedule variance, earned value | Not started |
| Expense capture and billing of approved timesheets into invoices | Not started; `BillableHours` is recorded |
| Drag-and-drop kanban | Intentionally replaced by status selectors |
| Notifications / mentions | Not applicable — the platform has no notification system |
| Global search integration | Not applicable — no global search exists yet |
