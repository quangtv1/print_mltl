using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Output;

namespace MucLucHoSo.App.ViewModels;

public partial class Step3PreviewViewModel : StepViewModel
{
    public SessionState S => Wizard.Session;

    [ObservableProperty] private bool _showFilled = true;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private string _navText = "";
    [ObservableProperty] private string _jumpText = "";      // ô tên (giá trị gom nhóm) — gõ + Enter để nhảy
    [ObservableProperty] private bool _hasMoreThanPreview;   // tổng > số hồ sơ đọc được → gợi ý tăng số dòng đọc
    // Popup "Tạo file" cho hồ sơ đang xem
    [ObservableProperty] private bool _isCreatePopupOpen;
    [ObservableProperty] private bool _exportPdfChecked;
    [ObservableProperty] private bool _createDone;           // đã tạo xong → đổi nút sang Đóng / Mở thư mục
    [ObservableProperty] private string _createResult = "";
    private string _lastExportDir = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _wordAvailable = true;
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string? _selectedHighlight;
    [ObservableProperty] private double _zoomFactor = 0.7;

    // Cầu nối lệnh điều hướng wizard cho code-behind (Wizard là protected). Ctrl+Enter tiến, Shift+Enter lùi.
    public ICommand WizardNext => Wizard.NextCommand;
    public ICommand WizardBack => Wizard.BackCommand;

