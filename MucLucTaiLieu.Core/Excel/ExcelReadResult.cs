namespace MucLucTaiLieu.Core.Excel;

/// <summary>Result of reading a sheet: header names (row 1) and one dictionary per data row.</summary>
public sealed class ExcelReadResult
{
    public List<string> Headers { get; init; } = new();
    public List<Dictionary<string, string>> Rows { get; init; } = new();
}

/// <summary>Reads .xlsx workbooks into plain string data (mota3 §2).</summary>
public interface IExcelReader
{
    /// <summary>List worksheet names in the workbook.</summary>
    IReadOnlyList<string> ListSheets(string path);

    /// <summary>Read a sheet: header row (row 1) + every subsequent used row.</summary>
    ExcelReadResult Read(string path, string sheet);
}
