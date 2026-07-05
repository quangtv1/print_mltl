using MucLucHoSo.Core.Models;

namespace MucLucHoSo.Core.Grouping;

public sealed class NonConsecutiveGroupException(string key)
    : Exception($"Hồ sơ {key} xuất hiện nhiều cụm dữ liệu. Vui lòng sắp xếp lại dữ liệu nguồn.")
{
    public string GroupKey { get; } = key;
}

/// <summary>
/// Gom các dòng liên tiếp cùng giá trị cột nhóm thành HoSoJob (streaming).
/// Kiểm tra ràng buộc "liên tiếp": nếu một key đã đóng nhóm mà xuất hiện lại -> lỗi.
/// Trường cấp hồ sơ lấy từ DÒNG ĐẦU của nhóm. Biến trong bảng để engine merge tự lặp.
/// RAM giữ = số key đã gặp (số hồ sơ), không phải số dòng.
/// </summary>
public sealed class GroupEngine
{
    private readonly MappingConfig _map;
    public GroupEngine(MappingConfig map) => _map = map;

    private string NormKey(string raw)
    {
        var k = System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"\s+", " ").Trim();
        return _map.GroupCaseSensitive ? k : k.ToUpperInvariant();
    }

    public IEnumerable<HoSoJob> Group(IEnumerable<RowRecord> rows)
    {
        var closed = new HashSet<string>(StringComparer.Ordinal);
        string? curKey = null;
        var buffer = new List<RowRecord>();
        int index = 0;

        foreach (var row in rows)
        {
            var key = NormKey(row.Get(_map.GroupColumn));
            if (curKey is null)
            {
                curKey = key; buffer.Add(row); continue;
            }
            if (key == curKey) { buffer.Add(row); continue; }

            // đổi nhóm -> phát hành nhóm hiện tại
            yield return Build(curKey, buffer, index++);
            closed.Add(curKey);
            if (closed.Contains(key)) throw new NonConsecutiveGroupException(key);

            curKey = key; buffer = new List<RowRecord> { row };
        }
        if (curKey is not null && buffer.Count > 0)
            yield return Build(curKey, buffer, index);
    }

    private HoSoJob Build(string key, List<RowRecord> rows, int index)
    {
        var first = rows[0];
        var header = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var b in _map.Bindings)
        {
            switch (b.Kind)
            {
                case BindingKind.Constant:
                    header[b.Variable] = b.Constant ?? string.Empty; break;
                case BindingKind.Column:
                    // biến cấp hồ sơ: lấy theo dòng đầu (biến trong bảng sẽ resolve khi merge từng dòng)
                    header[b.Variable] = first.Get(b.Column!); break;
                // Auto: để merge engine chèn field PAGE/NUMPAGES
            }
        }
        return new HoSoJob
        {
            GroupIndex = index,
            GroupKey = key,
            Rows = rows,
            HeaderValues = header,
        };
    }
}
