namespace ShaunERP.Core.Platform;

public static class ModuleCatalog
{
    public static IReadOnlyList<ModuleDefinition> All { get; } =
    [
        new ModuleDefinition
        {
            Key = ModuleKeys.Home,
            Name = "Home",
            Description = "Main ERP dashboard and cross-module overview",
            Category = ModuleCategory.Home,
            BaseRoute = "/",
            DashboardRoute = "/",
            Icon = "home",
            DisplayOrder = 0,
            RequiredEntitlement = null,
            RequiredPermission = null,
            NavigationItems =
            [
                new ModuleNavItem { Key = "home.dashboard", Title = "Main dashboard", Route = "/", Icon = "dashboard", Order = 1 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Users,
            Name = "User Management",
            Description = "Users, roles, and account access",
            Category = ModuleCategory.Administration,
            BaseRoute = "/users",
            DashboardRoute = "/users",
            Icon = "users",
            RequiredEntitlement = ModuleKeys.Users,
            RequiredPermission = Permissions.UsersAccess,
            DisplayOrder = 10,
            Permissions =
            [
                Permissions.UsersAccess, Permissions.UsersView, Permissions.UsersCreate, Permissions.UsersUpdate,
                Permissions.UsersDelete, Permissions.RolesView, Permissions.RolesManage
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "users.list", Title = "Users", Route = "/users", Icon = "users", RequiredPermission = Permissions.UsersView, Group = "Directory", Order = 1 },
                new ModuleNavItem { Key = "users.create", Title = "Add user", Route = "/users/new", Icon = "user-plus", RequiredPermission = Permissions.UsersCreate, Group = "Directory", Order = 2 },
                new ModuleNavItem { Key = "users.roles", Title = "Roles", Route = "/roles", Icon = "shield", RequiredPermission = Permissions.RolesView, Group = "Access", Order = 3 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Crm,
            Name = "CRM",
            Description = "Enterprise CRM: leads, accounts, pipeline, campaigns, quotes, cases, and activities",
            Category = ModuleCategory.CustomerAndSales,
            BaseRoute = "/crm",
            DashboardRoute = "/crm",
            Icon = "crm",
            RequiredEntitlement = ModuleKeys.Crm,
            RequiredPermission = Permissions.CrmAccess,
            DisplayOrder = 20,
            Permissions =
            [
                Permissions.CrmAccess, Permissions.CrmDashboardView, Permissions.CrmPipelineView, Permissions.CrmForecastView,
                Permissions.CrmLeadsView, Permissions.CrmLeadsCreate, Permissions.CrmLeadsUpdate, Permissions.CrmLeadsDelete,
                Permissions.CrmProspectsView, Permissions.CrmProspectsCreate, Permissions.CrmProspectsUpdate, Permissions.CrmProspectsDelete,
                Permissions.CrmAccountsView, Permissions.CrmAccountsCreate, Permissions.CrmAccountsUpdate, Permissions.CrmAccountsDelete,
                Permissions.CrmContactsView, Permissions.CrmContactsCreate, Permissions.CrmContactsUpdate, Permissions.CrmContactsDelete,
                Permissions.CrmOpportunitiesView, Permissions.CrmOpportunitiesCreate, Permissions.CrmOpportunitiesUpdate, Permissions.CrmOpportunitiesDelete,
                Permissions.CrmCustomersView, Permissions.CrmCustomersCreate, Permissions.CrmCustomersUpdate, Permissions.CrmCustomersDelete,
                Permissions.CrmCampaignsView, Permissions.CrmCampaignsCreate, Permissions.CrmCampaignsUpdate, Permissions.CrmCampaignsDelete,
                Permissions.CrmProductsView, Permissions.CrmProductsCreate, Permissions.CrmProductsUpdate, Permissions.CrmProductsDelete,
                Permissions.CrmQuotesView, Permissions.CrmQuotesCreate, Permissions.CrmQuotesUpdate, Permissions.CrmQuotesDelete,
                Permissions.CrmCasesView, Permissions.CrmCasesCreate, Permissions.CrmCasesUpdate, Permissions.CrmCasesDelete,
                Permissions.CrmTasksView, Permissions.CrmTasksCreate, Permissions.CrmTasksUpdate, Permissions.CrmTasksDelete,
                Permissions.CrmMeetingsView, Permissions.CrmMeetingsCreate, Permissions.CrmMeetingsUpdate, Permissions.CrmMeetingsDelete,
                Permissions.CrmCallsView, Permissions.CrmCallsCreate, Permissions.CrmCallsUpdate, Permissions.CrmCallsDelete,
                Permissions.CrmNotesView, Permissions.CrmNotesCreate, Permissions.CrmNotesUpdate, Permissions.CrmNotesDelete,
                Permissions.CrmPeopleView, Permissions.CrmPeopleCreate, Permissions.CrmPeopleUpdate,
                Permissions.CrmIdentityReviewView, Permissions.CrmIdentityReviewResolve,
                Permissions.CrmCoursesView, Permissions.CrmCoursesManage, Permissions.CrmEnrollmentsView, Permissions.CrmEnrollmentsManage,
                Permissions.CrmSubscriptionsView, Permissions.CrmSubscriptionsManage,
                Permissions.CrmConsultationsView, Permissions.CrmConsultationsManage,
                Permissions.CrmEventsView, Permissions.CrmEventsManage,
                Permissions.CrmReportsView, Permissions.CrmReportsManage
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "crm.dashboard", Title = "CRM dashboard", Route = "/crm", Icon = "dashboard", RequiredPermission = Permissions.CrmDashboardView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "crm.pipeline", Title = "Pipeline board", Route = "/crm/pipeline", Icon = "pipeline", RequiredPermission = Permissions.CrmPipelineView, Group = "Overview", Order = 2 },
                new ModuleNavItem { Key = "crm.forecast", Title = "Forecast", Route = "/crm/forecast", Icon = "forecast", RequiredPermission = Permissions.CrmForecastView, Group = "Overview", Order = 3 },
                new ModuleNavItem { Key = "crm.people", Title = "People", Route = "/crm/people", Icon = "people", RequiredPermission = Permissions.CrmPeopleView, Group = "Identity", Order = 4 },
                new ModuleNavItem { Key = "crm.identity-review", Title = "Identity review", Route = "/crm/identity-review", Icon = "review", RequiredPermission = Permissions.CrmIdentityReviewView, Group = "Identity", Order = 5 },
                new ModuleNavItem { Key = "crm.leads", Title = "Leads", Route = "/crm/leads", Icon = "lead", RequiredPermission = Permissions.CrmLeadsView, Group = "Sales", Order = 6 },
                new ModuleNavItem { Key = "crm.prospects", Title = "Prospects", Route = "/crm/prospects", Icon = "prospect", RequiredPermission = Permissions.CrmProspectsView, Group = "Sales", Order = 7 },
                new ModuleNavItem { Key = "crm.opportunities", Title = "Opportunities", Route = "/crm/opportunities", Icon = "deal", RequiredPermission = Permissions.CrmOpportunitiesView, Group = "Sales", Order = 8 },
                new ModuleNavItem { Key = "crm.quotes", Title = "Quotes", Route = "/crm/quotes", Icon = "quote", RequiredPermission = Permissions.CrmQuotesView, Group = "Sales", Order = 9 },
                new ModuleNavItem { Key = "crm.accounts", Title = "Accounts", Route = "/crm/accounts", Icon = "account", RequiredPermission = Permissions.CrmAccountsView, Group = "Customers", Order = 10 },
                new ModuleNavItem { Key = "crm.contacts", Title = "Contacts", Route = "/crm/contacts", Icon = "contact", RequiredPermission = Permissions.CrmContactsView, Group = "Customers", Order = 11 },
                new ModuleNavItem { Key = "crm.customers", Title = "Customers", Route = "/crm/customers", Icon = "customer", RequiredPermission = Permissions.CrmCustomersView, Group = "Customers", Order = 12 },
                new ModuleNavItem { Key = "crm.courses", Title = "Courses", Route = "/crm/courses", Icon = "course", RequiredPermission = Permissions.CrmCoursesView, Group = "Training", Order = 13 },
                new ModuleNavItem { Key = "crm.enrollments", Title = "Students", Route = "/crm/students", Icon = "student", RequiredPermission = Permissions.CrmEnrollmentsView, Group = "Training", Order = 14 },
                new ModuleNavItem { Key = "crm.subscription-plans", Title = "Subscription plans", Route = "/crm/subscription-plans", Icon = "plan", RequiredPermission = Permissions.CrmSubscriptionsView, Group = "Subscriptions", Order = 15 },
                new ModuleNavItem { Key = "crm.subscription-members", Title = "Members", Route = "/crm/subscription-members", Icon = "member", RequiredPermission = Permissions.CrmSubscriptionsView, Group = "Subscriptions", Order = 16 },
                new ModuleNavItem { Key = "crm.report-purchases", Title = "All purchases", Route = "/crm/report-purchases", Icon = "report", RequiredPermission = Permissions.CrmReportsView, Group = "Report Purchases", Order = 17 },
                new ModuleNavItem { Key = "crm.report-horoscope", Title = "Horoscope reports", Route = "/crm/report-purchases?type=Horoscope", Icon = "horoscope", RequiredPermission = Permissions.CrmReportsView, Group = "Report Purchases", Order = 18 },
                new ModuleNavItem { Key = "crm.report-numerology", Title = "Numerology reports", Route = "/crm/report-purchases?type=Numerology", Icon = "numerology", RequiredPermission = Permissions.CrmReportsView, Group = "Report Purchases", Order = 19 },
                new ModuleNavItem { Key = "crm.consultations", Title = "Consultation contacts", Route = "/crm/consultations", Icon = "history", RequiredPermission = Permissions.CrmConsultationsView, Group = "Consultations", Order = 21 },
                new ModuleNavItem { Key = "crm.consultation-services", Title = "Consultation services", Route = "/crm/consultation-services", Icon = "consult", RequiredPermission = Permissions.CrmConsultationsView, Group = "Consultations", Order = 22 },
                new ModuleNavItem { Key = "crm.events", Title = "Events", Route = "/crm/events", Icon = "event", RequiredPermission = Permissions.CrmEventsView, Group = "Events", Order = 22 },
                new ModuleNavItem { Key = "crm.event-participants", Title = "Participants", Route = "/crm/event-participants", Icon = "participant", RequiredPermission = Permissions.CrmEventsView, Group = "Events", Order = 23 },
                new ModuleNavItem { Key = "crm.campaigns", Title = "Campaigns", Route = "/crm/campaigns", Icon = "campaign", RequiredPermission = Permissions.CrmCampaignsView, Group = "Marketing", Order = 24 },
                new ModuleNavItem { Key = "crm.products", Title = "Products", Route = "/crm/products", Icon = "product", RequiredPermission = Permissions.CrmProductsView, Group = "Marketing", Order = 25 },
                new ModuleNavItem { Key = "crm.cases", Title = "Cases", Route = "/crm/cases", Icon = "case", RequiredPermission = Permissions.CrmCasesView, Group = "Service", Order = 26 },
                new ModuleNavItem { Key = "crm.tasks", Title = "Tasks", Route = "/crm/tasks", Icon = "task", RequiredPermission = Permissions.CrmTasksView, Group = "Activities", Order = 27 },
                new ModuleNavItem { Key = "crm.meetings", Title = "Meetings", Route = "/crm/meetings", Icon = "meeting", RequiredPermission = Permissions.CrmMeetingsView, Group = "Activities", Order = 28 },
                new ModuleNavItem { Key = "crm.calls", Title = "Calls", Route = "/crm/calls", Icon = "call", RequiredPermission = Permissions.CrmCallsView, Group = "Activities", Order = 29 },
                new ModuleNavItem { Key = "crm.notes", Title = "Notes", Route = "/crm/notes", Icon = "note", RequiredPermission = Permissions.CrmNotesView, Group = "Activities", Order = 30 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Finance,
            Name = "Invoicing",
            Description = "AR invoicing: billing book, invoices, line items, and payments",
            Category = ModuleCategory.Finance,
            BaseRoute = "/finance",
            DashboardRoute = "/finance",
            Icon = "finance",
            RequiredEntitlement = ModuleKeys.Finance,
            RequiredPermission = Permissions.FinanceAccess,
            DisplayOrder = 30,
            IsPlaceholder = false,
            IsEnabled = true,
            Dependencies = [ModuleKeys.Crm],
            Permissions =
            [
                Permissions.FinanceAccess, Permissions.FinanceDashboardView,
                Permissions.FinanceInvoicesView, Permissions.FinanceInvoicesCreate,
                Permissions.FinanceInvoicesUpdate, Permissions.FinanceInvoicesDelete,
                Permissions.FinanceInvoicesMarkPaid, Permissions.FinancePaymentsRecord,
                Permissions.FinanceSettingsManage, Permissions.FinanceCustomersView
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "finance.dashboard", Title = "Finance dashboard", Route = "/finance", Icon = "dashboard", RequiredPermission = Permissions.FinanceDashboardView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "finance.invoices", Title = "Invoices", Route = "/finance/invoices", Icon = "invoice", RequiredPermission = Permissions.FinanceInvoicesView, Group = "Receivables", Order = 2 },
                new ModuleNavItem { Key = "finance.payments", Title = "Payments", Route = "/finance/payments", Icon = "payment", RequiredPermission = Permissions.FinancePaymentsRecord, Group = "Receivables", Order = 3 },
                new ModuleNavItem { Key = "finance.settings", Title = "Billing settings", Route = "/finance/settings", Icon = "settings", RequiredPermission = Permissions.FinanceSettingsManage, Group = "Setup", Order = 4 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Orders,
            Name = "Sales Orders",
            Description = "Order-to-cash: sales orders, line items, confirm/fulfill, and invoice from order",
            Category = ModuleCategory.CustomerAndSales,
            BaseRoute = "/orders",
            DashboardRoute = "/orders",
            Icon = "orders",
            RequiredEntitlement = ModuleKeys.Orders,
            RequiredPermission = Permissions.OrdersAccess,
            DisplayOrder = 35,
            IsPlaceholder = false,
            IsEnabled = true,
            Dependencies = [ModuleKeys.Crm, ModuleKeys.Finance],
            Permissions =
            [
                Permissions.OrdersAccess, Permissions.OrdersDashboardView,
                Permissions.OrdersView, Permissions.OrdersCreate, Permissions.OrdersUpdate, Permissions.OrdersDelete,
                Permissions.OrdersConfirm, Permissions.OrdersFulfill, Permissions.OrdersCancel,
                Permissions.OrdersCreateInvoice
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "orders.dashboard", Title = "Orders dashboard", Route = "/orders", Icon = "dashboard", RequiredPermission = Permissions.OrdersDashboardView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "orders.list", Title = "Sales orders", Route = "/orders/list", Icon = "order", RequiredPermission = Permissions.OrdersView, Group = "Orders", Order = 2 },
                new ModuleNavItem { Key = "orders.new", Title = "New order", Route = "/orders/new", Icon = "order-plus", RequiredPermission = Permissions.OrdersCreate, Group = "Orders", Order = 3 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Accounting,
            Name = "Accounting",
            Description = "Double-entry books: chart of accounts, vouchers, GST, and financial reports",
            Category = ModuleCategory.Finance,
            BaseRoute = "/accounting",
            DashboardRoute = "/accounting",
            Icon = "accounting",
            RequiredEntitlement = ModuleKeys.Accounting,
            RequiredPermission = Permissions.AccountingAccess,
            DisplayOrder = 38,
            IsPlaceholder = false,
            IsEnabled = true,
            Dependencies = [ModuleKeys.Finance],
            Permissions =
            [
                Permissions.AccountingAccess, Permissions.AccountingView,
                Permissions.AccountingLedgersView, Permissions.AccountingLedgersCreate, Permissions.AccountingLedgersEdit,
                Permissions.AccountingVouchersView, Permissions.AccountingVouchersCreate,
                Permissions.AccountingVouchersPost, Permissions.AccountingVouchersReverse,
                Permissions.AccountingReportsView, Permissions.AccountingSettingsManage,
                Permissions.AccountingFinancialYearManage, Permissions.AccountingPeriodLockManage,
                Permissions.CompanySettingsView, Permissions.CompanySettingsEdit, Permissions.GstSettingsManage
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "accounting.dashboard", Title = "Accounting dashboard", Route = "/accounting", Icon = "dashboard", RequiredPermission = Permissions.AccountingView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "accounting.journal", Title = "Journal", Route = "/accounting/vouchers/journal", Icon = "journal", RequiredPermission = Permissions.AccountingVouchersCreate, Group = "Transactions", Order = 2 },
                new ModuleNavItem { Key = "accounting.receipt", Title = "Receipt", Route = "/accounting/vouchers/receipt", Icon = "receipt", RequiredPermission = Permissions.AccountingVouchersCreate, Group = "Transactions", Order = 3 },
                new ModuleNavItem { Key = "accounting.payment", Title = "Payment", Route = "/accounting/vouchers/payment", Icon = "payment", RequiredPermission = Permissions.AccountingVouchersCreate, Group = "Transactions", Order = 4 },
                new ModuleNavItem { Key = "accounting.contra", Title = "Contra", Route = "/accounting/vouchers/contra", Icon = "contra", RequiredPermission = Permissions.AccountingVouchersCreate, Group = "Transactions", Order = 5 },
                new ModuleNavItem { Key = "accounting.vouchers", Title = "All vouchers", Route = "/accounting/vouchers", Icon = "vouchers", RequiredPermission = Permissions.AccountingVouchersView, Group = "Transactions", Order = 6 },
                new ModuleNavItem { Key = "accounting.coa", Title = "Chart of accounts", Route = "/accounting/ledgers", Icon = "coa", RequiredPermission = Permissions.AccountingLedgersView, Group = "Masters", Order = 7 },
                new ModuleNavItem { Key = "accounting.groups", Title = "Account groups", Route = "/accounting/groups", Icon = "groups", RequiredPermission = Permissions.AccountingLedgersView, Group = "Masters", Order = 8 },
                new ModuleNavItem { Key = "accounting.voucher-types", Title = "Voucher types", Route = "/accounting/voucher-types", Icon = "types", RequiredPermission = Permissions.AccountingSettingsManage, Group = "Masters", Order = 9 },
                new ModuleNavItem { Key = "accounting.currencies", Title = "Currencies", Route = "/accounting/currencies", Icon = "currency", RequiredPermission = Permissions.AccountingSettingsManage, Group = "Masters", Order = 10 },
                new ModuleNavItem { Key = "accounting.gl", Title = "General ledger", Route = "/accounting/reports/general-ledger", Icon = "gl", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 11 },
                new ModuleNavItem { Key = "accounting.tb", Title = "Trial balance", Route = "/accounting/reports/trial-balance", Icon = "tb", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 12 },
                new ModuleNavItem { Key = "accounting.pl", Title = "Profit & loss", Route = "/accounting/reports/profit-loss", Icon = "pl", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 13 },
                new ModuleNavItem { Key = "accounting.bs", Title = "Balance sheet", Route = "/accounting/reports/balance-sheet", Icon = "bs", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 14 },
                new ModuleNavItem { Key = "accounting.daybook", Title = "Day book", Route = "/accounting/reports/day-book", Icon = "day", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 15 },
                new ModuleNavItem { Key = "accounting.cashbook", Title = "Cash book", Route = "/accounting/reports/cash-book", Icon = "cash", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 16 },
                new ModuleNavItem { Key = "accounting.bankbook", Title = "Bank book", Route = "/accounting/reports/bank-book", Icon = "bank", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 17 },
                new ModuleNavItem { Key = "accounting.ar", Title = "Receivables", Route = "/accounting/reports/receivables", Icon = "ar", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 18 },
                new ModuleNavItem { Key = "accounting.ap", Title = "Payables", Route = "/accounting/reports/payables", Icon = "ap", RequiredPermission = Permissions.AccountingReportsView, Group = "Reports", Order = 19 },
                new ModuleNavItem { Key = "accounting.company", Title = "Company profile", Route = "/accounting/settings/company", Icon = "company", RequiredPermission = Permissions.CompanySettingsView, Group = "Configuration", Order = 20 },
                new ModuleNavItem { Key = "accounting.gst", Title = "GST settings", Route = "/accounting/settings/gst", Icon = "gst", RequiredPermission = Permissions.GstSettingsManage, Group = "Configuration", Order = 21 },
                new ModuleNavItem { Key = "accounting.fy", Title = "Financial years", Route = "/accounting/settings/financial-years", Icon = "fy", RequiredPermission = Permissions.AccountingFinancialYearManage, Group = "Configuration", Order = 22 },
                new ModuleNavItem { Key = "accounting.settings", Title = "Accounting settings", Route = "/accounting/settings", Icon = "settings", RequiredPermission = Permissions.AccountingSettingsManage, Group = "Configuration", Order = 23 },
                new ModuleNavItem { Key = "accounting.lock", Title = "Period lock", Route = "/accounting/settings/period-lock", Icon = "lock", RequiredPermission = Permissions.AccountingPeriodLockManage, Group = "Configuration", Order = 24 },
                new ModuleNavItem { Key = "accounting.ob", Title = "Opening balances", Route = "/accounting/opening-balances", Icon = "ob", RequiredPermission = Permissions.AccountingVouchersCreate, Group = "Configuration", Order = 25 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Hrms,
            Name = "HRMS",
            Description = "Employees, leave, and people operations",
            Category = ModuleCategory.People,
            BaseRoute = "/hrms",
            DashboardRoute = "/hrms",
            Icon = "hrms",
            RequiredEntitlement = ModuleKeys.Hrms,
            DisplayOrder = 40,
            IsPlaceholder = true,
            IsEnabled = false
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Projects,
            Name = "Projects",
            Description = "Project delivery: plans, tasks, kanban, timesheets, issues, risks, and change control",
            Category = ModuleCategory.ProjectsAndServices,
            BaseRoute = "/projects",
            DashboardRoute = "/projects",
            Icon = "projects",
            RequiredEntitlement = ModuleKeys.Projects,
            RequiredPermission = Permissions.ProjectsAccess,
            DisplayOrder = 50,
            IsPlaceholder = false,
            IsEnabled = true,
            Dependencies = [ModuleKeys.Crm],
            Permissions =
            [
                Permissions.ProjectsAccess, Permissions.ProjectsDashboardView,
                Permissions.ProjectsView, Permissions.ProjectsViewAll,
                Permissions.ProjectsCreate, Permissions.ProjectsUpdate,
                Permissions.ProjectsDelete, Permissions.ProjectsArchive,
                Permissions.ProjectsTasksView, Permissions.ProjectsTasksCreate,
                Permissions.ProjectsTasksUpdate, Permissions.ProjectsTasksAssign, Permissions.ProjectsTasksDelete,
                Permissions.ProjectsTimesheetsCreate, Permissions.ProjectsTimesheetsSubmit, Permissions.ProjectsTimesheetsApprove,
                Permissions.ProjectsBudgetView, Permissions.ProjectsBudgetEdit, Permissions.ProjectsFinancialsView,
                Permissions.ProjectsIssuesManage, Permissions.ProjectsRisksManage,
                Permissions.ProjectsChangeRequestsCreate, Permissions.ProjectsChangeRequestsApprove,
                Permissions.ProjectsTemplatesManage
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "projects.dashboard", Title = "Dashboard", Route = "/projects", Icon = "dashboard", RequiredPermission = Permissions.ProjectsDashboardView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "projects.list", Title = "Projects", Route = "/projects/list", Icon = "projects", RequiredPermission = Permissions.ProjectsView, Group = "Delivery", Order = 2 },
                new ModuleNavItem { Key = "projects.new", Title = "New project", Route = "/projects/new", Icon = "project-plus", RequiredPermission = Permissions.ProjectsCreate, Group = "Delivery", Order = 3 },
                new ModuleNavItem { Key = "projects.tasks", Title = "Task list", Route = "/projects/tasks", Icon = "task", RequiredPermission = Permissions.ProjectsTasksView, Group = "Execution", Order = 4 },
                new ModuleNavItem { Key = "projects.kanban", Title = "Kanban", Route = "/projects/kanban", Icon = "board", RequiredPermission = Permissions.ProjectsTasksView, Group = "Execution", Order = 5 },
                new ModuleNavItem { Key = "projects.my-work", Title = "My Work", Route = "/projects/my-work", Icon = "user-task", RequiredPermission = Permissions.ProjectsTasksView, Group = "Execution", Order = 6 },
                new ModuleNavItem { Key = "projects.timesheets", Title = "Timesheets", Route = "/projects/timesheets", Icon = "clock", RequiredPermission = Permissions.ProjectsTimesheetsCreate, Group = "Time", Order = 7 },
                new ModuleNavItem { Key = "projects.templates", Title = "Templates", Route = "/projects/templates", Icon = "template", RequiredPermission = Permissions.ProjectsTemplatesManage, Group = "Setup", Order = 8 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Settings,
            Name = "Settings",
            Description = "Company configuration, import/export, and production initialization",
            Category = ModuleCategory.Administration,
            BaseRoute = "/settings",
            DashboardRoute = "/settings/company-configuration",
            Icon = "settings",
            RequiredEntitlement = ModuleKeys.Settings,
            RequiredPermission = Permissions.SettingsCompanyConfigManage,
            DisplayOrder = 90,
            IsPlaceholder = false,
            IsEnabled = true,
            Permissions =
            [
                Permissions.SettingsCompanyConfigManage,
                Permissions.SettingsImportExport,
                Permissions.SettingsProductionInit
            ],
            NavigationItems =
            [
                new ModuleNavItem
                {
                    Key = "settings.company-configuration",
                    Title = "Company configuration",
                    Route = "/settings/company-configuration",
                    Icon = "company",
                    RequiredPermission = Permissions.SettingsCompanyConfigManage,
                    Group = "Configuration",
                    Order = 1
                },
                new ModuleNavItem
                {
                    Key = "settings.production-initialization",
                    Title = "Production initialization",
                    Route = "/settings/production-initialization",
                    Icon = "init",
                    RequiredPermission = Permissions.SettingsProductionInit,
                    Group = "Operations",
                    Order = 2
                }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Platform,
            Name = "Platform Admin",
            Description = "Tenants, module catalogue, plans, and system health",
            Category = ModuleCategory.Platform,
            BaseRoute = "/platform",
            DashboardRoute = "/platform",
            Icon = "platform",
            RequiredPermission = Permissions.PlatformAccess,
            DisplayOrder = 100,
            NavigationItems =
            [
                new ModuleNavItem { Key = "platform.dashboard", Title = "Platform dashboard", Route = "/platform", Icon = "dashboard", RequiredPermission = Permissions.PlatformAccess, Order = 1 },
                new ModuleNavItem { Key = "platform.tenants", Title = "Accounts", Route = "/platform/tenants", Icon = "building", RequiredPermission = Permissions.PlatformTenantsManage, Group = "Tenancy", Order = 2 }
            ]
        }
    ];
}
