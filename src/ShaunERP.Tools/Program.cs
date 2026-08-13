using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.ImportExport;
using ShaunERP.Infrastructure;
using ShaunERP.Infrastructure.Services.ImportExport;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 0;
}

var command = args[0];
if (!string.Equals(command, "bootstrap-users", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

var options = ParseOptions(args.Skip(1).ToArray());
var adminEmail = options.GetValueOrDefault("--admin-email")
    ?? Environment.GetEnvironmentVariable("PROD_ADMIN_EMAIL")
    ?? "shauninfomediaopc@gmail.com";
var adminPassword = options.GetValueOrDefault("--admin-password")
    ?? Environment.GetEnvironmentVariable("PROD_ADMIN_PASSWORD");
var accountantEmail = options.GetValueOrDefault("--accountant-email")
    ?? Environment.GetEnvironmentVariable("PROD_ACCOUNTANT_EMAIL")
    ?? "arokiabakkianathanl@gmail.com";
var accountantPassword = options.GetValueOrDefault("--accountant-password")
    ?? Environment.GetEnvironmentVariable("PROD_ACCOUNTANT_PASSWORD");

if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(accountantPassword))
{
    Console.Error.WriteLine("Admin and accountant passwords are required via flags or PROD_ADMIN_PASSWORD / PROD_ACCOUNTANT_PASSWORD.");
    return 2;
}

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", optional: true)
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=shaunerp;Username=shaunerp;Password=shaunerp"
    })
    .Build();

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddSingleton<IConfiguration>(config);
services.AddInfrastructure(config);
await using var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();
var bootstrap = scope.ServiceProvider.GetRequiredService<IProductionUserBootstrapService>();
var result = await bootstrap.BootstrapUsersAsync(new ProductionUserBootstrapRequest
{
    AdminEmail = adminEmail,
    AdminPassword = adminPassword,
    AccountantEmail = accountantEmail,
    AccountantPassword = accountantPassword
});

foreach (var message in result.Messages)
{
    Console.WriteLine(message);
}

return result.Success ? 0 : 3;

static Dictionary<string, string> ParseOptions(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].StartsWith("--", StringComparison.Ordinal))
        {
            map[args[i]] = args[i + 1];
            i++;
        }
    }

    return map;
}

static void PrintHelp()
{
    Console.WriteLine("""
        ShaunERP.Tools

        Commands:
          bootstrap-users --admin-email <email> --admin-password <pwd> --accountant-email <email> --accountant-password <pwd>

        Connection string:
          ConnectionStrings__DefaultConnection

        Password env fallbacks:
          PROD_ADMIN_PASSWORD, PROD_ACCOUNTANT_PASSWORD
        """);
}
