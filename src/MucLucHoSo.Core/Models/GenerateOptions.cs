namespace MucLucHoSo.Core.Models;

public sealed class GenerateOptions
{
    public required string OutputDirectory { get; init; }
    public string FileNamePattern { get; init; } = "MLHS_{so_ho_so}";
    public bool ExportPdf { get; init; } = false;
    public bool Overwrite { get; init; } = true;

    public bool MultiThread { get; init; } = true;
    public int? WorkerCount { get; init; }
    public bool Resume { get; init; } = true;
    public bool AuditLog { get; init; } = true;
    public bool SkipErrors { get; init; } = true;

    public int EffectiveWorkers =>
        MultiThread ? (WorkerCount ?? Math.Max(1, Environment.ProcessorCount - 2)) : 1;
}
