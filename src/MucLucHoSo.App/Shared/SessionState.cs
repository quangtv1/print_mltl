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
    [ObservableProperty] private int _readRowLimit = 100;   // số dòng đọc từ file (người dùng chỉnh ở Bước 1)
    [ObservableProperty] private int _readStartRow = 1;   // dòng header: bỏ qua dòng trống rồi đếm; header = dòng không-trống thứ N
    [ObservableProperty] private int _previewRowCount;   // số dòng thực đọc để xem nhanh
    public List<string> Headers { get; set; } = new();
    public List<RowRecord> PreviewRows { get; set; } = new();

    // --- Template ---
    [ObservableProperty] private string? _templatePath;
    [ObservableProperty] private RuntimeTemplate? _runtime;

    // --- Ghép biến ---
    [ObservableProperty] private string? _groupColumn;
    public ObservableCollection<MucLucHoSo.App.ViewModels.VariableBindingRowViewModel> Bindings { get; } = new();
    /// <summary>Số hồ sơ (nhóm) từ lần Validation gần nhất — dùng làm tổng cho thanh tiến trình ở Bước 4.</summary>
    [ObservableProperty] private int _validatedGroupCount;

    // --- Xuất ---
    [ObservableProperty] private string _outputDirectory = "";
    [ObservableProperty] private string _fileNamePrefix = "MLHS_";
    [ObservableProperty] private bool _exportPdf;
    [ObservableProperty] private bool _overwrite = true;
    [ObservableProperty] private bool _multiThread = false;
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
            if (r.IsImageField)
            {
                if (!string.IsNullOrWhiteSpace(r.Value))
                    list.Add(r.ImageFromColumn ? VariableBinding.ImageColumn(r.Variable, r.Value!)
                                               : VariableBinding.Image(r.Variable, r.Value!));
                continue;
            }
            if (r.IsColumn) list.Add(VariableBinding.FromColumn(r.Variable, r.Value!));
            else if (!string.IsNullOrWhiteSpace(r.Value)) list.Add(VariableBinding.FromConstant(r.Variable, r.Value!));
        }
        return new MappingConfig { GroupColumn = GroupColumn ?? "", Bindings = list };
    }

    public GenerateOptions BuildOptions() => new()
    {
        OutputDirectory = OutputDirectory,
        FileNamePrefix = FileNamePrefix,
        ExportPdf = ExportPdf,
        Overwrite = Overwrite,
        MultiThread = MultiThread,
        Resume = Resume,
        AuditLog = AuditLog,
        SkipErrors = SkipErrors,
    };
}
