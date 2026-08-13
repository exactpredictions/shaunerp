using ShaunERP.Core.ImportExport;
using ShaunERP.Core.Platform;
using ShaunERP.Infrastructure.Platform;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public interface ICsvImportEngine
{
    IReadOnlyList<IEntityCsvImporter> Importers { get; }
    IEntityCsvImporter? GetImporter(string entityKey);
    Task<CsvImportPreview> PreviewAsync(string entityKey, string csvContent, CancellationToken ct = default);
    Task<CsvImportResult> ApplyAsync(string entityKey, CsvImportPreview preview, CancellationToken ct = default);
    Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default);
    string GetSampleCsv(string entityKey);
}

public sealed class CsvImportEngine : ICsvImportEngine
{
    private readonly IReadOnlyDictionary<string, IEntityCsvImporter> _importers;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public CsvImportEngine(
        IEnumerable<IEntityCsvImporter> importers,
        ITenantContext tenant,
        IAuditService audit)
    {
        _importers = importers.ToDictionary(i => i.EntityKey, StringComparer.OrdinalIgnoreCase);
        _tenant = tenant;
        _audit = audit;
        Importers = _importers.Values.OrderBy(i => i.DisplayName).ToList();
    }

    public IReadOnlyList<IEntityCsvImporter> Importers { get; }

    public IEntityCsvImporter? GetImporter(string entityKey) =>
        _importers.TryGetValue(entityKey, out var importer) ? importer : null;

    public async Task<CsvImportPreview> PreviewAsync(string entityKey, string csvContent, CancellationToken ct = default)
    {
        EnsurePermission();
        var companyId = RequireCompanyId();
        var importer = RequireImporter(entityKey);
        var parsed = CsvParser.Parse(csvContent);
        if (!parsed.Success)
        {
            return new CsvImportPreview
            {
                EntityKey = entityKey,
                HeaderErrors = parsed.ParseErrors,
                Headers = parsed.Headers,
                Rows = Array.Empty<CsvRowValidation>()
            };
        }

        var required = importer.Columns.Where(c => c.Required).Select(c => c.Key).ToList();
        var headerErrors = new List<string>();
        foreach (var key in required)
        {
            if (!parsed.Headers.Any(h => h.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                headerErrors.Add($"Missing required column '{key}'.");
            }
        }

        if (headerErrors.Count > 0)
        {
            return new CsvImportPreview
            {
                EntityKey = entityKey,
                Headers = parsed.Headers,
                HeaderErrors = headerErrors,
                Rows = Array.Empty<CsvRowValidation>()
            };
        }

        return await importer.PreviewAsync(parsed, companyId, ct);
    }

    public async Task<CsvImportResult> ApplyAsync(string entityKey, CsvImportPreview preview, CancellationToken ct = default)
    {
        EnsurePermission();
        var companyId = RequireCompanyId();
        var importer = RequireImporter(entityKey);
        if (!preview.CanApply)
        {
            return new CsvImportResult
            {
                EntityKey = entityKey,
                Success = false,
                Failed = preview.InvalidCount,
                Messages = ["Preview has no valid rows to apply."],
                ErrorCsv = BuildErrorCsv(preview)
            };
        }

        var correlationId = Guid.NewGuid().ToString("N");
        var result = await importer.ApplyAsync(preview, companyId, ct);
        result = new CsvImportResult
        {
            EntityKey = result.EntityKey,
            Success = result.Success,
            Created = result.Created,
            Updated = result.Updated,
            Skipped = result.Skipped,
            Failed = result.Failed,
            Messages = result.Messages,
            ErrorCsv = result.ErrorCsv ?? BuildErrorCsv(preview),
            CorrelationId = correlationId
        };

        await _audit.WriteAsync(
            "csv.import.apply",
            companyId: companyId,
            userId: _tenant.UserId,
            entityType: entityKey,
            entityId: correlationId,
            afterValues: $"created={result.Created};updated={result.Updated};failed={result.Failed}",
            correlationId: correlationId,
            ct: ct);

        return result;
    }

    public async Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default)
    {
        EnsurePermission();
        var companyId = request.CompanyId ?? RequireCompanyId();
        request.CompanyId = companyId;
        var importer = RequireImporter(request.EntityKey);
        var result = await importer.ExportAsync(request, ct);
        await _audit.WriteAsync(
            "csv.export",
            companyId: companyId,
            userId: _tenant.UserId,
            entityType: request.EntityKey,
            entityId: result.RowCount.ToString(),
            ct: ct);
        return result;
    }

    public string GetSampleCsv(string entityKey)
    {
        EnsurePermission();
        return RequireImporter(entityKey).BuildSampleCsv();
    }

    private IEntityCsvImporter RequireImporter(string entityKey) =>
        GetImporter(entityKey)
        ?? throw new InvalidOperationException($"No CSV importer registered for '{entityKey}'.");

    private void EnsurePermission()
    {
        if (_tenant.IsSystemAdmin)
        {
            return;
        }

        if (!_tenant.Permissions.Contains(Permissions.SettingsImportExport))
        {
            throw new UnauthorizedAccessException("Import/export permission required.");
        }
    }

    private int RequireCompanyId() =>
        _tenant.CompanyId ?? throw new InvalidOperationException("Company context is required.");

    private static string BuildErrorCsv(CsvImportPreview preview)
    {
        var headers = preview.Headers.Concat(["__errors", "__warnings", "__action"]).ToList();
        var rows = preview.Rows.Where(r => !r.IsValid || r.Warnings.Count > 0).Select(r =>
        {
            var values = preview.Headers.Select(h => r.Values.TryGetValue(h, out var v) ? v : string.Empty)
                .Concat([string.Join("; ", r.Errors), string.Join("; ", r.Warnings), r.Action]);
            return values;
        });
        return CsvParser.ToCsv(headers, rows);
    }
}

public interface ICsvExportService
{
    Task<CsvExportResult> ExportAsync(string entityKey, CancellationToken ct = default);
    Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default);
}

public sealed class CsvExportService : ICsvExportService
{
    private readonly ICsvImportEngine _engine;

    public CsvExportService(ICsvImportEngine engine) => _engine = engine;

    public Task<CsvExportResult> ExportAsync(string entityKey, CancellationToken ct = default) =>
        _engine.ExportAsync(new CsvExportRequest { EntityKey = entityKey }, ct);

    public Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default) =>
        _engine.ExportAsync(request, ct);
}
