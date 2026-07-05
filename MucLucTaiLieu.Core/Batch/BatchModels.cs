using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Batch;

/// <summary>Outcome of rendering one hồ sơ in a batch. Pending = never processed (e.g. batch stopped early).</summary>
public enum BatchItemStatus { Pending, Success, Skipped, Failed }

/// <summary>Per-hồ-sơ result.</summary>
public sealed class BatchItemResult
{
    public required HoSo HoSo { get; init; }
    public required string FileName { get; init; }
    public BatchItemStatus Status { get; set; }
    public string? Error { get; set; }
}

/// <summary>Progress tick reported to the UI (marshalled via <see cref="IProgress{T}"/>).</summary>
public sealed record BatchProgress(int Done, int Total, string Log);

/// <summary>Everything needed to run one batch.</summary>
public sealed class BatchRequest
{
    public required IReadOnlyList<HoSo> HoSoList { get; init; }
    public required string TemplateId { get; init; }
    public required IReadOnlyDictionary<string, string> Mapping { get; init; }
    public required string OutDir { get; init; }
    public string Pattern { get; init; } = "{stt_file}_{so_ho_so}";
    public RunOptions Options { get; init; } = new();
}

/// <summary>Aggregate result; <see cref="FailedHoSo"/> feeds the retry action.</summary>
public sealed class BatchSummary
{
    public List<BatchItemResult> Items { get; } = new();
    public int Total => Items.Count;
    public int Succeeded => Items.Count(i => i.Status == BatchItemStatus.Success);
    public int Skipped => Items.Count(i => i.Status == BatchItemStatus.Skipped);
    public int Failed => Items.Count(i => i.Status == BatchItemStatus.Failed);
    public bool Stopped { get; set; }
    public IReadOnlyList<HoSo> FailedHoSo => Items.Where(i => i.Status == BatchItemStatus.Failed).Select(i => i.HoSo).ToList();
}
