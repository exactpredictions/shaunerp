namespace ShaunERP.Core.Platform;

public static class Permissions
{
    public const string UsersView = "users.users.view";
    public const string UsersCreate = "users.users.create";
    public const string UsersUpdate = "users.users.update";
    public const string UsersDelete = "users.users.delete";
    public const string RolesView = "users.roles.view";
    public const string RolesManage = "users.roles.manage";
    public const string UsersAccess = "users.access";

    public const string CrmAccess = "crm.access";
    public const string CrmDashboardView = "crm.dashboard.view";
    public const string CrmPipelineView = "crm.pipeline.view";
    public const string CrmForecastView = "crm.forecast.view";

    public const string CrmLeadsView = "crm.leads.view";
    public const string CrmLeadsCreate = "crm.leads.create";
    public const string CrmLeadsUpdate = "crm.leads.update";
    public const string CrmLeadsDelete = "crm.leads.delete";
    public const string CrmProspectsView = "crm.prospects.view";
    public const string CrmProspectsCreate = "crm.prospects.create";
    public const string CrmProspectsUpdate = "crm.prospects.update";
    public const string CrmProspectsDelete = "crm.prospects.delete";
    public const string CrmAccountsView = "crm.accounts.view";
    public const string CrmAccountsCreate = "crm.accounts.create";
    public const string CrmAccountsUpdate = "crm.accounts.update";
    public const string CrmAccountsDelete = "crm.accounts.delete";
    public const string CrmContactsView = "crm.contacts.view";
    public const string CrmContactsCreate = "crm.contacts.create";
    public const string CrmContactsUpdate = "crm.contacts.update";
    public const string CrmContactsDelete = "crm.contacts.delete";
    public const string CrmOpportunitiesView = "crm.opportunities.view";
    public const string CrmOpportunitiesCreate = "crm.opportunities.create";
    public const string CrmOpportunitiesUpdate = "crm.opportunities.update";
    public const string CrmOpportunitiesDelete = "crm.opportunities.delete";
    public const string CrmCustomersView = "crm.customers.view";
    public const string CrmCustomersCreate = "crm.customers.create";
    public const string CrmCustomersUpdate = "crm.customers.update";
    public const string CrmCustomersDelete = "crm.customers.delete";
    public const string CrmCampaignsView = "crm.campaigns.view";
    public const string CrmCampaignsCreate = "crm.campaigns.create";
    public const string CrmCampaignsUpdate = "crm.campaigns.update";
    public const string CrmCampaignsDelete = "crm.campaigns.delete";
    public const string CrmProductsView = "crm.products.view";
    public const string CrmProductsCreate = "crm.products.create";
    public const string CrmProductsUpdate = "crm.products.update";
    public const string CrmProductsDelete = "crm.products.delete";
    public const string CrmQuotesView = "crm.quotes.view";
    public const string CrmQuotesCreate = "crm.quotes.create";
    public const string CrmQuotesUpdate = "crm.quotes.update";
    public const string CrmQuotesDelete = "crm.quotes.delete";
    public const string CrmCasesView = "crm.cases.view";
    public const string CrmCasesCreate = "crm.cases.create";
    public const string CrmCasesUpdate = "crm.cases.update";
    public const string CrmCasesDelete = "crm.cases.delete";
    public const string CrmTasksView = "crm.tasks.view";
    public const string CrmTasksCreate = "crm.tasks.create";
    public const string CrmTasksUpdate = "crm.tasks.update";
    public const string CrmTasksDelete = "crm.tasks.delete";
    public const string CrmMeetingsView = "crm.meetings.view";
    public const string CrmMeetingsCreate = "crm.meetings.create";
    public const string CrmMeetingsUpdate = "crm.meetings.update";
    public const string CrmMeetingsDelete = "crm.meetings.delete";
    public const string CrmCallsView = "crm.calls.view";
    public const string CrmCallsCreate = "crm.calls.create";
    public const string CrmCallsUpdate = "crm.calls.update";
    public const string CrmCallsDelete = "crm.calls.delete";
    public const string CrmNotesView = "crm.notes.view";
    public const string CrmNotesCreate = "crm.notes.create";
    public const string CrmNotesUpdate = "crm.notes.update";
    public const string CrmNotesDelete = "crm.notes.delete";

