namespace MucLucHoSo.Core.Reading;

public static class ReaderFactory
{
    public static IRowReader Open(string path, string? sheetName = null, string? csvDelimiter = null, int startRow = 1)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" or ".tsv" => new CsvRowReader(path, csvDelimiter, startRow),
            ".xlsx" or ".xlsb" or ".xls" => new ExcelRowReader(path,
                sheetName ?? throw new ArgumentException("Cần chỉ định sheet cho Excel."), startRow),
            _ => throw new NotSupportedException($"Định dạng không hỗ trợ: {ext}")
        };
    }
}
