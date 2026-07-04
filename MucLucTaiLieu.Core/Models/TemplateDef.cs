namespace MucLucTaiLieu.Core.Models;

/// <summary>
/// A template variable binding as declared by the prototype (design3.html): the
/// variable token, the default Excel header it maps to, its group, and whether it
/// is auto-computed (e.g. {trang_so}) rather than sourced from Excel (mota3 §4.2/§6.3).
/// </summary>
public sealed class TemplateVar
{
    public string V { get; set; } = "";     // e.g. "{don_vi}"
    public string Col { get; set; } = "";    // default Excel header hint, or "(tự động)"
    public string G { get; set; } = "";      // group label
    public bool Auto { get; set; }           // true for {trang_so}/{tong_so_trang}
}

/// <summary>One of the 4 templates (mota3 §5).</summary>
public sealed class TemplateDef
{
    public string Id { get; set; } = "";     // "mau01".."mau04"
    public string Name { get; set; } = "";   // display name
    public List<TemplateVar> Vars { get; set; } = new();

    /// <summary>Variable tokens that come from Excel (excludes auto-computed ones).</summary>
    public IEnumerable<TemplateVar> InputVars => Vars.Where(v => !v.Auto);
}
