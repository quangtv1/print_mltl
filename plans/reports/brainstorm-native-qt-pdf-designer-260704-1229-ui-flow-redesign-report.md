---
title: "Brainstorm — Cập nhật UI + luồng logic theo bản thiết kế mới (Native Qt, chỉ PDF)"
date: 2026-07-04
type: brainstorm-report
status: approved
supersedes_core: plans/260703-1651-html-template-designer-pivot (HTML/Jinja2/wkhtmltopdf/zone-binding)
design_source: "Giao diện Print_MLTL_A4.zip (Claude Design — 'Trình thiết kế mẫu')"
modes: []
---

# Brainstorm: UI + luồng theo bản thiết kế mới (Native Qt + PDF)

## Vấn đề gốc
App hiện tại = bản cũ **5 tab docx + Google Sheet** (chưa pivot). Plan pivot HTML `260703-1651`
đã viết P1–P6 **nhưng chưa code**. User đưa **bản thiết kế mới (Claude Design)** = wizard 3 bước
Windows Fluent + trình soạn thảo mẫu WYSIWYG + xuất PDF. Yêu cầu: cập nhật UI + luồng logic
**theo đúng bản thiết kế**.

Bản thiết kế mới **lệch** plan cũ ở 3 điểm cốt lõi → phải chốt lại hướng (đã chốt qua hỏi đáp).

## Quyết định đã chốt (từ user, 2026-07-04)

| Hạng mục | Chọn |
|---|---|
| Engine bước 2 + xuất | **A. Native Qt** — `QTextEdit` editor + `QTextDocument`→`QPrinter` PDF |
| Định dạng xuất | **Chỉ PDF** (+ tùy chọn Excel tổng hợp) |
| Phạm vi | **Thay hẳn app cũ** (gỡ 5 tab docx + Google Sheet) |
| Độ sâu editor | **Sửa nội dung + định dạng đầy đủ** (B/I/U, cỡ chữ, màu chữ) |

## 3 điểm bản thiết kế mới lệch plan cũ (đã giải quyết)

| # | Plan cũ `260703-1651` | Bản thiết kế mới → chốt |
|---|---|---|
| D1 cơ chế bước 2 | Gán biến vào `data-zone` (click-to-bind), Jinja2, QWebEngine | **Editor WYSIWYG, chèn `{token}` tại con trỏ** (Native Qt) |
| D2 output | Batch HTML + PDF wkhtmltopdf theo yêu cầu | **Chỉ PDF trực tiếp** (QTextDocument→QPrinter) |
| D3 visual | `demo_ui_mockup.py` sidebar tối `#2563eb` | **Fluent sáng, stepper ngang `#0078d7`** |

Cú pháp biến: `{single_brace}` (không Jinja2 `{{ }}`).

## Các hướng engine đã cân nhắc

- **A. Native Qt (CHỌN).** QTextEdit + QTextDocument→QPrinter. Khớp thiết kế, nhẹ nhất, tái dùng
  gần toàn bộ backend, bỏ hết dep nặng. `QTextTableFormat.setHeaderRowCount` lặp header bảng qua
  trang (gỡ rủi ro lớn). Điểm cần đo: batch song song (không process-safe như docxtpl).
- **B. HTML/Jinja2 + QWebEngine + wkhtmltopdf.** Fidelity CSS cao nhưng exe nặng (~150MB + binary),
  editor WYSIWYG trên HTML thô phức tạp, mâu thuẫn tinh thần thiết kế. Loại.
- **C. Giữ docx (docxtpl).** Tái dùng render cũ nhưng thiết kế vẽ PDF + editor, còn kẹt dep nặng
  (LibreOffice xem PDF). Loại.

## Kiến trúc mục tiêu

**Template = QTextDocument (HTML nội bộ Qt) + `{token}`**, lưu trong `style.json` mở rộng
(`template_html` + format). Tài liệu thật = **header + 1 bảng lặp dòng + footer**; mỗi hồ sơ
(group) = 1 file PDF.

**Render 1 hồ sơ:** clone document → thay token header/footer bằng `document_fields` → bảng: lấy
"dòng mẫu" (dòng chứa token row_mapping) → **nhân N dòng** theo record trong group → thay `{col}`.

**Xuất PDF:** `QTextDocument` → `QPrinter(PdfFormat)` / `QPdfWriter`. Header bảng lặp qua trang
bằng `setHeaderRowCount(1)`. Footer "Trang x/y" vẽ per-page trong vòng in thủ công.

**Bỏ hẳn:** docxtpl, python-docx, PyMuPDF, wkhtmltopdf, QWebEngine, gspread, google-auth.

