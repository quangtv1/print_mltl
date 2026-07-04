namespace MucLucTaiLieu.Core.Models;

/// <summary>
/// One document (văn bản) row inside a <see cref="HoSo"/>. LoaiVb/SlTrang are only
/// used by Mẫu 03 but modelled for all templates (mota3 §4.1).
/// </summary>
public sealed class VanBan
{
    public string Stt { get; set; } = "";         // {stt}
    public string SoKyHieu { get; set; } = "";    // {so_ky_hieu}
    public string NgayThang { get; set; } = "";   // {ngay_thang}
    public string TacGia { get; set; } = "";      // {tac_gia}
    public string TrichYeu { get; set; } = "";    // {trich_yeu}
    public string ToSo { get; set; } = "";        // {to_so}
    public string GhiChu { get; set; } = "";      // {ghi_chu}
    public string LoaiVb { get; set; } = "";      // {loai_vb}  (Mẫu 03)
    public string SlTrang { get; set; } = "";     // {sl_trang} (Mẫu 03)
}
