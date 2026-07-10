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

    static CsvRowReader() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // cần cho code page ANSI (Windows-1258)

    public CsvRowReader(string path, string? delimiter = null, int startRow = 1)
    {
        // detectEncodingFromByteOrderMarks vẫn ưu tiên BOM thật (UTF-8/UTF-16) nếu có; encoding truyền vào
        // chỉ là fallback khi KHÔNG có BOM — dò UTF-8 vs ANSI để không làm hỏng chữ có dấu ở CSV không-UTF8.
        _sr = new StreamReader(path, DetectEncoding(path), detectEncodingFromByteOrderMarks: true);
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter ?? AutoDetectDelimiter(path),
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = false,   // giữ dòng trống để đếm số dòng vật lý đồng nhất với Excel
        };
        _csv = new CsvReader(_sr, cfg);
        // startRow = số dòng VẬT LÝ (1-based) của dòng tiêu đề. Đọc ĐÚNG dòng đó làm header — không bỏ dòng trống.
        for (int skip = 0; skip < startRow; skip++)
            if (!_csv.Read())
                throw new InvalidOperationException($"Không đủ dữ liệu: file không có tới dòng {startRow}.");
        _csv.ReadHeader();
        _headers = (_csv.HeaderRecord ?? Array.Empty<string>())
                   .Select(h => (h ?? string.Empty).Trim()).ToList();
    }

    // Fallback encoding khi CSV không có BOM: nếu mẫu đầu file là UTF-8 hợp lệ → UTF-8, ngược lại coi là
    // ANSI Windows-1258 (Excel VN hay xuất CSV kiểu này). Tránh mojibake "Nguyễn" → "Nguyá»…n".
    private static Encoding DetectEncoding(string path)
    {
        byte[] sample; int len;
        using (var fs = File.OpenRead(path))
        {
            sample = new byte[(int)Math.Min(fs.Length, 256 * 1024)];
            len = fs.Read(sample, 0, sample.Length);
        }
        if (IsLikelyUtf8(sample, len)) return new UTF8Encoding(false);
        try { return Encoding.GetEncoding(1258); }   // Windows-1258 (tiếng Việt)
        catch { return Encoding.Default; }
    }

    // Quét cấu trúc byte UTF-8. Byte cao đơn lẻ (ANSI) → false. Chuỗi multibyte bị cắt ở cuối mẫu → coi hợp lệ.
    private static bool IsLikelyUtf8(byte[] b, int len)
    {
        int i = 0;
        while (i < len)
        {
            byte c = b[i];
            if (c < 0x80) { i++; continue; }
            int extra = (c & 0xE0) == 0xC0 ? 1 : (c & 0xF0) == 0xE0 ? 2 : (c & 0xF8) == 0xF0 ? 3 : -1;
            if (extra < 0) return false;
            if (i + extra >= len) return true;   // sequence cắt ở biên mẫu — không kết luận là ANSI
            for (int k = 1; k <= extra; k++)
                if ((b[i + k] & 0xC0) != 0x80) return false;
            i += extra + 1;
        }
        return true;
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
