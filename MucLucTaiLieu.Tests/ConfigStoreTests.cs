using MucLucTaiLieu.Core.Config;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mltl-cfg-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new ConfigStore(_dir);
        var cfg = new AppConfig
        {
            TemplateId = "mau03",
            GroupCol = "Số, ký hiệu hồ sơ",
            PdfPattern = "{stt_file}_{so_ho_so}",
            ColMap = { ["{so_ho_so}"] = "Số, ký hiệu hồ sơ", ["{stt}"] = "STT" },
            RunOptions = { Overwrite = true, SkipErrors = true, ThreadCount = 4 },
        };

        store.Save(cfg);
        var loaded = store.Load();

        Assert.Equal("mau03", loaded.TemplateId);
        Assert.Equal("Số, ký hiệu hồ sơ", loaded.GroupCol);
        Assert.Equal("STT", loaded.ColMap["{stt}"]);
        Assert.True(loaded.RunOptions.Overwrite);
        Assert.Equal(4, loaded.RunOptions.ThreadCount);
    }

    [Fact]
    public void Load_FiltersMappingsReferencingUnknownColumns()
    {
        var store = new ConfigStore(_dir);
        store.Save(new AppConfig
        {
            ColMap = { ["{so_ho_so}"] = "Số, ký hiệu hồ sơ", ["{stale}"] = "Cột đã bị xóa" },
        });

        var loaded = store.Load(validColumns: new[] { "STT", "Số, ký hiệu hồ sơ" });

        Assert.True(loaded.ColMap.ContainsKey("{so_ho_so}"));
        Assert.False(loaded.ColMap.ContainsKey("{stale}")); // dropped: column no longer exists
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var loaded = new ConfigStore(_dir).Load();
        Assert.Equal("mau01", loaded.TemplateId);
        Assert.Empty(loaded.ColMap);
    }

    [Fact]
    public void Namespaces_UseSeparateFiles()
    {
        var store = new ConfigStore(_dir);
        store.Save(new AppConfig { TemplateId = "mau02" }, "appA");
        store.Save(new AppConfig { TemplateId = "mau04" }, "appB");

        Assert.Equal("mau02", store.Load("appA").TemplateId);
        Assert.Equal("mau04", store.Load("appB").TemplateId);
        Assert.NotEqual(store.FilePath("appA"), store.FilePath("appB"));
    }
}
