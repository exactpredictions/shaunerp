namespace ShaunERP.Infrastructure.Platform;

public interface ITenantContext
{
    int? UserId { get; }
    int? CompanyId { get; }
    string? CompanyName { get; }
    bool IsSystemAdmin { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Permissions { get; }
    IReadOnlySet<string> Entitlements { get; }
    void Set(TenantContextSnapshot snapshot);
    void Clear();
}

public sealed class TenantContextSnapshot
{
    public int? UserId { get; init; }
    public int? CompanyId { get; init; }
    public string? CompanyName { get; init; }
    public bool IsSystemAdmin { get; init; }
    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> Entitlements { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class TenantContext : ITenantContext
{
    private TenantContextSnapshot _snapshot = new();

    public int? UserId => _snapshot.UserId;
    public int? CompanyId => _snapshot.CompanyId;
    public string? CompanyName => _snapshot.CompanyName;
    public bool IsSystemAdmin => _snapshot.IsSystemAdmin;
    public bool IsAuthenticated => UserId is > 0;
    public IReadOnlySet<string> Permissions => _snapshot.Permissions;
    public IReadOnlySet<string> Entitlements => _snapshot.Entitlements;

    public void Set(TenantContextSnapshot snapshot) => _snapshot = snapshot;

    public void Clear() => _snapshot = new();
}
