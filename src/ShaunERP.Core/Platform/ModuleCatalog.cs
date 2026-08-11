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
                Permissions.CrmNotesView, Permissions.CrmNotesCreate, Permissions.CrmNotesUpdate, Permissions.CrmNotesDelete
            ],
            NavigationItems =
            [
                new ModuleNavItem { Key = "crm.dashboard", Title = "CRM dashboard", Route = "/crm", Icon = "dashboard", RequiredPermission = Permissions.CrmDashboardView, Group = "Overview", Order = 1 },
                new ModuleNavItem { Key = "crm.pipeline", Title = "Pipeline board", Route = "/crm/pipeline", Icon = "pipeline", RequiredPermission = Permissions.CrmPipelineView, Group = "Overview", Order = 2 },
                new ModuleNavItem { Key = "crm.forecast", Title = "Forecast", Route = "/crm/forecast", Icon = "forecast", RequiredPermission = Permissions.CrmForecastView, Group = "Overview", Order = 3 },
                new ModuleNavItem { Key = "crm.leads", Title = "Leads", Route = "/crm/leads", Icon = "lead", RequiredPermission = Permissions.CrmLeadsView, Group = "Sales", Order = 4 },
                new ModuleNavItem { Key = "crm.prospects", Title = "Prospects", Route = "/crm/prospects", Icon = "prospect", RequiredPermission = Permissions.CrmProspectsView, Group = "Sales", Order = 5 },
                new ModuleNavItem { Key = "crm.opportunities", Title = "Opportunities", Route = "/crm/opportunities", Icon = "deal", RequiredPermission = Permissions.CrmOpportunitiesView, Group = "Sales", Order = 6 },
                new ModuleNavItem { Key = "crm.quotes", Title = "Quotes", Route = "/crm/quotes", Icon = "quote", RequiredPermission = Permissions.CrmQuotesView, Group = "Sales", Order = 7 },
                new ModuleNavItem { Key = "crm.accounts", Title = "Accounts", Route = "/crm/accounts", Icon = "account", RequiredPermission = Permissions.CrmAccountsView, Group = "Customers", Order = 8 },
                new ModuleNavItem { Key = "crm.contacts", Title = "Contacts", Route = "/crm/contacts", Icon = "contact", RequiredPermission = Permissions.CrmContactsView, Group = "Customers", Order = 9 },
                new ModuleNavItem { Key = "crm.customers", Title = "Customers", Route = "/crm/customers", Icon = "customer", RequiredPermission = Permissions.CrmCustomersView, Group = "Customers", Order = 10 },
                new ModuleNavItem { Key = "crm.campaigns", Title = "Campaigns", Route = "/crm/campaigns", Icon = "campaign", RequiredPermission = Permissions.CrmCampaignsView, Group = "Marketing", Order = 11 },
                new ModuleNavItem { Key = "crm.products", Title = "Products", Route = "/crm/products", Icon = "product", RequiredPermission = Permissions.CrmProductsView, Group = "Marketing", Order = 12 },
                new ModuleNavItem { Key = "crm.cases", Title = "Cases", Route = "/crm/cases", Icon = "case", RequiredPermission = Permissions.CrmCasesView, Group = "Service", Order = 13 },
                new ModuleNavItem { Key = "crm.tasks", Title = "Tasks", Route = "/crm/tasks", Icon = "task", RequiredPermission = Permissions.CrmTasksView, Group = "Activities", Order = 14 },
                new ModuleNavItem { Key = "crm.meetings", Title = "Meetings", Route = "/crm/meetings", Icon = "meeting", RequiredPermission = Permissions.CrmMeetingsView, Group = "Activities", Order = 15 },
                new ModuleNavItem { Key = "crm.calls", Title = "Calls", Route = "/crm/calls", Icon = "call", RequiredPermission = Permissions.CrmCallsView, Group = "Activities", Order = 16 },
                new ModuleNavItem { Key = "crm.notes", Title = "Notes", Route = "/crm/notes", Icon = "note", RequiredPermission = Permissions.CrmNotesView, Group = "Activities", Order = 17 }
            ]
        },
        new ModuleDefinition
        {
            Key = ModuleKeys.Finance,
            Name = "Invoicing",
            Description = "Customers, invoices, and payments — coming next",
            Category = ModuleCategory.Finance,
            BaseRoute = "/finance",
            DashboardRoute = "/finance",
            Icon = "finance",
            RequiredEntitlement = ModuleKeys.Finance,
            DisplayOrder = 30,
            IsPlaceholder = true,
            IsEnabled = false
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
            Description = "Projects, issues, and delivery tracking",
            Category = ModuleCategory.ProjectsAndServices,
            BaseRoute = "/projects",
            DashboardRoute = "/projects",
            Icon = "projects",
            RequiredEntitlement = ModuleKeys.Projects,
            DisplayOrder = 50,
            IsPlaceholder = true,
            IsEnabled = false
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
