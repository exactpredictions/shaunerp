using ShaunERP.Infrastructure.Services.ImportExport;

namespace ShaunERP.Tests;

public class CsvParserTests
{
    [Fact]
    public void Escape_prefixes_phone_so_excel_does_not_treat_as_formula()
    {
        var escaped = CsvParser.Escape("+91-9000000001");
        Assert.Equal("'+91-9000000001", escaped);
    }

    [Fact]
    public void Parse_strips_excel_text_marker_from_phone()
    {
        var csv = "Email,Phone\nsample@example.com,'+91-9000000001\n";
        var result = CsvParser.Parse(csv);
        Assert.Empty(result.ParseErrors);
        Assert.Equal("+91-9000000001", result.Rows[0]["Phone"]);
    }

    [Fact]
    public void Roundtrip_preserves_phone_literal()
    {
        var csv = CsvParser.ToCsv(
            ["Phone"],
            [["+91-9000000001"]]);
        var parsed = CsvParser.Parse(csv);
        Assert.Equal("+91-9000000001", parsed.Rows[0]["Phone"]);
    }
}
