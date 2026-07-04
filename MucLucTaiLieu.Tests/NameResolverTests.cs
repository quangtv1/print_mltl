using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Templating;

namespace MucLucTaiLieu.Tests;

public class NameResolverTests
{
    private static readonly DateTime Batch = new(2026, 7, 5, 14, 30, 0);

    [Fact]
    public void Build_DifferentHoSo_ProducesDifferentNames()
    {
        var a = NameResolver.Build("{stt_file}_{so_ho_so}", new HoSo { SoHoSo = "42359" }, 0, Batch);
        var b = NameResolver.Build("{stt_file}_{so_ho_so}", new HoSo { SoHoSo = "42360" }, 1, Batch);
        Assert.NotEqual(a, b);
        Assert.Equal("001_42359.pdf", a);
        Assert.Equal("002_42360.pdf", b);
    }

    [Theory]
    [InlineData(0, "001")]
    [InlineData(1, "002")]
    [InlineData(41, "042")]
    [InlineData(999, "1000")]
    public void Build_SttFile_IsOneBasedThreeDigit(int index, string expected)
    {
        var name = NameResolver.Build("{stt_file}", new HoSo(), index, Batch);
        Assert.Equal($"{expected}.pdf", name);
    }

    [Fact]
    public void Build_NgayGio_UsesBatchTimestampFormat()
    {
        var name = NameResolver.Build("{ngay_gio}", new HoSo(), 0, Batch);
        Assert.Equal("20260705_1430.pdf", name);
    }

    [Fact]
    public void Sanitize_ForbiddenChars_ReplacedWithUnderscore()
    {
        var name = NameResolver.Sanitize("a/b\\c:d*e?f\"g<h>i|j");
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain('\\', name);
        foreach (var c in new[] { ':', '*', '?', '"', '<', '>', '|' })
            Assert.DoesNotContain(c, name[..^4]); // ignore ".pdf"
        Assert.EndsWith(".pdf", name);
    }

    [Theory]
    [InlineData("CON", "_CON.pdf")]
    [InlineData("con.pdf", "_con.pdf")]
    [InlineData("LPT1", "_LPT1.pdf")]
    [InlineData("NUL", "_NUL.pdf")]
    public void Sanitize_ReservedDeviceNames_AreEscaped(string input, string expected)
    {
        Assert.Equal(expected, NameResolver.Sanitize(input));
    }

    [Fact]
    public void Sanitize_PathTraversal_IsNeutralized()
    {
        var name = NameResolver.Sanitize("../../etc/passwd");
        Assert.DoesNotContain("..", name);
        Assert.DoesNotContain('/', name);
    }

    [Fact]
    public void MakeUnique_Collisions_AppendNumericSuffix()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.GetTempPath();
        Assert.Equal("a.pdf", NameResolver.MakeUnique(dir, "a.pdf", used));
        Assert.Equal("a_2.pdf", NameResolver.MakeUnique(dir, "a.pdf", used));
        Assert.Equal("a_3.pdf", NameResolver.MakeUnique(dir, "a.pdf", used));
    }

    [Fact]
    public void MakeUnique_EscapingName_IsRejected()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(Path.GetTempPath(), "mltl-out");
        Assert.Throws<InvalidOperationException>(
            () => NameResolver.MakeUnique(dir, ".." + Path.DirectorySeparatorChar + "evil.pdf", used));
    }
}
