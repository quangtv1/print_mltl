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

    public ExcelRowReader(string path, string sheetName)
    {
        _sheet = sheetName;
        _fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        _reader = ExcelReaderFactory.CreateReader(_fs); // tự nhận XLSX/XLSB
        MoveToSheet(_sheet);
        ReadHeaderRow();
    }

    private void MoveToSheet(string sheet)
    {
        do { if (string.Equals(_reader.Name, sheet, StringComparison.Ordinal)) return; }
        while (_reader.NextResult());
        throw new InvalidOperationException($"Không tìm thấy sheet '{sheet}'.");
    }

    private void ReadHeaderRow()
    {
        if (!_reader.Read()) throw new InvalidOperationException("Sheet rỗng.");
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
