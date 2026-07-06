using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MucLucHoSo.Core.Templating;

/// <summary>
/// Biên dịch một Template DOCX -> RuntimeTemplate (một lần). Nhận diện bảng dữ liệu và HÀNG MẪU
/// = hàng chứa nhiều biến cấp dòng nhất. Không cần đánh dấu {#documents}: cả hàng mẫu chính là loop.
/// </summary>
public static class TemplateCompiler
{
    public static RuntimeTemplate Compile(string docxPath, string? id = null, string? name = null)
    {
        var bytes = File.ReadAllBytes(docxPath);
        using var ms = new MemoryStream();
        ms.Write(bytes, 0, bytes.Length); ms.Position = 0;
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart!.Document.Body!;

        var tables = body.Descendants<Table>().ToList();
        int bestTable = -1, bestRow = -1, bestCount = 0;
        var rowFields = new HashSet<string>(StringComparer.Ordinal);

        for (int ti = 0; ti < tables.Count; ti++)
        {
            var rows = tables[ti].Elements<TableRow>().ToList();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                var vars = TokensInElement(rows[ri]);
                if (vars.Count > bestCount)
                { bestCount = vars.Count; bestTable = ti; bestRow = ri; rowFields = vars; }
            }
        }
        if (bestTable < 0 || bestCount == 0)
            throw new InvalidOperationException($"Template '{docxPath}' không tìm thấy hàng mẫu chứa biến.");

        // Tất cả token trong tài liệu
        var all = TokensInElement(body);
        foreach (var hp in doc.MainDocumentPart!.HeaderParts)
            foreach (var v in TokensInElement(hp.Header)) all.Add(v);
        foreach (var fp in doc.MainDocumentPart!.FooterParts)
            foreach (var v in TokensInElement(fp.Footer)) all.Add(v);

        var autoFields = new HashSet<string>(all.Where(RuntimeTemplate.KnownAutoFields.Contains), StringComparer.Ordinal);
        var headerFields = new HashSet<string>(
            all.Where(v => !rowFields.Contains(v) && !RuntimeTemplate.KnownAutoFields.Contains(v)),
            StringComparer.Ordinal);

        // Biến ảnh: alt-text/tên ảnh bắt đầu bằng "image" (body + header + footer).
        var imageFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in OpenXmlHelpers.ImageMarkersIn(body)) imageFields.Add(m);
        foreach (var hp in doc.MainDocumentPart!.HeaderParts)
            foreach (var m in OpenXmlHelpers.ImageMarkersIn(hp.Header)) imageFields.Add(m);
        foreach (var fp in doc.MainDocumentPart!.FooterParts)
            foreach (var m in OpenXmlHelpers.ImageMarkersIn(fp.Footer)) imageFields.Add(m);

        // Thứ tự đọc: token theo vị trí trong tài liệu (body → header → footer), rồi tới ảnh.
        var order = new List<string>();
        void AddOrdered(IEnumerable<string> names) { foreach (var n in names) if (!order.Contains(n)) order.Add(n); }
        AddOrdered(OrderedTokensIn(body));
        foreach (var hp in doc.MainDocumentPart!.HeaderParts) AddOrdered(OrderedTokensIn(hp.Header));
        foreach (var fp in doc.MainDocumentPart!.FooterParts) AddOrdered(OrderedTokensIn(fp.Footer));
        AddOrdered(OpenXmlHelpers.ImageMarkersIn(body));
        foreach (var hp in doc.MainDocumentPart!.HeaderParts) AddOrdered(OpenXmlHelpers.ImageMarkersIn(hp.Header));
        foreach (var fp in doc.MainDocumentPart!.FooterParts) AddOrdered(OpenXmlHelpers.ImageMarkersIn(fp.Footer));

        return new RuntimeTemplate
        {
            Id = id ?? Path.GetFileNameWithoutExtension(docxPath),
            Name = name ?? Path.GetFileNameWithoutExtension(docxPath),
            TemplateBytes = bytes,
            TableIndex = bestTable,
            PrototypeRowIndex = bestRow,
            RowFields = rowFields,
            HeaderFields = headerFields,
            AutoFields = autoFields,
            ImageFields = imageFields,
            FieldOrder = order,
        };
    }

    /// <summary>Token theo thứ tự xuất hiện (đọc trái→phải, trên→xuống) trong một phạm vi.</summary>
    private static IEnumerable<string> OrderedTokensIn(DocumentFormat.OpenXml.OpenXmlElement el)
    {
        var text = string.Concat(el.Descendants<Text>().Select(t => t.Text));
        foreach (System.Text.RegularExpressions.Match m in OpenXmlHelpers.TokenRx.Matches(text))
            yield return m.Groups[1].Value;
    }

    private static HashSet<string> TokensInElement(DocumentFormat.OpenXml.OpenXmlElement el)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var text = string.Concat(el.Descendants<Text>().Select(t => t.Text));
        foreach (System.Text.RegularExpressions.Match m in OpenXmlHelpers.TokenRx.Matches(text))
            set.Add(m.Groups[1].Value);
        return set;
    }
}
