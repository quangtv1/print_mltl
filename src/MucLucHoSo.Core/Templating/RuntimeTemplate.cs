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

    public static readonly IReadOnlySet<string> KnownAutoFields =
        new HashSet<string> { "trang_so", "tong_so_trang" };
}