    /// <summary>View People (Party identity) directory.</summary>
    public const string CrmPeopleView = "crm.people.view";
    public const string CrmPeopleCreate = "crm.people.create";
    public const string CrmPeopleUpdate = "crm.people.update";
    /// <summary>View and resolve ImportMatchReview / identity conflicts.</summary>
    public const string CrmIdentityReviewView = "crm.identity_review.view";
    public const string CrmIdentityReviewResolve = "crm.identity_review.resolve";

    public const string CrmCoursesView = "crm.courses.view";
    public const string CrmCoursesManage = "crm.courses.manage";
    public const string CrmEnrollmentsView = "crm.enrollments.view";
    public const string CrmEnrollmentsManage = "crm.enrollments.manage";
    public const string CrmSubscriptionsView = "crm.subscriptions.view";
    public const string CrmSubscriptionsManage = "crm.subscriptions.manage";
    public const string CrmConsultationsView = "crm.consultations.view";
    public const string CrmConsultationsManage = "crm.consultations.manage";
    public const string CrmEventsView = "crm.events.view";
    public const string CrmEventsManage = "crm.events.manage";
    public const string CrmReportsView = "crm.reports.view";
    public const string CrmReportsManage = "crm.reports.manage";

    public const string FinanceAccess = "finance.access";
    public const string FinanceDashboardView = "finance.dashboard.view";
    public const string FinanceInvoicesView = "finance.invoices.view";
    public const string FinanceInvoicesCreate = "finance.invoices.create";
    public const string FinanceInvoicesUpdate = "finance.invoices.update";
    public const string FinanceInvoicesDelete = "finance.invoices.delete";
    public const string FinanceInvoicesMarkPaid = "finance.invoices.mark_paid";
    public const string FinancePaymentsRecord = "finance.payments.record";
    public const string FinanceSettingsManage = "finance.settings.manage";
    public const string FinanceCustomersView = "finance.customers.view";

    public const string OrdersAccess = "orders.access";
    public const string OrdersDashboardView = "orders.dashboard.view";
    public const string OrdersView = "orders.view";
    public const string OrdersCreate = "orders.create";
    public const string OrdersUpdate = "orders.update";
    public const string OrdersDelete = "orders.delete";
    public const string OrdersConfirm = "orders.confirm";
    public const string OrdersFulfill = "orders.fulfill";
    public const string OrdersCancel = "orders.cancel";
    public const string OrdersCreateInvoice = "orders.create_invoice";

    public const string AccountingAccess = "accounting.access";
    public const string AccountingView = "accounting.view";
    public const string AccountingLedgersView = "accounting.ledgers.view";
    public const string AccountingLedgersCreate = "accounting.ledgers.create";
    public const string AccountingLedgersEdit = "accounting.ledgers.edit";
    public const string AccountingVouchersView = "accounting.vouchers.view";
    public const string AccountingVouchersCreate = "accounting.vouchers.create";
    public const string AccountingVouchersPost = "accounting.vouchers.post";
    public const string AccountingVouchersReverse = "accounting.vouchers.reverse";
    public const string AccountingReportsView = "accounting.reports.view";
    public const string AccountingSettingsManage = "accounting.settings.manage";
    public const string AccountingFinancialYearManage = "accounting.financialyear.manage";
    public const string AccountingPeriodLockManage = "accounting.periodlock.manage";
    public const string CompanySettingsView = "company.settings.view";
    public const string CompanySettingsEdit = "company.settings.edit";
    public const string GstSettingsManage = "gst.settings.manage";

