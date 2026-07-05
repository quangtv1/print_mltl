using MucLucHoSo.Core.Models;
namespace MucLucHoSo.Core.Reading;

/// <summary>Đọc dữ liệu theo luồng: trả header + lần lượt từng dòng, KHÔNG load hết vào RAM.</summary>
public interface IRowReader : IDisposable
{
    IReadOnlyList<string> Headers { get; }
    /// <summary>Duyệt lần lượt từng dòng dữ liệu (sau header). Lazy — không giữ toàn bộ.</summary>
    IEnumerable<RowRecord> ReadRows();
}
