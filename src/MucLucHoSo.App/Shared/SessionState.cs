using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Templating;

namespace MucLucHoSo.App.Shared;

/// <summary>Trạng thái chia sẻ giữa 4 bước wizard.</summary>
public sealed partial class SessionState : ObservableObject
{
    // --- Nguồn dữ liệu ---
    [ObservableProperty] private SourceKind _sourceKind = SourceKind.Xlsx;
    [ObservableProperty] private string? _sourcePath;
    [ObservableProperty] private string? _sheetName;
    [ObservableProperty] private string? _csvDelimiter;
    [ObservableProperty] private bool _useCsvCache;
    public ObservableCollection<string> AvailableSheets { get; } = new();

    [ObservableProperty] private bool _dataLoaded;
    [ObservableProperty] private int _previewRowCount;   // số dòng đọc để xem nhanh (<=100)
    public List<string> Headers { get; set; } = new();
    public List<RowRecord> PreviewRows { get; set; } = new();

    // --- Template ---
    [ObservableProperty] private string? _templatePath;
    [ObservableProperty] private RuntimeTemplate? _runtime;

    // --- Ghép biến ---
    [ObservableProperty] private string? _groupColumn;
    public ObservableCollection<MucLucHoSo.App.ViewModels.VariableBindingRowViewModel> Bindings { get; } = new();

    // --- Xuất ---
    [ObservableProperty] private string _outputDirectory = "";
    [ObservableProperty] private string _fileNamePattern = "MLHS_{so_ho_so}";
    [ObservableProperty] private bool _exportPdf;
    [ObservableProperty] private bool _overwrite = true;
    [ObservableProperty] private bool _multiThread = true;
    [ObservableProperty] private bool _resume = true;
    [ObservableProperty] private bool _auditLog = true;
    [ObservableProperty] private bool _skipErrors = true;

    /// <summary>Dựng MappingConfig từ các dòng ghép biến hiện tại.</summary>
    public MappingConfig BuildMapping()
    {
        var list = new List<VariableBinding>();
        foreach (var r in Bindings)
        {
            if (r.IsAutoField) { list.Add(VariableBinding.AutoField(r.Variable)); continue; }
            if (r.IsColumn) list.Add(VariableBinding.FromColumn(r.Variable, r.Value!));
            else if (!string.IsNullOrWhiteSpace(r.Value)) list.Add(VariableBinding.FromConstant(r.Variable, r.Value!));
        }
        return new MappingConfig { GroupColumn = GroupColumn ?? "", Bindings = list };
    }

    public GenerateOptions BuildOptions() => new()
    {
        OutputDirectory = OutputDirectory,
        FileNamePattern = FileNamePattern,
        ExportPdf = ExportPdf,
        Overwrite = Overwrite,
        MultiThread = MultiThread,
        Resume = Resume,
        AuditLog = AuditLog,
        SkipErrors = SkipErrors,
    };
}
