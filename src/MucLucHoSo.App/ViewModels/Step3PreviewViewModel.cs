using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Models;

namespace MucLucHoSo.App.ViewModels;

public partial class Step3PreviewViewModel : StepViewModel
{
    public SessionState S => Wizard.Session;

    [ObservableProperty] private bool _showFilled = true;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private string _navText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _wordAvailable = true;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string? _selectedHighlight;
    [ObservableProperty] private double _zoomFactor = 0.7;

    public ObservableCollection<ImageSource> PreviewPages { get; } = new();
    public string ZoomText => $"{ZoomFactor * 100:0}%";
    public string TemplateName => S.Runtime?.Name ?? "";

    public ObservableCollection<string> HeaderVars { get; } = new();
    public ObservableCollection<string> RowVars { get; } = new();
    public ObservableCollection<string> AutoVars { get; } = new();
    [ObservableProperty] private bool _hasAutoVars;

    private readonly ConcurrentDictionary<string, List<ImageSource>> _cache = new();
    private CancellationTokenSource? _prefetchCts;
    private List<HoSoJob> _jobs = new();
    private int _index;

    public Step3PreviewViewModel(WizardViewModel w) : base(w, 3, "Xem trước", "Xem trước kết quả (WYSIWYG) trên dữ liệu mẫu.") { }

    public override void OnActivated()
    {
        WordAvailable = Wizard.Preview.WordAvailable;
        OnPropertyChanged(nameof(TemplateName));
        RebuildVarGroups();
        _cache.Clear();
        _prefetchCts?.Cancel();
        _prefetchCts = new CancellationTokenSource();
        if (!WordAvailable) { StatusText = "Không tìm thấy Microsoft Word — không thể render Xem trước."; return; }
        try { _jobs = Wizard.Core.BuildPreviewJobs(S); }
        catch (Exception ex) { _jobs = new(); StatusText = "Lỗi dựng dữ liệu xem trước: " + ex.Message; }
        _index = 0;
        NotifyNavCanExec();
        _ = RefreshAsync();
        _ = PrefetchAllAsync(_prefetchCts.Token);   // tải trước toàn bộ hồ sơ ở nền
    }

    private void RebuildVarGroups()
    {
        HeaderVars.Clear(); RowVars.Clear(); AutoVars.Clear();
        HasAutoVars = false;
        if (S.Runtime is null) return;
        foreach (var v in S.Runtime.HeaderFields.OrderBy(x => x)) HeaderVars.Add(v);
        foreach (var v in S.Runtime.RowFields.OrderBy(x => x)) RowVars.Add(v);
        foreach (var v in S.Runtime.AutoFields.OrderBy(x => x)) AutoVars.Add(v);
        HasAutoVars = AutoVars.Count > 0;
    }

    private const double ZoomMin = 0.4, ZoomMax = 3.0;

    partial void OnShowFilledChanged(bool value) => _ = RefreshAsync();
    partial void OnZoomFactorChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomText));
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Bật/tắt nút Tiến/Lùi theo vị trí hồ sơ để tránh "click chết" ở đầu/cuối.</summary>
    private void NotifyNavCanExec()
    {
        PrevCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPrev))]
    private void Prev() { if (_index > 0) { _index--; _ = RefreshAsync(); NotifyNavCanExec(); } }
    private bool CanPrev() => _index > 0;

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next() { if (_index < _jobs.Count - 1) { _index++; _ = RefreshAsync(); NotifyNavCanExec(); } }
    private bool CanNext() => _index < _jobs.Count - 1;

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn() { ZoomFactor = Math.Min(ZoomMax, Math.Round(ZoomFactor + 0.1, 2)); }
    private bool CanZoomIn() => ZoomFactor < ZoomMax - 1e-6;

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut() { ZoomFactor = Math.Max(ZoomMin, Math.Round(ZoomFactor - 0.1, 2)); }
    private bool CanZoomOut() => ZoomFactor > ZoomMin + 1e-6;

    [RelayCommand] private void ZoomReset() { ZoomFactor = 1.0; }

    [RelayCommand]
    private void SelectVar(string? variable)
    {
        SelectedHighlight = SelectedHighlight == variable ? null : variable;
        _ = RefreshAsync();
    }

    private static string Key(bool filled, int idx, string? hl) => filled ? $"f|{idx}|{hl}" : $"t|{hl}";

    private async Task<List<ImageSource>> GetPagesAsync(bool filled, int idx, string? hl)
    {
        var key = Key(filled, idx, hl);
        if (_cache.TryGetValue(key, out var cached)) return cached;
        HoSoJob? job = filled && _jobs.Count > 0 ? _jobs[Math.Clamp(idx, 0, _jobs.Count - 1)] : null;
        var map = S.BuildMapping();
        var rt = S.Runtime!;
        var path = await Task.Run(() => Wizard.Preview.RenderPdf(rt, map, job, hl));
        var pages = await Wizard.Pdf.RenderAsync(path);
        _cache[key] = pages;
        return pages;
    }

    private async Task RefreshAsync()
    {
        if (!WordAvailable || S.Runtime is null) return;
        Busy = true;
        try
        {
            HoSoJob? job = ShowFilled && _jobs.Count > 0 ? _jobs[Math.Clamp(_index, 0, _jobs.Count - 1)] : null;
            NavText = _jobs.Count > 0 && job != null
                ? $"{job.GroupKey} ({Math.Min(_index + 1, _jobs.Count)}/{_jobs.Count})"
                : "(không có dữ liệu mẫu)";
            var pages = await GetPagesAsync(ShowFilled, _index, SelectedHighlight);
            PreviewPages.Clear();
            foreach (var p in pages) PreviewPages.Add(p);
            HasPreview = PreviewPages.Count > 0;
            StatusText = ShowFilled ? "Xem trước (dữ liệu mẫu) — đúng như file sẽ xuất." : "Xem Template (placeholder).";
        }
        catch (Exception ex) { StatusText = "Lỗi render: " + ex.Message; }
        finally { Busy = false; }
    }

    /// <summary>Render trước toàn bộ hồ sơ (chế độ Xem trước, không tô sáng) ở nền, tuần tự, có thể huỷ.</summary>
    private async Task PrefetchAllAsync(CancellationToken ct)
    {
        try { await Task.Delay(150, ct); } catch { return; }
        for (int i = 0; i < _jobs.Count; i++)
        {
            if (ct.IsCancellationRequested) return;
            if (_cache.ContainsKey(Key(true, i, null))) continue;
            try { await GetPagesAsync(true, i, null); } catch { }
        }
    }
}
