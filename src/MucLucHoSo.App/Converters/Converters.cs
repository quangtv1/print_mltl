using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
