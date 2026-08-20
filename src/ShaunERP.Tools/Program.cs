using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure;
using ShaunERP.Infrastructure.Persistence;
using ShaunERP.Infrastructure.Platform;
using ShaunERP.Infrastructure.Services.Identity;
using ShaunERP.Infrastructure.Services.ImportExport;
using System.Text.Json;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintHelp();
    return 0;
}

var command = args[0];
return command.ToLowerInvariant() switch
{
    "bootstrap-users" => await RunBootstrapUsersAsync(args.Skip(1).ToArray()),
    "import-csv" => await RunImportCsvAsync(args.Skip(1).ToArray()),
    "party-analyze" => await RunPartyBackfillAsync(args.Skip(1).ToArray(), analyzeOnly: true),
    "party-backfill" => await RunPartyBackfillAsync(args.Skip(1).ToArray(), analyzeOnly: false),
    "students-analyze" => await RunStudentImportAsync(args.Skip(1).ToArray(), analyzeOnly: true),
    "students-import" => await RunStudentImportAsync(args.Skip(1).ToArray(), analyzeOnly: false),
    "premium-analyze" => await RunPremiumImportAsync(args.Skip(1).ToArray(), analyzeOnly: true),
    "premium-import" => await RunPremiumImportAsync(args.Skip(1).ToArray(), analyzeOnly: false),
    "reports-analyze" => await RunReportPurchaseImportAsync(args.Skip(1).ToArray(), analyzeOnly: true),
    "reports-import" => await RunReportPurchaseImportAsync(args.Skip(1).ToArray(), analyzeOnly: false),
    "consultations-analyze" => await RunConsultationImportAsync(args.Skip(1).ToArray(), analyzeOnly: true),
    "consultations-import" => await RunConsultationImportAsync(args.Skip(1).ToArray(), analyzeOnly: false),
    _ => Unknown(command)
};

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

static async Task<int> RunBootstrapUsersAsync(string[] args)
{
    var options = ParseOptions(args);
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

    await using var provider = BuildProvider();
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
}

static async Task<int> RunImportCsvAsync(string[] args)
{
    var options = ParseOptions(args);
    var entity = options.GetValueOrDefault("--entity") ?? "leads";
    var file = options.GetValueOrDefault("--file");
    var batchSize = int.TryParse(options.GetValueOrDefault("--batch-size"), out var bs) ? Math.Clamp(bs, 50, 2000) : 500;
    var previewOnly = options.ContainsKey("--preview-only");
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;

    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
    {
        Console.Error.WriteLine("--file path is required and must exist.");
        return 2;
    }

    Console.WriteLine($"Reading {file} ...");
    var csvText = await File.ReadAllTextAsync(file);
    var parsed = CsvParser.Parse(csvText);
    if (!parsed.Success)
    {
        foreach (var err in parsed.ParseErrors)
        {
            Console.Error.WriteLine(err);
        }

        return 4;
    }

    Console.WriteLine($"Parsed rows: {parsed.Rows.Count}");
    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var engine = scope.ServiceProvider.GetRequiredService<ICsvImportEngine>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId} admin={snapshot.IsSystemAdmin}");

    var headers = parsed.Headers;
    var totalCreated = 0;
    var totalUpdated = 0;
    var totalSkipped = 0;
    var totalFailedBatches = 0;
    var batchIndex = 0;

    for (var offset = 0; offset < parsed.Rows.Count; offset += batchSize)
    {
        batchIndex++;
        var slice = parsed.Rows.Skip(offset).Take(batchSize).ToList();
        var batchCsv = CsvParser.ToCsv(headers, slice.Select(r => headers.Select(h => r.TryGetValue(h, out var v) ? v : "")));
        Console.WriteLine($"Batch {batchIndex}: rows {offset + 1}-{offset + slice.Count} ...");

        var preview = await engine.PreviewAsync(entity, batchCsv);
        Console.WriteLine($"  preview valid={preview.ValidCount} invalid={preview.InvalidCount} create={preview.CreateCount} update={preview.UpdateCount}");

        if (previewOnly)
        {
            totalSkipped += preview.InvalidCount;
            continue;
        }

        if (!preview.CanApply)
        {
            totalSkipped += preview.InvalidCount;
            continue;
        }

        var result = await engine.ApplyAsync(entity, preview);
        if (!result.Success)
        {
            totalFailedBatches++;
            Console.Error.WriteLine($"  APPLY FAILED: {string.Join("; ", result.Messages)}");
            continue;
        }

        totalCreated += result.Created;
        totalUpdated += result.Updated;
        totalSkipped += result.Skipped;
        Console.WriteLine($"  applied created={result.Created} updated={result.Updated} skipped={result.Skipped}");
    }

    var leadCount = await db.Leads.AsNoTracking().CountAsync(l => l.CompanyId == snapshot.CompanyId);
    Console.WriteLine("=== IMPORT SUMMARY ===");
    Console.WriteLine($"entity={entity}");
    Console.WriteLine($"created={totalCreated}");
    Console.WriteLine($"updated={totalUpdated}");
    Console.WriteLine($"skipped_invalid={totalSkipped}");
    Console.WriteLine($"failed_batches={totalFailedBatches}");
    Console.WriteLine($"leads_in_company_now={leadCount}");
    Console.WriteLine(previewOnly ? "MODE=preview-only" : "MODE=apply");
    return totalFailedBatches == 0 ? 0 : 7;
}

