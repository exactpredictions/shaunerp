using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Identity;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.Identity;

public enum IdentityMatchConfidence
{
    NoMatch = 0,
    HighConfidence = 1,
    Ambiguous = 2,
    Conflict = 3
}

public enum IdentityMatchBy
{
    None = 0,
    Phone = 1,
    Email = 2,
    PhoneAndEmail = 3
}

public sealed class IdentityMatchRequest
{
    public int CompanyId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? DisplayName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class IdentityMatchCandidate
{
    public int PartyId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? MatchedPhone { get; init; }
    public string? MatchedEmail { get; init; }
}

public sealed class IdentityMatchResult
{
    public IdentityMatchConfidence Confidence { get; init; } = IdentityMatchConfidence.NoMatch;
    public IdentityMatchBy MatchedBy { get; init; } = IdentityMatchBy.None;
    public string StatusCode { get; init; } = ImportMatchStatuses.NoMatch;
    public string? Reason { get; init; }
    public int? PartyId { get; init; }
    public IReadOnlyList<IdentityMatchCandidate> Candidates { get; init; } = Array.Empty<IdentityMatchCandidate>();
}

public interface IIdentityMatchService
{
    Task<IdentityMatchResult> MatchAsync(IdentityMatchRequest request, CancellationToken ct = default);
}

public sealed class IdentityMatchService : IIdentityMatchService
{
    private readonly ShaunErpDbContext _db;
    private readonly IPhoneNormalizer _phones;
    private readonly IEmailNormalizer _emails;

    public IdentityMatchService(ShaunErpDbContext db, IPhoneNormalizer phones, IEmailNormalizer emails)
    {
        _db = db;
        _phones = phones;
        _emails = emails;
    }

    public async Task<IdentityMatchResult> MatchAsync(IdentityMatchRequest request, CancellationToken ct = default)
    {
        var phone = _phones.Normalize(request.Phone);
        var email = _emails.Normalize(request.Email);
        var hasPhone = !string.IsNullOrWhiteSpace(phone.NormalizedPhone) && phone.NormalizedPhone.Length >= 8;
        var hasEmail = !string.IsNullOrWhiteSpace(email);

        if (!hasPhone && !hasEmail)
        {
            return new IdentityMatchResult
            {
                Confidence = IdentityMatchConfidence.NoMatch,
                StatusCode = ImportMatchStatuses.NoMatch,
                Reason = "No usable phone or email."
            };
        }

        List<int> phonePartyIds = [];
        if (hasPhone)
        {
            phonePartyIds = await (
                from ph in _db.PartyPhones.AsNoTracking()
                join party in _db.Parties.AsNoTracking() on ph.PartyId equals party.Id
                where ph.IsActive && party.CompanyId == request.CompanyId
                      && (ph.NormalizedPhone == phone.NormalizedPhone
                          || (phone.E164Phone != null && ph.E164Phone == phone.E164Phone))
                select ph.PartyId
            ).Distinct().ToListAsync(ct);
        }

        List<int> emailPartyIds = [];
        if (hasEmail)
        {
            emailPartyIds = await (
                from em in _db.PartyEmails.AsNoTracking()
                join party in _db.Parties.AsNoTracking() on em.PartyId equals party.Id
                where em.IsActive && party.CompanyId == request.CompanyId
                      && em.NormalizedEmail == email
                select em.PartyId
            ).Distinct().ToListAsync(ct);
        }

        var both = phonePartyIds.Intersect(emailPartyIds).ToList();
        if (both.Count == 1)
        {
            return await BuildAsync(both[0], IdentityMatchConfidence.HighConfidence, IdentityMatchBy.PhoneAndEmail,
                ImportMatchStatuses.HighConfidence, "Matched by phone and email.", ct);
        }

        if (both.Count > 1)
        {
            return await AmbiguousAsync(both, IdentityMatchBy.PhoneAndEmail,
                "Multiple parties share this phone+email combination.", ct);
        }

        // Phone hit + email hit different parties → conflict
        if (phonePartyIds.Count > 0 && emailPartyIds.Count > 0 && both.Count == 0)
        {
            var ids = phonePartyIds.Concat(emailPartyIds).Distinct().ToList();
            return await AmbiguousAsync(ids, IdentityMatchBy.None,
                "ConflictingPhoneAndEmail: phone and email resolve to different parties.", ct,
                ImportMatchStatuses.Conflict, IdentityMatchConfidence.Conflict);
        }

        if (phonePartyIds.Count == 1 && emailPartyIds.Count == 0)
        {
            // same phone, incoming has email not on file → high confidence if name soft-compatible; else still high for phone-only identity in CRM imports
            // Conservative: if incoming email present and party has ANY different email → conflict/ambiguous
            var partyId = phonePartyIds[0];
            if (hasEmail)
            {
                var existingEmails = await _db.PartyEmails.AsNoTracking()
                    .Where(e => e.PartyId == partyId && e.IsActive && e.NormalizedEmail != null)
                    .Select(e => e.NormalizedEmail!)
                    .ToListAsync(ct);
                if (existingEmails.Count > 0 && !existingEmails.Contains(email!))
                {
                    return await AmbiguousAsync([partyId], IdentityMatchBy.Phone,
                        "ConflictingEmail: same phone, different email on file.", ct,
                        ImportMatchStatuses.Conflict, IdentityMatchConfidence.Conflict);
                }
            }

            if (await NamesConflictAsync(partyId, request, ct))
            {
                return await AmbiguousAsync([partyId], IdentityMatchBy.Phone,
                    "AmbiguousMatch: same phone but different person name.", ct);
            }

            return await BuildAsync(partyId, IdentityMatchConfidence.HighConfidence, IdentityMatchBy.Phone,
                ImportMatchStatuses.HighConfidence, "Matched by phone.", ct);
        }

        if (emailPartyIds.Count == 1 && phonePartyIds.Count == 0)
        {
            var partyId = emailPartyIds[0];
            if (hasPhone)
            {
                var existingPhones = await _db.PartyPhones.AsNoTracking()
                    .Where(p => p.PartyId == partyId && p.IsActive && p.NormalizedPhone != null)
                    .Select(p => p.NormalizedPhone!)
                    .ToListAsync(ct);
                if (existingPhones.Count > 0 && !existingPhones.Contains(phone.NormalizedPhone!))
                {
                    return await AmbiguousAsync([partyId], IdentityMatchBy.Email,
                        "ConflictingPhone: same email, different phone on file.", ct,
                        ImportMatchStatuses.Conflict, IdentityMatchConfidence.Conflict);
                }
            }

            if (await NamesConflictAsync(partyId, request, ct))
            {
                return await AmbiguousAsync([partyId], IdentityMatchBy.Email,
                    "AmbiguousMatch: same email but different person name.", ct);
            }

            return await BuildAsync(partyId, IdentityMatchConfidence.HighConfidence, IdentityMatchBy.Email,
                ImportMatchStatuses.HighConfidence, "Matched by email.", ct);
        }

        if (phonePartyIds.Count > 1)
        {
            return await AmbiguousAsync(phonePartyIds, IdentityMatchBy.Phone,
                "Multiple parties share this normalized phone (possible shared/family number).", ct);
        }

        if (emailPartyIds.Count > 1)
        {
            return await AmbiguousAsync(emailPartyIds, IdentityMatchBy.Email,
                "Multiple parties share this email.", ct);
        }

        return new IdentityMatchResult
        {
            Confidence = IdentityMatchConfidence.NoMatch,
            StatusCode = ImportMatchStatuses.NoMatch,
            Reason = "No existing party matched."
        };
    }