    public const string ProjectsAccess = "projects.access";
    public const string ProjectsDashboardView = "projects.dashboard.view";
    public const string ProjectsView = "projects.view";
    /// <summary>See every project in the account, not only ones the user owns/manages/is a member of.</summary>
    public const string ProjectsViewAll = "projects.view_all";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsUpdate = "projects.update";
    public const string ProjectsDelete = "projects.delete";
    public const string ProjectsArchive = "projects.archive";
    public const string ProjectsTasksView = "projects.tasks.view";
    public const string ProjectsTasksCreate = "projects.tasks.create";
    public const string ProjectsTasksUpdate = "projects.tasks.update";
    public const string ProjectsTasksAssign = "projects.tasks.assign";
    public const string ProjectsTasksDelete = "projects.tasks.delete";
    public const string ProjectsTimesheetsCreate = "projects.timesheets.create";
    public const string ProjectsTimesheetsSubmit = "projects.timesheets.submit";
    public const string ProjectsTimesheetsApprove = "projects.timesheets.approve";
    public const string ProjectsBudgetView = "projects.budget.view";
    public const string ProjectsBudgetEdit = "projects.budget.edit";
    public const string ProjectsFinancialsView = "projects.financials.view";
    public const string ProjectsIssuesManage = "projects.issues.manage";
    public const string ProjectsRisksManage = "projects.risks.manage";
    public const string ProjectsChangeRequestsCreate = "projects.change_requests.create";
    public const string ProjectsChangeRequestsApprove = "projects.change_requests.approve";
    public const string ProjectsTemplatesManage = "projects.templates.manage";

    public const string SettingsCompanyConfigManage = "settings.company_config.manage";
    public const string SettingsImportExport = "settings.import_export";
    public const string SettingsProductionInit = "settings.production_init";

    public const string PlatformAccess = "platform.access";
    public const string PlatformTenantsManage = "platform.tenants.manage";
    public const string PlatformModulesManage = "platform.modules.manage";

    public static IReadOnlyList<string> All { get; } =
    [
        UsersAccess, UsersView, UsersCreate, UsersUpdate, UsersDelete, RolesView, RolesManage,
        CrmAccess, CrmDashboardView, CrmPipelineView, CrmForecastView,
        CrmLeadsView, CrmLeadsCreate, CrmLeadsUpdate, CrmLeadsDelete,
        CrmProspectsView, CrmProspectsCreate, CrmProspectsUpdate, CrmProspectsDelete,
        CrmAccountsView, CrmAccountsCreate, CrmAccountsUpdate, CrmAccountsDelete,
        CrmContactsView, CrmContactsCreate, CrmContactsUpdate, CrmContactsDelete,
        CrmOpportunitiesView, CrmOpportunitiesCreate, CrmOpportunitiesUpdate, CrmOpportunitiesDelete,
        CrmCustomersView, CrmCustomersCreate, CrmCustomersUpdate, CrmCustomersDelete,
        CrmCampaignsView, CrmCampaignsCreate, CrmCampaignsUpdate, CrmCampaignsDelete,
        CrmProductsView, CrmProductsCreate, CrmProductsUpdate, CrmProductsDelete,
        CrmQuotesView, CrmQuotesCreate, CrmQuotesUpdate, CrmQuotesDelete,
        CrmCasesView, CrmCasesCreate, CrmCasesUpdate, CrmCasesDelete,
        CrmTasksView, CrmTasksCreate, CrmTasksUpdate, CrmTasksDelete,
        CrmMeetingsView, CrmMeetingsCreate, CrmMeetingsUpdate, CrmMeetingsDelete,
        CrmCallsView, CrmCallsCreate, CrmCallsUpdate, CrmCallsDelete,
        CrmNotesView, CrmNotesCreate, CrmNotesUpdate, CrmNotesDelete,
        CrmPeopleView, CrmPeopleCreate, CrmPeopleUpdate,
        CrmIdentityReviewView, CrmIdentityReviewResolve,
        CrmCoursesView, CrmCoursesManage, CrmEnrollmentsView, CrmEnrollmentsManage,
        CrmSubscriptionsView, CrmSubscriptionsManage,
        CrmConsultationsView, CrmConsultationsManage,
        CrmEventsView, CrmEventsManage,
        CrmReportsView, CrmReportsManage,
        FinanceAccess, FinanceDashboardView,
        FinanceInvoicesView, FinanceInvoicesCreate, FinanceInvoicesUpdate, FinanceInvoicesDelete,
        FinanceInvoicesMarkPaid, FinancePaymentsRecord, FinanceSettingsManage, FinanceCustomersView,
        OrdersAccess, OrdersDashboardView, OrdersView, OrdersCreate, OrdersUpdate, OrdersDelete,
        OrdersConfirm, OrdersFulfill, OrdersCancel, OrdersCreateInvoice,
        AccountingAccess, AccountingView, AccountingLedgersView, AccountingLedgersCreate, AccountingLedgersEdit,
        AccountingVouchersView, AccountingVouchersCreate, AccountingVouchersPost, AccountingVouchersReverse,
        AccountingReportsView, AccountingSettingsManage, AccountingFinancialYearManage, AccountingPeriodLockManage,
        CompanySettingsView, CompanySettingsEdit, GstSettingsManage,
        ProjectsAccess, ProjectsDashboardView, ProjectsView, ProjectsViewAll,
        ProjectsCreate, ProjectsUpdate, ProjectsDelete, ProjectsArchive,
        ProjectsTasksView, ProjectsTasksCreate, ProjectsTasksUpdate, ProjectsTasksAssign, ProjectsTasksDelete,
        ProjectsTimesheetsCreate, ProjectsTimesheetsSubmit, ProjectsTimesheetsApprove,
        ProjectsBudgetView, ProjectsBudgetEdit, ProjectsFinancialsView,
        ProjectsIssuesManage, ProjectsRisksManage,
        ProjectsChangeRequestsCreate, ProjectsChangeRequestsApprove, ProjectsTemplatesManage,
        SettingsCompanyConfigManage, SettingsImportExport, SettingsProductionInit,
        PlatformAccess, PlatformTenantsManage, PlatformModulesManage
    ];

