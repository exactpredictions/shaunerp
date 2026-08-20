using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShaunERP.Core.Crm;
using ShaunERP.Core.Identity;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services;
using ShaunERP.Infrastructure.Services.Identity;

namespace ShaunERP.Tests;

public class PartyIdentityTests
{
    private readonly IPhoneNormalizer _phones = new PhoneNormalizer();
    private readonly IEmailNormalizer _emails = new EmailNormalizer();

    [Theory]
    [InlineData("9876543210", "9876543210", "+919876543210")]
    [InlineData("09876543210", "9876543210", "+919876543210")]
    [InlineData("919876543210", "9876543210", "+919876543210")]
    [InlineData("+919876543210", "9876543210", "+919876543210")]
    [InlineData("+91 98765 43210", "9876543210", "+919876543210")]
    public void Phone_normalizer_canonicalizes_indian_mobiles(string input, string normalized, string e164)
    {
        var result = _phones.Normalize(input);
        Assert.True(result.IsValidIndianMobile);
        Assert.Equal(normalized, result.NormalizedPhone);
        Assert.Equal(e164, result.E164Phone);
    }

    [Fact]
    public void Email_normalizer_trims_and_lowercases()
    {
        Assert.Equal("ravi@example.com", _emails.Normalize(" Ravi@Example.COM "));
        Assert.Equal("ravi@example.com", _emails.Normalize("ravi@example.com"));
        Assert.Null(_emails.Normalize("  "));
    }

