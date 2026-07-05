namespace MucLucHoSo.Core.Models;

public enum BindingKind { Column, Constant, Auto }

/// <summary>
/// Ràng buộc một biến của template với: một CỘT Excel, một HẰNG do người dùng nhập,
/// hoặc biến TỰ ĐỘNG (trang_so/tong_so_trang).
/// </summary>
public sealed class VariableBinding
{
    public required string Variable { get; init; }
    public BindingKind Kind { get; init; }
    public string? Column { get; init; }
    public string? Constant { get; init; }

    public static VariableBinding FromColumn(string variable, string column)
        => new() { Variable = variable, Kind = BindingKind.Column, Column = column };
    public static VariableBinding FromConstant(string variable, string value)
        => new() { Variable = variable, Kind = BindingKind.Constant, Constant = value };
    public static VariableBinding AutoField(string variable)
        => new() { Variable = variable, Kind = BindingKind.Auto };
}
