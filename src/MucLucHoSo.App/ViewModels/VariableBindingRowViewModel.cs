using CommunityToolkit.Mvvm.ComponentModel;

namespace MucLucHoSo.App.ViewModels;

/// <summary>
/// Một dòng ghép biến. Ô bên trái là combo có thể sửa (editable):
/// chọn một CỘT Excel từ danh sách, hoặc GÕ một giá trị HẰNG (tĩnh).
/// Ô bên phải là biến của template.
/// </summary>
public partial class VariableBindingRowViewModel : ObservableObject
{
    public int Index { get; }
    public string Variable { get; }
    public bool IsAutoField { get; }
    public bool IsRowField { get; }
    public IReadOnlyList<string> AvailableColumns { get; }
    private readonly Action _changed;

    /// <summary>Giá trị ô trái: tên cột (nếu trùng header) hoặc chuỗi hằng.</summary>
    [ObservableProperty] private string? _value;

    public VariableBindingRowViewModel(int index, string variable, bool isRowField, bool isAuto,
        IReadOnlyList<string> columns, Action changed)
    {
        Index = index; Variable = variable; IsRowField = isRowField; IsAutoField = isAuto;
        AvailableColumns = columns; _changed = changed;
    }

    public bool IsColumn => !string.IsNullOrEmpty(Value) && AvailableColumns.Contains(Value!);
    public bool IsBound => IsAutoField || !string.IsNullOrWhiteSpace(Value);

    /// <summary>Nhãn cột "Tự khớp".</summary>
    public string MatchText => IsAutoField ? "tự động"
        : IsColumn ? "✓ khớp"
        : string.IsNullOrWhiteSpace(Value) ? "chưa ghép" : "hằng";

    public bool MatchOk => IsAutoField || IsBound;

    partial void OnValueChanged(string? value)
    {
        OnPropertyChanged(nameof(IsColumn));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(MatchText));
        OnPropertyChanged(nameof(MatchOk));
        _changed();
    }
}
