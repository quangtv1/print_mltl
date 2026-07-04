using ClosedXML.Excel;
using MucLucTaiLieu.Core.Excel;

namespace MucLucTaiLieu.Tests;

public class ClosedXmlReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mltl-xlsx-" + Guid.NewGuid().ToString("N"));

    public ClosedXmlReaderTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    private string MakeWorkbook(string sheet, Action<IXLWorksheet> build, string? file = null)
    {
        var path = Path.Combine(_dir, file ?? Guid.NewGuid().ToString("N") + ".xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheet);
        build(ws);
        wb.SaveAs(path);
        return path;
    }

    [Fact]
    public void Read_ReturnsHeadersAndRows_EmptyCellIsEmptyString()
    {
        var path = MakeWorkbook("Data", ws =>
        {
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Tác giả";
            ws.Cell(2, 1).Value = "1";
            ws.Cell(2, 2).Value = "UBND";
            ws.Cell(3, 1).Value = "2";
            // (3,2) left blank on purpose
        });

        var result = new ClosedXmlReader().Read(path, "Data");

        Assert.Equal(new[] { "STT", "Tác giả" }, result.Headers);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("UBND", result.Rows[0]["Tác giả"]);
        Assert.Equal("", result.Rows[1]["Tác giả"]); // blank cell -> ""
    }

    [Fact]
    public void Read_NumericCell_HasNoTrailingPointZero()
    {
        var path = MakeWorkbook("Data", ws =>
        {
            ws.Cell(1, 1).Value = "SoTo";
            ws.Cell(1, 2).Value = "Trang";
            ws.Cell(2, 1).Value = 12;      // integer stored as number
            ws.Cell(2, 2).Value = 12.5;
        });

        var row = new ClosedXmlReader().Read(path, "Data").Rows[0];

        Assert.Equal("12", row["SoTo"]);   // not "12.0"
        Assert.Equal("12.5", row["Trang"]);
    }

    [Fact]
    public void ListSheets_ReturnsAllSheetNames()
    {
        var path = MakeWorkbook("Alpha", ws => ws.Cell(1, 1).Value = "x");
        using (var wb = new XLWorkbook(path)) { wb.Worksheets.Add("Beta"); wb.Save(); }

        var sheets = new ClosedXmlReader().ListSheets(path);

        Assert.Equal(new[] { "Alpha", "Beta" }, sheets);
    }

    [Fact]
    public void Read_MissingSheet_ThrowsVietnameseError()
    {
        var path = MakeWorkbook("Data", ws => ws.Cell(1, 1).Value = "H");
        var ex = Assert.Throws<InvalidDataException>(() => new ClosedXmlReader().Read(path, "KhongCo"));
        Assert.Contains("sheet", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_CorruptFile_ThrowsVietnameseError()
    {
        var path = Path.Combine(_dir, "broken.xlsx");
        File.WriteAllText(path, "this is not a real xlsx");
        var ex = Assert.Throws<InvalidDataException>(() => new ClosedXmlReader().Read(path, "Data"));
        Assert.Contains("Excel", ex.Message);
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => new ClosedXmlReader().Read(Path.Combine(_dir, "nope.xlsx"), "Data"));
    }

    [Fact]
    public void Read_FileExceedingSizeLimit_FailsFast()
    {
        var path = MakeWorkbook("Data", ws => ws.Cell(1, 1).Value = "H");
        var reader = new ClosedXmlReader(maxFileBytes: 10); // any real xlsx exceeds 10 bytes
        var ex = Assert.Throws<InvalidDataException>(() => reader.Read(path, "Data"));
        Assert.Contains("quá lớn", ex.Message);
    }

    [Fact]
    public void Read_CellExceedingLengthLimit_FailsFast()
    {
        var path = MakeWorkbook("Data", ws =>
        {
            ws.Cell(1, 1).Value = "H";
            ws.Cell(2, 1).Value = new string('x', 50);
        });
        var reader = new ClosedXmlReader(maxCellChars: 5);
        var ex = Assert.Throws<InvalidDataException>(() => reader.Read(path, "Data"));
        Assert.Contains("quá dài", ex.Message);
    }
}
