using System.Globalization;
using ClosedXML.Excel;

namespace MucLucTaiLieu.Core.Excel;

/// <summary>
/// Writes a summary .xlsx (mota3 §9). Guards against CSV/formula injection: cells whose
/// first non-whitespace character is one of = + - @ are prefixed with an apostrophe —
/// except genuine numbers (so negative values like "-5" are preserved as-is).
/// </summary>
public sealed class ExcelExporter
{
    /// <summary>Apply the injection guard to one cell value.</summary>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Trim leading whitespace including tab/CR/LF before inspecting the first char.
        var trimmed = value.TrimStart(' ', '\t', '\r', '\n');
        if (trimmed.Length == 0) return value;

        var c = trimmed[0];
        var dangerous = c is '=' or '+' or '-' or '@';
        if (!dangerous) return value;

        // Keep real numbers (incl. negative) untouched; only guard formula/command-like text.
        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            return value;

        return "'" + value;
    }

    /// <summary>Write headers + rows to <paramref name="outPath"/> as a single worksheet.</summary>
    public void Export(string outPath, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, string sheetName = "Tổng hợp")
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);

        for (var c = 0; c < headers.Count; c++)
            ws.Cell(1, c + 1).SetValue(Sanitize(headers[c]));

        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < rows[r].Count; c++)
                ws.Cell(r + 2, c + 1).SetValue(Sanitize(rows[r][c]));

        wb.SaveAs(outPath);
    }
}
