using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Output;
using MucLucHoSo.Core.Pipeline;
using MucLucHoSo.Pdf.WordInterop;
using Serilog;

namespace MucLucHoSo.App.ViewModels;

/// <summary>Một dòng nhật ký có màu (bind Foreground = chuỗi hex).</summary>
public sealed record LogEntry(string Text, string Color);

public partial class Step4GenerateViewModel : StepViewModel
{
    public SessionState S => Wizard.Session;

    // Màu nhật ký (console tối)
    private const string CInfo = "#FF6F9BD1";  // dòng ">"  (thông tin)
    private const string CTime = "#FFE5B567";  // dòng "⏱" (ước tính)
    private const string COk   = "#FF7EC699";  // dòng "✓" (thành công)
    private const string CErr  = "#FFE06C75";  // dòng "✗" (lỗi)

    public ObservableCollection<LogEntry> LogLines { get; } = new();
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private double _progressMax = 1;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private bool _canOpenFolder;
    [ObservableProperty] private string _doneText = "";
    [ObservableProperty] private bool _hasRun;

    public string ProgressText => $"Tiến trình chạy ({ProgressValue:0}/{ProgressMax:0})";

    public string FileNamePreview
    {
        get
        {
            var b = (S.FileNamePattern ?? "").Replace("{so_ho_so}", "42359").Replace("{stt_file}", "1");
            if (string.IsNullOrWhiteSpace(b)) b = "MLHS_42359";
            return "→ " + b + ".docx" + (S.ExportPdf ? " + " + b + ".pdf" : "");
        }
    }

    public override ICommand? PrimaryCommand => GenerateCommand;
    public override string PrimaryLabel => HasRun ? "Tạo lại" : "Tạo mục lục";

    public Step4GenerateViewModel(WizardViewModel w)
        : base(w, 4, "Tạo mục lục", "Kết xuất DOCX/PDF hàng loạt.")
    {
        S.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SessionState.FileNamePattern) or nameof(SessionState.ExportPdf))
                OnPropertyChanged(nameof(FileNamePreview));
        };
    }

    partial void OnProgressValueChanged(double value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnProgressMaxChanged(double value) => OnPropertyChanged(nameof(ProgressText));
    partial void OnHasRunChanged(bool value) => OnPropertyChanged(nameof(PrimaryLabel));

    public override void OnActivated()
    {
        if (string.IsNullOrEmpty(S.OutputDirectory) && !string.IsNullOrEmpty(S.SourcePath))
            S.OutputDirectory = Path.Combine(Path.GetDirectoryName(S.SourcePath!)!, "Output");
        OnPropertyChanged(nameof(FileNamePreview));
    }

    private void Log(string text, string color) => Dispatch(() => LogLines.Add(new LogEntry(text, color)));

    [RelayCommand]
    private void BrowseOutput()
    {
        var dlg = new OpenFolderDialog { Title = "Chọn thư mục xuất" };
        if (dlg.ShowDialog() == true) S.OutputDirectory = dlg.FolderName;
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (S.Runtime is null) { DoneText = "Chưa chọn template (quay lại Bước 1)."; return; }
        if (string.IsNullOrWhiteSpace(S.OutputDirectory))
            S.OutputDirectory = Path.Combine(Path.GetDirectoryName(S.SourcePath ?? "") ?? Environment.CurrentDirectory, "Output");

        IsRunning = true; CanOpenFolder = false; DoneText = ""; LogLines.Clear();
        GenerateCommand.NotifyCanExecuteChanged();

        string fmt = S.ExportPdf ? "DOCX + PDF" : "DOCX";
        try
        {
            Directory.CreateDirectory(S.OutputDirectory);
            // Mỗi lần bấm "Tạo mục lục"/"Tạo lại" là chạy lại từ đầu:
            // xoá checkpoint cũ để Resume không bỏ qua các hồ sơ đã hoàn thành ở lần trước.
            try
            {
                var st = Path.Combine(S.OutputDirectory, "job.state.json");
                if (File.Exists(st)) File.Delete(st);
            }
            catch { }

            // Dòng thông tin đầu mẻ
            var opts = new List<string>();
            opts.Add(S.MultiThread ? "song song, đa luồng" : "đơn luồng");
            if (S.Overwrite) opts.Add("ghi đè file cũ");
            if (S.SkipErrors) opts.Add("bỏ qua hồ sơ lỗi");
            Log($"> Generate {fmt} hàng loạt — {string.Join("; ", opts)}.", CInfo);
            if (S.Resume) Log("> Resume Job: bật — lưu trạng thái job để tiếp tục khi dừng.", CInfo);
            if (S.AuditLog) Log("> Audit Log: ghi job.log.", CInfo);

            var logCfg = new LoggerConfiguration();
            if (S.AuditLog) logCfg = logCfg.WriteTo.File(Path.Combine(S.OutputDirectory, "job.log"));
            using var log = logCfg.CreateLogger();

            Func<IPdfConverter> pdfFactory;
            if (S.ExportPdf) pdfFactory = () => new WordInteropPdfConverter();
            else pdfFactory = () => new NullPdfConverter();

            var pipe = Wizard.Core.BuildPipeline(S, pdfFactory, log);
            pipe.OnProgress += (d, t) => Dispatch(() => { ProgressValue = d; ProgressMax = Math.Max(1, t); });
            pipe.OnHoSo += o =>
            {
                if (o.Status == HoSoStatus.Ok)
                {
                    var b = (S.FileNamePattern ?? "").Replace("{so_ho_so}", o.GroupKey);
                    Log($"✓ Hồ sơ {o.GroupKey} → {b}.docx" + (S.ExportPdf ? " + .pdf" : ""), COk);
                }
                else Log($"✗ Hồ sơ {o.GroupKey}" + (o.Message is null ? "" : " — " + o.Message), CErr);
            };

            var jobState = Path.Combine(S.OutputDirectory, "job.state.json");
            var readerFactory = Wizard.Core.ReaderFactoryFor(S);

            var sum = await Task.Run(() => pipe.RunAsync(readerFactory, jobState));

            if (S.AuditLog) Log($"> Đã ghi job.log ({sum.Total} dòng audit).", CInfo);
            Log($"✓ Hoàn tất: {sum.Ok} {fmt} • {sum.Elapsed.TotalSeconds:F0} giây • {sum.Errors} lỗi", COk);
            DoneText = "Hoàn tất";
            CanOpenFolder = true;
        }
        catch (Exception ex)
        {
            Log("✗ Lỗi: " + ex.Message, CErr);
            DoneText = "Lỗi";
        }
        finally { IsRunning = false; HasRun = true; GenerateCommand.NotifyCanExecuteChanged(); }
    }
    private bool CanGenerate() => !IsRunning && S.Runtime != null;

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (Directory.Exists(S.OutputDirectory))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{S.OutputDirectory}\"") { UseShellExecute = true });
    }

    private static void Dispatch(Action a) => Application.Current?.Dispatcher.Invoke(a);
}
