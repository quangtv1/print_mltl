---
phase: 3
title: "WebView2 Host Bridge & Template Engine"
status: pending
priority: P1
dependencies: [1, 2]
---

# Phase 3: WebView2 Host Bridge & Template Engine

## Overview
Lõi "giống 100%": host WebView2 nạp trang A4 của prototype (`App/Web/`), cầu nối C#↔JS đẩy template/record/
mapping vào trang và nhận HTML đã resolve + đo chiều cao, và `IPdfRenderer` in **1 hồ sơ → PDF** bằng
`CoreWebView2.PrintToPdfAsync`. Tái dùng **nguyên** engine phân trang của prototype (mota3 §8).

## Requirements
- Functional: khởi tạo WebView2 (env `%AppData%` user-data), load `Web/index.html`; API JS: `setTemplate(id)`,
  `setRecord(json)`, `setMapping(json)`, `resolveHtml()→string`, `measure()`; `PdfRenderer.RenderAsync(hoSo, template, mapping, outPath)`.
- Non-functional: chạy được **offscreen** (không hiện form) cho batch; preview==PDF (cùng HTML/engine).

## Architecture
- `App/Web/` (vendor, mota3 §8): giữ chuỗi HTML template + hàm `resolveDoc`/pagination (đo `getBoundingClientRect`)
  + `tokenSpan`/`defaultDoc(id)` + derive (`loai_vb` từ tiền tố trích yếu; `sl_trang` lấy từ Excel nếu có, else quy tắc).
  Thêm lớp mỏng `bridge.js`: expose `window.MLTL = { setTemplate, setRecord, setMapping, resolveHtml, measure, setMode, setZoom, setOrient }`.
- `App/WebHost/WebViewController.cs`: bọc `WebView2`; `EnsureCoreWebView2Async`; `ExecuteScriptAsync`/
  `PostWebMessageAsJson` gọi API JS; nhận kết quả qua `WebMessageReceived`. Dùng chung cho editor (P5) + renderer.
- `Core/Pdf/IPdfRenderer` → `App/Pdf/WebView2PrintRenderer` (ở App vì cần WebView2):
  `RenderAsync(hoSo, templateId, mapping, outPath)` → offscreen WebView2: setTemplate+setRecord+setMapping →
  chờ resolve/paginate → `CoreWebView2.PrintToPdfAsync(outPath, settings)` với `CoreWebView2PrintSettings`
  (A4, margin theo mota3 §3.3, `ShouldPrintBackgrounds=true`, scale).
- `TemplateCatalog`: 4 `TemplateDef` (id/name/vars) khớp prototype; đổi mẫu → panel/mapping cập nhật (P4/P5).

## Related Code Files
- Create: `App/WebHost/WebViewController.cs`, `App/Pdf/WebView2PrintRenderer.cs`, `App/Web/bridge.js`,
  `Core/Pdf/IPdfRenderer.cs`, `Core/Templating/TemplateCatalog.cs`
- Reference: `design_v3/mota3.html` §8 (engine/phân trang), §3.3 (kích thước A4/lề); vendored `Web/support.js`+`index.html`

## Implementation Steps
<!-- Updated: Validation Session 1 - spike PrintToPdf trước khi build tiếp -->
0. **SPIKE PrintToPdf (GATE bắt buộc, làm ĐẦU TIÊN):** dựng tối thiểu WebView2 + 1 hồ sơ nhiều trang → in
   `PrintToPdfAsync` → **so PDF với preview** (lề/scale/ngắt trang/header lặp/footer). **Chỉ khi khớp** mới
   làm tiếp P3/P4/P5/P6. Nếu lệch không tinh chỉnh được (`CoreWebView2PrintSettings`) → **chuyển Puppeteer NGAY**
   (cập nhật plan) trước khi dựng UI. Ghi kết quả spike vào `docs/`.
1. `bridge.js` expose API; verify load `index.html` trong WebView2, gọi `setTemplate('mau01')` render seed.
2. `WebViewController` (init, script-call, message round-trip) — dùng lại cho preview + renderer.
3. `WebView2PrintRenderer.RenderAsync` offscreen + `PrintToPdfAsync` (A4/lề) → 1 PDF.
4. Test tay (Windows): 1 hồ sơ nhiều dòng → PDF nhiều trang, header bảng lặp, footer "Trang x/y", **trùng preview**.

## Success Criteria
- [ ] WebView2 load trang A4 prototype; `setTemplate/setRecord/setMapping` đổi nội dung đúng.
- [ ] 1 hồ sơ → 1 PDF A4 qua `PrintToPdfAsync`; bảng dài chảy nhiều trang; header bảng lặp; footer số trang sát đáy.
- [ ] **PDF trùng khớp preview** (cùng HTML đã resolve).
- [ ] Renderer chạy **offscreen** (không hiện form) → sẵn cho batch P6.

## Risk Assessment
- **Nghiệm thu trên Windows** (WebView2 không có trên macOS) — CI windows-latest / máy thật; đây là gate then chốt sớm.
- `PrintToPdfAsync` margin/scale khác kỳ vọng → tinh chỉnh `CoreWebView2PrintSettings`; nếu vẫn lệch phân trang
  (do print-scaling), cân nhắc để prototype tự set `@page`/khổ CSS px→mm; fallback Puppeteer (đã ghi ở plan).
- Đồng bộ async: chờ engine phân trang xong (JS báo `ready`) trước khi PrintToPdf — dùng message round-trip, không sleep cứng.
- WebView2 user-data-folder cần ghi được (đặt `%AppData%\MLTL\WebView2`).