### Bản đồ module

| Hành động | Module |
|---|---|
| Tạo | `app/core/excel_reader.py`, `app/core/qt_pdf_renderer.py`, `app/ui/step_input.py`, `app/ui/step_design.py`, `app/ui/step_run.py` |
| Viết lại | `app/ui/main_window.py` (QStackedWidget 3 bước + stepper Fluent), `app/ui/theme.py` (QSS `#0078d7`), `app/models/style.py` + `app/core/style_config.py` (schema `template_html`), `app/core/batch_generator.py` (render→qt_pdf) |
| Tái dùng | `app/core/text_match.py`, `app/core/excel_exporter.py`, `app/ui/workers.py`, `app/core/platform_utils.py`, grouping/df_to_records của renderer cũ |
| Xoá | `connect_tab`, `mapping_tab`, `settings_tab`, `preview_widget`, `generate_tab`, `sheets_client`, `docx_renderer`, `pdf_preview`, `template_introspect` |

### 3 bước UI (bám thiết kế)

1. **Đầu vào:** Browse `.xlsx` → dropdown Sheet → Đọc dữ liệu (status "OK đọc N dòng · M cột");
   chọn **Mẫu** (thư viện đa mẫu); bảng **ghép biến↔cột** auto-match (`text_match`) + đánh dấu
   "Tự khớp"; chọn **cột gom nhóm**.
2. **Thiết kế & Preview:** `QTextEdit` rich-text (B/I/U, A−/A+, màu chữ) sửa header/footer tự do
   + bảng (cột từ mapping, định dạng dòng mẫu đầy đủ); panel phải = biến theo nhóm + biến tự động
   (`{stt_file}`, `{ngay_gio}`, `{trang_so}/{tong_so_trang}`); đặt con trỏ → bấm biến → chèn token;
   toggle Chỉnh sửa↔Xem trước; ◀▶ duyệt group; Lưu mẫu / Đặt lại mặc định.
3. **Chạy:** thư mục + mẫu **Tên PDF** (`{stt_file}`, `{so_ho_so}`…); tùy chọn đa luồng / ghi đè /
   Excel tổng hợp; progress + **log realtime**; mở thư mục.

## Rủi ro & giảm thiểu

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Batch PDF không process-safe (QTextDocument/QPrinter cần QApplication) | Cao | **QThreadPool** (chung app, mỗi thread 1 QTextDocument+QPdfWriter) thay ProcessPool; đo ở lúc implement; layout là C++ nên nghìn file vẫn ổn |
| Footer "Trang x/y" phải vẽ thủ công per-page | TB | Vòng in page-by-page, drawText footer mỗi trang |
| Nhận diện "dòng mẫu lặp" trong bảng | TB | Quy ước: dòng chứa token `row_mapping` = dòng lặp |
| Đóng gói exe (QPrinter/QPdfWriter cần plugin Qt in ấn) | TB | Test PyInstaller sớm; QPdfWriter thuộc QtGui (nhẹ) |
| Bỏ khả năng sửa bằng Word | Chấp nhận | User đã chốt chỉ PDF |

## Tiêu chí thành công
- Đi hết 3 bước với file Excel thật → xuất N PDF đúng dữ liệu, bảng dài chảy đúng qua trang, header bảng lặp, footer "Trang x/y".
- Bước 2: chèn biến tại con trỏ + định dạng B/I/U/màu; toggle Chỉnh sửa/Xem trước; ◀▶ duyệt hồ sơ đúng.
- Thêm/đổi mẫu trong thư viện dùng lại không sửa code.
- Excel tổng hợp + chạy song song + log realtime hoạt động.
- Đóng gói `.exe` Windows sạch, không dep native khó (không docx/wkhtmltopdf/QWebEngine/Google).

## Supersession
Thay các quyết định lõi của plan `260703-1651` (HTML/Jinja2/wkhtmltopdf/zone-binding/QWebEngine).
Giữ tinh thần: 3 bước, đa mẫu, Excel input, batch song song, Excel tổng hợp. Plan cũ nên đánh dấu
superseded khi plan mới tạo.

## Câu hỏi còn treo
1. Cách chạy song song cuối cùng (QThreadPool vs ProcessPool + QGuiApplication offscreen) — chốt
   bằng đo hiệu năng ở phase batch, không chặn thiết kế.
2. Thư viện đa mẫu: giữ nhiều mẫu (thiết kế vẽ "Mẫu 01/02…") — mặc định giữ; xác nhận nếu muốn tối giản 1 mẫu.
