---
title: "Pivot HTML — Trình thiết kế mẫu (v2)"
description: ""
status: cancelled
priority: P2
branch: "main"
tags: []
blockedBy: []
blocks: []
created: "2026-07-03T09:53:03.891Z"
createdBy: "ck:plan"
source: skill
---

# Pivot HTML — Trình thiết kế mẫu (v2)

> **SUPERSEDED (2026-07-04):** các quyết định lõi (HTML/Jinja2/wkhtmltopdf/zone-binding/QWebEngine)
> bị thay bởi `plans/260704-1229-native-qt-pdf-designer-rebuild` (Native Qt + PDF, editor WYSIWYG)
> theo bản thiết kế Claude Design mới. Plan này không thực thi.

## Overview

Rebuild lõi v2: đổi output từ `.docx` sang **HTML** (Jinja2 + CSS, A4 `@page`), thêm
**trình thiết kế mẫu** trực quan — preview A4 bên trái (QWebEngineView), panel biến bên
phải, **click-to-bind** biến vào vùng (`data-zone`), toggle Edit↔Preview, tiến/lùi nhồi
từng hồ sơ. UI gom về **3 bước**. Xuất hàng loạt HTML song song + Excel tổng hợp. In PDF
qua trình duyệt (Ctrl+P) — **không** engine PDF nhúng để đóng gói exe an toàn.

**Nguồn thiết kế:** `plans/reports/brainstorm-html-designer-pivot-260703-1638-html-template-designer-report.md`

**Đầu vào:** đọc **file Excel `.xlsx` cục bộ** (chọn file → chọn sheet), **không** dùng Google
Sheet nữa → bỏ auth/URL/khóa Google (hết rủi ro bảo mật khóa). *[Validation S3.]*

**Supersedes:** engine docx của plan `260703-1127-pyqt5-mltl-docx-generator` (P3 docx_renderer,
P4 pdf_preview docx→PDF); **thay** `sheets_client` bằng `excel_reader` mới. Các phần **giữ nguyên
tái dùng**: `excel_exporter`, `batch_generator` (đổi hàm render), `style_config`+`models` (mở rộng
schema), `text_match`, `workers`, `platform_utils`, theme QSS.

## Quyết định đã chốt (brainstorm + Validation Session 1)

- Output **HTML hàng loạt (mặc định) + PDF theo yêu cầu** (bỏ .docx). Batch chỉ sinh `.html`
  (Jinja2, song song → nghìn hồ sơ vài giây, né nút cổ chai PDF). PDF sinh **theo yêu cầu** bằng
  **wkhtmltopdf**: (a) từng hồ sơ ở bước Preview, (b) tùy chọn "xuất PDF tất cả" (song song, **có
  cảnh báo chậm** ~phút cho nghìn hồ sơ). *[Validation S2: tách PDF khỏi batch để giữ tốc độ.]*
- **Gán biến vào vùng** (không tọa độ tự do). Cơ chế bind **qua panel bên** (hoạt động với mọi
  engine preview); **click-trên-canvas** chỉ là tăng cường khi dùng QWebEngine.
- **Engine preview chốt ở P6** sau khi test đóng gói: QWebEngineView (fidelity + click-canvas,
  cần PyQtWebEngine — chưa cài, exe nặng) **hoặc** QTextBrowser (nhẹ, chỉ hiển thị). P1–P5 **không
  phụ thuộc** engine cụ thể (dựng sau abstraction). *[Validation: hoãn quyết định tới P6.]*
- **Thư viện mẫu** (đa mẫu chọn/switch), **không** builder layout tự do.
- Giữ **Excel** + **xuất song song** (ProcessPoolExecutor).
- Ảnh chữ ký = **tham chiếu đường dẫn** `<img src="...">` (đi kèm file khi copy). *[Validation: đổi
  từ data-URI.]* wkhtmltopdf cần `--enable-local-file-access` + base path để nạp ảnh cục bộ.
