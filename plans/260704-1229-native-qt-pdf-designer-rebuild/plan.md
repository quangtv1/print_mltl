---
title: "Native Qt + PDF — UI 3 bước theo bản thiết kế mới"
description: ""
status: completed
priority: P2
branch: "main"
tags: []
blockedBy: []
blocks: []
created: "2026-07-04T05:31:05.242Z"
createdBy: "ck:plan"
source: skill
---

# Native Qt + PDF — UI 3 bước theo bản thiết kế mới

## Overview

Cập nhật UI + luồng logic app "Tạo Mục Lục Hồ Sơ" theo **bản thiết kế mới (Claude Design)**:
wizard **3 bước** Windows Fluent (`#0078d7`), trình soạn thảo mẫu **WYSIWYG (QTextEdit)**, xuất
**chỉ PDF** bằng engine **Native Qt** (`QTextDocument` → `QPrinter`). **Thay hẳn** app cũ (5 tab
docx + Google Sheet). Đầu vào **file Excel `.xlsx`**. Mỗi hồ sơ (group) = 1 PDF; batch song song +
Excel tổng hợp + log realtime.

**Nguồn thiết kế:** `plans/reports/brainstorm-native-qt-pdf-designer-260704-1229-ui-flow-redesign-report.md`
**Bản thiết kế:** `Giao diện Print_MLTL_A4.zip` (Claude Design — "Trình thiết kế mẫu").

## Quyết định đã chốt (user, 2026-07-04)

- Engine: **Native Qt** — `QTextEdit` editor + `QTextDocument`→`QPrinter` PDF (bỏ HTML/Jinja2/wkhtmltopdf/QWebEngine).
- Output: **chỉ PDF** (+ tùy chọn Excel tổng hợp).
- Phạm vi: **thay hẳn** app cũ (gỡ 5 tab docx + Google Sheet).
- Editor: **sửa nội dung + định dạng đầy đủ** (B/I/U, cỡ chữ, màu chữ), chèn `{token}` tại con trỏ.
- Cú pháp biến: `{single_brace}`.

## Tài liệu đích

"MỤC LỤC VĂN BẢN TRONG HỒ SƠ" = **header** (Hồ sơ số, Phông số, tiêu đề, cơ quan…) + **1 bảng**
(Tt, Số ký hiệu, Ngày tháng, Tác giả, Trích yếu, Trang, Ghi chú — cột từ mapping) + **footer**
"Trang x/y". Bảng **lặp dòng** theo record trong group; header bảng lặp qua trang.

## Acceptance Criteria (toàn plan)

- [ ] Đầu vào: chọn `.xlsx` → liệt kê sheet → đọc đúng dữ liệu (validate header rỗng/trùng); không còn Google.
- [ ] Đi hết 3 bước với Excel thật → xuất **N PDF** đúng dữ liệu; bảng dài chảy đúng qua trang, header bảng lặp, footer "Trang x/y".
- [ ] Bước 2: chèn biến tại con trỏ + định dạng B/I/U/cỡ/màu; toggle Chỉnh sửa↔Xem trước; ◀▶ duyệt hồ sơ; Lưu mẫu / Đặt lại mặc định.
- [ ] Bước 3: batch song song + log realtime (hồ sơ nào xong / tổng / thời gian) + tùy chọn ghi đè + Excel tổng hợp.
- [ ] Thư viện đa mẫu: thêm/đổi mẫu dùng lại không sửa code.
- [ ] Không còn phụ thuộc docxtpl/python-docx/PyMuPDF/wkhtmltopdf/QWebEngine/gspread/google-auth trong luồng chính.
- [ ] Đóng gói `.exe` Windows chạy sạch (PDF qua QtGui, không lib native khó).

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Excel Reader & Style Schema](./phase-01-excel-reader-style-schema.md) | Done |
| 2 | [Qt PDF Renderer](./phase-02-qt-pdf-renderer.md) | Done |
| 3 | [Batch Generator & Excel](./phase-03-batch-generator-excel.md) | Done |
| 4 | [UI Shell & Step Input](./phase-04-ui-shell-step-input.md) | Done |
| 5 | [Step Design Editor](./phase-05-step-design-editor.md) | Done |
| 6 | [Step Run & Cleanup Packaging](./phase-06-step-run-cleanup-packaging.md) | Done |

## Trạng thái triển khai (2026-07-04)

