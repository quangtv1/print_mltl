using System.Globalization;
using System.Text;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Text;

/// <summary>
/// Vietnamese-aware header normalization and auto-matching of template variables to
/// Excel columns (mota3 §6.3). Auto-match never rejects or warns about duplicate
/// column usage — multiple variables may share one column.
/// </summary>
public static class HeaderMatch
{
    /// <summary>
    /// Normalize for fuzzy comparison: map đ/Đ to d/D *before* Unicode decomposition
    /// (đ has no NFD decomposition, so it must be handled explicitly), strip diacritics,
    /// lower-case, and drop every non-alphanumeric character.
    /// </summary>
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var pre = s.Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = pre.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;      // strip diacritic marks
            if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Suggest an Excel column for each input variable of the template, matching the
    /// variable's default column hint against the actual headers. Auto-computed
    /// variables are skipped. Unmatched variables are omitted from the result.
    /// </summary>
    public static Dictionary<string, string> AutoMatch(IEnumerable<TemplateVar> vars, IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var normHeaders = headers.Select(h => (raw: h, norm: Normalize(h))).ToList();

        foreach (var v in vars)
        {
            if (v.Auto) continue;
            var target = Normalize(v.Col);
            if (target.Length == 0) continue;

            // 1) exact normalized match, else 2) header containing/contained-by the hint.
            var hit = normHeaders.FirstOrDefault(h => h.norm == target);
            if (hit.raw is null)
                hit = normHeaders.FirstOrDefault(h => h.norm.Contains(target) || target.Contains(h.norm));
            if (hit.raw is not null)
                map[v.V] = hit.raw;
        }
        return map;
    }
}