- Module docx cũ (`docx_renderer`, `pdf_preview`, 5 tab UI...) **xóa hẳn ở P6**.

## Acceptance Criteria (toàn plan)

- [ ] **Đầu vào Excel:** chọn file `.xlsx` → liệt kê sheet → chọn sheet → đọc đúng dữ liệu (validate header rỗng/trùng); không còn Google auth/URL.
- [ ] Đi hết 3 bước → **batch xuất N `.html`** đúng dữ liệu (nghìn hồ sơ trong vài giây).
- [ ] **PDF theo yêu cầu:** xuất PDF 1 hồ sơ ở bước Preview đúng (bảng dài chảy đúng qua trang, footer Trang x/y, ảnh chữ ký). Tùy chọn "xuất PDF tất cả" chạy song song + cảnh báo thời gian.
- [ ] Bước 2: chọn biến ở panel + gán vào vùng → preview cập nhật; tiến/lùi xem đúng từng hồ sơ; toggle Edit/Preview. (Click-canvas là bonus nếu dùng QWebEngine.)
- [ ] Thêm/đổi mẫu HTML trong `styles/` dùng lại không sửa code.
- [ ] Excel tổng hợp + batch HTML song song vẫn hoạt động; log realtime (hồ sơ nào xong / tổng / thời gian).
- [ ] Đóng gói `.exe` Windows chạy sạch (kèm wkhtmltopdf binary + engine preview đã chốt), không lib PDF native khó.
- [ ] Không còn phụ thuộc docxtpl/python-docx/PyMuPDF cho luồng chính (đã xóa).

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [HTML Renderer & Template Format](./phase-01-html-renderer-template-format.md) | Pending |
| 2 | [Batch Generator HTML](./phase-02-batch-generator-html.md) | Pending |
| 3 | [Interactive Preview (QWebEngine)](./phase-03-interactive-preview-qwebengine.md) | Pending |
| 4 | [UI Rebuild 3 Steps](./phase-04-ui-rebuild-3-steps.md) | Pending |
| 5 | [Template Library (Multi-template)](./phase-05-template-library-multi-template.md) | Pending |
| 6 | [Packaging & Cleanup](./phase-06-packaging-cleanup.md) | Pending |

## Dependencies

Chuỗi: P1 → (P2, P3 song song được) → P4 → P5 → P6.
Cross-plan: **supersedes** engine docx của `260703-1127-pyqt5-mltl-docx-generator` (đã implemented);
không hard-block vì plan cũ đã xong, nhưng P6 sẽ gỡ/deprecate module docx cũ.

## Tech Stack

PyQt5 · **Jinja2 (mới)** · **wkhtmltopdf binary (mới)** · **pandas + openpyxl** (đọc Excel qua
`pandas.read_excel`, engine openpyxl — đã có) · PyInstaller. Engine preview (PyQtWebEngine **hoặc**
QTextBrowser): **chốt ở P6**. Bỏ: **gspread, google-auth** (không còn Google Sheet), docxtpl,
python-docx, PyMuPDF, LibreOffice.

## Rủi ro chính

| Rủi ro | Giảm thiểu |
|---|---|
| Bundle wkhtmltopdf binary trong exe + resolve path (dev/frozen) | `resolve_wkhtmltopdf()` như `resolve_soffice`; test đóng gói ở P6 |
| PDF chậm ở quy mô nghìn (wkhtmltopdf khởi động process/file) | **Batch chỉ HTML**; PDF theo yêu cầu (1 hồ sơ) hoặc "xuất tất cả" song song + cảnh báo thời gian |
| Engine preview (WebEngine nặng vs QTextBrowser CSS hạn chế) | P1–P5 sau abstraction; chốt ở P6 sau test bundle |
| Số trang footer chỉ trong PDF (CSS `@page`), không thấy trên preview màn hình | Chấp nhận; ghi chú UI |
| Vứt bỏ engine docx vừa xây | Thực tế pivot; giữ excel_exporter/batch/UI khung/text_match/workers |

