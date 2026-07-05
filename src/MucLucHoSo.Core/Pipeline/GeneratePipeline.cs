using System.Diagnostics;
using System.Threading.Channels;
using MucLucHoSo.Core.Grouping;
using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Output;
using MucLucHoSo.Core.Reading;
using MucLucHoSo.Core.Templating;
using Serilog;

namespace MucLucHoSo.Core.Pipeline;

/// <summary>
/// Pipeline 2 tầng:
///  - Merge Pool (đa luồng, OpenXML, KHÔNG cần Office) -> .docx
///  - Word Converter (tuần tự, 1 instance) -> .pdf   [chỉ khi bật PDF]
/// Nối bằng Channel (đường dẫn docx). DOCX luôn ra nhanh; PDF nối đuôi.
/// </summary>
public sealed class GeneratePipeline
{
    private readonly RuntimeTemplate _tpl;
    private readonly MappingConfig _map;
    private readonly GenerateOptions _opt;
    private readonly Func<IPdfConverter> _pdfFactory;
    private readonly ILogger _log;

    private readonly GenerateSummary _summary = new();
    private readonly object _summaryLock = new();
    private readonly object _ckLock = new();
    private JobState _state = new();
    private string _jobStatePath = "";
    private int _done;
    private int _total;

    public event Action<HoSoOutcome>? OnHoSo;
    public event Action<int, int>? OnProgress;

    public GeneratePipeline(RuntimeTemplate tpl, MappingConfig map, GenerateOptions opt,
        Func<IPdfConverter> pdfFactory, ILogger log)
    { _tpl = tpl; _map = map; _opt = opt; _pdfFactory = pdfFactory; _log = log; }

    public async Task<GenerateSummary> RunAsync(
        Func<IRowReader> readerFactory, string jobStatePath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_opt.OutputDirectory);
        _jobStatePath = jobStatePath;
        _state = _opt.Resume ? JobState.LoadOrNew(jobStatePath) : new JobState();
        int startAfter = _opt.Resume ? _state.LastCompletedGroupIndex : -1;

        var sw = Stopwatch.StartNew();
        var merger = new DocxMerger(_map);
        int fileIndex = 0;

        var pdfChannel = Channel.CreateBounded<(string docx, string pdf, HoSoOutcome ok)>(
            new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });

        Task pdfTask = _opt.ExportPdf
            ? PdfConsumerAsync(pdfChannel.Reader, ct)
            : Task.CompletedTask;

        var throttle = new SemaphoreSlim(_opt.EffectiveWorkers);
        var mergeTasks = new List<Task>();

        try
        {
            foreach (var job in EnumerateJobs(readerFactory, startAfter))
            {
                ct.ThrowIfCancellationRequested();
                await throttle.WaitAsync(ct);
                int fi = Interlocked.Increment(ref fileIndex);
                var captured = job;
                mergeTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var baseName = FileNameBuilder.Build(_opt.FileNamePattern, captured, fi);
                        var docxPath = Path.Combine(_opt.OutputDirectory, baseName + ".docx");
                        if (_opt.Overwrite || !File.Exists(docxPath))
                        {
                            var bytes = merger.Merge(_tpl, captured);
                            await File.WriteAllBytesAsync(docxPath, bytes, ct);
                        }
                        var ok = new HoSoOutcome(captured.GroupIndex, captured.GroupKey, HoSoStatus.Ok, docxPath);
                        if (_opt.ExportPdf)
                        {
                            var pdfPath = Path.Combine(_opt.OutputDirectory, baseName + ".pdf");
                            await pdfChannel.Writer.WriteAsync((docxPath, pdfPath, ok), ct);
                        }
                        else
                        {
                            Complete(ok);
                            SaveCheckpoint(captured.GroupIndex);
                        }
                    }
                    catch (Exception ex)
                    {
                        HandleError(new HoSoOutcome(captured.GroupIndex, captured.GroupKey,
                            HoSoStatus.Error, ErrorType: ex.GetType().Name, Message: ex.Message));
                        if (!_opt.SkipErrors) throw;
                    }
                    finally { throttle.Release(); }
                }, ct));
            }

            await Task.WhenAll(mergeTasks);
        }
        finally
        {
            pdfChannel.Writer.TryComplete();
            await pdfTask;
        }

        lock (_summaryLock) { _summary.Total = _total; _summary.Elapsed = sw.Elapsed; }
        _log.Information("Hoàn tất: {Ok}/{Total} OK, {Err} lỗi, {Sec:F0}s",
            _summary.Ok, _summary.Total, _summary.Errors, _summary.Elapsed.TotalSeconds);
        return _summary;
    }

    private IEnumerable<HoSoJob> EnumerateJobs(Func<IRowReader> readerFactory, int startAfter)
    {
        using var reader = readerFactory();
        var engine = new GroupEngine(_map);
        foreach (var job in engine.Group(reader.ReadRows()))
        {
            Interlocked.Increment(ref _total);
            if (job.GroupIndex <= startAfter) { Interlocked.Increment(ref _done); continue; }
            yield return job;
        }
    }

    private async Task PdfConsumerAsync(
        ChannelReader<(string docx, string pdf, HoSoOutcome ok)> reader, CancellationToken ct)
    {
        // Chạy trên luồng nền; WordInteropPdfConverter tự marshal vào STA thread nội bộ.
        await Task.Run(async () =>
        {
            using var pdf = _pdfFactory();
            await foreach (var item in reader.ReadAllAsync(ct))
            {
                try
                {
                    pdf.Convert(item.docx, item.pdf);
                    Complete(item.ok with { PdfPath = item.pdf });
                    SaveCheckpoint(item.ok.GroupIndex);
                }
                catch (Exception ex)
                {
                    HandleError(item.ok with
                    { Status = HoSoStatus.Error, ErrorType = "PdfError", Message = ex.Message });
                    if (!_opt.SkipErrors) throw;
                }
            }
        }, ct);
    }

    private void Complete(HoSoOutcome ok)
    {
        lock (_summaryLock) { _summary.Ok++; }
        int d = Interlocked.Increment(ref _done);
        _log.Information("{Group} OK", ok.GroupKey);
        OnHoSo?.Invoke(ok); OnProgress?.Invoke(d, Volatile.Read(ref _total));
    }

    private void HandleError(HoSoOutcome outcome)
    {
        lock (_summaryLock) { _summary.Errors++; _summary.Failures.Add(outcome); }
        int d = Interlocked.Increment(ref _done);
        _log.Error("{Group} ERROR [{Type}] {Msg}", outcome.GroupKey, outcome.ErrorType, outcome.Message);
        OnHoSo?.Invoke(outcome); OnProgress?.Invoke(d, Volatile.Read(ref _total));
    }

    private void SaveCheckpoint(int groupIndex)
    {
        if (!_opt.Resume) return;
        lock (_ckLock)
        {
            if (groupIndex > _state.LastCompletedGroupIndex)
            { _state.LastCompletedGroupIndex = groupIndex; _state.Save(_jobStatePath); }
        }
    }
}
