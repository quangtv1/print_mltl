using System.Text.Json;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Seed;

/// <summary>
/// Loads the built-in template definitions and per-template seed records extracted
/// from the design3 prototype (App/Assets/seed/*.json). Used for preview before a
/// real Excel file is loaded (mota3 §5).
/// </summary>
public sealed class SeedStore
{
    private readonly string _seedDir;

    // snake_case JSON keys (so_ho_so, so_ky_hieu, …) map onto PascalCase model props.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public SeedStore(string seedDir) => _seedDir = seedDir;

    /// <summary>Load the 4 template definitions from templates.json.</summary>
    public List<TemplateDef> LoadTemplates()
    {
        var path = Path.Combine(_seedDir, "templates.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<TemplateDef>>(json, Options)
            ?? throw new InvalidDataException($"Không đọc được templates.json tại {path}.");
    }

    /// <summary>Load the seed hồ sơ records for one template (e.g. "mau01").</summary>
    public List<HoSo> LoadHoSo(string templateId)
    {
        var path = Path.Combine(_seedDir, templateId + ".json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<HoSo>>(json, Options)
            ?? throw new InvalidDataException($"Không đọc được seed {templateId}.json tại {path}.");
    }
}
