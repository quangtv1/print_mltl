using System.Globalization;
using System.Text;

namespace MucLucHoSo.App.Shared;

public static class TextUtil
{
    /// <summary>Bỏ dấu tiếng Việt + về chữ thường + bỏ ký tự không chữ/số — để so khớp cột↔biến.</summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>Bỏ dấu + thường + mỗi cụm ký tự không chữ/số → một "_", trim "_" đầu/cuối.
    /// Dùng để so khớp cột↔biến theo snake_case, VD "Ngày, tháng, năm sinh" → "ngay_thang_nam_sinh".</summary>
    public static string Slug(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(t.Length);
        bool prevSep = false;
        foreach (var ch in t)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); prevSep = false; }
            else if (!prevSep && sb.Length > 0) { sb.Append('_'); prevSep = true; }
        }
        var r = sb.ToString();
        return r.EndsWith('_') ? r[..^1] : r;   // trim "_" cuối; đầu đã tránh nhờ sb.Length>0
    }
}
