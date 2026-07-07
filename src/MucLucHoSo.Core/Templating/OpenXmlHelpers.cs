using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace MucLucHoSo.Core.Templating;

internal static class OpenXmlHelpers
{
    public static readonly Regex TokenRx = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

    // ===== Biến ẢNH: nhận diện bằng alt-text/tên ảnh bắt đầu bằng "image" =====

    /// <summary>Tên biến ảnh của một Drawing (alt-text/title/name bắt đầu bằng "image"), hoặc null.</summary>
    public static string? ImageMarker(Drawing d)
    {
        foreach (var dp in d.Descendants<DW.DocProperties>())
        {
            var s = PickImage(dp.Description?.Value, dp.Title?.Value, dp.Name?.Value);
            if (s != null) return s;
        }
        foreach (var cn in d.Descendants<PIC.NonVisualDrawingProperties>())
        {
            var s = PickImage(cn.Description?.Value, cn.Name?.Value);
            if (s != null) return s;
        }
        return null;
    }

    private static string? PickImage(params string?[] vals)
    {
        foreach (var v in vals)
            if (!string.IsNullOrWhiteSpace(v) && v.Trim().StartsWith("image", StringComparison.OrdinalIgnoreCase))
                return v.Trim();
        return null;
    }

    /// <summary>Liệt kê tên biến ảnh trong một phạm vi (body/header/footer).</summary>
    public static IEnumerable<string> ImageMarkersIn(OpenXmlElement scope)
    {
        foreach (var d in scope.Descendants<Drawing>())
        {
            var m = ImageMarker(d);
            if (m != null) yield return m;
        }
    }

