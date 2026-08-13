namespace ShaunERP.Core.ImportExport;

public sealed class CsvColumnDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? Description { get; set; }
    public string? Example { get; set; }
}

public sealed class CsvParseResult
{
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } = Array.Empty<IReadOnlyDictionary<string, string>>();
    public IReadOnlyList<string> ParseErrors { get; init; } = Array.Empty<string>();
    public bool Success => ParseErrors.Count == 0;
}

public sealed class CsvRowValidation
{
    public int RowNumber { get; init; }
    public bool IsValid { get; init; }
    public string Action { get; init; } = "Create";
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public string? MatchKey { get; init; }
    public int? ExistingId { get; init; }
}

public sealed class CsvImportPreview
{
    public string EntityKey { get; init; } = string.Empty;
    public IReadOnlyList<string> Headers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CsvRowValidation> Rows { get; init; } = Array.Empty<CsvRowValidation>();
    public int ValidCount => Rows.Count(r => r.IsValid);
    public int InvalidCount => Rows.Count(r => !r.IsValid);
    public int CreateCount => Rows.Count(r => r.IsValid && r.Action.Equals("Create", StringComparison.OrdinalIgnoreCase));
    public int UpdateCount => Rows.Count(r => r.IsValid && r.Action.Equals("Update", StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<string> HeaderErrors { get; init; } = Array.Empty<string>();
    public bool CanApply => HeaderErrors.Count == 0 && ValidCount > 0;
}

public sealed class CsvImportResult
{
    public string EntityKey { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    public string? ErrorCsv { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class CsvExportRequest
{
    public string EntityKey { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public bool IncludeInactive { get; set; }
    public string? Search { get; set; }
}

public sealed class CsvExportResult
{
    public string FileName { get; init; } = "export.csv";
    public string ContentType { get; init; } = "text/csv; charset=utf-8";
    public string CsvContent { get; init; } = string.Empty;
    public int RowCount { get; init; }
}

public interface IEntityCsvImporter
{
    string EntityKey { get; }
    string DisplayName { get; }
    IReadOnlyList<CsvColumnDefinition> Columns { get; }
    Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default);
    Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default);
    Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default);
    string BuildSampleCsv();
}
