using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Finance;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using CrmAccount = ShaunERP.Core.Crm.Account;
using GlAccount = ShaunERP.Core.Finance.Account;

namespace ShaunERP.Infrastructure.Services;

public class DbSeeder
{
    private readonly ShaunErpDbContext _db;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(ShaunErpDbContext db, ILogger<DbSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _db.Database.EnsureCreatedAsync(ct);
        await EnsurePlatformSchemaAsync(ct);
        await EnsureCrmSchemaAsync(ct);
        await EnsureRolesAndPermissionsAsync(ct);
        await NormalizeCompanyModulesAsync(ct);

        if (!await _db.Users.AnyAsync(ct))
        {
            _logger.LogInformation("Seeding demo admin user and sample company...");

            var admin = new User
            {
                Username = "admin",
                Email = "admin@shaunerp.local",
                FirstName = "System",
                LastName = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@12345"),
                IsActive = true,
                IsSystemAdmin = true
            };
            _db.Users.Add(admin);
            await _db.SaveChangesAsync(ct);

            var company = new Company
            {
                Name = "Demo Company",
                Domain = "demo.local",
                Description = "Seeded demo tenant"
            };
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);

            var companyUser = new CompanyUser { UserId = admin.Id, CompanyId = company.Id };
            _db.CompanyUsers.Add(companyUser);
            _db.CompanyModules.AddRange(
                Module(company.Id, ModuleKeys.Crm, "CRM"),
                Module(company.Id, ModuleKeys.Finance, "Finance"),
                Module(company.Id, ModuleKeys.Hrms, "HRMS"),
                Module(company.Id, ModuleKeys.Projects, "Projects"),
                Module(company.Id, ModuleKeys.Users, "Users"));
            await _db.SaveChangesAsync(ct);

            var adminRole = await _db.Roles.FirstAsync(r => r.Name == "Admin" && r.CompanyId == null, ct);
            _db.UserRoles.Add(new UserRole { CompanyUserId = companyUser.Id, RoleId = adminRole.Id });

            var books = new CompanyMaster
            {
                TenantCompanyId = company.Id,
                Name = "Demo Company",
                LegalName = "Demo Company LLC",
                Currency = "USD"
            };
            _db.CompanyMasters.Add(books);
            await _db.SaveChangesAsync(ct);

            _db.Accounts.AddRange(
                new GlAccount { CompanyMasterId = books.Id, Name = "Cash", AccountNumber = "1000", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = books.Id, Name = "Accounts Receivable", AccountNumber = "1100", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = books.Id, Name = "Accounts Payable", AccountNumber = "2000", Type = AccountType.Liability },
                new GlAccount { CompanyMasterId = books.Id, Name = "Sales Revenue", AccountNumber = "4000", Type = AccountType.Revenue },
                new GlAccount { CompanyMasterId = books.Id, Name = "Operating Expense", AccountNumber = "5000", Type = AccountType.Expense });

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seed complete. Login: admin / Admin@12345");
        }

