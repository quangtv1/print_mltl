using MucLucTaiLieu.Core.Seed;

namespace MucLucTaiLieu.Tests;

public class SeedStoreTests
{
    // Seed JSON is copied next to the test assembly via the .csproj <None Link="seed\..."> item.
    private static SeedStore Store => new(Path.Combine(AppContext.BaseDirectory, "seed"));

    [Fact]
    public void LoadTemplates_ReturnsFourTemplates()
    {
        var templates = Store.LoadTemplates();
        Assert.Equal(new[] { "mau01", "mau02", "mau03", "mau04" }, templates.Select(t => t.Id));
        Assert.All(templates, t => Assert.NotEmpty(t.Vars));
        // Auto vars ({trang_so}, {tong_so_trang}) excluded from InputVars.
        Assert.DoesNotContain(templates[0].InputVars, v => v.Auto);
    }

    [Theory]
    [InlineData("mau01", 3)]
    [InlineData("mau02", 1)]
    [InlineData("mau03", 1)]
    [InlineData("mau04", 1)]
    public void LoadHoSo_ReturnsExpectedRecordCount(string id, int expected)
    {
        Assert.Equal(expected, Store.LoadHoSo(id).Count);
    }

    [Fact]
    public void LoadHoSo_Mau01_MapsSnakeCaseFieldsAndNestedRows()
    {
        var first = Store.LoadHoSo("mau01")[0];
        Assert.Equal("42359", first.SoHoSo);
        Assert.Equal("Nguyễn Công Tùng", first.NguoiLap);
        Assert.NotEmpty(first.Rows);
        Assert.Equal("1", first.Rows[0].Stt);
    }

    [Fact]
    public void LoadHoSo_Mau03_PopulatesLoaiVbAndSlTrang()
    {
        var rows = Store.LoadHoSo("mau03")[0].Rows;
        Assert.Contains(rows, r => r.LoaiVb.Length > 0);  // Mẫu 03 has Loại VB column
        Assert.Contains(rows, r => r.SlTrang.Length > 0); // and SL trang column
    }
}
