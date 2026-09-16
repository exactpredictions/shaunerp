using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.Identity;

public sealed class EnsurePartyRequest
{
    public int CompanyId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? DisplayName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? SecondaryEmail { get; init; }
    public string? Mobile { get; init; }
    public bool CreateReviewOnAmbiguous { get; init; } = true;
    public int? ImportBatchId { get; init; }
    public int? ImportSourceRowId { get; init; }
    public string? ExternalId { get; init; }
}

public sealed class EnsurePartyResult
{
    public int? PartyId { get; init; }
    public bool Created { get; init; }
    public bool LinkedExisting { get; init; }
    public bool NeedsReview { get; init; }
    public IdentityMatchResult Match { get; init; } = new();
}

public interface IPartyIdentityService
{
    Task<EnsurePartyResult> EnsurePersonPartyAsync(EnsurePartyRequest request, CancellationToken ct = default);
    Task AttachContactChannelsAsync(int partyId, string? phone, string? mobile, string? email, string? secondaryEmail, CancellationToken ct = default);
    Task<int?> FindPriorImportedPartyIdAsync(int companyId, string? externalId, CancellationToken ct = default);
    string BuildDisplayName(string? firstName, string? lastName, string? fallback = null);
}

public sealed class PartyIdentityService : IPartyIdentityService
{
    private readonly ShaunErpDbContext _db;
    private readonly IIdentityMatchService _match;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;

    public PartyIdentityService(
        ShaunErpDbContext db,
        IIdentityMatchService match,
        IPhoneNormalizer phones,
        IEmailNormalizer emails)
    {
        _db = db;
        _match = match;
        _phones = phones;
        _emails = emails;
    }

    public string BuildDisplayName(string? firstName, string? lastName, string? fallback = null)
    {
        var name = $"{firstName} {lastName}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Unknown" : fallback.Trim();
    }

    /// <summary>
    /// Idempotent import lookup: only reuse a party previously linked for this
    /// <paramref name="externalId"/> when both the import batch and the party
    /// belong to <paramref name="companyId"/>.
    /// </summary>
    public async Task<int?> FindPriorImportedPartyIdAsync(int companyId, string? externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        return await (
            from row in _db.ImportSourceRows.AsNoTracking()
            join batch in _db.ImportBatches.AsNoTracking() on row.ImportBatchId equals batch.Id
            join party in _db.Parties.AsNoTracking() on row.MatchedPartyId equals party.Id
            where row.ExternalId == externalId
                  && row.MatchedPartyId != null
                  && batch.CompanyId == companyId
                  && party.CompanyId == companyId
            orderby row.Id descending
            select (int?)party.Id
        ).FirstOrDefaultAsync(ct);
    }

    public async Task<EnsurePartyResult> EnsurePersonPartyAsync(EnsurePartyRequest request, CancellationToken ct = default)
    {
        var primaryPhone = FirstNonEmpty(request.Mobile, request.Phone);
        var match = await _match.MatchAsync(new IdentityMatchRequest
        {
            CompanyId = request.CompanyId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            DisplayName = request.DisplayName,
            Phone = primaryPhone,
            Email = request.Email
        }, ct);

        if (match.Confidence == IdentityMatchConfidence.HighConfidence && match.PartyId is int existingId)
        {
            await AttachContactChannelsAsync(existingId, request.Phone, request.Mobile, request.Email, request.SecondaryEmail, ct);
            return new EnsurePartyResult
            {
                PartyId = existingId,
                LinkedExisting = true,
                Match = match
            };
        }

        if (match.Confidence is IdentityMatchConfidence.Ambiguous or IdentityMatchConfidence.Conflict)
        {
            if (request.CreateReviewOnAmbiguous)
            {
                await QueueReviewAsync(request, match, primaryPhone, ct);
            }

            // Conservative: create a new party rather than merge uncertain identities
            var createdAmbiguous = await CreatePersonAsync(request, ct);
            return new EnsurePartyResult
            {
                PartyId = createdAmbiguous.Id,
                Created = true,
                NeedsReview = true,
                Match = match
            };
        }

        var created = await CreatePersonAsync(request, ct);
        return new EnsurePartyResult
        {
            PartyId = created.Id,
            Created = true,
            Match = match
        };
    }

