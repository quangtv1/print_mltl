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

/// <summary>Trừ một hằng số khỏi giá trị double (ConverterParameter). Dùng chừa lề khi fit ảnh theo kích thước vùng chứa.</summary>
public sealed class SubtractConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        double d = v is double x ? x : 0;
        double sub = double.TryParse(p as string, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0;
        return Math.Max(0, d - sub);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>
/// Chip biến ở Bước 3: tô nền/chữ theo NHÓM màu và trạng thái đang chọn.
/// values[0] = tên biến của chip; values[1] = SelectedHighlight của VM; values[2] = mã màu nhóm (Tag: "purple"/…, mặc định accent).
/// ConverterParameter "bg" → Background (không chọn = nền nhạt, đang chọn = màu đặc); "fg" → Foreground (không chọn = màu đậm, đang chọn = trắng).
/// </summary>
public sealed class SelectedChipBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush White      = Frozen(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush Accent     = Frozen(0x00, 0x43, 0xA5);
    private static readonly SolidColorBrush AccentTint = Frozen(0xED, 0xF2, 0xFB);
    private static readonly SolidColorBrush Purple     = Frozen(0x7A, 0x3F, 0xF2);
    private static readonly SolidColorBrush PurpleTint = Frozen(0xF2, 0xEE, 0xFC);

    public object Convert(object[] values, Type t, object p, CultureInfo c)
    {
        string a = values.Length > 0 ? values[0] as string ?? "" : "";
        string b = values.Length > 1 ? values[1] as string ?? "" : "";
        string token = values.Length > 2 ? values[2] as string ?? "" : "";
        bool selected = a.Length > 0 && string.Equals(a, b, StringComparison.Ordinal);
        bool foreground = string.Equals(p as string, "fg", StringComparison.OrdinalIgnoreCase);

        var (full, tint) = string.Equals(token, "purple", StringComparison.OrdinalIgnoreCase)
            ? (Purple, PurpleTint)
            : (Accent, AccentTint);

        return foreground ? (selected ? White : full) : (selected ? full : tint);
    }

    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c) => throw new NotSupportedException();

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
