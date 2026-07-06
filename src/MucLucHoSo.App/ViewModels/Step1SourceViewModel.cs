using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Models;

namespace MucLucHoSo.App.ViewModels;

public sealed record TemplateItem(string Name, string Path, bool IsImported = false) { public override string ToString() => Name; }

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
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string _rowLimitText = "100";   // số dòng đọc (chỉnh ở dòng Định dạng)

    public ObservableCollection<ImageSource> PreviewPages { get; } = new();
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _previewBusy;
    [ObservableProperty] private bool _wordAvailable = true;
    [ObservableProperty] private string _previewTitle = "Xem nhanh";
    [ObservableProperty] private bool _isImportedTemplate;
    [ObservableProperty] private bool _hasImageVars;

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
        S.DataLoaded = false; UpdateCanGoNext();
    }

    [RelayCommand]
    private async Task ReadDataAsync()
    {
        if (string.IsNullOrEmpty(S.SourcePath)) { SetStatus("Chưa chọn tệp nguồn.", false); return; }
        if (!int.TryParse((RowLimitText ?? "").Trim(), out int limit) || limit <= 0)
        {
            System.Windows.MessageBox.Show("Số dòng đọc phải là số nguyên dương (VD 100).",
                "Số dòng đọc không hợp lệ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        S.ReadRowLimit = limit;
        Busy = true;
        try
        {
            // Đọc tối đa 'limit' dòng; nếu file ít hơn thì đọc hết.
            var (headers, rows) = await Task.Run(() =>
                Wizard.Core.ReadHead(S.SourcePath!, S.SheetName, S.CsvDelimiter, limit));
            S.Headers = headers; S.PreviewRows = rows; S.PreviewRowCount = rows.Count;
            S.DataLoaded = true;
            SetStatus($"✓ Đã đọc {rows.Count} dòng mẫu · {headers.Count} cột", true);
        }
        catch (Exception ex) { S.DataLoaded = false; SetStatus("Lỗi đọc dữ liệu: " + ex.Message, false); }
        finally { Busy = false; UpdateCanGoNext(); }
    }

    [RelayCommand]
    private void ImportTemplate()
    {
        var dlg = new OpenFileDialog { Filter = "Word (*.docx)|*.docx" };
        if (dlg.ShowDialog() != true) return;
        var item = new TemplateItem(Path.GetFileNameWithoutExtension(dlg.FileName), dlg.FileName, IsImported: true);
        Templates.Add(item); SelectedTemplate = item;
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
        FreeVars.Clear(); TableVars.Clear(); AutoVars.Clear(); ImageVars.Clear(); HasImageVars = false;
        S.Runtime = null; S.TemplatePath = null;
        IsImportedTemplate = value?.IsImported ?? false;
        if (value is null) { OnPropertyChanged(nameof(UsingTemplateText)); UpdateCanGoNext(); return; }
        try
        {
            var rt = Wizard.Core.Compile(value.Path);
            S.Runtime = rt; S.TemplatePath = value.Path;
            foreach (var v in rt.HeaderFields.OrderBy(x => x)) FreeVars.Add(v);
            foreach (var v in rt.RowFields.OrderBy(x => x)) TableVars.Add(v);
            foreach (var v in rt.AutoFields.OrderBy(x => x)) AutoVars.Add(v);
            foreach (var v in rt.ImageFields.OrderBy(x => x)) ImageVars.Add(v);
            HasImageVars = ImageVars.Count > 0;
        }
        catch (Exception ex) { SetStatus("Lỗi biên dịch template: " + ex.Message, false); }
        OnPropertyChanged(nameof(TemplateFileName));
        OnPropertyChanged(nameof(UsingTemplateText));
        UpdateCanGoNext();
        _ = RenderTemplatePreviewAsync();
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

    private void SetStatus(string text, bool ok) { StatusText = text; StatusIsOk = ok; }
    private void UpdateCanGoNext() => CanGoNext = S.DataLoaded && S.Runtime != null;
}
