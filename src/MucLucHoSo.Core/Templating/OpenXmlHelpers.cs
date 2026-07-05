using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MucLucHoSo.Core.Templating;

internal static class OpenXmlHelpers
{
    public static readonly Regex TokenRx = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

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
    public static void ReplaceTokens(OpenXmlElement scope, Func<string, string?> resolve,
                                     IReadOnlySet<string>? autoFields = null, string? highlightVar = null)
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

            if (!hasField)
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

            // có field: dựng lại run thành chuỗi run/field xen kẽ
            var rpr = r.GetFirstChild<RunProperties>();
            var parent = r.Parent!;
            var pieces = new List<OpenXmlElement>();
            int last = 0;
            foreach (Match m in TokenRx.Matches(s))
            {
                if (m.Index > last) pieces.Add(MkRun(rpr, s[last..m.Index]));
                var name = m.Groups[1].Value;
                if (autoFields!.Contains(name))
                    pieces.Add(MkField(rpr, name == "trang_so" ? "PAGE" : "NUMPAGES"));
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
    public static void ReplaceEverywhere(DocumentFormat.OpenXml.Packaging.MainDocumentPart main,
        Func<string, string?> resolve, IReadOnlySet<string>? autoFields = null, string? highlightVar = null)
    {
        ReplaceTokens(main.Document.Body!, resolve, autoFields, highlightVar);
        foreach (var hp in main.HeaderParts) ReplaceTokens(hp.Header, resolve, autoFields, highlightVar);
        foreach (var fp in main.FooterParts) ReplaceTokens(fp.Footer, resolve, autoFields, highlightVar);
    }
}
