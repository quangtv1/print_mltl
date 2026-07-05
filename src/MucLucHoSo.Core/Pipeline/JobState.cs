using System.Text.Json;

namespace MucLucHoSo.Core.Pipeline;

/// <summary>Checkpoint để Resume: chỉ số hồ sơ cuối đã hoàn tất.</summary>
public sealed class JobState
{
    public string JobId { get; set; } = "JOB001";
    public int LastCompletedGroupIndex { get; set; } = -1;

    public static JobState LoadOrNew(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<JobState>(File.ReadAllText(path)) ?? new JobState()
            : new JobState();

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
}
