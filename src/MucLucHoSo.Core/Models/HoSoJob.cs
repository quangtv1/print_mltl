namespace MucLucHoSo.Core.Models;

/// <summary>
/// Một hồ sơ đã gom: giá trị cấp hồ sơ (header + hằng) và danh sách dòng (văn bản).
/// Đây là đơn vị công việc chảy qua pipeline.
/// </summary>
public sealed class HoSoJob
{
    public required int GroupIndex { get; init; }
    public required string GroupKey { get; init; }
    public required IReadOnlyList<RowRecord> Rows { get; init; }
    public required IReadOnlyDictionary<string, string> HeaderValues { get; init; }
}
