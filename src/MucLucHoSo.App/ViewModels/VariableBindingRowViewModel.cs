using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace MucLucHoSo.App.ViewModels;

/// <summary>
/// Một dòng ghép biến. Ô nguồn tùy loại biến:
///  - thường: combo chọn CỘT Excel hoặc gõ HẰNG;
///  - tự động: Word tự tính (không nhập);
///  - ảnh: nút "Duyệt ảnh…" chọn đường dẫn file (áp cho mọi hồ sơ).
/// </summary>
public partial class VariableBindingRowViewModel : ObservableObject
{
    public int Index { get; }
    public string Variable { get; }
    public bool IsAutoField { get; }
    public bool IsRowField { get; }
    public bool IsImageField { get; }
    public IReadOnlyList<string> AvailableColumns { get; }
    private readonly Action _changed;

    /// <summary>Giá trị ô nguồn: tên cột / chuỗi hằng / đường dẫn ảnh.</summary>
    [ObservableProperty] private string? _value;

    public VariableBindingRowViewModel(int index, string variable, bool isRowField, bool isAuto,
        IReadOnlyList<string> columns, Action changed, bool isImage = false)
    {
        Index = index; Variable = variable; IsRowField = isRowField; IsAutoField = isAuto; IsImageField = isImage;
        AvailableColumns = columns; _changed = changed;
    }

    /// <summary>Ô combo cột/hằng chỉ dùng cho biến thường (không phải tự động/ảnh).</summary>
    public bool IsFieldEditable => !IsAutoField && !IsImageField;

    public bool IsColumn => !IsImageField && !string.IsNullOrEmpty(Value) && AvailableColumns.Contains(Value!);

    public bool IsBound => IsAutoField
        || (IsImageField ? (!string.IsNullOrWhiteSpace(Value) && File.Exists(Value)) : !string.IsNullOrWhiteSpace(Value));

    /// <summary>Nhãn cột "Tự khớp".</summary>
    public string MatchText => IsAutoField ? "tự động"
        : IsImageField ? (IsBound ? "ảnh" : "chưa chọn")
        : IsColumn ? "✓ khớp"
        : string.IsNullOrWhiteSpace(Value) ? "chưa ghép" : "hằng";

    public bool MatchOk => IsAutoField || IsBound;

    [RelayCommand]
    private void BrowseImage()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn ảnh (chữ ký / logo / con dấu)",
            Filter = "Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };
        if (dlg.ShowDialog() == true) Value = dlg.FileName;
    }

    partial void OnValueChanged(string? value)
    {
        OnPropertyChanged(nameof(IsColumn));
        OnPropertyChanged(nameof(IsBound));
        OnPropertyChanged(nameof(MatchText));
        OnPropertyChanged(nameof(MatchOk));
        _changed();
    }
}
