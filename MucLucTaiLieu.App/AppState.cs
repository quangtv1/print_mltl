using MucLucTaiLieu.Core.Config;
using MucLucTaiLieu.Core.Excel;
using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Seed;
using MucLucTaiLieu.Core.Templating;

namespace MucLucTaiLieu.App;

/// <summary>
/// Wizard-wide shared state: loaded Excel data, current template + mapping + grouping
/// column, and persisted config. Steps read/write this; MainForm owns it.
/// </summary>
public sealed class AppState
{
    public TemplateCatalog Catalog { get; }
    public SeedStore Seed { get; }
    public ConfigStore ConfigStore { get; }
    public AppConfig Config { get; set; }

    public string? ExcelPath { get; set; }
    public string? Sheet { get; set; }
    public ExcelReadResult? Data { get; set; }

    /// <summary>Bumped whenever Excel data changes, so steps can invalidate caches.</summary>
    public int DataVersion { get; private set; }

    public AppState()
    {
        var seedDir = Path.Combine(AppContext.BaseDirectory, "Assets", "seed");
        Seed = new SeedStore(seedDir);
        Catalog = TemplateCatalog.FromSeed(Seed);
        ConfigStore = new ConfigStore();
        Config = ConfigStore.Load(NamespaceKey);
    }

    /// <summary>Per-install config key so app variants don't share config.</summary>
    public static string NamespaceKey => AppContext.BaseDirectory;

    public TemplateDef CurrentTemplate => Catalog.Get(Config.TemplateId);

    public void SetData(string path, string sheet, ExcelReadResult data)
    {
        ExcelPath = path;
        Sheet = sheet;
        Data = data;
        DataVersion++;
    }

    public void Save() => ConfigStore.Save(Config, NamespaceKey);

    /// <summary>Records to preview: real Excel rows mapped to HoSo, else seed data.</summary>
    public List<HoSo> RecordsForPreview()
    {
        if (Data is null) return Seed.LoadHoSo(Config.TemplateId);
        return ExcelToHoSo();
    }

    /// <summary>Project the read Excel rows into HoSo using the current mapping + grouping column.</summary>
    public List<HoSo> ExcelToHoSo()
    {
        if (Data is null) return new();
        return HoSoMapper.Map(Data.Rows, Config.ColMap, Config.GroupCol);
    }
}
