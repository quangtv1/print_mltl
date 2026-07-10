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
    private int _row;   // số dòng vật lý (1-based) vừa đọc — mọi lần đọc đi qua ReadRow()

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

    // "Dòng trống" = mọi ô (đã Trim) đều rỗng — dùng chung cho skip-đếm header và bỏ dòng trắng ở ReadRows.
    private bool CurrentRowHasContent(int fieldCount)
    {
        for (int i = 0; i < fieldCount; i++)
            if (FormatCell(_reader.GetValue(i)).Length > 0) return true;
        return false;
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

    // Đọc 1 dòng vật lý, tăng bộ đếm dòng. Mọi lối đọc phải đi qua đây để _row luôn = dòng Excel thật.
    private bool ReadRow()
    {
        if (!_reader.Read()) return false;
        _row++;
        return true;
    }

    // Đọc tiến tới dòng KHÔNG-trống kế tiếp; false nếu hết dòng.
    private bool MoveToNextNonEmptyRow()
    {
        while (ReadRow())
            if (CurrentRowHasContent(_reader.FieldCount)) return true;
        return false;
    }

    private void ReadHeaderRow(int startRow)
    {
        // startRow = số dòng Excel VẬT LÝ (1-based) để bắt đầu tìm tiêu đề. Bỏ đúng startRow−1 dòng vật lý
        // (kể cả dòng trống — ExcelDataReader vẫn trả dòng trống), rồi lấy dòng KHÔNG-trống đầu tiên làm header.
        for (int skip = 0; skip < startRow - 1; skip++)
            if (!ReadRow())
                throw new InvalidOperationException($"Không đủ dữ liệu: file không có tới dòng {startRow}.");
        if (!MoveToNextNonEmptyRow())
            throw new InvalidOperationException($"Không tìm thấy dòng tiêu đề từ dòng {startRow} trở đi.");
        _headers = new List<string>(_reader.FieldCount);
        for (int i = 0; i < _reader.FieldCount; i++)
            _headers.Add(FormatCell(_reader.GetValue(i)).Trim());
        // cắt các cột header rỗng ở đuôi
        while (_headers.Count > 0 && string.IsNullOrEmpty(_headers[^1])) _headers.RemoveAt(_headers.Count - 1);
    }

    public IReadOnlyList<string> Headers => _headers;

    public IEnumerable<RowRecord> ReadRows()
    {
        while (ReadRow())
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
            yield return new RowRecord(dict) { SourceRow = _row };
        }
    }

    public void Dispose() { _reader.Dispose(); _fs.Dispose(); }
}
