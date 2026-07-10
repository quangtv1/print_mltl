namespace MucLucHoSo.Core.Models;

/// <summary>Một dòng dữ liệu nguồn: ánh xạ header cột -> giá trị (đọc dạng string, đã trim).</summary>
public sealed class RowRecord
{
    private readonly Dictionary<string, string> _byHeader;
    public RowRecord(Dictionary<string, string> byHeader) => _byHeader = byHeader;

    public string Get(string header) =>
        _byHeader.TryGetValue(header, out var v) ? v : string.Empty;

    public IReadOnlyDictionary<string, string> Values => _byHeader;

    /// <summary>Số dòng Excel/CSV vật lý (1-based) mà dòng dữ liệu này đọc được — dùng để hiển thị gợi ý.</summary>
    public int SourceRow { get; init; }
}
