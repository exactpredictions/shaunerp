using Microsoft.EntityFrameworkCore;
using ShaunERP.Core.Crm;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.ImportExport;

/// <summary>
/// Customer account numbers are the join key for invoice, order, contact, and
/// opportunity CSV imports. Duplicate numbers must not crash ToDictionary.
/// </summary>
internal sealed class CustomerAccountIndex
{
    public Dictionary<string, Customer> UniqueByNumber { get; }
    public HashSet<string> Duplicates { get; }

    public CustomerAccountIndex(
        Dictionary<string, Customer> uniqueByNumber,
        HashSet<string> duplicates)
    {
        UniqueByNumber = uniqueByNumber;
        Duplicates = duplicates;
    }

    public bool IsDuplicate(string? accountNumber) =>
        !string.IsNullOrWhiteSpace(accountNumber) && Duplicates.Contains(accountNumber);

    public bool TryGetUnique(string? accountNumber, out Customer customer)
    {
        customer = null!;
        return !string.IsNullOrWhiteSpace(accountNumber)
               && UniqueByNumber.TryGetValue(accountNumber, out customer!);
    }

    public static string DuplicateError(string accountNumber) =>
        $"Customer account '{accountNumber}' is duplicated; resolve the clash before importing.";
}

internal static class CustomerAccountLookup
{
    public static async Task<CustomerAccountIndex> LoadAsync(
        ShaunErpDbContext db, int companyId, CancellationToken ct)
    {
        var rows = await db.CrmCustomers
            .Where(c => c.CompanyId == companyId && c.AccountNumber != null)
            .ToListAsync(ct);

        var groups = rows
            .Where(c => !string.IsNullOrWhiteSpace(c.AccountNumber))
            .GroupBy(c => c.AccountNumber!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CustomerAccountIndex(
            groups.Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
            groups.Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
