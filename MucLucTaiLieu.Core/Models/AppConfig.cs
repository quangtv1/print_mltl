namespace MucLucTaiLieu.Core.Models;

/// <summary>Batch run options remembered between sessions (mota3 §10/§11).</summary>
public sealed class RunOptions
{
    public bool MultiThread { get; set; }
    public bool Overwrite { get; set; }
    public bool ExportExcel { get; set; }
    public bool SkipErrors { get; set; }
    /// <summary>Optional degree of parallelism; null = auto (ProcessorCount).</summary>
    public int? ThreadCount { get; set; }
}

/// <summary>
/// Last-used state persisted as JSON (mota3 §11): selected template, variable→column
/// mapping, grouping column, PDF filename pattern, run options.
/// </summary>
public sealed class AppConfig
{
    public string TemplateId { get; set; } = "mau01";
    /// <summary>Variable token (e.g. "{don_vi}") → Excel header.</summary>
    public Dictionary<string, string> ColMap { get; set; } = new();
    public string GroupCol { get; set; } = "";
    public string PdfPattern { get; set; } = "{stt_file}_{so_ho_so}";
    public RunOptions RunOptions { get; set; } = new();

    /// <summary>
    /// Drop mapping entries whose target column no longer exists among the current
    /// Excel headers (mota3 §11 "lọc bỏ mapping cũ không hợp lệ khi nạp").
    /// </summary>
    public void FilterInvalidMappings(IReadOnlyCollection<string> validColumns)
    {
        var valid = new HashSet<string>(validColumns, StringComparer.Ordinal);
        foreach (var key in ColMap.Keys.ToList())
        {
            if (!valid.Contains(ColMap[key]))
                ColMap.Remove(key);
        }
    }
}