Đã hoàn thành + kiểm thử headless (python3.10 / PyQt5 5.15.14, offscreen):
- **P1–P3** engine: excel_reader, qt_pdf_renderer (nhân dòng object-level, header lặp,
  footer page-by-page, escape 1-pass), batch_generator (serialize + dedup + log). Test:
  60 dòng→61 dòng bảng, PDF 3 trang, footer "Trang 1/3", dedup HS-001/_2/HS-002.
- **P4–P6** UI: theme Fluent + StepHeader, main_window 3 bước, step_input/step_design/
  step_run. Smoke full 3 bước → xuất 3 PDF + Excel qua QThread.
- **4 mẫu** đã seed `template_html` (van-phong-dat-dai, quang-ninh, dong-da, vinh-phuc) — render OK.
- **Cleanup:** đã xoá 9 module cũ + `print_mltl_parallel.py` + `build_default_template.py`;
  requirements gọn (PyQt5/pandas/openpyxl/pyinstaller); main.py bỏ freeze_support.
- **Còn lại (thủ công):** build PyInstaller `.exe` trên Windows (spec sẵn ở `packaging/mltl.spec`);
  key Google `get_link_pdf_.json` KHÔNG còn trên đĩa (khuyến nghị revoke/rotate nếu từng commit).

## Dependencies

Chuỗi: **P1 → P2 → P3**; **P1 → P4 → P5**; **(P3 + P5) → P6**.
P2 (renderer) dùng lại ở P5 (preview) và P3 (batch). P6 dọn code cũ + đóng gói.

**Cross-plan:**
- **Supersedes (core):** `260703-1651-html-template-designer-pivot` — plan này thay các quyết định
  lõi (HTML/Jinja2/wkhtmltopdf/zone-binding/QWebEngine → Native Qt/PDF/WYSIWYG). Plan 1651 nên đánh
  dấu `cancelled/superseded` khi khởi động P1.
- `260703-1127-pyqt5-mltl-docx-generator` (status: implemented) — P6 gỡ/deprecate module docx của plan này.

## Tái dùng / thay / xoá (bản đồ module)

| Hành động | Module |
|---|---|
| **Tạo** | `app/core/excel_reader.py`, `app/core/qt_pdf_renderer.py`, `app/ui/step_input.py`, `app/ui/step_design.py`, `app/ui/step_run.py` |
| **Viết lại** | `app/ui/main_window.py`, `app/ui/theme.py`, `app/models/style.py`, `app/core/style_config.py`, `app/core/batch_generator.py` |
| **Tái dùng** | `app/core/text_match.py`, `app/core/excel_exporter.py`, `app/ui/workers.py`, `app/core/platform_utils.py` |
| **Xoá (P6)** | `connect_tab`, `mapping_tab`, `settings_tab`, `preview_widget`, `generate_tab`, `sheets_client`, `docx_renderer`, `pdf_preview`, `template_introspect` |

## Rủi ro chính

| Rủi ro | Giảm thiểu |
|---|---|
| Batch PDF không process-safe (Qt cần QApplication) | **QThreadPool** chung app thay ProcessPool; đo hiệu năng ở P3 |
| Footer "Trang x/y" — QTextDocument không có footer sẵn | In page-by-page, drawText footer mỗi trang (P2) |
| Header bảng lặp qua trang | `QTextTableFormat.setHeaderRowCount(1)` (P2) |
| Đóng gói exe (plugin in ấn Qt) | Test PyInstaller sớm ở P6; QPdfWriter thuộc QtGui (nhẹ) |

## Câu hỏi còn treo

Không còn (đã chốt ở Validation Session 1). Song song: **serialize MVP, đo rồi mới tính** (không làm đa
luồng upfront). Nếu quá chậm mới cân nhắc QThreadPool/ProcessPool offscreen.

## Red Team Review

### Session — 2026-07-04
**Findings:** 15 (14 accepted, 1 rejected-by-user) — 4 hostile reviewers (Failure Mode, Assumption
Destroyer, Scope/Complexity, Security). Mọi finding có bằng chứng file:line; đã khử trùng lặp.
**Severity:** 4 Critical · 9 High · 2 Medium.

**Quyết định user:** giữ **đa mẫu = 4 mẫu** (Mẫu 01 Đông Hà `van-phong-dat-dai` đã có; 02 Quảng Ninh,
03 Đông Đa = docx; 04 Vĩnh Phúc = **PDF**) → Finding #13 (cắt đa mẫu) **Reject**. Hệ quả: Finding #3
(tạo template mặc định) nhân **×4** — dựng thêm 3 style dir + `template_html` cho cả 4 mẫu.