## Câu hỏi còn treo

Không còn (đã chốt ở Validation Session 1). Engine preview cụ thể sẽ xác định ở P6 dựa test đóng gói.

## Validation Log

### Verification Results (Full tier)
- Claims checked: 4 nhóm | Verified: 4 | Failed: 0 | Unverified: 0
- `jinja2.meta.find_undeclared_variables` ✓ · `PyQt5.QtWebChannel` ✓ (có sẵn; chỉ thiếu PyQtWebEngine=view) ·
  reuse surface (`docx_renderer` helpers, `excel_exporter`, `batch_generator`, `text_match`) ✓ ·
  `excel_exporter` format-agnostic (không ref docx) ✓.

### Session 1 (2026-07-03) — Quyết định chốt
1. **Output:** HTML **+ PDF tự động** (đổi từ HTML-only). PDF qua **wkhtmltopdf** (process-safe → song song).
2. **Engine preview:** **hoãn chốt tới P6**; P1–P5 dựng sau abstraction, bind qua panel (không phụ thuộc view).
3. **Ảnh chữ ký:** tham chiếu đường dẫn (đổi từ data-URI).
4. **Dọn code:** xóa hẳn module docx cũ ở P6.

Phase propagation: P1 (ảnh path + note wkhtmltopdf base path), P2 (thêm bước HTML→PDF wkhtmltopdf song song +
`resolve_wkhtmltopdf`), P3 (preview sau abstraction, bind panel-based, click-canvas = bonus WebEngine),
P4 (bind qua panel, không bắt buộc click-canvas), P6 (bundle wkhtmltopdf + chốt engine preview + xóa docx).

### Session 2 (2026-07-03) — Chế độ HTML-hàng-loạt + PDF-theo-yêu-cầu
Lý do: wkhtmltopdf khởi động 1 process/file → PDF hàng loạt nghìn hồ sơ tốn ~phút (nút cổ chai).
Chốt: **batch chỉ sinh HTML** (song song, nghìn hồ sơ vài giây); **PDF theo yêu cầu** — (a) 1 hồ sơ ở
bước Preview, (b) tùy chọn "xuất PDF tất cả" (song song + cảnh báo thời gian). `html_to_pdf` (wkhtmltopdf)
vẫn giữ nhưng gọi on-demand, không nằm trong worker batch mặc định.
Phase propagation: P2 (batch HTML-only; `html_to_pdf` tách riêng gọi on-demand), P4 (nút "Xuất PDF hồ sơ này"
ở step_design; tùy chọn "Xuất PDF tất cả" ở step_run).

### Session 3 (2026-07-03) — Đầu vào Excel thay Google Sheet
Chốt: đọc **file `.xlsx` cục bộ** (chọn file → chọn sheet) thay vì Google Sheet. Bỏ auth/URL/khóa
Google → đơn giản hơn, hết rủi ro bảo mật khóa. Module mới `app/core/excel_reader.py`
(`list_sheets(path)`, `read_df(path, sheet)`, validate header) thay `sheets_client`; dùng
`pandas.read_excel` (openpyxl). Bỏ dep gspread + google-auth.
Phase propagation: P4 (step_input: Browse `.xlsx` + dropdown sheet + đọc bằng `excel_reader`),
P6 (bỏ gspread/google-auth khỏi requirements — vốn không nằm trong luồng HTML).

### Whole-Plan Consistency Sweep (sau S3)
Re-đọc plan.md + 6 phase: "HTML-only"→"HTML batch + PDF on-demand" đồng bộ; "QWebEngineView bắt buộc"
→"engine preview chốt ở P6" nhất quán P3↔P4↔P6; ảnh data-URI→path nhất quán P1↔P2; PDF tách khỏi batch
nhất quán P2↔P4; **đầu vào Google Sheet→Excel** nhất quán overview/AC/tech/P4. **0 mâu thuẫn tồn đọng.**
