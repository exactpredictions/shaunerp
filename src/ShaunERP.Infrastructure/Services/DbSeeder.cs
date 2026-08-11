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
        await EnsureFinanceSchemaAsync(ct);
        await EnsureSalesOrderSchemaAsync(ct);
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
                Module(company.Id, ModuleKeys.Orders, "Sales Orders"),
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
                Currency = "USD",
                InvoiceNumberPrefix = "INV-",
                NextInvoiceSequence = 1001,
                OrderNumberPrefix = "SO-",
                NextOrderSequence = 1001,
                DefaultPaymentTerms = "Net 30",
                DefaultDueDays = 30,
                Email = "billing@demo.local",
                TaxId = "US-TAX-DEMO"
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

        try
        {
            await SeedRichDemoDataAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rich CRM/Finance demo seed failed; continuing startup.");
        }
        await AssignOrphanCrmToFirstCompanyAsync(ct);
        await AssignOrphanFinanceToFirstCompanyAsync(ct);
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

        // Ensure every company has Sales Orders entitlement enabled.
        var companyIds = await _db.Companies.Select(c => c.Id).ToListAsync(ct);
        var existing = await _db.CompanyModules.ToListAsync(ct);
        foreach (var companyId in companyIds)
        {
            var hasOrders = existing.Any(m =>
                m.CompanyId == companyId &&
                m.ModuleKey.Equals(ModuleKeys.Orders, StringComparison.OrdinalIgnoreCase));
            if (!hasOrders)
            {
                _db.CompanyModules.Add(Module(companyId, ModuleKeys.Orders, "Sales Orders"));
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

    /// <summary>
    /// Idempotent rich CRM + Finance demo data for Demo Company (CompanyId 1).
    /// Uses deterministic DEMO- keys so restarts do not duplicate forever.
    /// </summary>
    private async Task SeedRichDemoDataAsync(CancellationToken ct)
    {
        var companyId = await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (companyId is null) return;

        // Serialize across web+api hosts so concurrent startup does not double-insert.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(88442201);", ct);

        const string owner = "admin";
        var today = DateTime.UtcNow.Date;
        _logger.LogInformation("Seeding rich CRM/Finance demo data for company {CompanyId}…", companyId);

        // ——— Products ———
        var productDefs = new (string Code, string Name, string Family, string Category, decimal Price, bool Active)[]
        {
            ("DEMO-ERP-ANN", "ShaunERP Annual License", "Software", "Subscription", 240000m, true),
            ("DEMO-ERP-IMPL", "Implementation & Onboarding", "Services", "Professional Services", 85000m, true),
            ("DEMO-ERP-SUP", "Premium Support Plan", "Services", "Support", 48000m, true),
            ("DEMO-ERP-ADD", "Extra User Pack (10 seats)", "Software", "Add-on", 36000m, true),
            ("DEMO-ERP-LEGACY", "Legacy Connector (retired)", "Software", "Integration", 12000m, false)
        };
        foreach (var p in productDefs)
        {
            if (await _db.CrmProducts.AnyAsync(x => x.CompanyId == companyId && x.ProductCode == p.Code, ct))
                continue;
            _db.CrmProducts.Add(new CrmProduct
            {
                CompanyId = companyId,
                ProductCode = p.Code,
                Name = p.Name,
                ProductFamily = p.Family,
                Category = p.Category,
                UnitPrice = p.Price,
                CurrencyCode = "INR",
                IsActive = p.Active,
                UnitOfMeasure = p.Family == "Software" ? "License" : "Each",
                TaxRate = 18,
                Description = $"Demo catalog item {p.Code}",
                Status = p.Active ? "Active" : "Inactive"
            });
        }
        await _db.SaveChangesAsync(ct);
        var products = await _db.CrmProducts.Where(p => p.CompanyId == companyId && p.ProductCode.StartsWith("DEMO-")).ToListAsync(ct);
        var prodAnn = products.First(p => p.ProductCode == "DEMO-ERP-ANN");
        var prodImpl = products.First(p => p.ProductCode == "DEMO-ERP-IMPL");
        var prodSup = products.First(p => p.ProductCode == "DEMO-ERP-SUP");

        // ——— Campaigns ———
        var campaignDefs = new (string Name, string Type, string Status, int StartOffset, int EndOffset, decimal Budget, decimal Expected)[]
        {
            ("DEMO: Q1 Pipeline Push", "Email", "Completed", -90, -30, 45000m, 350000m),
            ("DEMO: Mid-Market Webinar Series", "Webinar", "Active", -14, 45, 75000m, 500000m),
            ("DEMO: Partner Co-sell Kickoff", "Partner", "Planning", 10, 60, 30000m, 200000m)
        };
        foreach (var c in campaignDefs)
        {
            if (await _db.Campaigns.AnyAsync(x => x.CompanyId == companyId && x.Name == c.Name, ct))
                continue;
            _db.Campaigns.Add(new Campaign
            {
                CompanyId = companyId,
                CampaignOwner = owner,
                Name = c.Name,
                CampaignType = c.Type,
                CampaignStatus = c.Status,
                StartDate = today.AddDays(c.StartOffset),
                EndDate = today.AddDays(c.EndOffset),
                BudgetedCost = c.Budget,
                ActualCost = c.Status == "Completed" ? c.Budget * 0.92m : c.Status == "Active" ? c.Budget * 0.4m : 0,
                ExpectedRevenue = c.Expected,
                ExpectedResponse = 80,
                NumSent = c.Status == "Planning" ? 0 : 850,
                Description = "Demo marketing campaign."
            });
        }
        await _db.SaveChangesAsync(ct);
        var activeCampaign = await _db.Campaigns.FirstAsync(c => c.CompanyId == companyId && c.Name == "DEMO: Mid-Market Webinar Series", ct);

        // ——— Accounts ———
        var accountDefs = new (string Number, string Name, string Type, string Industry, string City, string Rating, string Terms, decimal Credit, string Owner)[]
        {
            ("DEMO-ACC-01", "Only41 Partners", "Customer", "Professional Services", "Chennai", "Hot", "Net 30", 500000m, owner),
            ("DEMO-ACC-02", "Harbor Logistics India", "Customer", "Logistics", "Mumbai", "Warm", "Net 45", 750000m, "Rajesh Sales"),
            ("DEMO-ACC-03", "GreenLeaf Organics", "Customer", "Agriculture", "Coimbatore", "Warm", "Net 15", 200000m, owner),
            ("DEMO-ACC-04", "NovaTech Systems", "Prospect", "Technology", "Bengaluru", "Hot", "Net 30", 400000m, "Meera Sales"),
            ("DEMO-ACC-05", "Coastal Retail Group", "Customer", "Retail", "Kochi", "Cold", "Due on Receipt", 150000m, owner),
            ("DEMO-ACC-06", "Apex Manufacturing Co", "Partner", "Manufacturing", "Pune", "Warm", "Net 60", 1000000m, "Rajesh Sales")
        };
        foreach (var a in accountDefs)
        {
            if (await _db.CrmAccounts.AnyAsync(x => x.CompanyId == companyId && (x.AccountNumber == a.Number || x.AccountName == a.Name), ct))
            {
                // Backfill deterministic number on pre-existing demo account
                var existing = await _db.CrmAccounts.FirstOrDefaultAsync(
                    x => x.CompanyId == companyId && x.AccountName == a.Name && (x.AccountNumber == null || !x.AccountNumber.StartsWith("DEMO-")), ct);
                if (existing is not null && !await _db.CrmAccounts.AnyAsync(x => x.CompanyId == companyId && x.AccountNumber == a.Number, ct))
                    existing.AccountNumber = a.Number;
                continue;
            }
            _db.CrmAccounts.Add(new CrmAccount
            {
                CompanyId = companyId,
                AccountOwner = a.Owner,
                AccountName = a.Name,
                AccountNumber = a.Number,
                AccountType = a.Type,
                Industry = a.Industry,
                Website = $"https://{a.Number.ToLowerInvariant()}.example",
                Phone = "+91 44 2000 1000",
                Email = $"hello@{a.Number.ToLowerInvariant()}.example",
                Rating = a.Rating,
                Territory = "South India",
                BillingCity = a.City,
                BillingState = "TN",
                BillingCountry = "India",
                PaymentTerms = a.Terms,
                CreditLimit = a.Credit,
                Employees = 50 + (a.Number.GetHashCode() % 200),
                AnnualRevenue = a.Credit * 12,
                Tags = "demo-seed"
            });
        }
        await _db.SaveChangesAsync(ct);
        var accounts = await _db.CrmAccounts.Where(a => a.CompanyId == companyId && a.AccountNumber!.StartsWith("DEMO-ACC-")).ToListAsync(ct);

        // ——— Customers (10) ———
        var customerDefs = new (string Number, string Name, string Contact, string Email, string Phone, string Industry, string Terms, decimal Credit, string Owner, string Status, string? Addr)[]
        {
            ("CUST-DEMO-01", "Only41 Partners", "Suresh Nair", "billing@only41.example", "+91 44 2222 3333", "Professional Services", "Net 30", 500000m, owner, "Active", "12 Anna Salai, Chennai 600002"),
            ("CUST-DEMO-02", "Harbor Logistics India", "Anita Desai", "ap@harborlog.example", "+91 22 4000 2200", "Logistics", "Net 45", 750000m, "Rajesh Sales", "Active", "Andheri East, Mumbai 400069"),
            ("CUST-DEMO-03", "GreenLeaf Organics", "Karthik V", "finance@greenleaf.example", "+91 422 300 4500", "Agriculture", "Net 15", 200000m, owner, "Active", "RS Puram, Coimbatore 641002"),
            ("CUST-DEMO-04", "Coastal Retail Group", "Deepa Menon", "accounts@coastalretail.example", "+91 484 220 1100", "Retail", "Due on Receipt", 150000m, owner, "Active", "MG Road, Kochi 682011"),
            ("CUST-DEMO-05", "Apex Manufacturing Co", "Vikram Shah", "billing@apexmfg.example", "+91 20 6600 8800", "Manufacturing", "Net 60", 1000000m, "Rajesh Sales", "Active", "Hinjewadi, Pune 411057"),
            ("CUST-DEMO-06", "BrightPath Education", "Lakshmi Rao", "billing@brightpath.example", "+91 80 4111 2200", "Education", "Net 30", 300000m, "Meera Sales", "Active", "Indiranagar, Bengaluru 560038"),
            ("CUST-DEMO-07", "MediCare Clinics Network", "Dr. Arjun Patel", "finance@medicare.example", "+91 79 2650 3300", "Healthcare", "Net 30", 450000m, owner, "Active", "Navrangpura, Ahmedabad 380009"),
            ("CUST-DEMO-08", "Skyline Construction Ltd", "Ramesh Iyer", "ap@skyline.example", "+91 44 2811 9900", "Construction", "Net 45", 600000m, "Rajesh Sales", "Active", "Guindy, Chennai 600032"),
            ("CUST-DEMO-09", "PixelCraft Studios", "Nisha Banerjee", "hello@pixelcraft.example", "+91 33 4007 1200", "Media", "Net 15", 120000m, "Meera Sales", "Inactive", "Salt Lake, Kolkata 700091"),
            ("CUST-DEMO-10", "Northwind Traders IN", "Thomas Fernandes", "billing@northwind.example", "+91 832 245 6700", "Wholesale", "Net 30", 280000m, owner, "Active", "Panaji, Goa 403001")
        };
        foreach (var c in customerDefs)
        {
            if (await _db.CrmCustomers.AnyAsync(x => x.CompanyId == companyId && x.AccountNumber == c.Number, ct))
                continue;
            var linkedAccount = accounts.FirstOrDefault(a =>
                string.Equals(a.AccountName, c.Name, StringComparison.OrdinalIgnoreCase));
            _db.CrmCustomers.Add(new Customer
            {
                CompanyId = companyId,
                CompanyName = c.Name,
                PrimaryContact = c.Contact,
                Email = c.Email,
                PhoneNumber = c.Phone,
                BillingAddress = c.Addr,
                ShippingAddress = c.Addr,
                PaymentTerms = c.Terms,
                CreditLimit = c.Credit,
                TaxId = $"33AAAAA{c.Number[^2..]}Z5",
                CustomerOwner = c.Owner,
                AccountNumber = c.Number,
                Industry = c.Industry,
                Website = $"https://{c.Number.ToLowerInvariant()}.example",
                CustomerType = "Business",
                Rating = c.Status == "Inactive" ? "Cold" : "Warm",
                Territory = "India",
                AccountId = linkedAccount?.Id,
                Tags = "demo-seed",
                Description = "Demo CRM customer for invoicing and AR screens.",
                Status = c.Status
            });
        }
        await _db.SaveChangesAsync(ct);
        var customers = await _db.CrmCustomers.Where(c => c.CompanyId == companyId && c.AccountNumber!.StartsWith("CUST-DEMO-")).ToListAsync(ct);
        Customer Cust(string n) => customers.First(c => c.AccountNumber == n);

        // ——— Leads ———
        var leadDefs = new (string Email, string First, string Last, string Company, string Status, string Source, string Industry, string Rating, bool Qualified, string City, string Owner)[]
        {
            ("demo.lead.new@chennairetail.example", "Priya", "Sharma", "Chennai Retail Pvt Ltd", "New", "Website", "Retail", "Hot", false, "Chennai", owner),
            ("demo.lead.qualified@novatech.example", "Rahul", "Mehta", "NovaTech Systems", "Qualified", "Webinar", "Technology", "Hot", true, "Bengaluru", "Meera Sales"),
            ("demo.lead.converted@brightpath.example", "Sneha", "Krishnan", "BrightPath Education", "Converted", "Referral", "Education", "Warm", true, "Bengaluru", owner),
            ("demo.lead.lost@oldco.example", "Imran", "Khan", "OldCo Textiles", "Lost", "Cold Call", "Manufacturing", "Cold", false, "Tirupur", "Rajesh Sales"),
            ("demo.lead.contacted@foodhub.example", "Aisha", "Begum", "FoodHub Cloud Kitchens", "Contacted", "Trade Show", "Hospitality", "Warm", false, "Hyderabad", owner),
            ("demo.lead.nurture@fintech.example", "Daniel", "Joseph", "PayWave Fintech", "Nurture", "Partner", "Financial Services", "Hot", false, "Mumbai", "Meera Sales")
        };
        foreach (var l in leadDefs)
        {
            if (await _db.Leads.AnyAsync(x => x.CompanyId == companyId && x.Email == l.Email, ct))
                continue;
            _db.Leads.Add(new Lead
            {
                CompanyId = companyId,
                LeadOwner = l.Owner,
                FirstName = l.First,
                LastName = l.Last,
                Title = "Decision Maker",
                CompanyName = l.Company,
                Email = l.Email,
                Phone = "+91 44 4000 1000",
                Mobile = "+91 98765 40000",
                LeadSource = l.Source,
                Industry = l.Industry,
                LeadStatus = l.Status,
                Rating = l.Rating,
                IsQualified = l.Qualified,
                City = l.City,
                State = "India",
                Country = "India",
                AnnualRevenue = 2500000,
                NoOfEmployees = 120,
                Score = l.Status == "Lost" ? 15 : l.Qualified ? 78 : 45,
                CampaignId = activeCampaign.Id,
                Territory = "South India",
                Tags = "demo-seed",
                Description = $"Demo lead — {l.Status}."
            });
        }
        await _db.SaveChangesAsync(ct);
        var leads = await _db.Leads.Where(l => l.CompanyId == companyId && l.Email != null && l.Email.StartsWith("demo.lead.")).ToListAsync(ct);

        // ——— Prospects ———
        var prospectDefs = new (string Email, string First, string Last, string Company, string Stage, string Source, string Industry, string? LeadEmail)[]
        {
            ("demo.prospect.arun@merlin.example", "Arun", "Kumar", "Merlin Analytics", "Engaged", "Website", "Technology", null),
            ("demo.prospect.rahul@novatech.example", "Rahul", "Mehta", "NovaTech Systems", "Proposal", "Webinar", "Technology", "demo.lead.qualified@novatech.example"),
            ("demo.prospect.aisha@foodhub.example", "Aisha", "Begum", "FoodHub Cloud Kitchens", "Discovery", "Trade Show", "Hospitality", "demo.lead.contacted@foodhub.example"),
            ("demo.prospect.daniel@paywave.example", "Daniel", "Joseph", "PayWave Fintech", "Negotiation", "Partner", "Financial Services", "demo.lead.nurture@fintech.example")
        };
        foreach (var p in prospectDefs)
        {
            if (await _db.Prospects.AnyAsync(x => x.CompanyId == companyId && x.Email == p.Email, ct))
                continue;
            int? leadId = null;
            if (p.LeadEmail is not null)
            {
                var lead = leads.FirstOrDefault(l => l.Email == p.LeadEmail);
                leadId = lead?.Id;
            }
            _db.Prospects.Add(new Prospect
            {
                CompanyId = companyId,
                CompanyName = p.Company,
                FirstName = p.First,
                LastName = p.Last,
                Email = p.Email,
                PhoneNumber = "+91 90000 20000",
                LeadId = leadId,
                ProspectOwner = owner,
                Stage = p.Stage,
                Source = p.Source,
                Industry = p.Industry,
                Territory = "India",
                ExpectedCloseDate = today.AddDays(45),
                AccountId = accounts.FirstOrDefault(a => a.AccountName == p.Company)?.Id,
                Tags = "demo-seed",
                Description = $"Demo prospect — {p.Stage}."
            });
        }
        await _db.SaveChangesAsync(ct);
        var prospects = await _db.Prospects.Where(p => p.CompanyId == companyId && p.Email != null && p.Email.StartsWith("demo.prospect.")).ToListAsync(ct);

        // ——— Contacts ———
        var contactDefs = new (string Email, string First, string Last, string Title, string AccountName, string? CustNo, string Owner, bool Primary)[]
        {
            ("meera@only41.example", "Meera", "Iyer", "CFO", "Only41 Partners", "CUST-DEMO-01", owner, true),
            ("suresh@only41.example", "Suresh", "Nair", "CEO", "Only41 Partners", "CUST-DEMO-01", owner, false),
            ("anita@harborlog.example", "Anita", "Desai", "Finance Head", "Harbor Logistics India", "CUST-DEMO-02", "Rajesh Sales", true),
            ("karthik@greenleaf.example", "Karthik", "Venkat", "Operations", "GreenLeaf Organics", "CUST-DEMO-03", owner, true),
            ("rahul.c@novatech.example", "Rahul", "Mehta", "CTO", "NovaTech Systems", null, "Meera Sales", true),
            ("deepa@coastalretail.example", "Deepa", "Menon", "Procurement", "Coastal Retail Group", "CUST-DEMO-04", owner, true),
            ("vikram@apexmfg.example", "Vikram", "Shah", "Plant Director", "Apex Manufacturing Co", "CUST-DEMO-05", "Rajesh Sales", true),
            ("lakshmi@brightpath.example", "Lakshmi", "Rao", "Registrar", "BrightPath Education", "CUST-DEMO-06", "Meera Sales", true)
        };
        foreach (var c in contactDefs)
        {
            if (await _db.Contacts.AnyAsync(x => x.CompanyId == companyId && x.Email == c.Email, ct))
                continue;
            var acct = accounts.FirstOrDefault(a => a.AccountName == c.AccountName);
            var cust = c.CustNo is null ? null : customers.FirstOrDefault(x => x.AccountNumber == c.CustNo);
            _db.Contacts.Add(new Contact
            {
                CompanyId = companyId,
                ContactOwner = c.Owner,
                FirstName = c.First,
                LastName = c.Last,
                Title = c.Title,
                Department = "Finance",
                AccountName = c.AccountName,
                Email = c.Email,
                Phone = "+91 44 2222 3000",
                Mobile = "+91 98800 11000",
                CustomerId = cust?.Id,
                AccountId = acct?.Id,
                IsPrimary = c.Primary,
                MailingCity = acct?.BillingCity,
                MailingCountry = "India",
                LeadSource = "Demo",
                Tags = "demo-seed",
                PreferredContactMethod = "Email"
            });
        }
        await _db.SaveChangesAsync(ct);
        var contacts = await _db.Contacts.Where(c => c.CompanyId == companyId && c.Tags == "demo-seed").ToListAsync(ct);

        // ——— Opportunities ———
        var oppDefs = new (string Name, string Account, string Stage, double Prob, decimal Amount, int CloseDays, string Type, string Forecast, string? LeadEmail, string? CustNo)[]
        {
            ("DEMO: ShaunERP Annual — Chennai Retail", "Chennai Retail Pvt Ltd", "Qualification", 25, 240000m, 30, "New Business", "Pipeline", "demo.lead.new@chennairetail.example", null),
            ("DEMO: NovaTech Full Suite", "NovaTech Systems", "Proposal", 55, 420000m, 21, "New Business", "Best Case", "demo.lead.qualified@novatech.example", null),
            ("DEMO: Harbor Logistics Expansion", "Harbor Logistics India", "Negotiation", 75, 310000m, 14, "Existing Business", "Commit", null, "CUST-DEMO-02"),
            ("DEMO: Apex Manufacturing Rollout", "Apex Manufacturing Co", "Closed Won", 100, 580000m, -10, "New Business", "Closed", null, "CUST-DEMO-05"),
            ("DEMO: OldCo Textiles — lost", "OldCo Textiles", "Closed Lost", 0, 180000m, -20, "New Business", "Omitted", "demo.lead.lost@oldco.example", null),
            ("DEMO: PayWave Multi-entity", "PayWave Fintech", "Needs Analysis", 40, 650000m, 45, "New Business", "Pipeline", "demo.lead.nurture@fintech.example", null)
        };
        foreach (var o in oppDefs)
        {
            if (await _db.Opportunities.AnyAsync(x => x.CompanyId == companyId && x.DealName == o.Name, ct))
                continue;
            var acct = accounts.FirstOrDefault(a => a.AccountName == o.Account);
            var lead = o.LeadEmail is null ? null : leads.FirstOrDefault(l => l.Email == o.LeadEmail);
            var cust = o.CustNo is null ? null : customers.FirstOrDefault(c => c.AccountNumber == o.CustNo);
            var contact = contacts.FirstOrDefault(c => c.AccountName == o.Account);
            _db.Opportunities.Add(new Opportunity
            {
                CompanyId = companyId,
                DealOwner = owner,
                DealName = o.Name,
                AccountName = o.Account,
                Type = o.Type,
                Amount = o.Amount,
                ClosingDate = today.AddDays(o.CloseDays),
                Probability = o.Prob,
                ExpectedRevenue = Math.Round(o.Amount * (decimal)(o.Prob / 100.0), 2),
                Pipeline = "Standard",
                Stage = o.Stage,
                LeadId = lead?.Id,
                CustomerId = cust?.Id,
                AccountId = acct?.Id,
                ContactId = contact?.Id,
                ContactName = contact is null ? null : $"{contact.FirstName} {contact.LastName}",
                CampaignId = activeCampaign.Id,
                CampaignSource = activeCampaign.Name,
                ForecastCategory = o.Forecast,
                CurrencyCode = "INR",
                Territory = "India",
                Tags = "demo-seed",
                ActualCloseDate = o.Stage.StartsWith("Closed") ? today.AddDays(o.CloseDays) : null,
                ActualRevenue = o.Stage == "Closed Won" ? o.Amount : null,
                WinLossReason = o.Stage == "Closed Lost" ? "Budget freeze" : o.Stage == "Closed Won" ? "Best fit / references" : null,
                NextStep = o.Stage.StartsWith("Closed") ? null : "Schedule stakeholder demo",
                Description = $"Demo opportunity — {o.Stage}."
            });
        }
        await _db.SaveChangesAsync(ct);
        var opps = await _db.Opportunities.Where(o => o.CompanyId == companyId && o.DealName.StartsWith("DEMO:")).ToListAsync(ct);

        // ——— Quotes ———
        var quoteDefs = new (string Number, string Subject, string Stage, string AccountName, string? CustNo, decimal Subtotal)[]
        {
            ("DEMO-Q-1001", "DEMO Draft — Coastal Retail starter", "Draft", "Coastal Retail Group", "CUST-DEMO-04", 120000m),
            ("DEMO-Q-1002", "DEMO Sent — Harbor Logistics expansion", "Sent", "Harbor Logistics India", "CUST-DEMO-02", 310000m),
            ("DEMO-Q-1003", "DEMO Accepted — Apex Manufacturing", "Accepted", "Apex Manufacturing Co", "CUST-DEMO-05", 580000m),
            ("DEMO-Q-1004", "DEMO Presented — NovaTech suite", "Presented", "NovaTech Systems", null, 420000m)
        };
        foreach (var q in quoteDefs)
        {
            if (await _db.Quotes.AnyAsync(x => x.CompanyId == companyId && x.QuoteNumber == q.Number, ct))
                continue;
            var acct = accounts.FirstOrDefault(a => a.AccountName == q.AccountName);
            var cust = q.CustNo is null ? null : customers.FirstOrDefault(c => c.AccountNumber == q.CustNo);
            var contact = contacts.FirstOrDefault(c => c.AccountName == q.AccountName);
            var opp = opps.FirstOrDefault(o => o.AccountName == q.AccountName);
            var tax = Math.Round(q.Subtotal * 0.18m, 2);
            _db.Quotes.Add(new Quote
            {
                CompanyId = companyId,
                QuoteOwner = owner,
                QuoteNumber = q.Number,
                Subject = q.Subject,
                QuoteStage = q.Stage,
                ValidUntil = today.AddDays(21),
                AccountName = q.AccountName,
                AccountId = acct?.Id,
                ContactName = contact is null ? null : $"{contact.FirstName} {contact.LastName}",
                ContactId = contact?.Id,
                CustomerId = cust?.Id,
                OpportunityId = opp?.Id,
                CurrencyCode = "INR",
                Terms = "Net 30. Includes standard onboarding.",
                Subtotal = q.Subtotal,
                Tax = tax,
                GrandTotal = q.Subtotal + tax,
                Description = "Demo quote.",
                Lines =
                [
                    new QuoteLine
                    {
                        CompanyId = companyId,
                        ProductId = prodAnn.Id,
                        ProductName = prodAnn.Name,
                        Quantity = 1,
                        UnitPrice = Math.Min(prodAnn.UnitPrice, q.Subtotal),
                        LineTotal = Math.Min(prodAnn.UnitPrice, q.Subtotal),
                        SortOrder = 1
                    }
                ]
            });
        }
        await _db.SaveChangesAsync(ct);

        // ——— Cases ———
        var caseDefs = new (string Number, string Subject, string Type, string Priority, string Status, string AccountName, string? CustNo, bool Closed)[]
        {
            ("DEMO-CAS-1001", "Need SSO configuration help", "Problem", "High", "New", "Only41 Partners", "CUST-DEMO-01", false),
            ("DEMO-CAS-1002", "Invoice PDF branding request", "Request", "Medium", "In Progress", "Harbor Logistics India", "CUST-DEMO-02", false),
            ("DEMO-CAS-1003", "Login MFA reset", "Question", "Low", "Closed", "GreenLeaf Organics", "CUST-DEMO-03", true),
            ("DEMO-CAS-1004", "Module access for finance team", "Problem", "Critical", "Escalated", "Apex Manufacturing Co", "CUST-DEMO-05", false),
            ("DEMO-CAS-1005", "Report export timeout", "Problem", "Medium", "Closed", "Coastal Retail Group", "CUST-DEMO-04", true)
        };
        foreach (var c in caseDefs)
        {
            if (await _db.CrmCases.AnyAsync(x => x.CompanyId == companyId && x.CaseNumber == c.Number, ct))
                continue;
            var acct = accounts.FirstOrDefault(a => a.AccountName == c.AccountName);
            var cust = c.CustNo is null ? null : customers.FirstOrDefault(x => x.AccountNumber == c.CustNo);
            var contact = contacts.FirstOrDefault(x => x.AccountName == c.AccountName);
            _db.CrmCases.Add(new CrmCase
            {
                CompanyId = companyId,
                CaseOwner = owner,
                CaseNumber = c.Number,
                Subject = c.Subject,
                CaseType = c.Type,
                Priority = c.Priority,
                CaseStatus = c.Status,
                Origin = "Email",
                AccountName = c.AccountName,
                AccountId = acct?.Id,
                ContactName = contact is null ? null : $"{contact.FirstName} {contact.LastName}",
                ContactId = contact?.Id,
                CustomerId = cust?.Id,
                Description = $"Demo case — {c.Status} / {c.Priority}.",
                Resolution = c.Closed ? "Resolved via knowledge base + screen share." : null,
                ClosedDate = c.Closed ? today.AddDays(-3) : null,
                Tags = "demo-seed"
            });
        }
        await _db.SaveChangesAsync(ct);

        // ——— Tasks ———
        var taskDefs = new (string Subject, CrmTaskStatus Status, string Priority, int DueDays, string Type, string To)[]
        {
            ("DEMO: Follow up CRM demo", CrmTaskStatus.NotStarted, "High", 2, "Call", "Priya Sharma"),
            ("DEMO: Send Harbor proposal deck", CrmTaskStatus.InProgress, "Normal", 1, "Email", "Anita Desai"),
            ("DEMO: Close Apex onboarding checklist", CrmTaskStatus.Completed, "Normal", -5, "To-do", "Vikram Shah"),
            ("DEMO: Revisit OldCo in Q4", CrmTaskStatus.Deferred, "Low", 60, "Follow-up", "Imran Khan")
        };
        foreach (var t in taskDefs)
        {
            if (await _db.CrmTasks.AnyAsync(x => x.CompanyId == companyId && x.Subject == t.Subject, ct))
                continue;
            _db.CrmTasks.Add(new CrmTask
            {
                CompanyId = companyId,
                TaskOwner = owner,
                Subject = t.Subject,
                DueDate = today.AddDays(t.DueDays),
                TaskTo = t.To,
                TaskType = t.Type,
                Priority = t.Priority,
                TaskStatus = t.Status,
                Description = "Demo CRM task.",
                RelatedEntityType = CrmRelatedTypes.Lead,
                RelatedEntityId = leads.FirstOrDefault()?.Id
            });
        }

        // ——— Meetings ———
        var meetingDefs = new (string Title, int DayOffset, string Location, string Type, string Related)[]
        {
            ("DEMO: Discovery — Chennai Retail", 1, "Google Meet", "Online", "Lead: Priya Sharma"),
            ("DEMO: Negotiation — Harbor Logistics", 3, "Mumbai office", "In-person", "Account: Harbor Logistics India"),
            ("DEMO: QBReview — Apex Manufacturing", -2, "Zoom", "Online", "Customer: Apex Manufacturing Co")
        };
        foreach (var m in meetingDefs)
        {
            if (await _db.Meetings.AnyAsync(x => x.CompanyId == companyId && x.Title == m.Title, ct))
                continue;
            var from = today.AddDays(m.DayOffset).AddHours(10);
            _db.Meetings.Add(new Meeting
            {
                CompanyId = companyId,
                Title = m.Title,
                Location = m.Location,
                Host = owner,
                Participants = $"{owner}, stakeholder@example.com",
                RelatedTo = m.Related,
                From = from,
                To = from.AddHours(1),
                MeetingType = m.Type,
                Agenda = "Demo agenda: scope, timeline, commercials."
            });
        }

        // ——— Calls ———
        var callDefs = new (string Subject, string Type, string Purpose, string Result, string Contact, int HoursAgo)[]
        {
            ("DEMO: Discovery follow-up — Chennai Retail", "Outbound", "Prospecting", "Connected", "Priya Sharma", 5),
            ("DEMO: Support callback — Only41 SSO", "Inbound", "Support", "Connected", "Meera Iyer", 28),
            ("DEMO: No-answer — FoodHub", "Outbound", "Follow-up", "No Answer", "Aisha Begum", 50)
        };
        foreach (var c in callDefs)
        {
            if (await _db.CrmCalls.AnyAsync(x => x.CompanyId == companyId && x.Subject == c.Subject, ct))
                continue;
            _db.CrmCalls.Add(new CrmCall
            {
                CompanyId = companyId,
                CallOwner = owner,
                Subject = c.Subject,
                CallType = c.Type,
                CallPurpose = c.Purpose,
                CallStart = DateTime.UtcNow.AddHours(-c.HoursAgo),
                DurationMinutes = c.Result == "No Answer" ? 0 : 18,
                CallResult = c.Result,
                ContactName = c.Contact,
                PhoneNumber = "+91 98765 43210",
                RelatedEntityType = CrmRelatedTypes.Lead,
                RelatedEntityId = leads.FirstOrDefault()?.Id,
                Description = "Demo call log."
            });
        }

        // ——— Notes ———
        var noteDefs = new (string Title, string Body, string RelType, bool Pinned)[]
        {
            ("DEMO: Initial interest", "Lead came from website pricing page. Prefers morning calls IST.", CrmRelatedTypes.Lead, true),
            ("DEMO: Budget note", "Customer confirmed FY budget includes ERP replacement.", CrmRelatedTypes.Customer, false),
            ("DEMO: Case triage", "Escalated to product for MFA edge case on mobile.", CrmRelatedTypes.Case, false),
            ("DEMO: Campaign insight", "Webinar CTR strong from logistics vertical.", CrmRelatedTypes.Campaign, false)
        };
        foreach (var n in noteDefs)
        {
            if (await _db.CrmNotes.AnyAsync(x => x.CompanyId == companyId && x.Title == n.Title, ct))
                continue;
            int relatedId = n.RelType switch
            {
                CrmRelatedTypes.Lead => leads.First().Id,
                CrmRelatedTypes.Customer => customers.First().Id,
                CrmRelatedTypes.Case => (await _db.CrmCases.Where(x => x.CompanyId == companyId && x.CaseNumber!.StartsWith("DEMO-")).Select(x => x.Id).FirstAsync(ct)),
                CrmRelatedTypes.Campaign => activeCampaign.Id,
                _ => leads.First().Id
            };
            _db.CrmNotes.Add(new CrmNote
            {
                CompanyId = companyId,
                NoteOwner = owner,
                Title = n.Title,
                Body = n.Body,
                IsPinned = n.Pinned,
                RelatedEntityType = n.RelType,
                RelatedEntityId = relatedId
            });
        }
        await _db.SaveChangesAsync(ct);

        // ——— Finance book + invoices ———
        var book = await _db.CompanyMasters.FirstOrDefaultAsync(b => b.TenantCompanyId == companyId, ct);
        if (book is null)
        {
            book = new CompanyMaster
            {
                TenantCompanyId = companyId,
                Name = "Demo Company",
                LegalName = "Demo Company LLC",
                Currency = "INR",
                InvoiceNumberPrefix = "INV-",
                NextInvoiceSequence = 2001,
                DefaultPaymentTerms = "Net 30",
                DefaultDueDays = 30,
                Email = "billing@demo.local",
                TaxId = "US-TAX-DEMO",
                BillingCity = "Chennai",
                BillingCountry = "India"
            };
            _db.CompanyMasters.Add(book);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(book.InvoiceNumberPrefix)) book.InvoiceNumberPrefix = "INV-";
            if (book.NextInvoiceSequence < 2001) book.NextInvoiceSequence = 2001;
            if (book.DefaultDueDays <= 0) book.DefaultDueDays = 30;
            await _db.SaveChangesAsync(ct);
        }

        // Ensure GL stubs exist
        if (!await _db.Accounts.AnyAsync(a => a.CompanyMasterId == book.Id, ct))
        {
            _db.Accounts.AddRange(
                new GlAccount { CompanyMasterId = book.Id, Name = "Cash", AccountNumber = "1000", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = book.Id, Name = "Accounts Receivable", AccountNumber = "1100", Type = AccountType.Asset },
                new GlAccount { CompanyMasterId = book.Id, Name = "Accounts Payable", AccountNumber = "2000", Type = AccountType.Liability },
                new GlAccount { CompanyMasterId = book.Id, Name = "Sales Revenue", AccountNumber = "4000", Type = AccountType.Revenue },
                new GlAccount { CompanyMasterId = book.Id, Name = "Operating Expense", AccountNumber = "5000", Type = AccountType.Expense });
            await _db.SaveChangesAsync(ct);
        }

        async Task<Invoice?> EnsureDemoInvoiceAsync(
            string number,
            string custNo,
            InvoiceStatus status,
            DateTime invoiceDate,
            DateTime dueDate,
            decimal unitPrice,
            decimal? payAmount = null,
            string? payRef = null)
        {
            var existing = await _db.Invoices.Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.CompanyMasterId == book.Id && i.InvoiceNumber == number, ct);
            if (existing is not null) return existing;

            var cust = Cust(custNo);
            var qty = 1m;
            var taxRate = 18m;
            var net = InvoiceLine.LineNetAmount(qty, unitPrice, 0);
            var taxAmt = Math.Round(net * (taxRate / 100m), 2);
            var lineTotal = Math.Round(net + taxAmt, 2);

            var inv = new Invoice
            {
                CompanyId = companyId,
                CompanyMasterId = book.Id,
                CustomerId = cust.Id,
                CustomerName = cust.CompanyName,
                InvoiceNumber = number,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                InvoiceStatus = status,
                CurrencyCode = book.Currency is "USD" or "INR" ? "INR" : book.Currency,
                PaymentTerms = cust.PaymentTerms ?? book.DefaultPaymentTerms,
                Notes = "Demo invoice [demo-seed]",
                BillToAddress = cust.BillingAddress,
                Salesperson = cust.CustomerOwner ?? owner,
                Terms = cust.PaymentTerms,
                PurchaseOrderNumber = $"PO-{number[^4..]}",
                Subtotal = net,
                TaxTotal = taxAmt,
                GrandTotal = lineTotal,
                Amount = lineTotal,
                AmountPaid = 0,
                IsPaid = status == InvoiceStatus.Paid,
                Lines =
                [
                    new InvoiceLine
                    {
                        CompanyId = companyId,
                        ProductId = prodAnn.Id,
                        ProductName = prodAnn.Name,
                        Description = status == InvoiceStatus.Void ? "Voided demo line" : "Annual subscription / services",
                        Quantity = qty,
                        UnitPrice = unitPrice,
                        TaxRate = taxRate,
                        TaxAmount = taxAmt,
                        LineTotal = lineTotal,
                        SortOrder = 1,
                        CreatedDate = DateTime.UtcNow
                    }
                ]
            };

            if (status is InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid)
            {
                var paid = payAmount ?? (status == InvoiceStatus.Paid ? lineTotal : Math.Round(lineTotal * 0.4m, 2));
                inv.AmountPaid = paid;
                inv.IsPaid = status == InvoiceStatus.Paid;
                inv.Payments.Add(new InvoicePayment
                {
                    CompanyId = companyId,
                    Amount = paid,
                    PaymentDate = invoiceDate.AddDays(5),
                    Method = "Bank transfer",
                    Reference = payRef ?? $"NEFT-{number}",
                    Notes = status == InvoiceStatus.Paid ? "Full settlement" : "Partial payment on account"
                });
                if (status == InvoiceStatus.Paid && paid < lineTotal)
                {
                    inv.Payments.Add(new InvoicePayment
                    {
                        CompanyId = companyId,
                        Amount = lineTotal - paid,
                        PaymentDate = invoiceDate.AddDays(12),
                        Method = "UPI",
                        Reference = $"UPI-{number}",
                        Notes = "Balance payment"
                    });
                    inv.AmountPaid = lineTotal;
                }
            }

            _db.Invoices.Add(inv);
            return inv;
        }

        await EnsureDemoInvoiceAsync("DEMO-INV-D01", "CUST-DEMO-04", InvoiceStatus.Draft, today.AddDays(-2), today.AddDays(28), 85000m);
        await EnsureDemoInvoiceAsync("DEMO-INV-S01", "CUST-DEMO-01", InvoiceStatus.Sent, today.AddDays(-5), today.AddDays(25), 240000m);
        await EnsureDemoInvoiceAsync("DEMO-INV-O01", "CUST-DEMO-03", InvoiceStatus.Sent, today.AddDays(-45), today.AddDays(-15), 120000m); // overdue
        await EnsureDemoInvoiceAsync("DEMO-INV-P01", "CUST-DEMO-02", InvoiceStatus.PartiallyPaid, today.AddDays(-20), today.AddDays(10), 310000m, payAmount: 150000m, payRef: "NEFT-HARBOR-1");
        await EnsureDemoInvoiceAsync("DEMO-INV-F01", "CUST-DEMO-05", InvoiceStatus.Paid, today.AddDays(-40), today.AddDays(-10), 580000m);
        await EnsureDemoInvoiceAsync("DEMO-INV-V01", "CUST-DEMO-09", InvoiceStatus.Void, today.AddDays(-12), today.AddDays(18), 48000m);
        await EnsureDemoInvoiceAsync("DEMO-INV-S02", "CUST-DEMO-06", InvoiceStatus.Sent, today.AddDays(-3), today.AddDays(27), 180000m);
        await EnsureDemoInvoiceAsync("DEMO-INV-P02", "CUST-DEMO-07", InvoiceStatus.PartiallyPaid, today.AddDays(-18), today.AddDays(12), 220000m, payAmount: 50000m);

        // Link legacy INV-1001 to a CRM customer if still unlinked
        var legacy = await _db.Invoices.FirstOrDefaultAsync(i => i.CompanyMasterId == book.Id && i.InvoiceNumber == "INV-1001", ct);
        if (legacy is not null && legacy.CustomerId is null)
        {
            var c1 = Cust("CUST-DEMO-01");
            legacy.CustomerId = c1.Id;
            legacy.CustomerName = c1.CompanyName;
            legacy.BillToAddress = c1.BillingAddress;
            legacy.Salesperson = c1.CustomerOwner;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation("Rich CRM/Finance demo seed complete for company {CompanyId}.", companyId);
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

    private async Task EnsureFinanceSchemaAsync(CancellationToken ct)
    {
        foreach (var sql in new[]
        {
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "BillingStreet" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "BillingCity" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "BillingState" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "BillingZip" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "BillingCountry" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "InvoiceNumberPrefix" text NOT NULL DEFAULT 'INV-';""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "NextInvoiceSequence" integer NOT NULL DEFAULT 1001;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "DefaultPaymentTerms" text NULL;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "DefaultDueDays" integer NOT NULL DEFAULT 30;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "CompanyId" integer NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "CustomerId" integer NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "CustomerName" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "InvoiceStatus" integer NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "CurrencyCode" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "PaymentTerms" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Subtotal" numeric(18,2) NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Discount" numeric(18,2) NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "TaxTotal" numeric(18,2) NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "GrandTotal" numeric(18,2) NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "AmountPaid" numeric(18,2) NOT NULL DEFAULT 0;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "PurchaseOrderNumber" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "BillToAddress" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Salesperson" text NULL;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "Terms" text NULL;""",
            """ALTER TABLE "Invoices" ALTER COLUMN "FinanceCustomerId" DROP NOT NULL;"""
        })
        {
            try { await _db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Finance schema statement skipped: {Sql}", sql); }
        }

        // Backfill totals from legacy Amount / IsPaid
        try
        {
            await _db.Database.ExecuteSqlRawAsync("""
                UPDATE "Invoices"
                SET "GrandTotal" = "Amount",
                    "Subtotal" = CASE WHEN "Subtotal" = 0 THEN "Amount" ELSE "Subtotal" END,
                    "InvoiceStatus" = CASE WHEN "IsPaid" THEN 3 ELSE "InvoiceStatus" END,
                    "AmountPaid" = CASE WHEN "IsPaid" THEN "Amount" ELSE "AmountPaid" END,
                    "CurrencyCode" = COALESCE(NULLIF("CurrencyCode", ''), 'USD')
                WHERE "GrandTotal" = 0 AND "Amount" <> 0;
                """, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not backfill invoice totals");
        }

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "InvoiceLines" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL,
                "InvoiceId" integer NOT NULL,
                "ProductId" integer NULL,
                "ProductName" text NOT NULL DEFAULT '',
                "Description" text NULL,
                "Quantity" numeric(18,2) NOT NULL DEFAULT 1,
                "UnitPrice" numeric(18,2) NOT NULL DEFAULT 0,
                "Discount" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                "LineTotal" numeric(18,2) NOT NULL DEFAULT 0,
                "SortOrder" integer NOT NULL DEFAULT 0
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "InvoicePayments" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                "CompanyId" integer NULL,
                "InvoiceId" integer NOT NULL,
                "Amount" numeric(18,2) NOT NULL DEFAULT 0,
                "PaymentDate" timestamp with time zone NOT NULL,
                "Method" text NULL,
                "Reference" text NULL,
                "Notes" text NULL
            );
            """, ct);

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Invoices_CompanyId_InvoiceNumber"
            ON "Invoices" ("CompanyId", "InvoiceNumber");
            """, ct);

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "OrderNumberPrefix" text NOT NULL DEFAULT 'SO-';""", ct);
            await _db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "NextOrderSequence" integer NOT NULL DEFAULT 1001;""", ct);
            await _db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "SalesOrderId" integer NULL;""", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not add sales-order columns to finance tables");
        }
    }

    private async Task EnsureSalesOrderSchemaAsync(CancellationToken ct)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SalesOrders" (
                    "Id" SERIAL PRIMARY KEY,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                    "CompanyId" integer NULL,
                    "CustomerId" integer NULL,
                    "CustomerName" text NULL,
                    "QuoteId" integer NULL,
                    "OrderNumber" text NOT NULL DEFAULT '',
                    "OrderDate" timestamp with time zone NOT NULL,
                    "RequestedDeliveryDate" timestamp with time zone NULL,
                    "OrderStatus" integer NOT NULL DEFAULT 0,
                    "CurrencyCode" text NULL,
                    "PaymentTerms" text NULL,
                    "PurchaseOrderNumber" text NULL,
                    "BillToAddress" text NULL,
                    "ShipToAddress" text NULL,
                    "Salesperson" text NULL,
                    "Notes" text NULL,
                    "Terms" text NULL,
                    "Subtotal" numeric(18,2) NOT NULL DEFAULT 0,
                    "Discount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxTotal" numeric(18,2) NOT NULL DEFAULT 0,
                    "GrandTotal" numeric(18,2) NOT NULL DEFAULT 0,
                    "AmountInvoiced" numeric(18,2) NOT NULL DEFAULT 0
                );
                """, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SalesOrders create skipped");
        }

        // EF maps enum Status -> column "Status" which collides with BaseEntity.Status text.
        // Prefer renaming enum column to OrderStatus if present as Status1 from above, or ensure OrderStatus.
        foreach (var sql in new[]
        {
            """ALTER TABLE "SalesOrders" ADD COLUMN IF NOT EXISTS "OrderStatus" integer NOT NULL DEFAULT 0;""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "OrderNumberPrefix" text NOT NULL DEFAULT 'SO-';""",
            """ALTER TABLE "CompanyMasters" ADD COLUMN IF NOT EXISTS "NextOrderSequence" integer NOT NULL DEFAULT 1001;""",
            """ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "SalesOrderId" integer NULL;"""
        })
        {
            try { await _db.Database.ExecuteSqlRawAsync(sql, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Sales order schema statement skipped: {Sql}", sql); }
        }

        // Prefer OrderStatus column; migrate Status1 if an older patch created it.
        try
        {
            await _db.Database.ExecuteSqlRawAsync("""
                DO $$
                BEGIN
                  IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'SalesOrders' AND column_name = 'Status1'
                  ) AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'SalesOrders' AND column_name = 'OrderStatus'
                  ) THEN
                    UPDATE "SalesOrders" SET "OrderStatus" = "Status1";
                  ELSIF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'SalesOrders' AND column_name = 'Status1'
                  ) THEN
                    ALTER TABLE "SalesOrders" RENAME COLUMN "Status1" TO "OrderStatus";
                  END IF;
                END $$;
                """, ct);
        }
        catch { /* ignore */ }

        try
        {
            await _db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SalesOrderLines" (
                    "Id" SERIAL PRIMARY KEY,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    "CreatedBy" text NULL, "ModifiedBy" text NULL, "Status" text NULL,
                    "CompanyId" integer NULL,
                    "SalesOrderId" integer NOT NULL,
                    "ProductId" integer NULL,
                    "ProductName" text NULL,
                    "Description" text NULL,
                    "Quantity" numeric(18,2) NOT NULL DEFAULT 1,
                    "UnitPrice" numeric(18,2) NOT NULL DEFAULT 0,
                    "Discount" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxRate" numeric(18,2) NOT NULL DEFAULT 0,
                    "TaxAmount" numeric(18,2) NOT NULL DEFAULT 0,
                    "LineTotal" numeric(18,2) NOT NULL DEFAULT 0,
                    "SortOrder" integer NOT NULL DEFAULT 0
                );
                """, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SalesOrderLines create skipped");
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_SalesOrders_CompanyId_OrderNumber"
                ON "SalesOrders" ("CompanyId", "OrderNumber");
                """, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SalesOrders index create skipped");
        }
    }

    private async Task AssignOrphanFinanceToFirstCompanyAsync(CancellationToken ct)
    {
        var companyId = await _db.Companies.OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (companyId is null) return;

        try
        {
            await _db.CompanyMasters
                .Where(b => b.TenantCompanyId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TenantCompanyId, companyId), ct);

            var bookIds = await _db.CompanyMasters
                .Where(b => b.TenantCompanyId == companyId)
                .Select(b => b.Id)
                .ToListAsync(ct);

            if (bookIds.Count > 0)
            {
                await _db.Invoices
                    .Where(i => i.CompanyId == null && bookIds.Contains(i.CompanyMasterId))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
                await _db.InvoiceLines
                    .Where(l => l.CompanyId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
                await _db.InvoicePayments
                    .Where(p => p.CompanyId == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompanyId, companyId), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not assign orphan finance rows to company {CompanyId}", companyId);
        }
    }
}
