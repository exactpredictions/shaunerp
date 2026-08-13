using ShaunERP.Core.ImportExport;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public abstract class EntityCsvImporterBase : IEntityCsvImporter
{
    public abstract string EntityKey { get; }
    public abstract string DisplayName { get; }
    public abstract IReadOnlyList<CsvColumnDefinition> Columns { get; }

    public abstract Task<CsvImportPreview> PreviewAsync(CsvParseResult parsed, int companyId, CancellationToken ct = default);
    public abstract Task<CsvImportResult> ApplyAsync(CsvImportPreview preview, int companyId, CancellationToken ct = default);
    public abstract Task<CsvExportResult> ExportAsync(CsvExportRequest request, CancellationToken ct = default);

    public virtual string BuildSampleCsv()
    {
        var headers = Columns.Select(c => c.Key);
        var sample = Columns.Select(c => c.Example ?? (c.Required ? $"SAMPLE-{c.Key}" : string.Empty));
        return CsvParser.ToCsv(headers, [sample]);
    }

    protected static CsvImportPreview BuildPreview(
        string entityKey,
        CsvParseResult parsed,
        IReadOnlyList<CsvRowValidation> rows) =>
        new()
        {
            EntityKey = entityKey,
            Headers = parsed.Headers,
            Rows = rows,
            HeaderErrors = Array.Empty<string>()
        };

    protected static CsvImportResult EmptyApply(string entityKey) =>
        new()
        {
            EntityKey = entityKey,
            Success = false,
            Messages = ["No valid rows to apply."]
        };

    protected string BuildSampleCsv(params IReadOnlyDictionary<string, string>[] rows)
    {
        var headers = Columns.Select(c => c.Key).ToList();
        var data = rows.Select(r => headers.Select(h => r.TryGetValue(h, out var v) ? v : string.Empty));
        return CsvParser.ToCsv(headers, data);
    }
}
