namespace MucLucHoSo.Core.Validation;

public sealed class ValidationResult
{
    public long TotalRows { get; set; }
    public int TotalGroups { get; set; }
    public int LargestGroupSize { get; set; }
    public string? LargestGroupKey { get; set; }
    public bool Consecutive { get; set; } = true;
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0 && Consecutive;

    public override string ToString() =>
        $"Tổng dòng: {TotalRows}\nTổng hồ sơ: {TotalGroups}\n" +
        $"Hồ sơ lớn nhất: {LargestGroupSize} văn bản" +
        (LargestGroupKey is null ? "" : $" (hồ sơ {LargestGroupKey})") + "\n" +
        $"Hồ sơ liên tiếp: {(Consecutive ? "đạt" : "KHÔNG đạt")}\n" +
        $"Trạng thái: {(IsValid ? "Hợp lệ" : "Có lỗi — " + string.Join("; ", Errors))}";
}
