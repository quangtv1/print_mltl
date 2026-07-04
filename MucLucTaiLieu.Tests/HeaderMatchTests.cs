using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Text;

namespace MucLucTaiLieu.Tests;

public class HeaderMatchTests
{
    [Theory]
    [InlineData("Đống Đa", "dongda")]     // đ/Đ must map to d before diacritic strip
    [InlineData("đơn vị", "donvi")]
    [InlineData("Hồ sơ số", "hososo")]
    [InlineData("STT", "stt")]
    [InlineData("Số, ký hiệu hồ sơ", "sokyhieuhoso")]
    [InlineData("  ", "")]
    public void Normalize_HandlesVietnameseAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, HeaderMatch.Normalize(input));
    }

    [Fact]
    public void AutoMatch_SuggestsColumns_AndSkipsAutoVars()
    {
        var vars = new List<TemplateVar>
        {
            new() { V = "{don_vi}", Col = "Đơn vị chủ quản" },
            new() { V = "{stt}", Col = "STT" },
            new() { V = "{so_ho_so}", Col = "Số, ký hiệu hồ sơ" },
            new() { V = "{trang_so}", Col = "(tự động)", Auto = true },
        };
        var headers = new List<string> { "STT", "Số, ký hiệu hồ sơ", "Đơn vị chủ quản", "Tác giả" };

        var map = HeaderMatch.AutoMatch(vars, headers);

        Assert.Equal("Đơn vị chủ quản", map["{don_vi}"]);
        Assert.Equal("STT", map["{stt}"]);
        Assert.Equal("Số, ký hiệu hồ sơ", map["{so_ho_so}"]);
        Assert.False(map.ContainsKey("{trang_so}")); // auto var never mapped
    }

    [Fact]
    public void AutoMatch_AllowsMultipleVarsSharingOneColumn_NoWarning()
    {
        var vars = new List<TemplateVar>
        {
            new() { V = "{so_ho_so}", Col = "Số, ký hiệu hồ sơ" },
            new() { V = "{ho_so_dup}", Col = "Số, ký hiệu hồ sơ" },
        };
        var headers = new List<string> { "Số, ký hiệu hồ sơ" };

        var map = HeaderMatch.AutoMatch(vars, headers);

        Assert.Equal(map["{so_ho_so}"], map["{ho_so_dup}"]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void AutoMatch_UnmatchedVar_IsOmitted()
    {
        var vars = new List<TemplateVar> { new() { V = "{khong_co}", Col = "Cột không tồn tại xyz" } };
        var headers = new List<string> { "STT", "Tác giả" };

        var map = HeaderMatch.AutoMatch(vars, headers);

        Assert.Empty(map);
    }
}
