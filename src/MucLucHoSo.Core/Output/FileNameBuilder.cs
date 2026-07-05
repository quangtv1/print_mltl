using System.Text.RegularExpressions;
using MucLucHoSo.Core.Models;

namespace MucLucHoSo.Core.Output;

/// <summary>Dựng tên file từ mẫu: hỗ trợ mọi biến hồ sơ + {stt_file} (STT tăng dần) + {ngay_gio}.</summary>
public static class FileNameBuilder
{
    private static readonly Regex Rx = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly char[] Invalid = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    public static string Build(string pattern, HoSoJob job, int fileIndex)
    {
        var name = Rx.Replace(pattern, m =>
        {
            var key = m.Groups[1].Value;
            return key switch
            {
                "stt_file" => fileIndex.ToString(),
                "ngay_gio" => DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                _ => job.HeaderValues.TryGetValue(key, out var v) ? v
                     : (key == "so_ho_so" ? job.GroupKey : m.Value)
            };
        });
        foreach (var c in Invalid) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? $"MLHS_{fileIndex}" : name.Trim();
    }
}
