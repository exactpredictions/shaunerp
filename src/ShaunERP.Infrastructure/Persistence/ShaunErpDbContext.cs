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

    // CRM
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Prospect> Prospects => Set<Prospect>();
    public DbSet<ShaunERP.Core.Crm.Account> CrmAccounts => Set<ShaunERP.Core.Crm.Account>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Customer> CrmCustomers => Set<Customer>();
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
            .HasIndex(x => new { x.CompanyId, x.ProductCode });

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