    [Fact]
    public async Task Match_same_phone_and_email_is_high_confidence()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@gmail.com");
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            FirstName = "Ravi",
            LastName = "Kumar",
            Phone = "+91 98765 43210",
            Email = "ravi@gmail.com"
        });

        Assert.Equal(IdentityMatchConfidence.HighConfidence, match.Confidence);
        Assert.Equal(IdentityMatchBy.PhoneAndEmail, match.MatchedBy);
        Assert.Equal(party.Id, match.PartyId);
    }

    [Fact]
    public async Task Match_same_phone_different_email_is_conflict()
    {
        await using var db = CreateDb();
        await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@gmail.com");
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            Phone = "9876543210",
            Email = "other@gmail.com"
        });

        Assert.Equal(IdentityMatchConfidence.Conflict, match.Confidence);
    }

    [Fact]
    public async Task Match_same_email_different_phone_is_conflict()
    {
        await using var db = CreateDb();
        await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@gmail.com");
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            Phone = "9123456789",
            Email = "ravi@gmail.com"
        });

        Assert.Equal(IdentityMatchConfidence.Conflict, match.Confidence);
    }

    [Fact]
    public async Task Match_phone_only_is_high_confidence()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", null);
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            Phone = "09876543210"
        });

        Assert.Equal(IdentityMatchConfidence.HighConfidence, match.Confidence);
        Assert.Equal(IdentityMatchBy.Phone, match.MatchedBy);
        Assert.Equal(party.Id, match.PartyId);
    }

    [Fact]
    public async Task Match_email_only_is_high_confidence()
    {
        await using var db = CreateDb();
        var party = await SeedPartyAsync(db, 1, "Ravi", "Kumar", null, "ravi@gmail.com");
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            Email = "Ravi@Gmail.com"
        });

        Assert.Equal(IdentityMatchConfidence.HighConfidence, match.Confidence);
        Assert.Equal(IdentityMatchBy.Email, match.MatchedBy);
        Assert.Equal(party.Id, match.PartyId);
    }

    [Fact]
    public async Task Match_same_phone_different_name_is_ambiguous()
    {
        await using var db = CreateDb();
        await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", null);
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            FirstName = "Raj",
            LastName = "Kumar",
            Phone = "9876543210"
        });

        Assert.Equal(IdentityMatchConfidence.Ambiguous, match.Confidence);
    }

    [Fact]
    public async Task Match_neither_is_no_match()
    {
        await using var db = CreateDb();
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            FirstName = "Only",
            LastName = "Name"
        });

        Assert.Equal(IdentityMatchConfidence.NoMatch, match.Confidence);
    }

    [Fact]
    public async Task Match_shared_phone_two_parties_is_ambiguous()
    {
        await using var db = CreateDb();
        await SeedPartyAsync(db, 1, "Ravi", "Kumar", "9876543210", "ravi@gmail.com");
        await SeedPartyAsync(db, 1, "Raj", "Kumar", "9876543210", "raj@gmail.com");
        var match = await CreateMatch(db).MatchAsync(new IdentityMatchRequest
        {
            CompanyId = 1,
            Phone = "9876543210"
        });

        Assert.Equal(IdentityMatchConfidence.Ambiguous, match.Confidence);
        Assert.True(match.Candidates.Count >= 2);
    }

    [Fact]
    public async Task CreateLead_new_person_creates_party()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);
        var lead = await crm.CreateLeadAsync(new Lead
        {
            FirstName = "Anita",
            LastName = "Sharma",
            Mobile = "+91 90000 00001",
            Email = "anita@example.com"
        });

        Assert.NotNull(lead.PartyId);
        Assert.Equal(1, await db.Parties.CountAsync());
        Assert.Equal(1, await db.PartyPhones.CountAsync());
        Assert.Equal(1, await db.PartyEmails.CountAsync());
    }

    [Fact]
    public async Task CreateLead_existing_party_links_same_identity()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);
        var first = await crm.CreateLeadAsync(new Lead
        {
            FirstName = "Anita",
            LastName = "Sharma",
            Mobile = "9000000001",
            Email = "anita@example.com"
        });
        var second = await crm.CreateLeadAsync(new Lead
        {
            FirstName = "Anita",
            LastName = "Sharma",
            Mobile = "+919000000001",
            Email = "anita@example.com"
        });

        Assert.Equal(first.PartyId, second.PartyId);
        Assert.Equal(1, await db.Parties.CountAsync());
        Assert.Equal(2, await db.Leads.CountAsync());
    }

    [Fact]
    public async Task Backfill_is_idempotent_and_preserves_party_id()
    {
        await using var db = CreateDb();
        db.Leads.Add(new Lead
        {
            CompanyId = 1,
            FirstName = "Bala",
            LastName = "Nair",
            Mobile = "9876501234",
            Email = "bala@example.com",
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateBackfill(db, companyId: 1);
        var first = await service.BackfillAsync(new LeadPartyBackfillOptions { CompanyId = 1, BatchSize = 50 });
        var partyId = (await db.Leads.SingleAsync()).PartyId;
        Assert.True(first.Success);
        Assert.NotNull(partyId);

        var second = await service.BackfillAsync(new LeadPartyBackfillOptions { CompanyId = 1, BatchSize = 50 });
        Assert.Equal(0, second.CreatedParties);
        Assert.Equal(partyId, (await db.Leads.SingleAsync()).PartyId);
        Assert.Equal(1, await db.Parties.CountAsync());
    }

    [Fact]
    public async Task Backfill_respects_company_boundaries()
    {
        await using var db = CreateDb();
        db.Leads.AddRange(
            new Lead { CompanyId = 1, FirstName = "A", LastName = "One", Email = "a@x.com", Mobile = "9000000001", CreatedDate = DateTime.UtcNow },
            new Lead { CompanyId = 2, FirstName = "B", LastName = "Two", Email = "b@x.com", Mobile = "9000000002", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = CreateBackfill(db, companyId: 1);
        var report = await service.BackfillAsync(new LeadPartyBackfillOptions { CompanyId = 1 });
        Assert.Equal(1, report.LeadsWithPartyId);
        Assert.Equal(1, await db.Leads.CountAsync(l => l.CompanyId == 1 && l.PartyId != null));
        Assert.Equal(0, await db.Leads.CountAsync(l => l.CompanyId == 2 && l.PartyId != null));
    }

    [Fact]
    public async Task Conversion_preserves_party_id_through_customer()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);
        var lead = await crm.CreateLeadAsync(new Lead
        {
            FirstName = "Dev",
            LastName = "Patel",
            CompanyName = "Patel Co",
            Mobile = "9888777666",
            Email = "dev@patel.com"
        });
        var partyId = lead.PartyId;
        Assert.NotNull(partyId);

        var (ok, error, result) = await crm.ConvertLeadAsync(lead.Id, new LeadConversionRequest(
            CreateProspect: true, CreateAccount: true, CreateContact: true, CreateOpportunity: true,
            OpportunityName: "Deal", OpportunityAmount: 1000));
        Assert.True(ok, error);
        Assert.Equal(partyId, result!.Prospect!.PartyId);
        Assert.Equal(partyId, result.Contact!.PartyId);
        Assert.Equal(partyId, result.Opportunity!.PartyId);

        var (winOk, winError, customer) = await crm.WinOpportunityAsync(result.Opportunity.Id);
        Assert.True(winOk, winError);
        Assert.Equal(partyId, customer!.PartyId);
        Assert.Equal(partyId, (await db.Opportunities.SingleAsync()).PartyId);
    }

    [Fact]
    public async Task Contact_update_creates_party_and_attaches_channels()
    {
        await using var db = CreateDb();
        var crm = CreateCrm(db, companyId: 1);
        var contact = await crm.CreateContactAsync(new Contact
        {
            FirstName = "Nila",
            LastName = "Rao",
            Email = "nila@example.com",
            Mobile = "9000011111"
        });
        Assert.NotNull(contact.PartyId);

        contact.Email = "nila.rao@example.com";
        var updated = await crm.UpdateContactAsync(contact.Id, contact);
        Assert.Equal(contact.PartyId, updated!.PartyId);
        Assert.Contains(await db.PartyEmails.Where(e => e.PartyId == contact.PartyId).ToListAsync(),
            e => (e.NormalizedEmail ?? e.OriginalEmail)!.Contains("nila.rao@example.com"));
    }

    private static ShaunErpDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ShaunErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ShaunErpDbContext(options);
        db.Companies.AddRange(
            new Company { Id = 1, Name = "A" },
            new Company { Id = 2, Name = "B" });
        db.SaveChanges();
        return db;
    }

    private IdentityMatchService CreateMatch(ShaunErpDbContext db) =>
        new(db, _phones, _emails);

    private PartyIdentityService CreateParties(ShaunErpDbContext db) =>
        new(db, CreateMatch(db), _phones, _emails);

    private CrmService CreateCrm(ShaunErpDbContext db, int companyId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new CrmService(db, tenant, new ModuleRegistry(), CreateParties(db));
    }

    private LeadPartyBackfillService CreateBackfill(ShaunErpDbContext db, int companyId)
    {
        var tenant = new TenantContext();
        tenant.Set(new TenantContextSnapshot
        {
            UserId = 10,
            CompanyId = companyId,
            Entitlements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ModuleKeys.Crm },
            Permissions = new HashSet<string>(Permissions.AdminDefaults, StringComparer.OrdinalIgnoreCase)
        });
        return new LeadPartyBackfillService(
            db,
            CreateParties(db),
            CreateMatch(db),
            _phones,
            _emails,
            tenant,
            NullLogger<LeadPartyBackfillService>.Instance);
    }

    private async Task<Party> SeedPartyAsync(
        ShaunErpDbContext db,
        int companyId,
        string first,
        string last,
        string? phone,
        string? email)
    {
        var parties = CreateParties(db);
        var result = await parties.EnsurePersonPartyAsync(new EnsurePartyRequest
        {
            CompanyId = companyId,
            FirstName = first,
            LastName = last,
            Phone = phone,
            Mobile = phone,
            Email = email,
            CreateReviewOnAmbiguous = false
        });
        return await db.Parties.SingleAsync(p => p.Id == result.PartyId);
    }
}
