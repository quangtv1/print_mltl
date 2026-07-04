using System.Globalization;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Templating;

/// <summary>
/// Builds a safe PDF file name from a user pattern (mota3 §9). Supports document
/// tokens plus {stt_file} (1-based, 3-digit) and {ngay_gio} (batch timestamp,
/// computed once per batch and passed in). Sanitizes forbidden characters, blocks
/// path traversal and reserved device names, and guarantees uniqueness in a folder.
/// </summary>
public static class NameResolver
{
    // Windows reserved device names (case-insensitive, without extension).
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9",
    };

    // Forbidden on Windows filesystems: \ / : * ? " < > |  (plus control chars).
    private static readonly char[] Forbidden = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    /// <summary>
    /// Expand <paramref name="pattern"/> for <paramref name="hoSo"/> at zero-based
    /// <paramref name="index"/> using <paramref name="batchTime"/> for {ngay_gio},
    /// then sanitize into a safe "*.pdf" file name (no directory component).
    /// </summary>
    public static string Build(string pattern, HoSo hoSo, int index, DateTime batchTime)
    {
        var expanded = (pattern ?? "")
            .Replace("{stt_file}", (index + 1).ToString("D3", CultureInfo.InvariantCulture))
            .Replace("{ngay_gio}", batchTime.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture))
            .Replace("{so_ho_so}", hoSo.SoHoSo)
            .Replace("{don_vi}", hoSo.DonVi)
            .Replace("{chi_nhanh}", hoSo.ChiNhanh)
            .Replace("{tieu_de}", hoSo.TieuDe)
            .Replace("{nguoi_lap}", hoSo.NguoiLap);

        return Sanitize(expanded);
    }

    /// <summary>
    /// Turn an arbitrary string into a safe bare "*.pdf" file name: replace forbidden
    /// and control characters with '_', collapse any path traversal, escape reserved
    /// device names, and ensure a ".pdf" extension.
    /// </summary>
    public static string Sanitize(string name)
    {
        name ??= "";
        var chars = name.Select(c =>
            Array.IndexOf(Forbidden, c) >= 0 || char.IsControl(c) ? '_' : c).ToArray();
        var cleaned = new string(chars).Trim();

        // Path separators are already gone (turned into '_'); neutralize a bare ".." too.
        cleaned = cleaned.Replace("..", "_");
        cleaned = cleaned.Trim(' ', '.');
        if (cleaned.Length == 0) cleaned = "ho_so";

        // Reserved check applies to the base name (without extension).
        var withoutExt = cleaned.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? cleaned[..^4] : cleaned;
        if (Reserved.Contains(withoutExt))
            withoutExt = "_" + withoutExt;

        return withoutExt + ".pdf";
    }

    /// <summary>
    /// Return a name not already present in <paramref name="used"/> for <paramref name="dir"/>,
    /// suffixing "_2", "_3", … before the extension. Also asserts the resulting path stays
    /// inside <paramref name="dir"/> (defense-in-depth against traversal). Mutates <paramref name="used"/>.
    /// </summary>
    public static string MakeUnique(string dir, string name, ISet<string> used)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var candidate = name;
        var n = 1;
        while (used.Contains(candidate))
        {
            n++;
            candidate = $"{stem}_{n}{ext}";
        }

        // Guard: candidate must resolve to a direct child of dir.
        var full = Path.GetFullPath(Path.Combine(dir, candidate));
        var root = Path.GetFullPath(dir);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal) ||
            !string.Equals(Path.GetDirectoryName(full), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Tên tệp không hợp lệ (thoát khỏi thư mục đích): {name}");
        }

        used.Add(candidate);
        return candidate;
    }
}
