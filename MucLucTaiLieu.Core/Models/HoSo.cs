namespace MucLucTaiLieu.Core.Models;

/// <summary>
/// One archival file (hồ sơ): document-level fields plus its list of documents (văn bản).
/// Fields map 1-1 to template variables (mota3 §4.1). All string fields default to
/// empty so a missing seed/Excel value never surfaces as null in the template.
/// </summary>
public sealed class HoSo
{
    public string SoHoSo { get; set; } = "";     // {so_ho_so}
    public string DonVi { get; set; } = "";       // {don_vi}
    public string ChiNhanh { get; set; } = "";    // {chi_nhanh}
    public string TieuDe { get; set; } = "";      // {tieu_de}
    public string NguoiLap { get; set; } = "";    // {nguoi_lap}
    public List<VanBan> Rows { get; set; } = new();
}
