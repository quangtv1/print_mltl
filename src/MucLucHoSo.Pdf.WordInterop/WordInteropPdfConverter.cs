using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MucLucHoSo.Core.Output;

namespace MucLucHoSo.Pdf.WordInterop;

/// <summary>
/// Convert DOCX -> PDF bằng Microsoft Word (late-bound COM qua 'dynamic' — không cần PIA/interop assembly,
/// chạy trên mọi máy có cài Word). Gia cố cho batch:
///  - Tái dùng MỘT instance Word cho cả job (không mở/đóng Word mỗi file).
///  - Chạy toàn bộ tương tác Word trên MỘT luồng STA riêng (COM yêu cầu STA).
///  - Watchdog: quá thời gian -> kill WINWORD, dựng lại instance (job Resume sẽ tiếp tục).
/// Phạm vi: tự động hoá Office phía client, tương tác (trong phạm vi Microsoft hỗ trợ).
/// </summary>
public sealed class WordInteropPdfConverter : IPdfConverter
{
    private const int WdExportFormatPdf = 17;
    private const int WdAlertsNone = 0;
    private const int MsoAutomationSecurityForceDisable = 3;
    private const int WdExportOptimizeForOnScreen = 1;
    private const int WdExportCreateNoBookmarks = 0;
    private const int WdDoNotSaveChanges = 0;

    private readonly TimeSpan _timeout;
    private readonly Thread _sta;
    private readonly BlockingCollection<WorkItem> _queue = new();
    private dynamic? _app;
    private volatile bool _disposed;

    private sealed class WorkItem
    {
        public required Action Action { get; init; }
        public readonly ManualResetEventSlim Done = new(false);
        public Exception? Error;
    }

    public WordInteropPdfConverter(TimeSpan? perFileTimeout = null)
    {
        _timeout = perFileTimeout ?? TimeSpan.FromSeconds(60);
        _sta = new Thread(Pump) { IsBackground = true, Name = "WordInteropSTA" };
        _sta.SetApartmentState(ApartmentState.STA);
        _sta.Start();
    }

    public bool IsAvailable => Type.GetTypeFromProgID("Word.Application") != null;

    public void Convert(string docxPath, string pdfPath)
    {
        var item = new WorkItem { Action = () => DoConvert(docxPath, pdfPath) };
        _queue.Add(item);
        if (!item.Done.Wait(_timeout))
        {
            // Quá hạn: giết Word, dựng lại, báo lỗi để pipeline ghi nhận & Resume bỏ qua file này.
            KillWord();
            throw new TimeoutException($"Word convert quá {_timeout.TotalSeconds:F0}s: {Path.GetFileName(docxPath)}");
        }
        if (item.Error != null) throw item.Error;
    }

    // ---- chạy trên STA thread ----
    private void Pump()
    {
        foreach (var item in _queue.GetConsumingEnumerable())
        {
            try { item.Action(); }
            catch (Exception ex) { item.Error = ex; }
            finally { item.Done.Set(); }
        }
    }

    private void EnsureApp()
    {
        if (_app != null) return;
        var t = Type.GetTypeFromProgID("Word.Application")
                ?? throw new InvalidOperationException("Không tìm thấy Microsoft Word trên máy.");
        _app = Activator.CreateInstance(t)!;
        _app.Visible = false;
        _app.DisplayAlerts = WdAlertsNone;
        try { _app.AutomationSecurity = MsoAutomationSecurityForceDisable; } catch { /* tùy phiên bản */ }
        try { _app.Options.SavePropertiesPrompt = false; } catch { }
        // Tắt các tính năng nền để convert nhanh hơn (không ảnh hưởng layout)
        try { _app.ScreenUpdating = false; } catch { }
        try { _app.Options.CheckSpellingAsYouType = false; } catch { }
        try { _app.Options.CheckGrammarAsYouType = false; } catch { }
        try { _app.Options.Pagination = false; } catch { }
    }

    private void DoConvert(string docxPath, string pdfPath)
    {
        EnsureApp();
        dynamic? doc = null;
        try
        {
            // QUAN TRỌNG: late-bound COM (dynamic) KHÔNG nhận tham số đặt tên (named args) -> DISP_E_UNKNOWNNAME.
            // Phải truyền THEO VỊ TRÍ.
            // Documents.Open(FileName) — chỉ cần đường dẫn.
            doc = _app!.Documents.Open(Path.GetFullPath(docxPath));
            try { doc.Fields.Update(); } catch { } // cập nhật PAGE/NUMPAGES
            // ExportAsFixedFormat(OutputFileName, ExportFormat, OpenAfterExport, OptimizeFor)
            doc.ExportAsFixedFormat(Path.GetFullPath(pdfPath), WdExportFormatPdf, false, WdExportOptimizeForOnScreen);
        }
        finally
        {
            if (doc != null)
            {
                try { doc.Close(WdDoNotSaveChanges); } catch { }
                Marshal.ReleaseComObject(doc);
            }
        }
    }

    private void KillWord()
    {
        try { if (_app != null) { _app.Quit(WdDoNotSaveChanges); Marshal.ReleaseComObject(_app); } } catch { }
        _app = null;
        foreach (var p in Process.GetProcessesByName("WINWORD"))
        { try { p.Kill(); p.WaitForExit(3000); } catch { } }
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var quit = new WorkItem { Action = () =>
        {
            try { if (_app != null) { _app.Quit(WdDoNotSaveChanges); Marshal.ReleaseComObject(_app); _app = null; } }
            catch { }
        }};
        _queue.Add(quit); quit.Done.Wait(TimeSpan.FromSeconds(10));
        _queue.CompleteAdding();
    }
}
