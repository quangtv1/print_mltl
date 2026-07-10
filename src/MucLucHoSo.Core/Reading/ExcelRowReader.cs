using ExcelDataReader;
using MucLucHoSo.Core.Models;
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

    public ExcelRowReader(string path, string sheetName, int headerRow = 1)
    {
        _sheet = sheetName;
        _fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _reader = ExcelReaderFactory.CreateReader(_fs); // tự nhận XLSX/XLSB
        MoveToSheet(_sheet);
        ReadHeaderRow(headerRow);
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
            if (((_reader.GetValue(i)?.ToString() ?? string.Empty).Trim()).Length > 0) return true;
        return false;
    }

    // Đọc tiến tới dòng KHÔNG-trống kế tiếp; false nếu hết dòng.
    private bool MoveToNextNonEmptyRow()
    {
        while (_reader.Read())
            if (CurrentRowHasContent(_reader.FieldCount)) return true;
        return false;
    }

    private void ReadHeaderRow(int headerRow)
    {
        // Bỏ qua headerRow−1 dòng KHÔNG-trống (đồng thời bỏ mọi dòng trống), rồi lấy dòng không-trống kế tiếp làm header.
        for (int skipped = 0; skipped < headerRow; skipped++)
            if (!MoveToNextNonEmptyRow())
                throw new InvalidOperationException($"Không đủ dữ liệu: cần ≥ {headerRow} dòng có nội dung.");
        _headers = new List<string>(_reader.FieldCount);
        for (int i = 0; i < _reader.FieldCount; i++)
            _headers.Add((_reader.GetValue(i)?.ToString() ?? string.Empty).Trim());
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
                var v = (_reader.GetValue(i)?.ToString() ?? string.Empty).Trim();
                if (v.Length > 0) anyValue = true;
                dict[_headers[i]] = v;
            }
            if (!anyValue) continue; // bỏ dòng trắng hoàn toàn
            yield return new RowRecord(dict);
        }
    }

    public void Dispose() { _reader.Dispose(); _fs.Dispose(); }
}
