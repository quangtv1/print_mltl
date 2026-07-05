namespace MucLucHoSo.Core.Pipeline;

public enum HoSoStatus { Ok, Error }

public sealed record HoSoOutcome(int GroupIndex, string GroupKey, HoSoStatus Status,
    string? DocxPath = null, string? PdfPath = null, string? ErrorType = null, string? Message = null);

public sealed class GenerateSummary
{
    public int Total { get; set; }
    public int Ok { get; set; }
    public int Errors { get; set; }
    public TimeSpan Elapsed { get; set; }
    public List<HoSoOutcome> Failures { get; } = new();
}
