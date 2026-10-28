using Microsoft.EntityFrameworkCore;
using ShaunERP.Core;
using ShaunERP.Core.Accounting;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Hrms;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Projects;
using ShaunERP.Core.Sales;

namespace ShaunERP.Infrastructure.Persistence;

public class ShaunErpDbContext : DbContext
{
    public ShaunErpDbContext(DbContextOptions<ShaunErpDbContext> options) : base(options)
    {
    }

    // Identity / tenancy
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<CompanyModule> CompanyModules => Set<CompanyModule>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<UserCompanyPreference> UserCompanyPreferences => Set<UserCompanyPreference>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Party identity
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyPerson> PartyPersons => Set<PartyPerson>();
    public DbSet<PartyPhone> PartyPhones => Set<PartyPhone>();
    public DbSet<PartyEmail> PartyEmails => Set<PartyEmail>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportSourceRow> ImportSourceRows => Set<ImportSourceRow>();
    public DbSet<ImportMatchReview> ImportMatchReviews => Set<ImportMatchReview>();

    // CRM
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Prospect> Prospects => Set<Prospect>();
    public DbSet<ShaunERP.Core.Crm.Account> CrmAccounts => Set<ShaunERP.Core.Crm.Account>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Customer> CrmCustomers => Set<Customer>();
    public DbSet<TrainingProgram> TrainingPrograms => Set<TrainingProgram>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<SubscriptionMembership> SubscriptionMemberships => Set<SubscriptionMembership>();
    public DbSet<ConsultationService> ConsultationServices => Set<ConsultationService>();
    public DbSet<ConsultationEngagement> ConsultationEngagements => Set<ConsultationEngagement>();
    public DbSet<CrmEvent> CrmEvents => Set<CrmEvent>();
    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();
    public DbSet<ReportProduct> ReportProducts => Set<ReportProduct>();
    public DbSet<ReportPurchase> ReportPurchases => Set<ReportPurchase>();
    public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<CrmCall> CrmCalls => Set<CrmCall>();
    public DbSet<CrmNote> CrmNotes => Set<CrmNote>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CrmProduct> CrmProducts => Set<CrmProduct>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<CrmCase> CrmCases => Set<CrmCase>();

    // Finance
    public DbSet<CompanyMaster> CompanyMasters => Set<CompanyMaster>();
    public DbSet<ShaunERP.Core.Finance.Account> Accounts => Set<ShaunERP.Core.Finance.Account>();
    public DbSet<FinanceCustomer> FinanceCustomers => Set<FinanceCustomer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Bill> Bills => Set<Bill>();

    // Sales orders
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();

    // Accounting
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<CompanyStatutoryRegistration> CompanyStatutoryRegistrations => Set<CompanyStatutoryRegistration>();
    public DbSet<CompanyGstSettings> CompanyGstSettings => Set<CompanyGstSettings>();
    public DbSet<GstTaxRate> GstTaxRates => Set<GstTaxRate>();
    public DbSet<CurrencyMaster> Currencies => Set<CurrencyMaster>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<FinancialYear> FinancialYears => Set<FinancialYear>();
    public DbSet<AccountingPeriodLock> AccountingPeriodLocks => Set<AccountingPeriodLock>();
    public DbSet<AccountingSettings> AccountingSettings => Set<AccountingSettings>();
    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<VoucherType> VoucherTypes => Set<VoucherType>();
    public DbSet<AccountingVoucher> AccountingVouchers => Set<AccountingVoucher>();
    public DbSet<AccountingVoucherLine> AccountingVoucherLines => Set<AccountingVoucherLine>();
    public DbSet<AccountingAllocation> AccountingAllocations => Set<AccountingAllocation>();
    public DbSet<InventoryItemCategory> InventoryItemCategories => Set<InventoryItemCategory>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    // HRMS
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectTaskCollaborator> ProjectTaskCollaborators => Set<ProjectTaskCollaborator>();
    public DbSet<ProjectTaskChecklistItem> ProjectTaskChecklistItems => Set<ProjectTaskChecklistItem>();
    public DbSet<ProjectTaskDependency> ProjectTaskDependencies => Set<ProjectTaskDependency>();
    public DbSet<ProjectRisk> ProjectRisks => Set<ProjectRisk>();
    public DbSet<ProjectChangeRequest> ProjectChangeRequests => Set<ProjectChangeRequest>();
    public DbSet<ProjectTimesheet> ProjectTimesheets => Set<ProjectTimesheet>();
    public DbSet<ProjectTimeTracker> ProjectTimeTrackers => Set<ProjectTimeTracker>();
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<ProjectAttachment> ProjectAttachments => Set<ProjectAttachment>();
    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();
    public DbSet<ProjectTemplatePhase> ProjectTemplatePhases => Set<ProjectTemplatePhase>();
    public DbSet<ProjectTemplateTask> ProjectTemplateTasks => Set<ProjectTemplateTask>();
    public DbSet<ProjectNumberSequence> ProjectNumberSequences => Set<ProjectNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<CompanyUser>()
            .HasIndex(x => new { x.UserId, x.CompanyId })
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(x => new { x.RoleId, x.PermissionId })
            .IsUnique();

