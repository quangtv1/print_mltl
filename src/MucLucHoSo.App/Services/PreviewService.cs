using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Output;
using MucLucHoSo.Core.Templating;
using MucLucHoSo.Pdf.WordInterop;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

namespace MucLucHoSo.App.Services;

/// <summary>
/// Render Xem trước = tạo DOCX (template gốc hoặc đã merge 1 hồ sơ) rồi convert PDF bằng Word.
/// Giữ một WordInteropPdfConverter "ấm" để đổi hồ sơ nhanh. Đúng nguyên tắc Preview = Output.
/// </summary>
public sealed class PreviewService : IDisposable
{
    private readonly string _tmpDir;
    private IPdfConverter? _pdf;
    private int _seq;

    public PreviewService()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "MLHS_preview");
        Directory.CreateDirectory(_tmpDir);
    }

    public bool WordAvailable
    {
        get { try { EnsurePdf(); return _pdf!.IsAvailable; } catch { return false; } }
    }

    private void EnsurePdf() => _pdf ??= new WordInteropPdfConverter(TimeSpan.FromSeconds(30));

    private bool _warmed;
    /// <summary>Làm nóng Word ở nền (mở+convert một docx trắng) để lần xem trước đầu tiên không phải chờ Word khởi động nguội.</summary>
    public void Warmup()
    {
        if (_warmed) return;
        _warmed = true;
        Task.Run(() =>
        {
            try
            {
                EnsurePdf();
                if (!_pdf!.IsAvailable) return;
                var docx = Path.Combine(_tmpDir, "warm.docx");
                var pdf = Path.Combine(_tmpDir, "warm.pdf");
                File.WriteAllBytes(docx, BlankDocx());
                _pdf!.Convert(docx, pdf);
            }
            catch { /* không sao nếu máy chưa có Word */ }
        });
    }

    private static byte[] BlankDocx()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(".")))));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    /// <summary>job = null -> Xem Template (giữ placeholder). highlightVar: tô sáng biến. Trả về PDF tạm.</summary>
    public string RenderPdf(RuntimeTemplate rt, MappingConfig map, HoSoJob? job, string? highlightVar = null)
    {
        EnsurePdf();
        var stamp = Interlocked.Increment(ref _seq);
        var docx = Path.Combine(_tmpDir, $"prev_{stamp}.docx");
        var pdf  = Path.Combine(_tmpDir, $"prev_{stamp}.pdf");

        byte[] bytes = job is null ? rt.TemplateBytes : new DocxMerger(map).Merge(rt, job, highlightVar);
        File.WriteAllBytes(docx, bytes);
        _pdf!.Convert(docx, pdf);
        return pdf;
    }

    /// <summary>Tạo file thật cho MỘT hồ sơ: merge DOCX (không tô sáng) → ghi; nếu có pdfPath thì convert bằng Word.</summary>
    public void ExportFile(RuntimeTemplate rt, MappingConfig map, HoSoJob job, string docxPath, string? pdfPath)
    {
        var bytes = new DocxMerger(map).Merge(rt, job);
        File.WriteAllBytes(docxPath, bytes);
        if (pdfPath != null) { EnsurePdf(); _pdf!.Convert(docxPath, pdfPath); }
    }

    public void Dispose() { try { _pdf?.Dispose(); } catch { } }
}
