using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Models;

namespace MucLucHoSo.App.ViewModels;

public sealed record TemplateItem(string Name, string Path, bool IsImported = false, bool IsImportAction = false) { public override string ToString() => Name; }

public partial class Step1SourceViewModel : StepViewModel
{
    public SessionState S => Wizard.Session;

    public ObservableCollection<TemplateItem> Templates { get; } = new();
    public ObservableCollection<string> FreeVars { get; } = new();
    public ObservableCollection<string> TableVars { get; } = new();
    public ObservableCollection<string> AutoVars { get; } = new();
    public ObservableCollection<string> ImageVars { get; } = new();

    [ObservableProperty] private TemplateItem? _selectedTemplate;
    [ObservableProperty] private string _statusText = "Chưa đọc dữ liệu.";
    [ObservableProperty] private bool _statusIsOk;
    [ObservableProperty] private bool _hasStatus = true;   // ẩn dòng status khi rỗng để không chừa khoảng trống
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _rowLimitText = "100";   // số dòng đọc (chỉnh ở dòng Định dạng)
    [ObservableProperty] private string _readFromText = "1";   // dòng header (bỏ dòng trống rồi đếm), mặc định 1
    [ObservableProperty] private bool _hasReadInfo;   // nhãn "đã đọc" ẩn tới khi đọc thành công
    [ObservableProperty] private string _readInfoText = "";
    [ObservableProperty] private bool _hasFirstRowPreview;   // dòng gợi ý giá trị dòng đầu, ẩn theo HasReadInfo
    [ObservableProperty] private string _firstRowPreviewLabel = "";   // "Giá trị dòng x: " (thường)
    [ObservableProperty] private string _firstRowPreviewText = "";    // các giá trị (in đậm)
    private CancellationTokenSource? _reloadCts;   // huỷ lần đọc-lại chờ debounce trước đó

    public ObservableCollection<ImageSource> PreviewPages { get; } = new();
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _previewBusy;
    [ObservableProperty] private bool _wordAvailable = true;
    [ObservableProperty] private string _previewTitle = "Xem nhanh";
    [ObservableProperty] private bool _isImportedTemplate;
    [ObservableProperty] private bool _hasImageVars;
    [ObservableProperty] private string _templateWarning = "";   // cảnh báo trùng tên token/Alt-Text

    // Trạng thái vùng biến: chưa chọn / chọn-nhưng-rỗng / chọn-có-biến
    [ObservableProperty] private bool _hasDataVars;
    [ObservableProperty] private bool _showEmptyPrompt = true;    // chưa chọn mẫu → "Hãy chọn mẫu để tiếp tục"
    [ObservableProperty] private bool _showNoVarWarning;          // chọn mẫu nhưng không có biến dữ liệu
    [ObservableProperty] private bool _showVarLists;              // chọn mẫu có biến → hiện các nhóm

    // Chống kẹt/đệ quy khi dòng cuối ComboBox là hành động Import
    private TemplateItem? _prevTemplate;                          // mẫu hợp lệ gần nhất (để revert khi hủy Import)
    private bool _suppressSelectionHandler;

    public string UsingTemplateText => $"Đang sử dụng template \"{TemplateFileName}\"";

    public string TemplateFileName =>
        string.IsNullOrEmpty(S.TemplatePath) ? "" : Path.GetFileName(S.TemplatePath);

    public string PreviewHint => WordAvailable
        ? "Chọn mẫu để xem nhanh."
        : "Không tìm thấy Microsoft Word — không thể xem nhanh (vẫn xuất DOCX bình thường).";

    partial void OnWordAvailableChanged(bool value) => OnPropertyChanged(nameof(PreviewHint));

    public Step1SourceViewModel(WizardViewModel w) : base(w, 1, "Nguồn", "Chọn nguồn dữ liệu và Template DOCX.")
    {
        CanGoNext = false;
        LoadBuiltInTemplates();
    }