    private async Task<bool> NamesConflictAsync(int partyId, IdentityMatchRequest request, CancellationToken ct)
    {
        var incoming = NormalizeNameToken(request.DisplayName)
                       ?? NormalizeNameToken($"{request.FirstName} {request.LastName}");
        if (incoming is null)
        {
            return false; // no name evidence → do not block phone/email match
        }

        var party = await _db.Parties.AsNoTracking()
            .Include(p => p.Person)
            .FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party is null)
        {
            return false;
        }

        var existing = NormalizeNameToken(party.DisplayName)
                       ?? NormalizeNameToken($"{party.Person?.FirstName} {party.Person?.LastName}");
        if (existing is null)
        {
            return false;
        }

        if (existing == incoming)
        {
            return false;
        }

        // Soft: if either full name contains the other token set, allow (e.g. "Ravi" vs "Ravi Kumar")
        if (existing.Contains(incoming) || incoming.Contains(existing))
        {
            return false;
        }

        var existingParts = existing.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var incomingParts = incoming.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (existingParts.Intersect(incomingParts).Any())
        {
            // Shared token (e.g. same last name) is weak — still ambiguous if first tokens differ
            var eFirst = existingParts[0];
            var iFirst = incomingParts[0];
            if (eFirst == iFirst)
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeNameToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cleaned = new string(raw.Trim().ToLowerInvariant()
            .Where(c => char.IsLetter(c) || c == ' ')
            .ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }

    private async Task<IdentityMatchResult> BuildAsync(
        int partyId,
        IdentityMatchConfidence confidence,
        IdentityMatchBy by,
        string status,
        string reason,
        CancellationToken ct)
    {
        var party = await _db.Parties.AsNoTracking().FirstAsync(p => p.Id == partyId, ct);
        return new IdentityMatchResult
        {
            Confidence = confidence,
            MatchedBy = by,
            StatusCode = status,
            Reason = reason,
            PartyId = partyId,
            Candidates =
            [
                new IdentityMatchCandidate
                {
                    PartyId = partyId,
                    DisplayName = party.DisplayName
                }
            ]
        };
    }

    private async Task<IdentityMatchResult> AmbiguousAsync(
        IReadOnlyList<int> partyIds,
        IdentityMatchBy by,
        string reason,
        CancellationToken ct,
        string status = ImportMatchStatuses.Ambiguous,
        IdentityMatchConfidence confidence = IdentityMatchConfidence.Ambiguous)
    {
        var parties = await _db.Parties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .Select(p => new IdentityMatchCandidate { PartyId = p.Id, DisplayName = p.DisplayName })
            .ToListAsync(ct);

        return new IdentityMatchResult
        {
            Confidence = confidence,
            MatchedBy = by,
            StatusCode = status,
            Reason = reason,
            Candidates = parties
        };
    }
}