static async Task<int> RunPartyBackfillAsync(string[] args, bool analyzeOnly)
{
    var options = ParseOptions(args);
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;
    var batchSize = int.TryParse(options.GetValueOrDefault("--batch-size"), out var bs) ? Math.Clamp(bs, 50, 2000) : 200;
    var importedBy = options.GetValueOrDefault("--imported-by") ?? "party-backfill-tool";

    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var backfill = scope.ServiceProvider.GetRequiredService<ILeadPartyBackfillService>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId}");
    Console.WriteLine(analyzeOnly ? "MODE=party-analyze" : "MODE=party-backfill");

    var opts = new LeadPartyBackfillOptions
    {
        CompanyId = snapshot.CompanyId,
        BatchSize = batchSize,
        AnalyzeOnly = analyzeOnly,
        ImportedBy = importedBy
    };

    var report = analyzeOnly
        ? await backfill.AnalyzeAsync(opts)
        : await backfill.BackfillAsync(opts);

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var message in report.Messages)
    {
        Console.WriteLine(message);
    }

    return report.Success ? 0 : 8;
}

static async Task<int> RunStudentImportAsync(string[] args, bool analyzeOnly)
{
    var options = ParseOptions(args);
    var file = options.GetValueOrDefault("--file");
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;
    var importedBy = options.GetValueOrDefault("--imported-by") ?? "tools";

    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
    {
        Console.Error.WriteLine("--file path is required and must exist.");
        return 2;
    }

    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var importer = scope.ServiceProvider.GetRequiredService<IStudentImportService>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId}");
    Console.WriteLine(analyzeOnly ? "MODE=students-analyze" : "MODE=students-import");

    var opts = new StudentImportOptions
    {
        CompanyId = snapshot.CompanyId,
        FilePath = file,
        AnalyzeOnly = analyzeOnly,
        ImportedBy = importedBy
    };

    var report = analyzeOnly
        ? await importer.AnalyzeAsync(opts)
        : await importer.ImportAsync(opts);

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var message in report.Messages)
    {
        Console.WriteLine(message);
    }

    if (!analyzeOnly && report.Conflicts > 0)
    {
        Console.Error.WriteLine($"WARNING: {report.Conflicts} identity conflicts encountered.");
    }

    return report.Success ? 0 : 8;
}

static async Task<int> RunPremiumImportAsync(string[] args, bool analyzeOnly)
{
    var options = ParseOptions(args);
    var file = options.GetValueOrDefault("--file");
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;
    var importedBy = options.GetValueOrDefault("--imported-by") ?? "tools";

    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
    {
        Console.Error.WriteLine("--file path is required and must exist.");
        return 2;
    }

    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var importer = scope.ServiceProvider.GetRequiredService<IPremiumSubscriberImportService>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId}");
    Console.WriteLine(analyzeOnly ? "MODE=premium-analyze" : "MODE=premium-import");

    var opts = new PremiumSubscriberImportOptions
    {
        CompanyId = snapshot.CompanyId,
        FilePath = file,
        AnalyzeOnly = analyzeOnly,
        ImportedBy = importedBy
    };

    var report = analyzeOnly
        ? await importer.AnalyzeAsync(opts)
        : await importer.ImportAsync(opts);

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var message in report.Messages)
    {
        Console.WriteLine(message);
    }

    var conflicts = report.Active.Conflicts + report.Inactive.Conflicts;
    if (!analyzeOnly && conflicts > 0)
    {
        Console.Error.WriteLine($"WARNING: {conflicts} identity conflicts encountered.");
    }

    return report.Success ? 0 : 8;
}