    // Tên hiển thị trong dropdown = tên file mẫu (bỏ đuôi .docx). Đổi tên file trong
    // thư mục templates/ là dropdown tự đổi theo — không cần sửa code.
    private void LoadBuiltInTemplates()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "templates");
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*.docx").OrderBy(x => x))
                    Templates.Add(new TemplateItem(Path.GetFileNameWithoutExtension(f), f));
        }
        catch { /* bỏ qua */ }
        // Dòng cuối cùng = hành động Import (thay cho nút "Import DOCX…" rời).
        Templates.Add(new TemplateItem("➕ Import DOCX…", "", IsImportAction: true));
    }

    [RelayCommand]
    private void BrowseSource()
    {
        var dlg = new OpenFileDialog { Filter = "Dữ liệu (*.xlsx;*.xlsb;*.csv)|*.xlsx;*.xlsb;*.csv" };
        if (dlg.ShowDialog() != true) return;
        S.SourcePath = dlg.FileName;
        var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        S.SourceKind = ext == ".csv" ? SourceKind.Csv : ext == ".xlsb" ? SourceKind.Xlsb : SourceKind.Xlsx;
        S.AvailableSheets.Clear(); S.SheetName = null;
        if (S.SourceKind != SourceKind.Csv)
        {
            try
            {
                foreach (var name in Wizard.Core.GetSheetNames(dlg.FileName)) S.AvailableSheets.Add(name);
                S.SheetName = S.AvailableSheets.FirstOrDefault();
            }
            catch (Exception ex) { SetStatus($"Không đọc được sheet: {ex.Message}", false); }
        }
        S.DataLoaded = false; HasReadInfo = false; UpdateCanGoNext();
    }

    private static bool TryPositive(string? s, out int n) => int.TryParse((s ?? "").Trim(), out n) && n >= 1;

    // Nút "Đọc dữ liệu": huỷ lần tự-đọc đang chờ (nếu có) rồi đọc ngay theo yêu cầu người dùng.
    [RelayCommand]
    private Task ReadDataAsync()
    {
        _reloadCts?.Cancel();
        return DoReadAsync(CancellationToken.None);
    }

    // Lõi đọc dùng chung cho nút bấm và tự-đọc debounce. token huỷ → bỏ kết quả (tránh ghi đè bằng dữ liệu cũ).
    private async Task DoReadAsync(CancellationToken token)
    {
        if (string.IsNullOrEmpty(S.SourcePath)) { SetStatus("Chưa chọn tệp nguồn.", false); return; }
        if (!TryPositive(RowLimitText, out int limit))
        {
            System.Windows.MessageBox.Show("Số dòng đọc phải là số nguyên dương (VD 100).",
                "Số dòng đọc không hợp lệ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        if (!TryPositive(ReadFromText, out int start))
        {
            System.Windows.MessageBox.Show("Ô \"Đọc từ\" phải là số nguyên ≥ 1 (dòng tiêu đề, mặc định 1).",
                "Đọc từ không hợp lệ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        S.ReadRowLimit = limit;
        S.ReadStartRow = start;
        Busy = true; HasReadInfo = false;
        try
        {
            // Đọc tối đa 'limit' dòng; header = đúng dòng vật lý 'start' (dữ liệu từ dòng kế, bỏ dòng trắng).
            var (headers, rows) = await Task.Run(() =>
                Wizard.Core.ReadHead(S.SourcePath!, S.SheetName, S.CsvDelimiter, limit, start), token);
            if (token.IsCancellationRequested) return;   // đã có lần đọc mới hơn — bỏ kết quả này
            S.Headers = headers; S.PreviewRows = rows; S.PreviewRowCount = rows.Count;
            S.DataLoaded = true;
            ReadInfoText = $"✓ Đã đọc {rows.Count} dòng · {headers.Count} cột";
            HasReadInfo = true;
            UpdateFirstRowPreview();   // gợi ý giá trị dòng dữ liệu đầu tiên
            SetStatus("", false);   // thành công → chỉ hiện nhãn inline, xoá thông báo lỗi cũ
        }
        catch (OperationCanceledException) { return; }   // bị huỷ — giữ nguyên trạng thái, để lần mới xử lý
        catch (Exception ex) { S.DataLoaded = false; HasReadInfo = false; SetStatus("Lỗi đọc dữ liệu: " + ex.Message, false); }
        finally { if (!token.IsCancellationRequested) { Busy = false; UpdateCanGoNext(); } }
    }

    // Đổi "Đọc từ" / "Số dòng đọc" → ẩn nhãn cũ; nếu đã có file (+sheet với Excel) thì tự đọc lại sau ~400ms.
    partial void OnReadFromTextChanged(string value) => ScheduleAutoReread();
    partial void OnRowLimitTextChanged(string value) => ScheduleAutoReread();

    private void ScheduleAutoReread()
    {
        HasReadInfo = false;
        _reloadCts?.Cancel();   // huỷ lần đọc chờ trước — kể cả khi giá trị vừa thành không hợp lệ
        if (string.IsNullOrEmpty(S.SourcePath)) return;
        if (S.SourceKind != SourceKind.Csv && string.IsNullOrEmpty(S.SheetName)) return;
        if (!TryPositive(ReadFromText, out _) || !TryPositive(RowLimitText, out _))
        {
            SetStatus("Giá trị \"Đọc từ\" / \"Số dòng đọc\" phải là số nguyên ≥ 1.", false);
            return;   // không tự đọc khi giá trị chưa hợp lệ (không bật hộp thoại lúc đang gõ)
        }
        var cts = new CancellationTokenSource();
        _reloadCts = cts;
        _ = DebouncedRereadAsync(cts.Token);
    }

    private async Task DebouncedRereadAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(400, token);   // gộp nhiều phím gõ nhanh thành 1 lần đọc
            await DoReadAsync(token);
        }
        catch (OperationCanceledException) { /* bị huỷ do gõ tiếp — bỏ qua */ }
    }

    /// <summary>Mở dialog chọn DOCX. Chèn mẫu mới TRƯỚC dòng "Import" và chọn nó; trả về false nếu người dùng hủy.</summary>
    private bool PerformImport()
    {
        var dlg = new OpenFileDialog { Filter = "Word (*.docx)|*.docx" };
        if (dlg.ShowDialog() != true) return false;
        var item = new TemplateItem(Path.GetFileNameWithoutExtension(dlg.FileName), dlg.FileName, IsImported: true);
        int insertAt = Math.Max(0, Templates.Count - 1);   // trước sentinel Import ở cuối
        Templates.Insert(insertAt, item);
        SelectedTemplate = item;
        return true;
    }

    [RelayCommand]
    private void OpenInWord()
    {
        if (S.TemplatePath is null) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(S.TemplatePath) { UseShellExecute = true }); }
        catch { }
    }

    partial void OnSelectedTemplateChanged(TemplateItem? value)
    {
        if (_suppressSelectionHandler) return;

        // Dòng cuối "➕ Import DOCX…": mở dialog thay vì chọn mẫu.
        if (value?.IsImportAction == true)
        {
            // PerformImport tự gán SelectedTemplate = mẫu mới (chạy lại handler cho mẫu thật).
            if (!PerformImport())
            {
                // Hủy dialog → quay về mẫu trước đó. Defer qua Dispatcher để ComboBox không tự
                // commit lại giá trị đang chờ (dòng Import) sau setter — WPF Selector reentrancy.
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _suppressSelectionHandler = true;
                    SelectedTemplate = _prevTemplate;
                    _suppressSelectionHandler = false;
                }));
            }
            return;
        }

        _prevTemplate = value;
        FreeVars.Clear(); TableVars.Clear(); AutoVars.Clear(); ImageVars.Clear();
        HasImageVars = false; HasDataVars = false; TemplateWarning = "";
        S.Runtime = null; S.TemplatePath = null;
        IsImportedTemplate = false;   // chỉ bật lại khi compile thành công (tránh banner rỗng khi lỗi)
        if (value is null)
        {
            UpdateVarPanelState();
            OnPropertyChanged(nameof(UsingTemplateText)); UpdateCanGoNext(); return;
        }
        try
        {
            var rt = Wizard.Core.Compile(value.Path);
            S.Runtime = rt; S.TemplatePath = value.Path;
            IsImportedTemplate = value.IsImported;
            // Token {image...} gộp vào nhóm Biến ảnh → bỏ khỏi Biến tự do / trong bảng (khử trùng).
            var tokenImg = rt.ImageTokenFields;
            foreach (var v in rt.HeaderFields.Except(tokenImg).OrderBy(rt.OrderOf)) FreeVars.Add(v);
            foreach (var v in rt.RowFields.Except(tokenImg).OrderBy(rt.OrderOf)) TableVars.Add(v);
            foreach (var v in rt.AutoFields.OrderBy(rt.OrderOf)) AutoVars.Add(v);
            foreach (var v in rt.ImageFields.Union(tokenImg).OrderBy(rt.OrderOf)) ImageVars.Add(v);
            HasImageVars = ImageVars.Count > 0;
            // Có biến dữ liệu = biến tự do ∪ trong bảng ∪ ảnh (không tính trang_so/tong_so_trang).
            HasDataVars = FreeVars.Count > 0 || TableVars.Count > 0 || ImageVars.Count > 0;
            // Trùng tên: cùng một tên vừa là token {image...} vừa là Alt Text ảnh → cảnh báo đổi tên.
            var dup = rt.ImageFields.Intersect(rt.ImageTokenFields).OrderBy(x => x).ToList();
            TemplateWarning = dup.Count > 0
                ? $"⚠ Tên {string.Join(", ", dup)} vừa là token {{image…}} vừa là Alt Text ảnh — đổi tên một cái để tránh trùng."
                : "";
        }
        catch (Exception ex) { SetStatus("Lỗi biên dịch template: " + ex.Message, false); }
        UpdateVarPanelState();
        OnPropertyChanged(nameof(TemplateFileName));
        OnPropertyChanged(nameof(UsingTemplateText));
        UpdateCanGoNext();
        _ = RenderTemplatePreviewAsync();
    }

    /// <summary>Đồng bộ 3 trạng thái vùng biến theo mẫu đã chọn và có biến dữ liệu hay không.</summary>
    private void UpdateVarPanelState()
    {
        bool selected = SelectedTemplate is { IsImportAction: false };   // đã chọn mẫu thật (không phải dòng Import)
        bool compiled = S.Runtime != null;
        ShowVarLists = compiled && HasDataVars;
        ShowNoVarWarning = compiled && !HasDataVars;
        // Chưa chọn mẫu → nhắc chọn. Chọn mẫu mà lỗi biên dịch → để trống (status báo lỗi), không nhắc sai.
        ShowEmptyPrompt = !selected;
    }

    private async Task RenderTemplatePreviewAsync()
    {
        PreviewPages.Clear(); HasPreview = false;
        if (S.Runtime is null) { PreviewTitle = "Xem nhanh"; return; }
        PreviewTitle = $"Xem nhanh — {SelectedTemplate?.Name} (placeholder chưa điền dữ liệu)";
        WordAvailable = Wizard.Preview.WordAvailable;
        if (!WordAvailable) return;
        PreviewBusy = true;
        try
        {
            var rt = S.Runtime!;
            var map = new MappingConfig { GroupColumn = "", Bindings = Array.Empty<VariableBinding>() };
            var path = await Task.Run(() => Wizard.Preview.RenderPdf(rt, map, null));
            var pages = await Wizard.Pdf.RenderAsync(path);
            PreviewPages.Clear();
            foreach (var p in pages) PreviewPages.Add(p);
            HasPreview = PreviewPages.Count > 0;
        }
        catch (Exception ex) { SetStatus("Lỗi xem nhanh: " + ex.Message, false); }
        finally { PreviewBusy = false; }
    }

    // Xác nhận đúng dòng: hiện NỘI DUNG dòng vật lý "Đọc từ dòng" (= chính dòng header vừa đọc), nhãn khớp số đã nhập.
    private void UpdateFirstRowPreview()
    {
        if (S.Headers.Count == 0) { FirstRowPreviewLabel = ""; FirstRowPreviewText = ""; HasFirstRowPreview = false; return; }
        FirstRowPreviewLabel = $"Giá trị dòng {S.ReadStartRow}: ";
        FirstRowPreviewText = string.Join("; ", S.Headers);
        HasFirstRowPreview = true;
    }

    // Ẩn nhãn "đã đọc" (đọc lại/lỗi/đổi tệp) cũng ẩn luôn dòng gợi ý để không hiện dữ liệu cũ lệch pha.
    partial void OnHasReadInfoChanged(bool value)
    {
        if (!value) { FirstRowPreviewLabel = ""; FirstRowPreviewText = ""; HasFirstRowPreview = false; }
    }

    private void SetStatus(string text, bool ok) { StatusText = text; StatusIsOk = ok; HasStatus = !string.IsNullOrEmpty(text); }
    private void UpdateCanGoNext() => CanGoNext = S.DataLoaded && S.Runtime != null && HasDataVars;
}