    /// <summary>
    /// Đổi RUỘT ảnh: với mỗi Drawing có alt-text khớp images[name], nạp file mới thành ImagePart
    /// và trỏ blip sang đó — giữ nguyên vị trí/kích thước/bao chữ đã vẽ trong Word.
    /// </summary>
    public static void ReplaceImages<T>(T part, OpenXmlElement scope, IReadOnlyDictionary<string, string> images)
        where T : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        if (images.Count == 0) return;
        foreach (var drawing in scope.Descendants<Drawing>().ToList())
        {
            var name = ImageMarker(drawing);
            if (name == null || !images.TryGetValue(name, out var path)) continue;
            var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
            if (blip == null || string.IsNullOrEmpty(blip.Embed?.Value)) continue;
            if (!File.Exists(path)) continue;

            var oldId = blip.Embed!.Value!;
            var imgPart = part.AddImagePart(ImageTypeOf(path));
            using (var fs = File.OpenRead(path)) imgPart.FeedData(fs);
            blip.Embed = part.GetIdOfPart(imgPart);
            try { part.DeletePart(part.GetPartById(oldId)); } catch { /* ảnh giả có thể dùng chung — bỏ qua */ }
        }
    }

    private static PartTypeInfo ImageTypeOf(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            ".bmp" => ImagePartType.Bmp,
            ".gif" => ImagePartType.Gif,
            ".tif" or ".tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Png,
        };

    /// <summary>Text đầy đủ của một paragraph (nối mọi Text).</summary>
    public static string ParagraphText(Paragraph p) =>
        string.Concat(p.Descendants<Text>().Select(t => t.Text));

    /// <summary>Gộp các run liền kề cùng định dạng (giảm phân mảnh, giúp placeholder liền mạch).</summary>
    public static void CoalesceRuns(OpenXmlElement scope)
    {
        foreach (var p in scope.Descendants<Paragraph>().ToList())
        {
            Run? prev = null; string prevRpr = "";
            foreach (var r in p.Elements<Run>().ToList())
            {
                // chỉ gộp run "thuần text" (có <w:t>, không có break/tab/hình)
                var t = r.GetFirstChild<Text>();
                bool simple = t != null && r.ChildElements.All(c => c is RunProperties || c is Text);
                var rpr = r.GetFirstChild<RunProperties>()?.OuterXml ?? "";
                if (simple && prev != null && rpr == prevRpr)
                {
                    var pt = prev.GetFirstChild<Text>()!;
                    pt.Text += t!.Text;
                    pt.Space = SpaceProcessingModeValues.Preserve;
                    r.Remove();
                }
                else { prev = simple ? r : null; prevRpr = rpr; }
            }
        }
    }

    private static Text MkText(string s) =>
        new(s) { Space = SpaceProcessingModeValues.Preserve };

    private static Run MkRun(RunProperties? rpr, string text)
    {
        var r = new Run();
        if (rpr != null) r.AppendChild((RunProperties)rpr.CloneNode(true));
        r.AppendChild(MkText(text));
        return r;
    }

    /// <summary>Run chứa một field đơn giản (PAGE / NUMPAGES) — Word/LibreOffice tự tính khi render.</summary>
    private static SimpleField MkField(RunProperties? rpr, string instruction)
    {
        var fld = new SimpleField { Instruction = instruction };
        fld.AppendChild(MkRun(rpr, "1")); // giá trị cache tạm; sẽ được cập nhật khi mở/xuất
        return fld;
    }

    /// <summary>
    /// Thay mọi {var} trong phạm vi 'scope'.
    /// resolve(var) -> giá trị text (thay bằng text) hoặc null (giữ nguyên token).
    /// autoFields: token thuộc nhóm này -> chèn field PAGE/NUMPAGES thay vì text.
    /// </summary>
    public static void ReplaceTokens<TPart>(TPart owner, OpenXmlElement scope, Func<string, string?> resolve,
                                     IReadOnlySet<string>? autoFields = null, string? highlightVar = null,
                                     IReadOnlySet<string>? imageTokens = null)
        where TPart : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        foreach (var r in scope.Descendants<Run>().ToList())
        {
            var t = r.GetFirstChild<Text>();
            if (t == null) continue;
            var s = t.Text;
            if (s.IndexOf('{') < 0) continue;
            if (!TokenRx.IsMatch(s)) continue;

            bool hasHighlight = highlightVar != null && s.Contains("{" + highlightVar + "}");
            bool hasField = autoFields != null &&
                TokenRx.Matches(s).Any(m => autoFields.Contains(m.Groups[1].Value));
            bool hasImage = imageTokens != null &&
                TokenRx.Matches(s).Any(m => imageTokens.Contains(m.Groups[1].Value));

            if (!hasField && !hasImage)
            {
                // chỉ thay text
                t.Text = TokenRx.Replace(s, m =>
                {
                    var v = resolve(m.Groups[1].Value);
                    return v ?? m.Value; // null -> giữ token
                });
                t.Space = SpaceProcessingModeValues.Preserve;
                if (hasHighlight) ApplyHighlight(r);
                continue;
            }

            // có field / ảnh: dựng lại run thành chuỗi run/field/ảnh xen kẽ
            var rpr = r.GetFirstChild<RunProperties>();
            var parent = r.Parent!;
            var pieces = new List<OpenXmlElement>();
            int last = 0;
            foreach (Match m in TokenRx.Matches(s))
            {
                if (m.Index > last) pieces.Add(MkRun(rpr, s[last..m.Index]));
                var name = m.Groups[1].Value;
                if (autoFields != null && autoFields.Contains(name))
                    pieces.Add(MkField(rpr, name == "trang_so" ? "PAGE" : "NUMPAGES"));
                else if (imageTokens != null && imageTokens.Contains(name))
                    pieces.Add(MkImageRun(owner, resolve(name)));   // giá trị = đường dẫn ảnh
                else
                {
                    var v = resolve(name);
                    pieces.Add(MkRun(rpr, v ?? m.Value));
                }
                last = m.Index + m.Length;
            }
            if (last < s.Length) pieces.Add(MkRun(rpr, s[last..]));

            foreach (var pc in pieces) parent.InsertBefore(pc, r);
            r.Remove();
        }
    }

    /// <summary>Tô nền vàng cho run (dùng khi xem trước để nổi bật biến).</summary>
    private static void ApplyHighlight(Run r)
    {
        var rpr = r.GetFirstChild<RunProperties>();
        if (rpr == null) { rpr = new RunProperties(); r.InsertAt(rpr, 0); }
        rpr.RemoveAllChildren<Shading>();
        rpr.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "FFFF00" });
    }

    /// <summary>Áp dụng ReplaceTokens cho body + mọi header/footer part.</summary>
    public static void ReplaceEverywhere(MainDocumentPart main,
        Func<string, string?> resolve, IReadOnlySet<string>? autoFields = null, string? highlightVar = null,
        IReadOnlySet<string>? imageTokens = null)
    {
        ReplaceTokens(main, main.Document.Body!, resolve, autoFields, highlightVar, imageTokens);
        foreach (var hp in main.HeaderParts) ReplaceTokens(hp, hp.Header, resolve, autoFields, highlightVar, imageTokens);
        foreach (var fp in main.FooterParts) ReplaceTokens(fp, fp.Footer, resolve, autoFields, highlightVar, imageTokens);
    }

    /// <summary>Run chứa ảnh inline từ đường dẫn (kích thước = tự nhiên của ảnh; lỗi/không có → run rỗng).</summary>
    private static OpenXmlElement MkImageRun<TPart>(TPart owner, string? path)
        where TPart : OpenXmlPart, ISupportedRelationship<ImagePart>
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return new Run();
        var imgPart = owner.AddImagePart(ImageTypeOf(path));
        using (var fs = File.OpenRead(path)) imgPart.FeedData(fs);
        var relId = owner.GetIdOfPart(imgPart);
        var (cx, cy) = ImageSizeEmu(path);
        return new Run(BuildInlineDrawing(relId, cx, cy));
    }

    private static Drawing BuildInlineDrawing(string relId, long cx, long cy) =>
        new Drawing(new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.DocProperties { Id = 1U, Name = "img" },
            new A.Graphic(new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = "img" },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(new A.Blip { Embed = relId }, new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                        new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
            ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

    // Kích thước tự nhiên (px) -> EMU (1px @96dpi = 9525 EMU); không đọc được -> ~2cm vuông.
    private static (long cx, long cy) ImageSizeEmu(string path)
    {
        try { var (w, h) = ReadPixelSize(path); if (w > 0 && h > 0) return (w * 9525L, h * 9525L); }
        catch { }
        return (1905000L, 1905000L);
    }

    private static (int w, int h) ReadPixelSize(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        if (ext == ".png")
        {
            fs.Seek(16, SeekOrigin.Begin);
            return (ReadBE32(br), ReadBE32(br));
        }
        if (ext is ".jpg" or ".jpeg")
        {
            fs.Seek(2, SeekOrigin.Begin);
            while (fs.Position < fs.Length - 1)
            {
                if (br.ReadByte() != 0xFF) continue;
                byte marker = br.ReadByte();
                if (marker is >= 0xC0 and <= 0xC3)
                {
                    ReadBE16(br); br.ReadByte();           // length + precision
                    int h = ReadBE16(br), w = ReadBE16(br);
                    return (w, h);
                }
                int len = ReadBE16(br);
                if (len < 2) break;
                fs.Seek(len - 2, SeekOrigin.Current);
            }
        }
        return (0, 0);
    }

    private static int ReadBE32(BinaryReader b) { var x = b.ReadBytes(4); return (x[0] << 24) | (x[1] << 16) | (x[2] << 8) | x[3]; }
    private static int ReadBE16(BinaryReader b) { var x = b.ReadBytes(2); return (x[0] << 8) | x[1]; }
}
