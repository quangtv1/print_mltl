namespace MucLucHoSo.Core.Output;

/// <summary>Trừu tượng hoá bước DOCX -> PDF. Hiện thực chính: WordInteropPdfConverter (dự án riêng).</summary>
public interface IPdfConverter : IDisposable
{
    /// <summary>Convert 1 file .docx -> .pdf (đường dẫn đích). Đồng bộ, tuần tự theo bản chất Word.</summary>
    void Convert(string docxPath, string pdfPath);
    bool IsAvailable { get; }
}

/// <summary>Bộ chuyển đổi rỗng: dùng khi không bật PDF (hoặc không có Word).</summary>
public sealed class NullPdfConverter : IPdfConverter
{
    public bool IsAvailable => false;
    public void Convert(string docxPath, string pdfPath) =>
        throw new NotSupportedException("PDF chưa được cấu hình (không có backend Word).");
    public void Dispose() { }
}
