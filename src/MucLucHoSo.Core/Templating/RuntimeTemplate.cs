namespace MucLucHoSo.Core.Templating;

/// <summary>
/// Template đã biên dịch một lần: giữ bytes gốc (bất biến, chia sẻ read-only giữa các worker)
/// + siêu dữ liệu định vị bảng dữ liệu và hàng mẫu (prototype).
/// </summary>
public sealed class RuntimeTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required byte[] TemplateBytes { get; init; }

    /// <summary>Chỉ số bảng dữ liệu trong danh sách Table theo thứ tự tài liệu.</summary>
    public required int TableIndex { get; init; }
    /// <summary>Chỉ số hàng mẫu trong bảng dữ liệu (thường = 1, ngay sau hàng tiêu đề).</summary>
    public required int PrototypeRowIndex { get; init; }

    /// <summary>Biến cấp dòng (nằm trong hàng mẫu) — engine tự lặp theo nhóm.</summary>
    public required IReadOnlySet<string> RowFields { get; init; }
    /// <summary>Biến cấp hồ sơ / hằng (nằm ngoài hàng mẫu).</summary>
    public required IReadOnlySet<string> HeaderFields { get; init; }
    /// <summary>Biến tự động có mặt trong template (trang_so/tong_so_trang).</summary>
    public required IReadOnlySet<string> AutoFields { get; init; }
    /// <summary>Biến ảnh: alt-text của ảnh trong template bắt đầu bằng "image" (chữ ký/logo/con dấu).</summary>
    public IReadOnlySet<string> ImageFields { get; init; } = new HashSet<string>();

    /// <summary>Thứ tự biến xuất hiện trong template (đọc trái→phải, trên→xuống) — để sắp xếp bảng ghép.</summary>
    public IReadOnlyList<string> FieldOrder { get; init; } = Array.Empty<string>();

    /// <summary>Hạng của một biến theo thứ tự đọc (nhỏ = xuất hiện trước). Không có → cuối bảng.</summary>
    public int OrderOf(string field)
    {
        for (int i = 0; i < FieldOrder.Count; i++)
            if (FieldOrder[i] == field) return i;
        return int.MaxValue;
    }

    public static readonly IReadOnlySet<string> KnownAutoFields =
        new HashSet<string> { "trang_so", "tong_so_trang" };
}
