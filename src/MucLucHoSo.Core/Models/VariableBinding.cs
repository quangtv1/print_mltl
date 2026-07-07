namespace MucLucHoSo.Core.Models;

public enum BindingKind { Column, Constant, Auto, Image }

/// <summary>
/// Ràng buộc một biến của template với: một CỘT Excel, một HẰNG do người dùng nhập,
/// biến TỰ ĐỘNG (trang_so/tong_so_trang), hoặc ẢNH (đường dẫn tuyệt đối, áp cho mọi hồ sơ).
/// </summary>
public sealed class VariableBinding
{
    public required string Variable { get; init; }
    public BindingKind Kind { get; init; }
    public string? Column { get; init; }
    public string? Constant { get; init; }
    public string? ImagePath { get; init; }

    public static VariableBinding FromColumn(string variable, string column)
        => new() { Variable = variable, Kind = BindingKind.Column, Column = column };
    public static VariableBinding FromConstant(string variable, string value)
        => new() { Variable = variable, Kind = BindingKind.Constant, Constant = value };
    public static VariableBinding AutoField(string variable)
        => new() { Variable = variable, Kind = BindingKind.Auto };
    /// <summary>Ảnh hằng: một file áp cho mọi hồ sơ.</summary>
    public static VariableBinding Image(string variable, string path)
        => new() { Variable = variable, Kind = BindingKind.Image, ImagePath = path };
    /// <summary>Ảnh theo cột: đường dẫn ảnh lấy từ một cột Excel (mỗi hồ sơ một ảnh).</summary>
    public static VariableBinding ImageColumn(string variable, string column)
        => new() { Variable = variable, Kind = BindingKind.Image, Column = column };
}