        await SeedCrmDemoAsync(ct);
        await SeedCrmExtendedDemoAsync(ct);
        await AssignOrphanCrmToFirstCompanyAsync(ct);
    }

    private async Task EnsureRolesAndPermissionsAsync(CancellationToken ct)
    {
        var defaults = new (string Name, string Description)[]
        {
            ("Admin", "Full access within the company"),
            ("Manager", "Manage CRM and invoicing operations"),
            ("User", "Standard operational access")
        };

        foreach (var (name, description) in defaults)
        {
            if (!await _db.Roles.AnyAsync(r => r.Name == name && r.CompanyId == null, ct))
            {
                _db.Roles.Add(new Role { Name = name, Description = description, CompanyId = null });
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var permissionName in Permissions.All)
        {
            if (!await _db.Permissions.AnyAsync(p => p.Name == permissionName, ct))
            {
                _db.Permissions.Add(new Permission
                {
                    Name = permissionName,
                    Description = permissionName
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        await AssignRolePermissionsAsync("Admin", Permissions.AdminDefaults, ct);
        await AssignRolePermissionsAsync("Manager", Permissions.ManagerDefaults, ct);
        await AssignRolePermissionsAsync("User", Permissions.UserDefaults, ct);
    }

    private async Task AssignRolePermissionsAsync(string roleName, IReadOnlyList<string> permissionNames, CancellationToken ct)
    {
        var role = await _db.Roles.FirstAsync(r => r.Name == roleName && r.CompanyId == null, ct);
        var permissions = await _db.Permissions.Where(p => permissionNames.Contains(p.Name)).ToListAsync(ct);
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync(ct);

        foreach (var permission in permissions)
        {
            if (existing.Contains(permission.Id))
            {
                continue;
            }

            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task NormalizeCompanyModulesAsync(CancellationToken ct)
    {
        var modules = await _db.CompanyModules.ToListAsync(ct);
        foreach (var module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.ModuleKey))
            {
                module.ModuleKey = ModuleKeys.Normalize(module.ModuleName);
            }
            else
            {
                module.ModuleKey = ModuleKeys.Normalize(module.ModuleKey);
            }

            if (string.IsNullOrWhiteSpace(module.ModuleName))
            {
                module.ModuleName = module.ModuleKey;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task AssignOrphanCrmToFirstCompanyAsync(CancellationToken ct)
    {
        var companyId = await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (companyId is null)
        {
            return;
        }

        try
        {
            await _db.Leads.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Prospects.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmAccounts.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Contacts.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Opportunities.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmCustomers.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmTasks.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Meetings.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmCalls.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmNotes.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Campaigns.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmProducts.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.Quotes.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.QuoteLines.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            await _db.CrmCases.Where(x => x.CompanyId == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not assign orphan CRM rows to company {CompanyId}", companyId);
        }
    }

    private async Task SeedCrmDemoAsync(CancellationToken ct)
    {
        if (await _db.Leads.AnyAsync(ct)) return;

        _logger.LogInformation("Seeding sample CRM records...");
        var companyId = await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

        var lead = new Lead
        {
            CompanyId = companyId,
            LeadOwner = "admin",
            FirstName = "Priya",
            LastName = "Sharma",
            Title = "IT Manager",
            CompanyName = "Chennai Retail Pvt Ltd",
            Email = "priya@chennairetail.example",
            Phone = "+91 44 4000 1000",
            Mobile = "+91 98765 43210",
            LeadSource = "Website",
            Industry = "Retail",
            LeadStatus = "New",
            Rating = "Hot",
            City = "Chennai",
            Country = "India",
            Description = "Interested in ShaunERP demo."
        };
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        var prospect = new Prospect
        {
            CompanyId = companyId,
            CompanyName = "Merlin Analytics Prospects",
            FirstName = "Arun",
            LastName = "Kumar",
            Email = "arun@example.com",
            PhoneNumber = "+91 90000 11111",
            LeadId = null
        };
        _db.Prospects.Add(prospect);

        var customer = new Customer
        {
            CompanyId = companyId,
            CompanyName = "Only41 Partners",
            PrimaryContact = "Suresh",
            Email = "billing@only41.example",
            PhoneNumber = "+91 44 2222 3333",
            PaymentTerms = "Net 30",
            CreditLimit = 500000,
            TaxId = "33AAAAA0000A1Z0"
        };
        _db.CrmCustomers.Add(customer);
        await _db.SaveChangesAsync(ct);

        _db.Contacts.Add(new Contact
        {
            CompanyId = companyId,
            ContactOwner = "admin",
            FirstName = "Meera",
            LastName = "Iyer",
            Title = "CFO",
            AccountName = customer.CompanyName,
            Email = "meera@only41.example",
            Phone = "+91 44 2222 3334",
            CustomerId = customer.Id
        });

        _db.Opportunities.Add(new Opportunity
        {
            CompanyId = companyId,
            DealOwner = "admin",
            DealName = "ShaunERP Annual License",
            AccountName = "Chennai Retail Pvt Ltd",
            Type = "New Business",
            Amount = 240000,
            ClosingDate = DateTime.UtcNow.AddDays(30),
            Probability = 40,
            Pipeline = "Standard",
            Stage = "Qualification",
            LeadId = lead.Id,
            ExpectedRevenue = 96000
        });

        _db.CrmTasks.Add(new CrmTask
        {
            CompanyId = companyId,
            TaskOwner = "admin",
            Subject = "Follow up CRM demo",
            DueDate = DateTime.UtcNow.AddDays(2),
            TaskTo = "Priya Sharma",
            TaskType = "Call",
            Priority = "High",
            TaskStatus = CrmTaskStatus.NotStarted,
            Description = "Schedule product walkthrough."
        });

        _db.Meetings.Add(new Meeting
        {
            CompanyId = companyId,
            Title = "Discovery call — Chennai Retail",
            Location = "Google Meet",
            Host = "admin",
            Participants = "priya@chennairetail.example, admin",
            RelatedTo = "Lead: Priya Sharma",
            From = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
            To = DateTime.UtcNow.AddDays(1).Date.AddHours(11),
            MeetingType = "Online",
            Agenda = "Requirements and timeline"
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedCrmExtendedDemoAsync(CancellationToken ct)
    {
        var companyId = await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (companyId is null) return;

        if (!await _db.CrmAccounts.AnyAsync(ct))
        {
            _logger.LogInformation("Seeding CRM accounts, campaigns, products, quotes, cases…");
            var account = new CrmAccount
            {
                CompanyId = companyId,
                AccountOwner = "admin",
                AccountName = "Only41 Partners",
                AccountNumber = "ACC-1001",
                AccountType = "Customer",
                Industry = "Professional Services",
                Website = "https://only41.example",
                Phone = "+91 44 2222 3333",
                Email = "hello@only41.example",
                Rating = "Hot",
                Territory = "South India",
                BillingCity = "Chennai",
                BillingCountry = "India",
                PaymentTerms = "Net 30",
                CreditLimit = 500000
            };
            _db.CrmAccounts.Add(account);
            await _db.SaveChangesAsync(ct);

            _db.Campaigns.Add(new Campaign
            {
                CompanyId = companyId,
                CampaignOwner = "admin",
                Name = "Q3 ERP Awareness",
                CampaignType = "Webinar",
                CampaignStatus = "Active",
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate = DateTime.UtcNow.AddDays(45),
                BudgetedCost = 75000,
                ExpectedRevenue = 500000,
                ExpectedResponse = 120,
                NumSent = 850,
                Description = "Webinar + nurture for mid-market ERP buyers."
            });

            var product = new CrmProduct
            {
                CompanyId = companyId,
                ProductCode = "ERP-ANN",
                Name = "ShaunERP Annual License",
                ProductFamily = "Software",
                Category = "Subscription",
                UnitPrice = 240000,
                CurrencyCode = "INR",
                IsActive = true,
                UnitOfMeasure = "License",
                TaxRate = 18
            };
            _db.CrmProducts.Add(product);
            await _db.SaveChangesAsync(ct);

            var quote = new Quote
            {
                CompanyId = companyId,
                QuoteOwner = "admin",
                QuoteNumber = $"Q-{DateTime.UtcNow:yyyyMMdd}-1001",
                Subject = "ShaunERP Annual — Only41",
                QuoteStage = "Presented",
                ValidUntil = DateTime.UtcNow.AddDays(21),
                AccountName = account.AccountName,
                AccountId = account.Id,
                ContactName = "Meera Iyer",
                CurrencyCode = "INR",
                Terms = "Net 30. Includes onboarding.",
                Lines =
                [
                    new QuoteLine
                    {
                        CompanyId = companyId,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = 1,
                        UnitPrice = product.UnitPrice,
                        LineTotal = product.UnitPrice,
                        SortOrder = 1
                    }
                ]
            };
            quote.Subtotal = 240000;
            quote.Tax = 43200;
            quote.GrandTotal = 283200;
            _db.Quotes.Add(quote);

            _db.CrmCases.Add(new CrmCase
            {
                CompanyId = companyId,
                CaseOwner = "admin",
                CaseNumber = $"CAS-{DateTime.UtcNow:yyyyMMdd}-1001",
                Subject = "Need SSO configuration help",
                CaseType = "Problem",
                Priority = "High",
                CaseStatus = "New",
                Origin = "Email",
                AccountName = account.AccountName,
                AccountId = account.Id,
                ContactName = "Meera Iyer",
                Description = "Customer wants Azure AD SSO for CRM module."
            });

            _db.CrmCalls.Add(new CrmCall
            {
                CompanyId = companyId,
                CallOwner = "admin",
                Subject = "Discovery follow-up — Chennai Retail",
                CallType = "Outbound",
                CallPurpose = "Prospecting",
                CallStart = DateTime.UtcNow.AddHours(-5),
                DurationMinutes = 18,
                CallResult = "Connected",
                ContactName = "Priya Sharma",
                PhoneNumber = "+91 98765 43210",
                RelatedEntityType = CrmRelatedTypes.Lead,
                Description = "Confirmed budget cycle and next demo date."
            });

            var leadId = await _db.Leads.OrderBy(l => l.Id).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
            if (leadId is not null)
            {
                _db.CrmNotes.Add(new CrmNote
                {
                    CompanyId = companyId,
                    NoteOwner = "admin",
                    Title = "Initial interest",
                    Body = "Lead came from website pricing page. Prefers morning calls IST.",
                    IsPinned = true,
                    RelatedEntityType = CrmRelatedTypes.Lead,
                    RelatedEntityId = leadId.Value
                });
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private static CompanyModule Module(int companyId, string key, string name) => new()
    {
        CompanyId = companyId,
        ModuleKey = key,
        ModuleName = name,
        IsEnabled = true
    };

    private async Task EnsurePlatformSchemaAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "CompanyModules" ADD COLUMN IF NOT EXISTS "ModuleKey" text NOT NULL DEFAULT '';
            """, ct);
        await _db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "CompanyModules" ADD COLUMN IF NOT EXISTS "IsEnabled" boolean NOT NULL DEFAULT TRUE;
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserCompanyPreferences" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "UserId" integer NOT NULL,
                "CompanyId" integer NOT NULL,
                "LastModuleKey" text NULL,
                "FavouriteModules" text NULL,
                "RecentModules" text NULL,
                "SidebarCollapsed" boolean NOT NULL DEFAULT FALSE
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCompanyPreferences_UserId_CompanyId"
            ON "UserCompanyPreferences" ("UserId", "CompanyId");
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "CompanyId" integer NULL,
                "UserId" integer NULL,
                "Action" text NOT NULL,
                "EntityType" text NULL,
                "EntityId" text NULL,
                "BeforeValues" text NULL,
                "AfterValues" text NULL,
                "CorrelationId" text NULL,
                "IpAddress" text NULL,
                "UserAgent" text NULL,
                "OccurredAt" timestamp with time zone NOT NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Permissions_Name" ON "Permissions" ("Name");
            """, ct);
    }

    private async Task EnsureCrmSchemaAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Prospects" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "CompanyId" integer NULL,
                "CompanyName" text NULL,
                "FirstName" text NULL,
                "LastName" text NULL,
                "Email" text NULL,
                "PhoneNumber" text NULL,
                "LeadId" integer NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Customers" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "CompanyId" integer NULL,
                "CompanyName" text NULL,
                "PrimaryContact" text NULL,
                "Email" text NULL,
                "PhoneNumber" text NULL,
                "BillingAddress" text NULL,
                "PaymentTerms" text NULL,
                "CreditLimit" numeric(18,2) NULL,
                "TaxId" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Meetings" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "CompanyId" integer NULL,
                "Title" text NULL,
                "Location" text NULL,
                "AllDay" boolean NOT NULL DEFAULT FALSE,
                "From" timestamp with time zone NULL,
                "To" timestamp with time zone NULL,
                "Host" text NULL,
                "Participants" text NULL,
                "RelatedTo" text NULL,
                "Repeat" text NULL,
                "ParticipantsReminder" timestamp with time zone NULL,
                "Reminder" timestamp with time zone NULL,
                "Agenda" text NULL,
                "MeetingType" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CrmAccounts" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL,
                "AccountOwner" text NULL, "AccountName" text NOT NULL DEFAULT '',
                "AccountNumber" text NULL, "AccountType" text NULL, "Industry" text NULL,
                "Website" text NULL, "Phone" text NULL, "Fax" text NULL, "Email" text NULL,
                "ParentAccountId" integer NULL, "Ownership" text NULL, "Employees" integer NULL,
                "AnnualRevenue" numeric(18,2) NULL, "Rating" text NULL, "Territory" text NULL,
                "BillingStreet" text NULL, "BillingCity" text NULL, "BillingState" text NULL,
                "BillingZip" text NULL, "BillingCountry" text NULL,
                "ShippingStreet" text NULL, "ShippingCity" text NULL, "ShippingState" text NULL,
                "ShippingZip" text NULL, "ShippingCountry" text NULL,
                "Description" text NULL, "Tags" text NULL, "TaxId" text NULL,
                "PaymentTerms" text NULL, "CreditLimit" numeric(18,2) NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Campaigns" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "CampaignOwner" text NULL, "Name" text NOT NULL DEFAULT '',
                "CampaignType" text NULL, "CampaignStatus" text NULL,
                "StartDate" timestamp with time zone NULL, "EndDate" timestamp with time zone NULL,
                "BudgetedCost" numeric(18,2) NULL, "ActualCost" numeric(18,2) NULL,
                "ExpectedRevenue" numeric(18,2) NULL, "ExpectedResponse" integer NULL,
                "NumSent" integer NULL, "Description" text NULL, "ParentCampaign" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CrmProducts" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "ProductCode" text NOT NULL DEFAULT '',
                "Name" text NOT NULL DEFAULT '', "ProductFamily" text NULL, "Category" text NULL,
                "Description" text NULL, "UnitPrice" numeric(18,2) NOT NULL DEFAULT 0,
                "CurrencyCode" text NULL, "IsActive" boolean NOT NULL DEFAULT TRUE,
                "UnitOfMeasure" text NULL, "TaxRate" numeric(18,2) NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Quotes" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "QuoteOwner" text NULL,
                "QuoteNumber" text NOT NULL DEFAULT '', "Subject" text NOT NULL DEFAULT '',
                "QuoteStage" text NULL, "ValidUntil" timestamp with time zone NULL,
                "AccountName" text NULL, "ContactName" text NULL,
                "AccountId" integer NULL, "OpportunityId" integer NULL,
                "ContactId" integer NULL, "CustomerId" integer NULL,
                "Subtotal" numeric(18,2) NOT NULL DEFAULT 0, "Discount" numeric(18,2) NOT NULL DEFAULT 0,
                "Tax" numeric(18,2) NOT NULL DEFAULT 0, "GrandTotal" numeric(18,2) NOT NULL DEFAULT 0,
                "CurrencyCode" text NULL, "Terms" text NULL, "Description" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "QuoteLines" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "QuoteId" integer NOT NULL,
                "ProductId" integer NULL, "ProductName" text NOT NULL DEFAULT '',
                "Description" text NULL, "Quantity" numeric(18,2) NOT NULL DEFAULT 1,
                "UnitPrice" numeric(18,2) NOT NULL DEFAULT 0, "Discount" numeric(18,2) NOT NULL DEFAULT 0,
                "LineTotal" numeric(18,2) NOT NULL DEFAULT 0, "SortOrder" integer NOT NULL DEFAULT 0
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CrmCases" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "CaseOwner" text NULL,
                "CaseNumber" text NOT NULL DEFAULT '', "Subject" text NOT NULL DEFAULT '',
                "CaseType" text NULL, "Priority" text NULL, "CaseStatus" text NULL,
                "Origin" text NULL, "Reason" text NULL, "Description" text NULL,
                "Resolution" text NULL, "ClosedDate" timestamp with time zone NULL,
                "AccountName" text NULL, "ContactName" text NULL,
                "AccountId" integer NULL, "ContactId" integer NULL, "CustomerId" integer NULL,
                "Tags" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CrmCalls" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "CallOwner" text NULL,
                "Subject" text NOT NULL DEFAULT '', "CallType" text NULL, "CallPurpose" text NULL,
                "CallStart" timestamp with time zone NULL, "DurationMinutes" integer NULL,
                "CallResult" text NULL, "Description" text NULL,
                "RelatedEntityType" text NULL, "RelatedEntityId" integer NULL,
                "ContactName" text NULL, "PhoneNumber" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "CrmNotes" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL, "NoteOwner" text NULL,
                "Title" text NOT NULL DEFAULT '', "Body" text NULL,
                "IsPinned" boolean NOT NULL DEFAULT FALSE,
                "RelatedEntityType" text NOT NULL DEFAULT 'Lead',
                "RelatedEntityId" integer NOT NULL DEFAULT 0
            );
            """, ct);

        foreach (var sql in new[]
        {
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "SecondaryEmail" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Fax" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Website" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "EmailOptOut" boolean NOT NULL DEFAULT FALSE;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "NoOfEmployees" integer NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "SkypeId" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Twitter" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Street" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "State" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "ZipCode" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "PreferredContactMethod" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Gender" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Language" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Title" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "LeadOwner" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Rating" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Score" integer NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Territory" text NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "CampaignId" integer NULL;""",
            """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "ProspectOwner" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Stage" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Source" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Industry" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Territory" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "ExpectedCloseDate" timestamp with time zone NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Description" text NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "AccountId" integer NULL;""",
            """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "ContactOwner" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Title" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Department" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "DateOfBirth" timestamp with time zone NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "VendorName" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "ReportingTo" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "SecondaryEmail" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "HomePhone" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Fax" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Assistant" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "AsstPhone" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "LeadSource" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "SkypeId" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "EmailOptOut" boolean NOT NULL DEFAULT FALSE;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Twitter" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "MailingStreet" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "MailingCity" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "MailingState" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "MailingZip" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "MailingCountry" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "OtherStreet" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "OtherCity" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "OtherState" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "OtherZip" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "OtherCountry" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "ProspectId" integer NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "CustomerId" integer NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "AccountId" integer NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "PreferredContactMethod" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Gender" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Language" text NULL;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "IsPrimary" boolean NOT NULL DEFAULT FALSE;""",
            """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "DealOwner" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "Type" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "NextStep" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "LeadSource" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ContactName" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ExpectedRevenue" numeric(18,2) NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "CampaignSource" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ActualCloseDate" timestamp with time zone NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ActualRevenue" numeric(18,2) NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "WinLossReason" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "Competitor" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ProspectId" integer NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "CustomerId" integer NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "AccountId" integer NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ContactId" integer NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "CampaignId" integer NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "ForecastCategory" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "Territory" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "CurrencyCode" text NULL;""",
            """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "CustomerOwner" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "AccountNumber" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Industry" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Website" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "CustomerType" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "ShippingAddress" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Rating" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Territory" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "AccountId" integer NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Tags" text NULL;""",
            """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "Description" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "TaskTo" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "TaskType" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "Reminder" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "Repeat" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "RelatedEntityType" text NULL;""",
            """ALTER TABLE "CrmTasks" ADD COLUMN IF NOT EXISTS "RelatedEntityId" integer NULL;""",
            """ALTER TABLE "Meetings" ADD COLUMN IF NOT EXISTS "RelatedEntityType" text NULL;""",
            """ALTER TABLE "Meetings" ADD COLUMN IF NOT EXISTS "RelatedEntityId" integer NULL;"""
        })
        {
            await _db.Database.ExecuteSqlRawAsync(sql, ct);
        }
    }
}