    // Ẩn/hiện cột "Biến của mẫu" để xem preview rộng hơn (icon góc trên phải).
    [ObservableProperty] private bool _sidebarCollapsed;
    public GridLength SidebarWidth => SidebarCollapsed ? new GridLength(0) : new GridLength(320);
    public string SidebarToggleGlyph => SidebarCollapsed ? "" : "";   // ‹ hiện / › ẩn
    public string SidebarToggleTip => SidebarCollapsed ? "Hiện cột biến" : "Ẩn cột biến";
    partial void OnSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarToggleGlyph));
        OnPropertyChanged(nameof(SidebarToggleTip));
    }
    [RelayCommand] private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    public ObservableCollection<ImageSource> PreviewPages { get; } = new();
    public string ZoomText => $"{ZoomFactor * 100:0}%";
    public string TemplateName => S.Runtime?.Name ?? "";

    private HoSoJob? CurrentJob => _jobs.Count > 0 ? _jobs[Math.Clamp(_index, 0, _jobs.Count - 1)] : null;
    private string ExportBaseName => CurrentJob is null ? "" : FileNameBuilder.Build(S.FileNamePrefix, CurrentJob, _index + 1);
    public string ExportDocxName => ExportBaseName + ".docx";
    public string ExportPdfName => ExportBaseName + ".pdf";
    public string ExportDirText => string.IsNullOrWhiteSpace(S.OutputDirectory) ? "(sẽ tạo thư mục Output cạnh file Excel)" : S.OutputDirectory;

    public ObservableCollection<string> HeaderVars { get; } = new();
    public ObservableCollection<string> RowVars { get; } = new();
    public ObservableCollection<string> AutoVars { get; } = new();
    public ObservableCollection<string> ImageVars { get; } = new();
    [ObservableProperty] private bool _hasAutoVars;
    [ObservableProperty] private bool _hasImageVars;

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
        HeaderVars.Clear(); RowVars.Clear(); AutoVars.Clear(); ImageVars.Clear();
        HasAutoVars = false; HasImageVars = false;
        if (S.Runtime is null) return;
        var rt = S.Runtime;
        // Token {image...} gộp vào nhóm Biến ảnh → bỏ khỏi Biến tự do / trong bảng (khử trùng như Màn 1).
        var tokenImg = rt.ImageTokenFields;
        foreach (var v in rt.HeaderFields.Except(tokenImg).OrderBy(rt.OrderOf)) HeaderVars.Add(v);
        foreach (var v in rt.RowFields.Except(tokenImg).OrderBy(rt.OrderOf)) RowVars.Add(v);
        foreach (var v in rt.AutoFields.OrderBy(rt.OrderOf)) AutoVars.Add(v);
        foreach (var v in rt.ImageFields.Union(tokenImg).OrderBy(rt.OrderOf)) ImageVars.Add(v);
        HasAutoVars = AutoVars.Count > 0;
        HasImageVars = ImageVars.Count > 0;
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

    /// <summary>Gõ giá trị gom nhóm + Enter → nhảy đến hồ sơ khớp (chính xác trước, rồi chứa).</summary>
    [RelayCommand]
    private void Jump()
    {
        if (_jobs.Count == 0) return;
        var q = (JumpText ?? "").Trim();
        if (q.Length == 0) return;
        int idx = _jobs.FindIndex(j => string.Equals((j.GroupKey ?? "").Trim(), q, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) idx = _jobs.FindIndex(j => (j.GroupKey ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) { _index = idx; NotifyNavCanExec(); _ = RefreshAsync(); }
        else StatusText = $"Không tìm thấy hồ sơ có giá trị \"{q}\".";
    }

    [RelayCommand]
    private void OpenCreatePopup()
    {
        if (CurrentJob is null) return;
        if (string.IsNullOrWhiteSpace(S.OutputDirectory) && !string.IsNullOrEmpty(S.SourcePath))
            S.OutputDirectory = Path.Combine(Path.GetDirectoryName(S.SourcePath!)!, "Output");
        ExportPdfChecked = S.ExportPdf && WordAvailable;
        CreateResult = "";
        CreateDone = false;
        RaiseExportInfo();
        IsCreatePopupOpen = true;
    }

    [RelayCommand] private void CloseCreatePopup() => IsCreatePopupOpen = false;

    [RelayCommand]
    private void OpenCreatedFolder()
    {
        var dir = string.IsNullOrWhiteSpace(_lastExportDir) ? S.OutputDirectory : _lastExportDir;
        if (Directory.Exists(dir))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    [RelayCommand]
    private void ChangeExportDir()
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục lưu" };
        if (dlg.ShowDialog() == true) { S.OutputDirectory = dlg.FolderName; RaiseExportInfo(); }
    }

    [RelayCommand]
    private async Task CreateFileAsync()
    {
        var job = CurrentJob;
        if (job is null || S.Runtime is null) return;
        var dir = string.IsNullOrWhiteSpace(S.OutputDirectory)
            ? Path.Combine(Path.GetDirectoryName(S.SourcePath ?? "") ?? Environment.CurrentDirectory, "Output")
            : S.OutputDirectory;
        bool wantPdf = ExportPdfChecked;
        Busy = true; CreateResult = "Đang tạo…";
        try
        {
            Directory.CreateDirectory(dir);
            var baseName = FileNameBuilder.Build(S.FileNamePrefix, job, _index + 1);
            var docx = Path.Combine(dir, baseName + ".docx");
            string? pdf = wantPdf ? Path.Combine(dir, baseName + ".pdf") : null;
            var map = S.BuildMapping();
            var rt = S.Runtime!;
            await Task.Run(() => Wizard.Preview.ExportFile(rt, map, job, docx, pdf));
            _lastExportDir = dir;
            CreateResult = $"✓ Đã tạo xong: {baseName}.docx" + (pdf != null ? " + " + baseName + ".pdf" : "") + "\nTại: " + dir;
            CreateDone = true;
        }
        catch (Exception ex) { CreateResult = "Lỗi tạo file: " + ex.Message; }
        finally { Busy = false; }
    }

    private void RaiseExportInfo()
    {
        OnPropertyChanged(nameof(ExportDocxName));
        OnPropertyChanged(nameof(ExportPdfName));
        OnPropertyChanged(nameof(ExportDirText));
    }

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

    private int _refreshGen;   // chống đua render: chỉ áp kết quả của lần refresh mới nhất

    private async Task RefreshAsync()
    {
        if (!WordAvailable || S.Runtime is null) return;
        int gen = ++_refreshGen;
        Busy = true;
        try
        {
            HoSoJob? job = ShowFilled && _jobs.Count > 0 ? _jobs[Math.Clamp(_index, 0, _jobs.Count - 1)] : null;
            if (_jobs.Count > 0)
            {
                var cur = _jobs[Math.Clamp(_index, 0, _jobs.Count - 1)];
                JumpText = cur.GroupKey ?? "";
                // y = tổng hồ sơ đã Validation ở Bước 2 (nếu chưa có thì tạm dùng số đọc được).
                int total = S.ValidatedGroupCount > 0 ? S.ValidatedGroupCount : _jobs.Count;
                NavText = $"{Math.Min(_index + 1, _jobs.Count)}/{total}";
                HasMoreThanPreview = total > _jobs.Count;
            }
            else { JumpText = ""; NavText = "(không có dữ liệu mẫu)"; HasMoreThanPreview = false; }
            RaiseExportInfo();
            var pages = await GetPagesAsync(ShowFilled, _index, SelectedHighlight);
            if (gen != _refreshGen) return;   // đã có lần điều hướng mới hơn → bỏ kết quả cũ (tránh lệch ảnh↔NavText)
            PreviewPages.Clear();
            foreach (var p in pages) PreviewPages.Add(p);
            HasPreview = PreviewPages.Count > 0;
            StatusText = ShowFilled ? "Xem trước (dữ liệu mẫu) — đúng như file sẽ xuất." : "Xem Template (placeholder).";
        }
        catch (Exception ex) { if (gen == _refreshGen) StatusText = "Lỗi render: " + ex.Message; }
        finally { if (gen == _refreshGen) Busy = false; }
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
