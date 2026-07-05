using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MucLucHoSo.App.Converters;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => !(v is bool b && b);
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    // param "invert" => hiện khi null
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        bool hasValue = v is not null && !(v is string s && s.Length == 0);
        bool invert = string.Equals(p as string, "invert", StringComparison.OrdinalIgnoreCase);
        return (invert ? !hasValue : hasValue) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>So khớp giá trị enum với tham số (dùng cho RadioButton/segmented).</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v?.ToString() == p?.ToString();
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        (v is bool b && b) ? Enum.Parse(t, (string)p) : Binding.DoNothing;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        (v is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>
/// Chip biến ở Bước 3: tô nền/chữ theo trạng thái đang chọn.
/// values[0] = tên biến của chip; values[1] = SelectedHighlight của VM.
/// ConverterParameter "bg" → trả về Background; "fg" → trả về Foreground.
/// </summary>
public sealed class SelectedChipBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush Accent = Frozen(0x00, 0x43, 0xA5); // nền khi chọn / chữ khi không chọn
    private static readonly SolidColorBrush White  = Frozen(0xFF, 0xFF, 0xFF); // nền khi không chọn / chữ khi chọn

    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        bool selected = values.Length >= 2 && values[0] is string a && values[1] is string b
                        && string.Equals(a, b, StringComparison.Ordinal);
        bool foreground = string.Equals(p as string, "fg", StringComparison.OrdinalIgnoreCase);
        return foreground ? (selected ? White : Accent) : (selected ? Accent : White);
    }

    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
