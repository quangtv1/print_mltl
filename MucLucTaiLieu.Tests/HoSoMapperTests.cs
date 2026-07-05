using MucLucTaiLieu.Core.Templating;

namespace MucLucTaiLieu.Tests;

public class HoSoMapperTests
{
    private static Dictionary<string, string> Row(string soHoSo, string stt, string trichYeu) => new()
    {
        ["Số, ký hiệu hồ sơ"] = soHoSo,
        ["STT"] = stt,
        ["Trích yếu nội dung VB"] = trichYeu,
    };

    private static readonly Dictionary<string, string> ColMap = new()
    {
        ["{so_ho_so}"] = "Số, ký hiệu hồ sơ",
        ["{stt}"] = "STT",
        ["{trich_yeu}"] = "Trích yếu nội dung VB",
    };

    [Fact]
    public void Map_GroupsRowsByColumn_IntoDistinctHoSo()
    {
        var rows = new[]
        {
            Row("A", "1", "vb a1"),
            Row("A", "2", "vb a2"),
            Row("B", "1", "vb b1"),
        };

        var result = HoSoMapper.Map(rows, ColMap, groupCol: "Số, ký hiệu hồ sơ");

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].SoHoSo);
        Assert.Equal(2, result[0].Rows.Count);           // A has two documents
        Assert.Equal("vb a1", result[0].Rows[0].TrichYeu);
        Assert.Equal("B", result[1].SoHoSo);
        Assert.Single(result[1].Rows);
    }

    [Fact]
    public void Map_NoGroupColumn_CollapsesToSingleHoSo()
    {
        var rows = new[] { Row("A", "1", "x"), Row("B", "2", "y") };

        var result = HoSoMapper.Map(rows, ColMap, groupCol: null);

        Assert.Single(result);
        Assert.Equal(2, result[0].Rows.Count);
    }

    [Fact]
    public void Map_UnmappedVariable_YieldsEmptyString()
    {
        var rows = new[] { Row("A", "1", "x") };
        var mapWithoutTrichYeu = new Dictionary<string, string> { ["{so_ho_so}"] = "Số, ký hiệu hồ sơ" };

        var result = HoSoMapper.Map(rows, mapWithoutTrichYeu, groupCol: null);

        Assert.Equal("", result[0].Rows[0].TrichYeu);
        Assert.Equal("A", result[0].SoHoSo);
    }
}