| # | Sev | Finding | Disp | Áp vào |
|---|-----|---------|------|--------|
| 1 | Crit | Tên file 1-token + đảo âm tiết `{so_ho_so}` → N hồ sơ đổ về 1 file (`resolve_output_name` chỉ thay `{ho_so_so}`) | Accept | P1, P2, P6 |
| 2 | Crit | Bỏ `make_unique_path` dedup + skip-check trong runnable song song → ghi đè im lặng, mất PDF | Accept | P3 |
| 3 | Crit | Không có nguồn `template_html` mặc định (`styles/` chỉ có `.docx` nhị phân) → **×4 mẫu** | Accept | P1 |
| 4 | Crit | QThreadPool render Qt không thread-safe (font/glyph cache chung) → crash/glyph hỏng | Accept | P3 |
| 5 | High | `QRunnable` không phát signal → cần `QObject` signaller + QueuedConnection | Accept | P3 |
| 6 | High | Token biến bịa (`so_ho_so`≠`ho_so_so`; `nguoi_lap`/`don_vi` không có; `tieu_de` là settings) | Accept | P1, P5 |
| 7 | High | Tên helper bịa (`resolve_document_fields`/`format_output_name` không tồn tại) | Accept | P2 |
| 8 | High | Escape HTML thiếu (chỉ dòng bảng, bỏ quote+doc-field) → thụt lùi `autoescape`; cần 1-pass regex | Accept | P2 |
| 9 | High | `{trang_so}/{tong_so_trang}` body-token nhưng chỉ resolve lúc in → in ra literal | Accept | P2, P5 |
| 10 | High | Preview không WYSIWYG cho footer/phân trang (cuộn liên tục) | Accept | P2, P5 |
| 11 | High | Sửa tự do đụng "dòng-mẫu bắt buộc"; thiếu gate validate + đồng bộ cột↔mapping | Accept | P5 |
| 12 | High | Khóa service-account `get_link_pdf_.json` còn trên đĩa + `print_mltl_parallel.py` hardcode | Accept | P6 |
| 13 | High | Thư viện đa mẫu YAGNI | **Reject** | — (user giữ 4 mẫu) |
| 14 | Med | Đồ thị xoá module chưa soát (`batch_generator.py:21`, `preview_widget.py:23-24`, `main_window.py:66-89`) | Accept | P6 |
| 15 | Med | Row-clone bằng string trên `toHtml()` mong manh + vòng in per-page dễ mất dòng | Accept | P2 |

**Gộp (minor, Accept):** font embedding + gate test Windows (không chỉ macOS); recompute state khi
Back (tránh KeyError `group_dataframe`); chống formula-injection khi ghi Excel tổng hợp; dọn
`freeze_support` + thêm `pyinstaller` vào requirements + tạo `.spec` (chưa có).

Chi tiết fix nằm ở mục **"Red Team Fixes"** trong từng phase file.

### Whole-Plan Consistency Sweep
Sau khi áp: token filename chuẩn hoá `{ho_so_so}` + auto-vars mới nhất quán P1↔P2↔P6; tên helper thật
nhất quán P2; đa mẫu ×4 nhất quán P1↔P4↔P5; footer settings-only nhất quán P2↔P5; dedup giữ ở P3;
xoá key Google ở P6. **0 mâu thuẫn tồn đọng.**

## Validation Log

### Verification Results
Bỏ qua verification pass — `## Red Team Review` đã có bằng chứng file:line (guard của validate). Không
còn tag `[UNVERIFIED]`. Failed: 0 → plan đủ điều kiện implement.

### Session 1 (2026-07-04) — 4 quyết định chốt
1. **Preview = phân trang thật (WYSIWYG đầy đủ)** — render theo trang A4, thấy footer/ngắt trang/header
   lặp; P2 tách hàm render-trang→QImage cho P5 tái dùng. *(Propagate: P2, P5)*
2. **Soạn tay `template_html` cả 4 mẫu ngay** (Vĩnh Phúc dựa PDF làm tham chiếu). *(P1)*
3. **Editor: header/footer sửa tự do, bảng khoá cột theo mapping** (không thêm/xoá cột tuỳ ý ở MVP). *(P5, khớp Red Team #11)*
4. **Song song: serialize MVP, đo rồi mới tính** — không đầu tư đa luồng upfront; checkbox "đa luồng"
   disabled tới khi có bản kiểm chứng. *(P3)*

### Whole-Plan Consistency Sweep (sau Session 1)
Preview paged nhất quán P2↔P5 (P2 expose render-trang, P5 dùng); serialize-first nhất quán P3↔UI (checkbox
disabled); 4 mẫu + khoá cột nhất quán P1↔P5. **0 mâu thuẫn tồn đọng.**
