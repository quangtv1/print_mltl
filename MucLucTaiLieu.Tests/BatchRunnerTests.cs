using MucLucTaiLieu.Core.Batch;
using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Pdf;

namespace MucLucTaiLieu.Tests;

public class BatchRunnerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mltl-batch-" + Guid.NewGuid().ToString("N"));
    public BatchRunnerTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    /// <summary>Fake renderer: writes a stub file; optionally fails for chosen hồ sơ.</summary>
    private sealed class FakeRenderer : IPdfRenderer
    {
        public Func<HoSo, bool>? FailWhen;
        public int RenderCount;

        public Task RenderAsync(HoSo hoSo, string templateId, IReadOnlyDictionary<string, string> mapping, string outPath, CancellationToken ct)
        {
            Interlocked.Increment(ref RenderCount);
            ct.ThrowIfCancellationRequested();
            if (FailWhen?.Invoke(hoSo) == true)
                throw new InvalidOperationException("render failed: " + hoSo.SoHoSo);
            File.WriteAllText(outPath, "PDF");
            return Task.CompletedTask;
        }
    }

    private sealed class CollectingProgress : IProgress<BatchProgress>
    {
        public readonly List<BatchProgress> Ticks = new();
        public void Report(BatchProgress value) { lock (Ticks) Ticks.Add(value); }
    }

    private BatchRequest Req(IReadOnlyList<HoSo> list, RunOptions? opts = null, string pattern = "{stt_file}_{so_ho_so}") =>
        new()
        {
            HoSoList = list,
            TemplateId = "mau01",
            Mapping = new Dictionary<string, string>(),
            OutDir = _dir,
            Pattern = pattern,
            Options = opts ?? new RunOptions(),
        };

    private static List<HoSo> HoSos(params string[] soHoSo) => soHoSo.Select(s => new HoSo { SoHoSo = s }).ToList();

    [Fact]
    public async Task Run_DuplicateSoHoSo_ProducesDistinctFiles()
    {
        var summary = await new BatchRunner(new FakeRenderer())
            .RunAsync(Req(HoSos("42359", "42359", "42360"), pattern: "{so_ho_so}"));

        var names = summary.Items.Select(i => i.FileName).ToList();
        Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(3, Directory.GetFiles(_dir, "*.pdf").Length);
        Assert.Equal(3, summary.Succeeded);
    }

    [Fact]
    public async Task Run_OverwriteOff_SkipsExisting_On_Rewrites()
    {
        var renderer = new FakeRenderer();
        var runner = new BatchRunner(renderer);
        var list = HoSos("1", "2");

        await runner.RunAsync(Req(list));                 // first run: 2 files
        Assert.Equal(2, renderer.RenderCount);

        var s2 = await runner.RunAsync(Req(list, new RunOptions { Overwrite = false }));
        Assert.Equal(2, renderer.RenderCount);            // nothing re-rendered
        Assert.Equal(2, s2.Skipped);

        var s3 = await runner.RunAsync(Req(list, new RunOptions { Overwrite = true }));
        Assert.Equal(4, renderer.RenderCount);            // re-rendered both
        Assert.Equal(2, s3.Succeeded);
    }

    [Fact]
    public async Task Run_SkipErrorsOn_ContinuesAndReportsFailure()
    {
        var renderer = new FakeRenderer { FailWhen = h => h.SoHoSo == "2" };
        var summary = await new BatchRunner(renderer)
            .RunAsync(Req(HoSos("1", "2", "3"), new RunOptions { SkipErrors = true }));

        Assert.Equal(1, summary.Failed);
        Assert.Equal(2, summary.Succeeded);
        Assert.False(summary.Stopped);
        Assert.Single(summary.FailedHoSo);
        Assert.Equal("2", summary.FailedHoSo[0].SoHoSo);
    }

    [Fact]
    public async Task Run_SkipErrorsOff_StopsBatch()
    {
        var renderer = new FakeRenderer { FailWhen = h => h.SoHoSo == "1" };
        var summary = await new BatchRunner(renderer)
            .RunAsync(Req(HoSos("1", "2", "3"), new RunOptions { SkipErrors = false }));

        Assert.True(summary.Stopped);
        Assert.True(summary.Failed >= 1);
        Assert.True(summary.Succeeded < 3); // did not finish the whole batch
    }

    [Fact]
    public async Task Run_ParallelAndSerial_ProduceSameFiles()
    {
        var list = HoSos("a", "b", "c", "d", "e");

        await new BatchRunner(new FakeRenderer()).RunAsync(Req(list, new RunOptions { MultiThread = false }));
        var serialFiles = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(x => x).ToArray();

        foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);

        await new BatchRunner(new FakeRenderer()).RunAsync(Req(list, new RunOptions { MultiThread = true, ThreadCount = 4 }));
        var parallelFiles = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(x => x).ToArray();

        Assert.Equal(serialFiles, parallelFiles);
    }

    [Fact]
    public async Task Run_Progress_ReachesTotal()
    {
        var progress = new CollectingProgress();
        var list = HoSos("1", "2", "3", "4");

        await new BatchRunner(new FakeRenderer()).RunAsync(Req(list), progress);

        Assert.NotEmpty(progress.Ticks);
        Assert.Equal(4, progress.Ticks.Max(t => t.Done));
        Assert.All(progress.Ticks, t => Assert.Equal(4, t.Total));
    }

    [Fact]
    public async Task Retry_RerunsOnlyFailedHoSo()
    {
        var renderer = new FakeRenderer { FailWhen = h => h.SoHoSo is "2" or "4" };
        var runner = new BatchRunner(renderer);
        var first = await runner.RunAsync(Req(HoSos("1", "2", "3", "4"), new RunOptions { SkipErrors = true }));

        Assert.Equal(2, first.FailedHoSo.Count);

        // Retry: same runner, renderer now succeeds for everyone.
        renderer.FailWhen = null;
        var retry = await runner.RunAsync(Req(first.FailedHoSo, new RunOptions { SkipErrors = true, Overwrite = true }, pattern: "{so_ho_so}"));

        Assert.Equal(2, retry.Total);
        Assert.Equal(2, retry.Succeeded);
    }
}