    public async Task AttachContactChannelsAsync(
        int partyId,
        string? phone,
        string? mobile,
        string? email,
        string? secondaryEmail,
        CancellationToken ct = default)
    {
        await UpsertPhoneAsync(partyId, mobile, "Mobile", preferPrimary: true, ct);
        await UpsertPhoneAsync(partyId, phone, "Phone", preferPrimary: false, ct);
        await UpsertEmailAsync(partyId, email, preferPrimary: true, ct);
        await UpsertEmailAsync(partyId, secondaryEmail, preferPrimary: false, ct);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Party> CreatePersonAsync(EnsurePartyRequest request, CancellationToken ct)
    {
        var display = BuildDisplayName(request.FirstName, request.LastName, request.DisplayName ?? request.Email ?? request.Phone);
        var party = new Party
        {
            CompanyId = request.CompanyId,
            PartyType = PartyTypes.Person,
            DisplayName = display,
            CreatedDate = DateTime.UtcNow,
            Status = "Active",
            Person = new PartyPerson
            {
                FirstName = NullIfEmpty(request.FirstName),
                LastName = NullIfEmpty(request.LastName),
                CreatedDate = DateTime.UtcNow,
                Status = "Active"
            }
        };
        _db.Parties.Add(party);
        await _db.SaveChangesAsync(ct);
        await AttachContactChannelsAsync(party.Id, request.Phone, request.Mobile, request.Email, request.SecondaryEmail, ct);
        return party;
    }

    private async Task UpsertPhoneAsync(int partyId, string? raw, string type, bool preferPrimary, CancellationToken ct)
    {
        var n = _phones.Normalize(raw);
        if (string.IsNullOrWhiteSpace(n.NormalizedPhone))
        {
            return;
        }

        var existing = await _db.PartyPhones
            .FirstOrDefaultAsync(p =>
                p.PartyId == partyId &&
                (p.NormalizedPhone == n.NormalizedPhone ||
                 (n.E164Phone != null && p.E164Phone == n.E164Phone)), ct);

        if (existing is not null)
        {
            existing.OriginalPhone ??= n.Original;
            existing.E164Phone ??= n.E164Phone;
            existing.IsActive = true;
            existing.UpdatedDate = DateTime.UtcNow;
            if (preferPrimary)
            {
                existing.IsPrimary = true;
            }

            return;
        }

        var anyPrimary = await _db.PartyPhones.AnyAsync(p => p.PartyId == partyId && p.IsPrimary, ct);
        _db.PartyPhones.Add(new PartyPhone
        {
            PartyId = partyId,
            OriginalPhone = n.Original,
            NormalizedPhone = n.NormalizedPhone,
            E164Phone = n.E164Phone,
            PhoneType = type,
            IsPrimary = preferPrimary || !anyPrimary,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
    }

    private async Task UpsertEmailAsync(int partyId, string? raw, bool preferPrimary, CancellationToken ct)
    {
        var normalized = _emails.Normalize(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var existing = await _db.PartyEmails
            .FirstOrDefaultAsync(e => e.PartyId == partyId && e.NormalizedEmail == normalized, ct);
        if (existing is not null)
        {
            existing.OriginalEmail ??= raw?.Trim();
            existing.IsActive = true;
            existing.UpdatedDate = DateTime.UtcNow;
            if (preferPrimary)
            {
                existing.IsPrimary = true;
            }

            return;
        }

        var anyPrimary = await _db.PartyEmails.AnyAsync(e => e.PartyId == partyId && e.IsPrimary, ct);
        _db.PartyEmails.Add(new PartyEmail
        {
            PartyId = partyId,
            OriginalEmail = raw?.Trim(),
            NormalizedEmail = normalized,
            EmailType = "Work",
            IsPrimary = preferPrimary || !anyPrimary,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
    }

    private async Task QueueReviewAsync(
        EnsurePartyRequest request,
        IdentityMatchResult match,
        string? primaryPhone,
        CancellationToken ct)
    {
        var candidate = match.Candidates.FirstOrDefault();
        _db.ImportMatchReviews.Add(new ImportMatchReview
        {
            CompanyId = request.CompanyId,
            ImportBatchId = request.ImportBatchId,
            ImportSourceRowId = request.ImportSourceRowId,
            CandidatePartyId = candidate?.PartyId,
            ReviewStatus = "Pending",
            MatchReason = match.Reason,
            IncomingName = BuildDisplayName(request.FirstName, request.LastName, request.DisplayName),
            IncomingPhone = primaryPhone,
            IncomingEmail = request.Email,
            CandidateName = candidate?.DisplayName,
            CreatedDate = DateTime.UtcNow,
            Status = "Active"
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
