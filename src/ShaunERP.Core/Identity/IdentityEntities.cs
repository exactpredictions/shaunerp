namespace ShaunERP.Core.Identity;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Description { get; set; }

    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
    public ICollection<CompanyModule> CompanyModules { get; set; } = new List<CompanyModule>();
}

public class User : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSystemAdmin { get; set; }
    /// <summary>When true, login succeeds but clients should force a password change.</summary>
    public bool MustChangePassword { get; set; }

    public ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

public class CompanyUser : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class Role : BaseEntity
{
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class UserRole : BaseEntity
{
    public int CompanyUserId { get; set; }
    public CompanyUser? CompanyUser { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
}

public class RolePermission : BaseEntity
{
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class CompanyModule : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    /// <summary>Legacy display / seed name. Prefer <see cref="ModuleKey"/>.</summary>
    public string ModuleName { get; set; } = string.Empty;
    /// <summary>Normalized module key such as crm, users, finance.</summary>
    public string ModuleKey { get; set; } = string.Empty;
    public string? Subdomain { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class Session : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserCompanyPreference : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public string? LastModuleKey { get; set; }
    /// <summary>Comma-separated favourite module keys.</summary>
    public string? FavouriteModules { get; set; }
    /// <summary>Comma-separated recently used module keys (most recent first).</summary>
    public string? RecentModules { get; set; }
    public bool SidebarCollapsed { get; set; }
}

public class AuditLog : BaseEntity
{
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? BeforeValues { get; set; }
    public string? AfterValues { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