        modelBuilder.Entity<UserCompanyPreference>()
            .HasIndex(x => new { x.UserId, x.CompanyId })
            .IsUnique();

        modelBuilder.Entity<CompanyModule>()
            .HasIndex(x => new { x.CompanyId, x.ModuleKey });

        modelBuilder.Entity<Customer>().ToTable("Customers");
        modelBuilder.Entity<ShaunERP.Core.Crm.Account>().ToTable("CrmAccounts");

        modelBuilder.Entity<Party>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.PartyType });
            e.HasIndex(x => new { x.CompanyId, x.DisplayName });
        });
        modelBuilder.Entity<PartyPerson>(e =>
        {
            e.HasIndex(x => x.PartyId).IsUnique();
            e.HasOne(x => x.Party).WithOne(x => x.Person).HasForeignKey<PartyPerson>(x => x.PartyId);
        });
        modelBuilder.Entity<PartyPhone>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.NormalizedPhone);
            e.HasIndex(x => x.E164Phone);
            e.HasOne(x => x.Party).WithMany(x => x.Phones).HasForeignKey(x => x.PartyId);
        });
        modelBuilder.Entity<PartyEmail>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.NormalizedEmail);
            e.HasOne(x => x.Party).WithMany(x => x.Emails).HasForeignKey(x => x.PartyId);
        });
        modelBuilder.Entity<ImportSourceRow>()
            .HasOne(x => x.ImportBatch).WithMany(x => x.Rows).HasForeignKey(x => x.ImportBatchId);

        modelBuilder.Entity<Lead>().HasIndex(x => x.PartyId);
        modelBuilder.Entity<Prospect>().HasIndex(x => x.PartyId);
        modelBuilder.Entity<Contact>().HasIndex(x => x.PartyId);
        modelBuilder.Entity<Customer>().HasIndex(x => x.PartyId);
        modelBuilder.Entity<Opportunity>().HasIndex(x => x.PartyId);

        modelBuilder.Entity<CrmEvent>().ToTable("Events");
        modelBuilder.Entity<TrainingProgram>().HasIndex(x => new { x.CompanyId, x.Name });
        modelBuilder.Entity<CourseEnrollment>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.ProgramId);
            e.HasIndex(x => new { x.CompanyId, x.PartyId, x.ProgramId }).IsUnique();
            e.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.Program).WithMany(x => x.Enrollments).HasForeignKey(x => x.ProgramId);
        });
        modelBuilder.Entity<SubscriptionPlan>().HasIndex(x => new { x.CompanyId, x.Name });
        modelBuilder.Entity<SubscriptionMembership>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.PlanId);
            e.HasIndex(x => new { x.CompanyId, x.MembershipStatus });
            e.HasIndex(x => new { x.CompanyId, x.ExternalReference });
            e.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.Plan).WithMany(x => x.Memberships).HasForeignKey(x => x.PlanId);
        });
        modelBuilder.Entity<ConsultationService>().HasIndex(x => new { x.CompanyId, x.Name });
        modelBuilder.Entity<ConsultationEngagement>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.ConsultationServiceId);
            e.HasIndex(x => new { x.CompanyId, x.EngagementStatus });
            e.HasIndex(x => new { x.CompanyId, x.ExternalReference });
            e.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.ConsultationService).WithMany(x => x.Engagements).HasForeignKey(x => x.ConsultationServiceId);
        });
        modelBuilder.Entity<CrmEvent>().HasIndex(x => new { x.CompanyId, x.Name });
        modelBuilder.Entity<EventParticipation>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => new { x.CompanyId, x.PartyId, x.EventId }).IsUnique();
            e.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.Event).WithMany(x => x.Participations).HasForeignKey(x => x.EventId);
        });
        modelBuilder.Entity<ReportProduct>().HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        modelBuilder.Entity<ReportPurchase>(e =>
        {
            e.HasIndex(x => x.PartyId);
            e.HasIndex(x => x.ReportProductId);
            e.HasIndex(x => new { x.CompanyId, x.PaymentDate });
            e.HasIndex(x => new { x.CompanyId, x.ExternalReference });
            e.HasOne(x => x.Party).WithMany().HasForeignKey(x => x.PartyId);
            e.HasOne(x => x.ReportProduct).WithMany(x => x.Purchases).HasForeignKey(x => x.ReportProductId);
        });

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => new { x.CompanyId, x.InvoiceNumber });

        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Lines)
            .WithOne(x => x.Invoice!)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasMany(x => x.Payments)
            .WithOne(x => x.Invoice!)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invoice>()
            .HasOne(x => x.FinanceCustomer)
            .WithMany()
            .HasForeignKey(x => x.FinanceCustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InvoiceLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invoice>()
            .Ignore(x => x.BalanceDue)
            .Ignore(x => x.IsOverdue)
            .Ignore(x => x.DisplayStatus);

        modelBuilder.Entity<Invoice>()
            .HasOne(x => x.SalesOrder)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SalesOrder>()
            .HasIndex(x => new { x.CompanyId, x.OrderNumber });

        modelBuilder.Entity<SalesOrder>()
            .HasMany(x => x.Lines)
            .WithOne(x => x.SalesOrder!)
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SalesOrder>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SalesOrder>()
            .HasOne(x => x.Quote)
            .WithMany()
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SalesOrder>()
            .Ignore(x => x.BalanceToInvoice)
            .Ignore(x => x.DisplayStatus);

        modelBuilder.Entity<SalesOrderLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InvoiceLine>()
            .Ignore(x => x.LineNet);

        ConfigureAccounting(modelBuilder);
        ConfigureProjects(modelBuilder);

        modelBuilder.Entity<Lead>()
            .Property(x => x.AnnualRevenue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Opportunity>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Prospect>()
            .HasOne(x => x.Lead)
            .WithMany()
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Contact>()
            .HasOne(x => x.Prospect)
            .WithMany()
            .HasForeignKey(x => x.ProspectId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Contact>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Prospect)
            .WithMany()
            .HasForeignKey(x => x.ProspectId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Lead)
            .WithMany()
            .HasForeignKey(x => x.LeadId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ShaunERP.Core.Crm.Account>()
            .HasOne(x => x.ParentAccount)
            .WithMany()
            .HasForeignKey(x => x.ParentAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Contact>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Contact)
            .WithMany()
            .HasForeignKey(x => x.ContactId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Opportunity>()
            .HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Lead>()
            .HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Prospect>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Quote>()
            .HasMany(x => x.Lines)
            .WithOne(x => x.Quote!)
            .HasForeignKey(x => x.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuoteLine>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CrmCase>()
            .HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CrmProduct>()
            .HasIndex(x => new { x.CompanyId, x.ProductCode })
            .IsUnique();

        modelBuilder.Entity<Quote>()
            .HasIndex(x => new { x.CompanyId, x.QuoteNumber });

        modelBuilder.Entity<CrmCase>()
            .HasIndex(x => new { x.CompanyId, x.CaseNumber });

        foreach (var decimalProp in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            decimalProp.SetPrecision(18);
            decimalProp.SetScale(2);
        }
    }

    private static void ConfigureAccounting(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyProfile>()
            .HasIndex(x => x.CompanyId)
            .IsUnique();

        modelBuilder.Entity<CompanyGstSettings>()
            .HasIndex(x => x.CompanyId)
            .IsUnique();

        modelBuilder.Entity<AccountingSettings>()
            .HasIndex(x => x.CompanyId)
            .IsUnique();

        modelBuilder.Entity<CurrencyMaster>()
            .HasIndex(x => new { x.CompanyId, x.CurrencyCode })
            .IsUnique();

        modelBuilder.Entity<FinancialYear>()
            .HasIndex(x => new { x.CompanyId, x.Name })
            .IsUnique();

        modelBuilder.Entity<AccountGroup>()
            .HasIndex(x => new { x.CompanyId, x.GroupCode })
            .IsUnique();

        modelBuilder.Entity<AccountGroup>()
            .HasOne(x => x.ParentGroup)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ledger>()
            .HasIndex(x => new { x.CompanyId, x.LedgerCode })
            .IsUnique();

        modelBuilder.Entity<Ledger>()
            .HasIndex(x => new { x.CompanyId, x.SystemCode });

        modelBuilder.Entity<Ledger>()
            .HasOne(x => x.AccountGroup)
            .WithMany(x => x.Ledgers)
            .HasForeignKey(x => x.AccountGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VoucherType>()
            .HasIndex(x => new { x.CompanyId, x.Code })
            .IsUnique();

        modelBuilder.Entity<AccountingVoucher>()
            .HasIndex(x => new { x.CompanyId, x.VoucherNumber })
            .IsUnique();

        modelBuilder.Entity<AccountingVoucher>()
            .HasIndex(x => new { x.CompanyId, x.SourceModule, x.SourceEntityType, x.SourceEntityId, x.PostingType });

        modelBuilder.Entity<AccountingVoucher>()
            .HasIndex(x => new { x.CompanyId, x.VoucherDate });

        modelBuilder.Entity<AccountingVoucher>()
            .HasIndex(x => new { x.CompanyId, x.FinancialYearId });

        modelBuilder.Entity<AccountingVoucher>()
            .HasMany(x => x.Lines)
            .WithOne(x => x.Voucher!)
            .HasForeignKey(x => x.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountingVoucher>()
            .HasOne(x => x.ReversalOfVoucher)
            .WithMany()
            .HasForeignKey(x => x.ReversalOfVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AccountingVoucherLine>()
            .HasIndex(x => new { x.CompanyId, x.LedgerId });

        modelBuilder.Entity<AccountingVoucherLine>()
            .HasOne(x => x.Ledger)
            .WithMany()
            .HasForeignKey(x => x.LedgerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GstTaxRate>()
            .HasIndex(x => new { x.CompanyId, x.TaxName, x.TaxType });

        modelBuilder.Entity<InventoryItem>()
            .HasIndex(x => new { x.CompanyId, x.ItemCode })
            .IsUnique();
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.Property(x => x.CompanyId).IsRequired();
            e.HasIndex(x => new { x.CompanyId, x.ProjectCode }).IsUnique();
            e.HasIndex(x => new { x.CompanyId, x.ProjectStatus });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Quote).WithMany().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SalesOrder).WithMany().HasForeignKey(x => x.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ManagerUser).WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Members).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Phases).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Milestones).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tasks).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Issues).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Risks).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ChangeRequests).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Timesheets).WithOne(x => x.Project!).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId, x.UserId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectPhase>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Milestones).WithOne(x => x.Phase!).HasForeignKey(x => x.PhaseId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Tasks).WithOne(x => x.Phase!).HasForeignKey(x => x.PhaseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectMilestone>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectTask>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasIndex(x => new { x.CompanyId, x.TaskStatus });
            e.HasIndex(x => new { x.CompanyId, x.AssigneeUserId });
            e.HasIndex(x => new { x.CompanyId, x.DueDate });
            e.HasIndex(x => new { x.CompanyId, x.TaskNumber }).IsUnique();
            e.HasOne(x => x.AssigneeUser).WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ParentTask).WithMany(x => x.Subtasks).HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.ChecklistItems).WithOne(x => x.Task!).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Collaborators).WithOne(x => x.Task!).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.DependenciesAsPredecessor).WithOne(x => x.PredecessorTask!)
                .HasForeignKey(x => x.PredecessorTaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.DependenciesAsSuccessor).WithOne(x => x.SuccessorTask!)
                .HasForeignKey(x => x.SuccessorTaskId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectTaskCollaborator>()
            .HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectTaskDependency>()
            .HasIndex(x => new { x.PredecessorTaskId, x.SuccessorTaskId })
            .IsUnique();

        modelBuilder.Entity<Issue>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasIndex(x => new { x.CompanyId, x.IssueNumber });
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectRisk>(e =>
        {
            e.Ignore(x => x.RiskScore);
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectChangeRequest>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.ProjectId });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectTimesheet>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.WorkDate });
            e.HasIndex(x => new { x.CompanyId, x.EntryStatus });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectTimeTracker>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.UserId, x.IsActive });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectComment>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId });
            e.HasOne(x => x.AuthorUser).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ParentComment).WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectAttachment>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.EntityType, x.EntityId });
            e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectTemplate>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
            e.HasMany(x => x.Phases).WithOne(x => x.Template!).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectTemplatePhase>()
            .HasMany(x => x.Tasks).WithOne(x => x.TemplatePhase!)
            .HasForeignKey(x => x.TemplatePhaseId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProjectNumberSequence>()
            .HasIndex(x => new { x.CompanyId, x.SequenceKey, x.Year })
            .IsUnique();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedDate = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
