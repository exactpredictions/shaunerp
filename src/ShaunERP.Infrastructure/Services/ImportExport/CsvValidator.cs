using System.Globalization;
using System.Text.RegularExpressions;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public static partial class CsvValidator
{
    private static readonly Regex EmailRegex = EmailPattern();
    private static readonly Regex GstinRegex = GstinPattern();
    private static readonly Regex PanRegex = PanPattern();

    public static bool TryEmail(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!EmailRegex.IsMatch(value.Trim()))
        {
            error = $"Invalid email '{value}'.";
            return false;
        }

        return true;
    }

    public static bool TryRequired(string? value, string field, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{field} is required.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryDate(string? value, out DateTime? date, out string? error)
    {
        date = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var formats = new[]
        {
            "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy",
            "yyyy/MM/dd", "dd-MMM-yyyy"
        };

        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            || DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
            return true;
        }

        error = $"Invalid date '{value}'.";
        return false;
    }

    public static bool TryDecimal(string? value, out decimal? number, out string? error)
    {
        number = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            number = parsed;
            return true;
        }

        error = $"Invalid decimal '{value}'.";
        return false;
    }

    public static bool TryGstin(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var v = value.Trim().ToUpperInvariant();
        if (!GstinRegex.IsMatch(v))
        {
            error = $"Invalid GSTIN '{value}' (expected 15-character Indian GSTIN).";
            return false;
        }

        return true;
    }

    public static bool TryPan(string? value, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var v = value.Trim().ToUpperInvariant();
        if (!PanRegex.IsMatch(v))
        {
            error = $"Invalid PAN '{value}' (expected AAAAA9999A).";
            return false;
        }

        return true;
    }

    public static string Get(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var v) ? v?.Trim() ?? string.Empty : string.Empty;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmailPattern();

    // 15-char GSTIN: 2 digit state + 10 char PAN + entity + Z + checksum
    [GeneratedRegex(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GstinPattern();

    [GeneratedRegex(@"^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PanPattern();
}
