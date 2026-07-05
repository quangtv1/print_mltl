using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Templating;

/// <summary>
/// Projects flat Excel rows into <see cref="HoSo"/> records (mota3 §4, §6.4). Rows sharing
/// the same value in the grouping column form one hồ sơ; document-level fields come from the
/// first row of each group, row-level fields from every row. With no grouping column all rows
/// collapse into a single hồ sơ. <paramref name="colMap"/> maps variable tokens (e.g. "{so_ho_so}")
/// to Excel headers.
/// </summary>
public static class HoSoMapper
{
    public static List<HoSo> Map(
        IReadOnlyList<Dictionary<string, string>> rows,
        IReadOnlyDictionary<string, string> colMap,
        string? groupCol)
    {
        string Val(Dictionary<string, string> row, string token)
        {
            if (colMap.TryGetValue(token, out var header) && header.Length > 0 && row.TryGetValue(header, out var v))
                return v;
            return "";
        }

        VanBan RowToVanBan(Dictionary<string, string> row) => new()
        {
            Stt = Val(row, "{stt}"),
            SoKyHieu = Val(row, "{so_ky_hieu}"),
            NgayThang = Val(row, "{ngay_thang}"),
            TacGia = Val(row, "{tac_gia}"),
            TrichYeu = Val(row, "{trich_yeu}"),
            ToSo = Val(row, "{to_so}"),
            GhiChu = Val(row, "{ghi_chu}"),
            LoaiVb = Val(row, "{loai_vb}"),
            SlTrang = Val(row, "{sl_trang}"),
        };

        HoSo NewHoSo(Dictionary<string, string> first) => new()
        {
            SoHoSo = Val(first, "{so_ho_so}"),
            DonVi = Val(first, "{don_vi}"),
            ChiNhanh = Val(first, "{chi_nhanh}"),
            TieuDe = Val(first, "{tieu_de}"),
            NguoiLap = Val(first, "{nguoi_lap}"),
        };

        var result = new List<HoSo>();
        if (rows.Count == 0) return result;

        var hasGroup = !string.IsNullOrEmpty(groupCol);
        if (!hasGroup)
        {
            var single = NewHoSo(rows[0]);
            single.Rows = rows.Select(RowToVanBan).ToList();
            result.Add(single);
            return result;
        }

        // Group by the column value, preserving first-seen order.
        var index = new Dictionary<string, HoSo>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = row.TryGetValue(groupCol!, out var k) ? k : "";
            if (!index.TryGetValue(key, out var hoSo))
            {
                hoSo = NewHoSo(row);
                index[key] = hoSo;
                result.Add(hoSo);
            }
            hoSo.Rows.Add(RowToVanBan(row));
        }
        return result;
    }
}
