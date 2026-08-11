namespace ShaunERP.Core.Platform;

public enum ModuleCategory
{
    Home = 0,
    Administration = 1,
    CustomerAndSales = 2,
    Finance = 3,
    Operations = 4,
    People = 5,
    ProjectsAndServices = 6,
    Commerce = 7,
    Analytics = 8,
    Platform = 9
}

public static class ModuleCategoryDisplay
{
    public static string GetLabel(ModuleCategory category) => category switch
    {
        ModuleCategory.Home => "Home",
        ModuleCategory.Administration => "Administration",
        ModuleCategory.CustomerAndSales => "Customer and Sales",
        ModuleCategory.Finance => "Finance",
        ModuleCategory.Operations => "Operations",
        ModuleCategory.People => "People",
        ModuleCategory.ProjectsAndServices => "Projects and Services",
        ModuleCategory.Commerce => "Commerce",
        ModuleCategory.Analytics => "Analytics",
        ModuleCategory.Platform => "Platform",
        _ => category.ToString()
    };
}
