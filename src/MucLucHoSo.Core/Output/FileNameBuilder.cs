using MucLucHoSo.Core.Models;

namespace MucLucHoSo.Core.Output;

/// <summary>
/// Dựng tên file theo cấu trúc: [tiền tố text] + [giá trị cột gom nhóm].
/// Cột gom nhóm luôn có nên không phụ thuộc biến riêng của từng template.
/// Nếu giá trị gom nhóm rỗng → dùng [tiền tố] + [STT tăng dần] để vẫn có tên hợp lệ, duy nhất.
/// Việc chống trùng (thêm _2, _3…) do pipeline xử lý khi ghi file.
/// </summary>
public static class FileNameBuilder
{
    private static readonly char[] Invalid = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    public static string Build(string? prefix, HoSoJob job, int fileIndex)
    {
        var pre = Sanitize(prefix ?? "");
        var group = Sanitize((job.GroupKey ?? "").Trim());
        var name = group.Length > 0 ? pre + group : pre + fileIndex;
        name = name.Trim();
        return string.IsNullOrWhiteSpace(name) ? $"MLHS_{fileIndex}" : name;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Invalid) s = s.Replace(c, '_');
        return s;
    }
}
