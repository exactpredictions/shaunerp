namespace ShaunERP.Core.Platform;

public sealed class ModuleNavItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Route { get; init; }
    public string? Icon { get; init; }
    public string? RequiredPermission { get; init; }
    public string? Group { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<ModuleNavItem> Children { get; init; } = Array.Empty<ModuleNavItem>();
}

public sealed class ModuleDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ModuleCategory Category { get; init; }
    public required string BaseRoute { get; init; }
    public required string DashboardRoute { get; init; }
    public string Icon { get; init; } = "module";
    public string? RequiredEntitlement { get; init; }
    public string? RequiredPermission { get; init; }
    public int DisplayOrder { get; init; }
    public string Version { get; init; } = "1.0.0";
    public bool IsEnabled { get; init; } = true;
    public bool IsPlaceholder { get; init; }
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ModuleNavItem> NavigationItems { get; init; } = Array.Empty<ModuleNavItem>();
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}
