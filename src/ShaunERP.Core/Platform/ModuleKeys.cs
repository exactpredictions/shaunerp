namespace ShaunERP.Core.Platform;

public static class ModuleKeys
{
    public const string Home = "home";
    public const string Users = "users";
    public const string Crm = "crm";
    public const string Finance = "finance";
    public const string Hrms = "hrms";
    public const string Projects = "projects";
    public const string Platform = "platform";

    public static string Normalize(string? keyOrName)
    {
        if (string.IsNullOrWhiteSpace(keyOrName))
        {
            return string.Empty;
        }

        var value = keyOrName.Trim().ToLowerInvariant();
        return value switch
        {
            "user" or "usermanagement" or "user-management" or "user_management" => Users,
            "crm" or "customer relationship" => Crm,
            "finance" or "invoicing" or "accounting" => Finance,
            "hrms" or "hr" or "human resources" => Hrms,
            "projects" or "project" => Projects,
            "platform" or "admin" or "platform-admin" => Platform,
            "home" or "dashboard" or "main" => Home,
            _ => value
        };
    }
}