    /// <summary>Full system access — every permission granted.</summary>
    public static IReadOnlyList<string> AdminDefaults { get; } = All;

    /// <summary>Full CRM + invoicing operational access plus user directory view.</summary>
    public static IReadOnlyList<string> ManagerDefaults { get; } =
    [
        UsersAccess, UsersView, RolesView,
        CrmAccess, CrmDashboardView, CrmPipelineView, CrmForecastView,
        CrmLeadsView, CrmLeadsCreate, CrmLeadsUpdate, CrmLeadsDelete,
        CrmProspectsView, CrmProspectsCreate, CrmProspectsUpdate, CrmProspectsDelete,
        CrmAccountsView, CrmAccountsCreate, CrmAccountsUpdate, CrmAccountsDelete,
        CrmContactsView, CrmContactsCreate, CrmContactsUpdate, CrmContactsDelete,
        CrmOpportunitiesView, CrmOpportunitiesCreate, CrmOpportunitiesUpdate, CrmOpportunitiesDelete,
        CrmCustomersView, CrmCustomersCreate, CrmCustomersUpdate, CrmCustomersDelete,
        CrmCampaignsView, CrmCampaignsCreate, CrmCampaignsUpdate, CrmCampaignsDelete,
        CrmProductsView, CrmProductsCreate, CrmProductsUpdate, CrmProductsDelete,
        CrmQuotesView, CrmQuotesCreate, CrmQuotesUpdate, CrmQuotesDelete,
        CrmCasesView, CrmCasesCreate, CrmCasesUpdate, CrmCasesDelete,
        CrmTasksView, CrmTasksCreate, CrmTasksUpdate, CrmTasksDelete,
        CrmMeetingsView, CrmMeetingsCreate, CrmMeetingsUpdate, CrmMeetingsDelete,
        CrmCallsView, CrmCallsCreate, CrmCallsUpdate, CrmCallsDelete,
        CrmNotesView, CrmNotesCreate, CrmNotesUpdate, CrmNotesDelete,
        CrmPeopleView, CrmPeopleCreate, CrmPeopleUpdate,
        CrmIdentityReviewView, CrmIdentityReviewResolve,
        CrmCoursesView, CrmCoursesManage, CrmEnrollmentsView, CrmEnrollmentsManage,
        CrmSubscriptionsView, CrmSubscriptionsManage,
        CrmConsultationsView, CrmConsultationsManage,
        CrmEventsView, CrmEventsManage,
        CrmReportsView, CrmReportsManage,
        FinanceAccess, FinanceDashboardView,
        FinanceInvoicesView, FinanceInvoicesCreate, FinanceInvoicesUpdate, FinanceInvoicesDelete,
        FinanceInvoicesMarkPaid, FinancePaymentsRecord, FinanceSettingsManage, FinanceCustomersView,
        OrdersAccess, OrdersDashboardView, OrdersView, OrdersCreate, OrdersUpdate, OrdersDelete,
        OrdersConfirm, OrdersFulfill, OrdersCancel, OrdersCreateInvoice,
        AccountingAccess, AccountingView, AccountingLedgersView, AccountingLedgersCreate, AccountingLedgersEdit,
        AccountingVouchersView, AccountingVouchersCreate, AccountingVouchersPost, AccountingVouchersReverse,
        AccountingReportsView, AccountingSettingsManage, AccountingFinancialYearManage, AccountingPeriodLockManage,
        CompanySettingsView, CompanySettingsEdit, GstSettingsManage,
        ProjectsAccess, ProjectsDashboardView, ProjectsView, ProjectsViewAll,
        ProjectsCreate, ProjectsUpdate, ProjectsDelete, ProjectsArchive,
        ProjectsTasksView, ProjectsTasksCreate, ProjectsTasksUpdate, ProjectsTasksAssign, ProjectsTasksDelete,
        ProjectsTimesheetsCreate, ProjectsTimesheetsSubmit, ProjectsTimesheetsApprove,
        ProjectsBudgetView, ProjectsBudgetEdit, ProjectsFinancialsView,
        ProjectsIssuesManage, ProjectsRisksManage,
        ProjectsChangeRequestsCreate, ProjectsChangeRequestsApprove, ProjectsTemplatesManage,
        SettingsCompanyConfigManage, SettingsImportExport
    ];

