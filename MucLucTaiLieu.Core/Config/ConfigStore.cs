using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Config;

/// <summary>
/// Persists last-used <see cref="AppConfig"/> as JSON under %AppData%\MLTL (mota3 §11).
/// Each app variant uses a distinct file keyed (namespaced) by its path so variants
/// never clobber each other. Invalid column mappings are dropped on load.
/// </summary>
public sealed class ConfigStore
{
    private readonly string _baseDir;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <param name="baseDir">Storage folder; defaults to %AppData%\MLTL (cross-platform ApplicationData).</param>
    public ConfigStore(string? baseDir = null)
    {
        _baseDir = baseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MLTL");
    }

    public string FilePath(string namespaceKey) =>
        Path.Combine(_baseDir, string.IsNullOrEmpty(namespaceKey) ? "config.json" : $"config_{Hash(namespaceKey)}.json");

    public void Save(AppConfig config, string namespaceKey = "")
    {
        Directory.CreateDirectory(_baseDir);
        File.WriteAllText(FilePath(namespaceKey), JsonSerializer.Serialize(config, Options));
    }

    /// <summary>
    /// Load persisted config, or a fresh default if none exists. When
    /// <paramref name="validColumns"/> is supplied, mappings referencing columns not in
    /// that set are removed (mota3 §11).
    /// </summary>
    public AppConfig Load(string namespaceKey = "", IReadOnlyCollection<string>? validColumns = null)
    {
        var path = FilePath(namespaceKey);
        if (!File.Exists(path)) return new AppConfig();

        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options) ?? new AppConfig();
        if (validColumns is not null)
            config.FilterInvalidMappings(validColumns);
        return config;
    }

    private static string Hash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
