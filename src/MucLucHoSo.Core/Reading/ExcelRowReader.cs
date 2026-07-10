using ExcelDataReader;
using MucLucHoSo.Core.Models;
using System.Globalization;
using System.Text;

namespace MucLucHoSo.Core.Reading;

/// <summary>Đọc XLSX/XLSB theo dòng bằng ExcelDataReader (streaming, RAM thấp).</summary>
public sealed class ExcelRowReader : IRowReader
{
    private readonly FileStream _fs;
    private readonly IExcelDataReader _reader;
    private readonly string _sheet;
    private List<string> _headers = new();

    static ExcelRowReader() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // cần cho XLSB/legacy

    public ExcelRowReader(string path, string sheetName, int startRow = 1)
    {
        _sheet = sheetName;
        _fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _reader = ExcelReaderFactory.CreateReader(_fs); // tự nhận XLSX/XLSB
        MoveToSheet(_sheet);
        ReadHeaderRow(startRow);
    }

    private void MoveToSheet(string sheet)
    {
        do { if (string.Equals(_reader.Name, sheet, StringComparison.Ordinal)) return; }
        while (_reader.NextResult());
        throw new InvalidOperationException($"Không tìm thấy sheet '{sheet}'.");
    }

    // ExcelDataReader trả ô số = double, ô ngày = DateTime. Format cố định theo InvariantCulture để không
    // phụ thuộc locale máy (tránh ngày dính "00:00:00", số ra dạng mũ, hay dấu phẩy thập phân theo vùng).
    private static string FormatCell(object? value) => value switch
    {
        null => string.Empty,
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : dt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
        // Số nguyên → không phần thập phân, không ký hiệu mũ, không dấu phân tách nghìn.
        double d => d == Math.Floor(d) && !double.IsInfinity(d)
            ? d.ToString("F0", CultureInfo.InvariantCulture)
            : d.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        _ => value.ToString()?.Trim() ?? string.Empty,
    };

    private void ReadHeaderRow(int startRow)
    {
        // startRow = số dòng Excel VẬT LÝ (1-based) của dòng tiêu đề. Đọc ĐÚNG dòng đó làm header — không bỏ
        // dòng trống, không trượt. ExcelDataReader trả cả dòng trống nên đọc startRow lần = đúng dòng startRow.
        for (int skip = 0; skip < startRow; skip++)
            if (!_reader.Read())
                throw new InvalidOperationException($"Không đủ dữ liệu: file không có tới dòng {startRow}.");
        _headers = new List<string>(_reader.FieldCount);
        for (int i = 0; i < _reader.FieldCount; i++)
            _headers.Add(FormatCell(_reader.GetValue(i)).Trim());
        // cắt các cột header rỗng ở đuôi
        while (_headers.Count > 0 && string.IsNullOrEmpty(_headers[^1])) _headers.RemoveAt(_headers.Count - 1);
    }

    public IReadOnlyList<string> Headers => _headers;

    public IEnumerable<RowRecord> ReadRows()
    {
        while (_reader.Read())
        {
            var dict = new Dictionary<string, string>(_headers.Count, StringComparer.Ordinal);
            bool anyValue = false;
            for (int i = 0; i < _headers.Count; i++)
            {
                var v = FormatCell(_reader.GetValue(i)).Trim();
                if (v.Length > 0) anyValue = true;
                dict[_headers[i]] = v;
            }
            if (!anyValue) continue; // bỏ dòng trắng hoàn toàn
            yield return new RowRecord(dict);
        }
    }

    public void Dispose() { _reader.Dispose(); _fs.Dispose(); }
}