static async Task<int> RunReportPurchaseImportAsync(string[] args, bool analyzeOnly)
{
    var options = ParseOptions(args);
    var file = options.GetValueOrDefault("--file");
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;
    var importedBy = options.GetValueOrDefault("--imported-by") ?? "tools";

    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
    {
        Console.Error.WriteLine("--file path is required and must exist.");
        return 2;
    }

    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var importer = scope.ServiceProvider.GetRequiredService<IReportPurchaseImportService>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId}");
    Console.WriteLine(analyzeOnly ? "MODE=reports-analyze" : "MODE=reports-import");

    var opts = new ReportPurchaseImportOptions
    {
        CompanyId = snapshot.CompanyId,
        FilePath = file,
        AnalyzeOnly = analyzeOnly,
        ImportedBy = importedBy
    };

    var report = analyzeOnly
        ? await importer.AnalyzeAsync(opts)
        : await importer.ImportAsync(opts);

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var message in report.Messages)
    {
        Console.WriteLine(message);
    }

    if (!analyzeOnly && report.Conflicts > 0)
    {
        Console.Error.WriteLine($"WARNING: {report.Conflicts} identity conflicts encountered.");
    }

    return report.Success ? 0 : 8;
}

static async Task<int> RunConsultationImportAsync(string[] args, bool analyzeOnly)
{
    var options = ParseOptions(args);
    var file = options.GetValueOrDefault("--file");
    var companyIdOverride = int.TryParse(options.GetValueOrDefault("--company-id"), out var cid) ? cid : (int?)null;
    var importedBy = options.GetValueOrDefault("--imported-by") ?? "tools";

    if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
    {
        Console.Error.WriteLine("--file path is required and must exist.");
        return 2;
    }

    await using var provider = BuildProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ShaunErpDbContext>();
    var authz = scope.ServiceProvider.GetRequiredService<IAuthorizationContextService>();
    var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
    var importer = scope.ServiceProvider.GetRequiredService<IConsultationImportService>();

    var admin = await db.Users.AsNoTracking()
        .Where(u => u.IsActive && (u.IsSystemAdmin || u.Email == "shauninfomediaopc@gmail.com"))
        .OrderByDescending(u => u.IsSystemAdmin)
        .FirstOrDefaultAsync();
    if (admin is null)
    {
        Console.Error.WriteLine("No active admin user found.");
        return 5;
    }

    var snapshot = await authz.BuildForUserAsync(admin.Id, companyIdOverride);
    if (snapshot.CompanyId is null)
    {
        Console.Error.WriteLine("Could not resolve company context for admin.");
        return 6;
    }

    tenant.Set(snapshot);
    Console.WriteLine($"Tenant: companyId={snapshot.CompanyId} userId={snapshot.UserId}");
    Console.WriteLine(analyzeOnly ? "MODE=consultations-analyze" : "MODE=consultations-import");

    var opts = new ConsultationImportOptions
    {
        CompanyId = snapshot.CompanyId,
        FilePath = file,
        AnalyzeOnly = analyzeOnly,
        ImportedBy = importedBy
    };

    var report = analyzeOnly
        ? await importer.AnalyzeAsync(opts)
        : await importer.ImportAsync(opts);

    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var message in report.Messages)
    {
        Console.WriteLine(message);
    }

    if (!analyzeOnly && report.Conflicts > 0)
    {
        Console.Error.WriteLine($"WARNING: {report.Conflicts} identity conflicts encountered.");
    }

    return report.Success ? 0 : 8;
}

static ServiceProvider BuildProvider()
{
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
    services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
    services.AddSingleton<IConfiguration>(config);
    services.AddInfrastructure(config);
    return services.BuildServiceProvider();
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        if (args[i] is "--preview-only")
        {
            map[args[i]] = "true";
            continue;
        }

        if (i + 1 < args.Length)
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
          import-csv --entity leads --file <path.csv> [--batch-size 500] [--preview-only] [--company-id N]
          party-analyze [--company-id N] [--batch-size 200]
          party-backfill [--company-id N] [--batch-size 200] [--imported-by name]
          students-analyze --file <path.xlsx> [--company-id N]
          students-import --file <path.xlsx> [--company-id N] [--imported-by name]
          premium-analyze --file <path.xlsx> [--company-id N]
          premium-import --file <path.xlsx> [--company-id N] [--imported-by name]
          reports-analyze --file <path.csv> [--company-id N]
          reports-import --file <path.csv> [--company-id N] [--imported-by name]
          consultations-analyze --file <path.xlsx> [--company-id N]
          consultations-import --file <path.xlsx> [--company-id N] [--imported-by name]

        Connection string:
          ConnectionStrings__DefaultConnection
        """);
}
