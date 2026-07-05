using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.Core.Pdf;

/// <summary>
/// Renders one hồ sơ to a PDF file. Implemented in the App layer by a WebView2
/// PrintToPdf renderer (needs WebView2); abstracted here so the batch runner is
/// testable with a fake renderer (mota3 §3, §8).
/// </summary>
public interface IPdfRenderer
{
    /// <summary>Render <paramref name="hoSo"/> with the given template + mapping to <paramref name="outPath"/>.</summary>
    Task RenderAsync(
        HoSo hoSo,
        string templateId,
        IReadOnlyDictionary<string, string> mapping,
        string outPath,
        CancellationToken ct = default);
}
