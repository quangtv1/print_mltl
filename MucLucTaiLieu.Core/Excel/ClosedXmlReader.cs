using System.Globalization;
using ClosedXML.Excel;

namespace MucLucTaiLieu.Core.Excel;

/// <summary>
/// ClosedXML-backed .xlsx reader. Coerces every cell to a stable, culture-invariant
/// string (numbers keep no spurious ".0", dates render dd/MM/yyyy) and fails fast with
/// Vietnamese messages on oversized files or overly long cells (mota3 §2).
/// </summary>
public sealed class ClosedXmlReader : IExcelReader
{
    private readonly long _maxFileBytes;
    private readonly int _maxCellChars;

    /// <param name="maxFileBytes">Reject files larger than this (default 50 MB).</param>
    /// <param name="maxCellChars">Reject cells longer than this (default 32767, the Excel limit).</param>
    public ClosedXmlReader(long maxFileBytes = 50L * 1024 * 1024, int maxCellChars = 32767)
    {
        _maxFileBytes = maxFileBytes;
        _maxCellChars = maxCellChars;
    }

    public IReadOnlyList<string> ListSheets(string path)
    {
        using var wb = Open(path);
        return wb.Worksheets.Select(ws => ws.Name).ToList();
    }

    public ExcelReadResult Read(string path, string sheet)
    {
        using var wb = Open(path);
        if (!wb.Worksheets.TryGetWorksheet(sheet, out var ws))
            throw new InvalidDataException($"Không tìm thấy sheet \"{sheet}\" trong tệp Excel.");

        var result = new ExcelReadResult();

        var headerRow = ws.Row(1);
        // (column number, header text) for each used header cell.
        var columns = new List<(int col, string name)>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = CellToString(cell);
            if (name.Length == 0) continue;
            columns.Add((cell.Address.ColumnNumber, name));
            result.Headers.Add(name);
        }

        if (columns.Count == 0)
            throw new InvalidDataException($"Sheet \"{sheet}\" không có dòng tiêu đề (dòng 1 trống).");

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            var record = new Dictionary<string, string>(columns.Count, StringComparer.Ordinal);
            foreach (var (col, name) in columns)
                record[name] = CellToString(row.Cell(col)); // duplicate headers: last wins
            result.Rows.Add(record);
        }

        return result;
    }

    private IXLWorkbook Open(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Không tìm thấy tệp Excel: {path}");

        var size = new FileInfo(path).Length;
        if (size > _maxFileBytes)
            throw new InvalidDataException(
                $"Tệp Excel quá lớn ({size / (1024 * 1024)} MB, giới hạn {_maxFileBytes / (1024 * 1024)} MB).");

        try
        {
            return new XLWorkbook(path);
        }
        catch (Exception ex) when (ex is not InvalidDataException and not FileNotFoundException)
        {
            throw new InvalidDataException($"Không đọc được tệp Excel (tệp hỏng hoặc sai định dạng): {Path.GetFileName(path)}", ex);
        }
    }

    private string CellToString(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";

        string s = cell.DataType switch
        {
            XLDataType.Number   => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            XLDataType.Boolean  => cell.GetBoolean() ? "TRUE" : "FALSE",
            _                   => cell.GetString(),
        };

        if (s.Length > _maxCellChars)
            throw new InvalidDataException(
                $"Ô {cell.Address} quá dài ({s.Length} ký tự, giới hạn {_maxCellChars}).");
        return s;
    }
}
