using MucLucTaiLieu.Core.Pdf;
using MucLucTaiLieu.Core.Templating;

namespace MucLucTaiLieu.Core.Batch;

/// <summary>
/// Orchestrates rendering a list of hồ sơ to PDFs (mota3 §9, §10). File names are
/// resolved sequentially up front (so {ngay_gio} is a single batch timestamp and
/// duplicates get distinct "_2"/"_3" suffixes); rendering then runs through
/// <see cref="Parallel.ForEachAsync"/> — serial by default (MaxDOP=1), opt-in parallel.
/// </summary>
public sealed class BatchRunner
{
    private readonly IPdfRenderer _renderer;

    public BatchRunner(IPdfRenderer renderer) => _renderer = renderer;

    public async Task<BatchSummary> RunAsync(
        BatchRequest req,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default)
    {
        var summary = new BatchSummary();
        var batchTime = DateTime.Now; // computed once so every file shares the timestamp

        // 1) Resolve all file names sequentially (deterministic, collision-free).
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<BatchItemResult>(req.HoSoList.Count);
        for (var i = 0; i < req.HoSoList.Count; i++)
        {
            var name = NameResolver.Build(req.Pattern, req.HoSoList[i], i, batchTime);
            name = NameResolver.MakeUnique(req.OutDir, name, used);
            plan.Add(new BatchItemResult { HoSo = req.HoSoList[i], FileName = name });
        }

        Directory.CreateDirectory(req.OutDir);

        var maxDop = req.Options.MultiThread
            ? (req.Options.ThreadCount ?? Environment.ProcessorCount)
            : 1;

        var done = 0;
        // When skipErrors is off, a failure cancels the whole batch.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxDop), CancellationToken = linked.Token };

        try
        {
            await Parallel.ForEachAsync(plan, options, async (item, token) =>
            {
                var path = Path.Combine(req.OutDir, item.FileName);
                try
                {
                    if (File.Exists(path) && !req.Options.Overwrite)
                    {
                        item.Status = BatchItemStatus.Skipped;
                        Report(progress, ref done, plan.Count, $"↷ Bỏ qua (đã tồn tại): {item.FileName}");
                        return;
                    }

                    await _renderer.RenderAsync(item.HoSo, req.TemplateId, req.Mapping, path, token);
                    item.Status = BatchItemStatus.Success;
                    Report(progress, ref done, plan.Count, $"✓ {item.FileName}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    item.Status = BatchItemStatus.Failed;
                    item.Error = ex.Message;
                    Report(progress, ref done, plan.Count, $"✗ Lỗi: {item.FileName} — {ex.Message}");

                    if (!req.Options.SkipErrors)
                    {
                        summary.Stopped = true;
                        progress?.Report(new BatchProgress(done, plan.Count, "■ Đã dừng (gặp lỗi, không bỏ qua lỗi)."));
                        linked.Cancel();
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Stop-on-error (or external cancel): items never processed stay at their default status.
        }

        summary.Items.AddRange(plan);
        return summary;
    }

    private static void Report(IProgress<BatchProgress>? progress, ref int done, int total, string log)
    {
        var n = Interlocked.Increment(ref done);
        progress?.Report(new BatchProgress(n, total, log));
    }
}
