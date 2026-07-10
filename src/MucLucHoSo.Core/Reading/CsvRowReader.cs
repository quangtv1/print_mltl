using CsvHelper;
using CsvHelper.Configuration;
using MucLucHoSo.Core.Models;
using System.Globalization;
using System.Text;

namespace MucLucHoSo.Core.Reading;

/// <summary>Đọc CSV theo dòng bằng CsvHelper (hỗ trợ ô có dấu ngoặc kép, xuống dòng trong ô).</summary>
public sealed class CsvRowReader : IRowReader
{
    private readonly StreamReader _sr;
    private readonly CsvReader _csv;
    private readonly List<string> _headers;

    public CsvRowReader(string path, string? delimiter = null, int headerRow = 1)
    {
        _sr = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter ?? AutoDetectDelimiter(path),
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
        };
        _csv = new CsvReader(_sr, cfg);
        // Bỏ qua headerRow−1 bản ghi KHÔNG-trống (đồng thời bỏ mọi bản ghi trống), rồi bản ghi không-trống kế tiếp = header.
        for (int skipped = 0; skipped < headerRow; skipped++)
            if (!MoveToNextNonEmptyRecord())
                throw new InvalidOperationException($"Không đủ dữ liệu: cần ≥ {headerRow} dòng có nội dung.");
        _csv.ReadHeader();
        _headers = (_csv.HeaderRecord ?? Array.Empty<string>())
                   .Select(h => (h ?? string.Empty).Trim()).ToList();
    }

    // "Bản ghi trống" = mọi trường (đã Trim) đều rỗng.
    private bool CurrentRecordHasContent()
    {
        var rec = _csv.Parser.Record;
        if (rec is null) return false;
        foreach (var f in rec) if (!string.IsNullOrWhiteSpace(f)) return true;
        return false;
    }

    // Đọc tiến tới bản ghi KHÔNG-trống kế tiếp; false nếu hết.
    private bool MoveToNextNonEmptyRecord()
    {
        while (_csv.Read())
            if (CurrentRecordHasContent()) return true;
        return false;
    }

    private static string AutoDetectDelimiter(string path)
    {
        using var probe = new StreamReader(path);
        var line = probe.ReadLine() ?? "";
        int commas = line.Count(c => c == ','), semis = line.Count(c => c == ';'), tabs = line.Count(c => c == '\t');
        return tabs > commas && tabs > semis ? "\t" : semis > commas ? ";" : ",";
    }

    public IReadOnlyList<string> Headers => _headers;

    public IEnumerable<RowRecord> ReadRows()
    {
        while (_csv.Read())
        {
            var dict = new Dictionary<string, string>(_headers.Count, StringComparer.Ordinal);
            bool anyValue = false;
            for (int i = 0; i < _headers.Count; i++)
            {
                var v = (_csv.TryGetField<string>(i, out var s) ? s : string.Empty)?.Trim() ?? string.Empty;
                if (v.Length > 0) anyValue = true;
                dict[_headers[i]] = v;
            }
            if (!anyValue) continue;
            yield return new RowRecord(dict);
        }
    }

    public void Dispose() { _csv.Dispose(); _sr.Dispose(); }
}
