using ExcelDataReader;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Grouping;
using MucLucHoSo.Core.Models;
using MucLucHoSo.Core.Output;
using MucLucHoSo.Core.Pipeline;
using MucLucHoSo.Core.Reading;
using MucLucHoSo.Core.Templating;
using MucLucHoSo.Core.Validation;
using Serilog;
using System.IO;
using System.Text;

namespace MucLucHoSo.App.Services;

/// <summary>Bọc các thao tác Core cho tầng UI (đọc đầu file, sheet, compile, validate, pipeline).</summary>
public sealed class CoreService
{
    static CoreService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public List<string> GetSheetNames(string path)
    {
        var names = new List<string>();
        using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(fs);
        do { names.Add(reader.Name); } while (reader.NextResult());
        return names;
    }

    public (List<string> headers, List<RowRecord> rows) ReadHead(
        string path, string? sheet, string? delimiter, int maxRows = 100, int headerRow = 1)
    {
        using var reader = ReaderFactory.Open(path, sheet, delimiter, headerRow);
        var headers = reader.Headers.ToList();
        var rows = new List<RowRecord>(maxRows);
        foreach (var r in reader.ReadRows())
        {
            rows.Add(r);
            if (rows.Count >= maxRows) break;
        }
        return (headers, rows);
    }

    public RuntimeTemplate Compile(string templatePath) => TemplateCompiler.Compile(templatePath);

    /// <summary>Validation streaming toàn bộ file (một lượt).</summary>
    public ValidationResult Validate(SessionState s)
    {
        using var reader = ReaderFactory.Open(s.SourcePath!, s.SheetName, s.CsvDelimiter, s.ReadStartRow);
        return Validator.Run(reader, s.BuildMapping(), s.Runtime!);
    }

    /// <summary>Dựng các HoSoJob mẫu từ dữ liệu xem nhanh (để Xem trước).</summary>
    public List<HoSoJob> BuildPreviewJobs(SessionState s)
    {
        var engine = new GroupEngine(s.BuildMapping());
        return engine.Group(s.PreviewRows).ToList();
    }

    public GeneratePipeline BuildPipeline(SessionState s, Func<IPdfConverter> pdfFactory, ILogger log) =>
        new(s.Runtime!, s.BuildMapping(), s.BuildOptions(), pdfFactory, log);

    public Func<IRowReader> ReaderFactoryFor(SessionState s) =>
        () => ReaderFactory.Open(s.SourcePath!, s.SheetName, s.CsvDelimiter, s.ReadStartRow);
}
