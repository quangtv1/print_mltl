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
    private readonly Dictionary<string, string> _images;       // biến ảnh -> đường dẫn file

    public DocxMerger(MappingConfig map)
    {
        _rowVarColumn = new(StringComparer.Ordinal);
        _images = new(StringComparer.Ordinal);
        foreach (var b in map.Bindings)
        {
            if (b.Kind == BindingKind.Column && b.Column is not null)
                _rowVarColumn[b.Variable] = b.Column; // dùng chung cho cả header & row
            else if (b.Kind == BindingKind.Image && !string.IsNullOrWhiteSpace(b.ImagePath))
                _images[b.Variable] = b.ImagePath!;
        }
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

            // 3) Đổi ruột ảnh (chữ ký/logo/con dấu) — ở body + header + footer
            if (_images.Count > 0)
            {
                OpenXmlHelpers.ReplaceImages(main, body, _images);
                foreach (var hp in main.HeaderParts) OpenXmlHelpers.ReplaceImages(hp, hp.Header, _images);
                foreach (var fp in main.FooterParts) OpenXmlHelpers.ReplaceImages(fp, fp.Footer, _images);
            }

            main.Document.Save();
        }
        return ms.ToArray();
    }
}
