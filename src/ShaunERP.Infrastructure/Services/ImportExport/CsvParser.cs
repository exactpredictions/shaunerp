using System.Text;
using ShaunERP.Core.ImportExport;

namespace ShaunERP.Infrastructure.Services.ImportExport;

public static class CsvParser
{
    public static CsvParseResult Parse(string csvText)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return new CsvParseResult
            {
                ParseErrors = ["CSV content is empty."]
            };
        }

        var lines = SplitLines(csvText);
        if (lines.Count == 0)
        {
            return new CsvParseResult { ParseErrors = ["CSV has no rows."] };
        }

        List<string> headers;
        try
        {
            headers = ParseLine(lines[0]);
        }
        catch (Exception ex)
        {
            return new CsvParseResult { ParseErrors = [$"Header parse error: {ex.Message}"] };
        }

        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            return new CsvParseResult { ParseErrors = ["CSV header row is empty."] };
        }

        var normalizedHeaders = headers.Select(NormalizeHeader).ToList();
        var rows = new List<IReadOnlyDictionary<string, string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var fields = ParseLine(line);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var c = 0; c < normalizedHeaders.Count; c++)
                {
                    var key = normalizedHeaders[c];
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    dict[key] = c < fields.Count ? fields[c].Trim() : string.Empty;
                }

                rows.Add(dict);
            }
            catch (Exception ex)
            {
                errors.Add($"Row {i + 1}: {ex.Message}");
            }
        }

        return new CsvParseResult
        {
            Headers = normalizedHeaders,
            Rows = rows,
            ParseErrors = errors
        };
    }

    public static string ToCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        }

        return sb.ToString();
    }

    public static string Escape(string? value)
    {
        value ??= string.Empty;
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return needsQuotes ? $"\"{value}\"" : value;
    }

    public static string NormalizeHeader(string header) =>
        (header ?? string.Empty).Trim().Trim('\uFEFF');

    private static List<string> SplitLines(string text)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                sb.Append(ch);
                continue;
            }

            if (!inQuotes && (ch == '\n' || ch == '\r'))
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0 || result.Count == 0)
        {
            result.Add(sb.ToString());
        }

        return result;
    }

    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(ch);
                }
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
