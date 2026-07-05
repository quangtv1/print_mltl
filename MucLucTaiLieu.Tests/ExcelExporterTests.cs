using MucLucTaiLieu.Core.Excel;

namespace MucLucTaiLieu.Tests;

public class ExcelExporterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mltl-xlsx-out-" + Guid.NewGuid().ToString("N"));
    public ExcelExporterTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@import", "'@import")]
    [InlineData("-run", "'-run")]
    [InlineData("\t=danger", "'\t=danger")] // tab before formula still guarded
    public void Sanitize_FormulaLikeText_IsPrefixed(string input, string expected)
    {
        Assert.Equal(expected, ExcelExporter.Sanitize(input));
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("-3.14")]
    [InlineData("+7")]
    [InlineData("42359")]
    [InlineData("Nguyễn Công Tùng")]
    [InlineData("")]
    public void Sanitize_NumbersAndPlainText_Unchanged(string input)
    {
        Assert.Equal(input, ExcelExporter.Sanitize(input));
    }

    [Fact]
    public void Export_NeutralizesFormulasAsTextAndKeepsNumbers()
    {
        var path = Path.Combine(_dir, "summary.xlsx");
        new ExcelExporter().Export(
            path,
            headers: new[] { "STT", "Trích yếu" },
            rows: new IReadOnlyList<string>[]
            {
                new[] { "1", "=EVIL()" },
                new[] { "2", "-5" },
            });

        using var wb = new ClosedXML.Excel.XLWorkbook(path);
        var ws = wb.Worksheet("Tổng hợp");

        Assert.Equal("STT", ws.Cell(1, 1).GetString());
        Assert.Equal("Trích yếu", ws.Cell(1, 2).GetString());

        var danger = ws.Cell(2, 2);
        Assert.False(danger.HasFormula);                    // stored as text, never a live formula
        Assert.True(danger.Style.IncludeQuotePrefix);       // forced-text (leading apostrophe in Excel)

        var number = ws.Cell(3, 2);
        Assert.Equal("-5", number.GetString());             // negative number preserved
        Assert.False(number.Style.IncludeQuotePrefix);      // not guarded
    }
}