    public static IReadOnlyList<string> UserDefaults { get; } =
    [
        CrmAccess, CrmDashboardView, CrmPipelineView, CrmForecastView,
        CrmLeadsView, CrmLeadsCreate, CrmLeadsUpdate,
        CrmProspectsView, CrmProspectsCreate, CrmProspectsUpdate,
        CrmAccountsView, CrmContactsView, CrmContactsCreate, CrmContactsUpdate,
        CrmOpportunitiesView, CrmOpportunitiesCreate, CrmOpportunitiesUpdate,
        CrmCustomersView, CrmCampaignsView, CrmProductsView,
        CrmQuotesView, CrmQuotesCreate, CrmQuotesUpdate,
        CrmCasesView, CrmCasesCreate, CrmCasesUpdate,
        CrmTasksView, CrmTasksCreate, CrmTasksUpdate,
        CrmMeetingsView, CrmMeetingsCreate, CrmMeetingsUpdate,
        CrmCallsView, CrmCallsCreate, CrmCallsUpdate,
        CrmNotesView, CrmNotesCreate, CrmNotesUpdate,
        CrmPeopleView,
        CrmIdentityReviewView,
        CrmCoursesView, CrmEnrollmentsView,
        CrmSubscriptionsView, CrmConsultationsView, CrmEventsView, CrmReportsView,
        FinanceAccess, FinanceDashboardView,
        FinanceInvoicesView, FinanceInvoicesCreate, FinanceInvoicesUpdate,
        FinanceInvoicesMarkPaid, FinancePaymentsRecord, FinanceCustomersView,
        OrdersAccess, OrdersDashboardView, OrdersView, OrdersCreate, OrdersUpdate,
        OrdersConfirm, OrdersCreateInvoice,
        AccountingAccess, AccountingView, AccountingLedgersView,
        AccountingVouchersView, AccountingVouchersCreate, AccountingReportsView,
        CompanySettingsView,
        ProjectsAccess, ProjectsDashboardView, ProjectsView,
        ProjectsTasksView, ProjectsTasksCreate, ProjectsTasksUpdate,
        ProjectsTimesheetsCreate, ProjectsTimesheetsSubmit
    ];
}
