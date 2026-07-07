using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MucLucHoSo.Core.Models;

namespace MucLucHoSo.Core.Templating;

/// <summary>
/// Merge một RuntimeTemplate với một HoSoJob -> bytes DOCX. Thuần OpenXML, KHÔNG cần Office.
/// An toàn đa luồng: mỗi lần clone bytes template riêng -> làm việc trên bản sao độc lập.
/// </summary>
public sealed class DocxMerger
{
    private readonly Dictionary<string, string> _rowVarColumn; // biến cấp dòng -> cột Excel
    private readonly Dictionary<string, string> _imageConst;   // biến ảnh HẰNG -> đường dẫn file
    private readonly Dictionary<string, string> _imageColumn;  // biến ảnh THEO CỘT -> cột chứa đường dẫn

    public DocxMerger(MappingConfig map)
    {
        _rowVarColumn = new(StringComparer.Ordinal);
        _imageConst = new(StringComparer.Ordinal);
        _imageColumn = new(StringComparer.Ordinal);
        foreach (var b in map.Bindings)
        {
            if (b.Kind == BindingKind.Column && b.Column is not null)
                _rowVarColumn[b.Variable] = b.Column; // dùng chung cho cả header & row
            else if (b.Kind == BindingKind.Image && !string.IsNullOrWhiteSpace(b.ImagePath))
                _imageConst[b.Variable] = b.ImagePath!;
            else if (b.Kind == BindingKind.Image && !string.IsNullOrWhiteSpace(b.Column))
                _imageColumn[b.Variable] = b.Column!;
        }
    }

    /// <summary>Đường dẫn ảnh cho một hồ sơ: hằng + (theo cột lấy từ dòng đầu nhóm).</summary>
    private Dictionary<string, string> ResolveImages(HoSoJob job)
    {
        var images = new Dictionary<string, string>(_imageConst, StringComparer.Ordinal);
        if (_imageColumn.Count > 0 && job.Rows.Count > 0)
        {
            var first = job.Rows[0];
            foreach (var kv in _imageColumn)
            {
                var p = first.Get(kv.Value);
                if (!string.IsNullOrWhiteSpace(p)) images[kv.Key] = p;   // rỗng -> giữ placeholder
            }
        }
        return images;
    }

    public byte[] Merge(RuntimeTemplate rt, HoSoJob job, string? highlightVar = null)
    {
        using var ms = new MemoryStream();
        ms.Write(rt.TemplateBytes, 0, rt.TemplateBytes.Length);
        ms.Position = 0;
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var main = doc.MainDocumentPart!;
            var body = main.Document.Body!;

            OpenXmlHelpers.CoalesceRuns(body);

            // 1) Nhân bản hàng mẫu theo từng văn bản trong hồ sơ
            var table = body.Descendants<Table>().ElementAt(rt.TableIndex);
            var rows = table.Elements<TableRow>().ToList();
            var proto = rows[rt.PrototypeRowIndex];

            OpenXmlElement anchor = proto;
            foreach (var docRow in job.Rows)
            {
                var clone = (TableRow)proto.CloneNode(true);
                OpenXmlHelpers.ReplaceTokens(clone, name =>
                    rt.RowFields.Contains(name) && _rowVarColumn.TryGetValue(name, out var col)
                        ? docRow.Get(col) : null, null, highlightVar);
                table.InsertAfter(clone, anchor);
                anchor = clone;
            }
            proto.Remove();

            // 2) Thay biến cấp hồ sơ / hằng + field tự động (PAGE/NUMPAGES) trên toàn tài liệu
            OpenXmlHelpers.ReplaceEverywhere(main,
                name => job.HeaderValues.TryGetValue(name, out var v) ? v : null,
                rt.AutoFields, highlightVar);

            // 3) Đổi ruột ảnh (chữ ký/logo/con dấu/QR) — hằng + theo cột (mỗi hồ sơ một ảnh)
            var images = ResolveImages(job);
            if (images.Count > 0)
            {
                OpenXmlHelpers.ReplaceImages(main, body, images);
                foreach (var hp in main.HeaderParts) OpenXmlHelpers.ReplaceImages(hp, hp.Header, images);
                foreach (var fp in main.FooterParts) OpenXmlHelpers.ReplaceImages(fp, fp.Footer, images);
            }

            main.Document.Save();
        }
        return ms.ToArray();
    }
}
