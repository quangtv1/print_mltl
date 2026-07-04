---
phase: 2
title: "Qt PDF Renderer"
status: completed
priority: P1
dependencies: [1]
---

# Phase 2: Qt PDF Renderer

## Overview
Engine lõi: nhận `template_html` + 1 group record → dựng `QTextDocument`, thay token, **nhân dòng
bảng** theo record, in ra **PDF** (`QPrinter`/`QPdfWriter`) với header bảng lặp qua trang + footer
"Trang x/y". Đây là phần rủi ro nhất; dùng lại ở P3 (batch) và P5 (preview).

## Requirements
- Functional: `render_group_pdf(template_html, doc_fields, records, out_path, settings, stt_file)`
  → 1 PDF A4. Preview: `render_group_document(...) -> QTextDocument` (không in) cho P5.
- Non-functional: chạy trong QThread được (không đụng widget GUI); tách render (build document) khỏi
  in (paint) để P5 tái dùng phần build.

## Architecture
`app/core/qt_pdf_renderer.py`:
- **Grouping/records** (chuyển từ `docx_renderer.py`, giữ hành vi) — **tên symbol THẬT** (Red Team #7):
  `group_dataframe(style, df)`, `df_to_records(df)`, `build_context(...)`, `_document_field_values(...)`,
  `resolve_output_name(style, ho_so_so)`, `sanitize_filename`, `make_unique_path`. (KHÔNG có
  `resolve_document_fields`/`format_output_name` trong code cũ — phải **viết mới** `format_output_name`.)
- **Token substitution (Red Team #8):** **1 pass** bằng 1 regex compiled `\{(\w+)\}` với hàm thay tra
  context dict đã resolve (token lạ → giữ literal hoặc rỗng) — **không** `str.replace` tuần tự từng token
  (tránh double-substitution/injection từ dữ liệu chứa `{...}`). **Escape MỌI giá trị Excel** (doc fields
  **và** ô bảng) qua 1 helper `html.escape(str(v), quote=True)` **trước** khi chèn — giữ tương đương
  `autoescape=True` của engine cũ (`docx_renderer.py:6,119`).
- **Bảng — nhân dòng ở tầng object, KHÔNG string (Red Team #15):** đánh **marker bảng dữ liệu** (vd
  `QTextTable` có 1 property/first-cell sentinel, hoặc quy ước bảng đầu tiên chứa token `row_mapping`);
  sau `setHtml`, tìm `QTextTable` đó qua `QTextDocument`, `insertRows` N và fill ô bằng `QTextCursor`
  — không cắt/dán chuỗi `<tr>` trong `toHtml()`. Áp `QTextTableFormat.setHeaderRowCount(1)` **lập trình**
  sau `setHtml` (không biểu diễn được trong HTML). Định nghĩa rõ luật nhận dạng khi có 0/nhiều dòng-mẫu (báo lỗi).
- **Build document:** `QTextDocument`, `setHtml(filled_html)`, set `QTextTableFormat.setHeaderRowCount(1)`
  cho bảng, `setPageSize(A4)`, lề, font từ `settings.font_name`.
- **In PDF (page-by-page cho footer):**
  - `QPrinter(QPrinter.HighResolution)` hoặc `QPdfWriter(out_path)`, `setPageSize(A4)`.
  - Nếu cần footer "Trang x/y": lặp trang qua `doc.documentLayout()`/`QAbstractTextDocumentLayout`,
    `painter.drawText` footer mỗi trang với `{trang_so}/{tong_so_trang}`. Nếu không có footer →
    `doc.print_(printer)` đơn giản.
- **Auto vars:** `{stt_file}` truyền từ ngoài (index group), `{ngay_gio}` = now. `{trang_so}`/
  `{tong_so_trang}` **chỉ ở footer settings** (render lúc in), **fill_template phải strip/từ chối** 2
  token này nếu lọt vào body (Red Team #9) — chúng không có giá trị runtime khi substitute body.
- **`format_output_name(pattern, ctx)` (VIẾT MỚI, Red Team #1/#4-filename):** expand đầy đủ token
  (`{ho_so_so}`, `{stt_file}`, `{ngay_gio}`, …) → `sanitize_filename` trên chuỗi **đã expand** → **cắt
  độ dài an toàn** (stem ≤ ~120 ký tự). Không phải đổi tên `resolve_output_name` (vốn chỉ thay `{ho_so_so}`).

## Related Code Files
- Create: `app/core/qt_pdf_renderer.py`
- Reference/di chuyển: `app/core/docx_renderer.py` (lấy `group_dataframe`, `df_to_records`,
  `_resolve_ho_so_so`, sanitize filename — bỏ phần docxtpl)
- Modify: `app/models/style.py` (dùng AUTO_VARS từ P1)
- Delete (P6): `docx_renderer.py` sau khi rút hết logic tái dùng

## Implementation Steps
1. Chuyển các helper grouping/records/filename từ `docx_renderer.py` sang `qt_pdf_renderer.py` (thuần, không Qt).
2. Viết `fill_template(template_html, ctx, records)` — thay token header/footer + nhân dòng bảng + escape.
3. Viết `build_document(filled_html, settings) -> QTextDocument` (header row lặp, A4, font, lề).
4. Viết `render_group_pdf(...)` — build + in page-by-page với footer trang; fallback `print_` nếu tắt footer.
5. Test tay: 1 group nhiều dòng (bảng tràn ≥2 trang) → mở PDF, kiểm header lặp + footer đúng số trang.

## Success Criteria
- [ ] 1 group → 1 PDF A4 đúng: header, bảng đủ dòng, cột đúng thứ tự/độ rộng.
- [ ] Bảng dài ≥2 trang: **header bảng lặp** đầu mỗi trang; footer "Trang x/y" đúng.
- [ ] Dữ liệu chứa `& < >` không phá layout.
- [ ] `build_document` gọi được từ QThread (không cần widget); phần build tách khỏi in.
- [ ] `{stt_file}`, `{ngay_gio}` render đúng.

## Red Team Fixes (áp dụng 2026-07-04)
- **#8** escape + 1-pass regex (đã sửa Architecture); **#7** tên helper thật + `format_output_name` viết mới.
- **#9** footer `{trang_so}/{tong_so_trang}` = settings-only, strip khỏi body.
- **#15** nhân dòng ở tầng `QTextTable` + marker; `setHeaderRowCount` áp lập trình sau `setHtml`.
- **#10 — vòng in per-page cụ thể + test mất dòng:** `for p in range(doc.pageCount()): painter.save();
  painter.translate(0, -p*pageHeightPx); painter.setClipRect(pageRect); doc.drawContents(painter, pageRect);
  painter.restore(); vẽ footer tại vị trí thiết bị cố định`. **Success criteria bắt buộc:** tài liệu ≥3
  trang → **đếm số dòng bảng trong PDF = số record** (không mất/nhân đôi dòng ở ranh giới trang).
  <!-- Updated: Validation Session 1 - paged preview -->**Tách hàm render 1 trang → `QImage`** (cùng
  logic clip/translate/footer) để **P5 preview phân trang** tái dùng, không chỉ ghi PDF.
- **Font embedding (Red Team minor):** bật nhúng font trong PDF writer (`QPdfWriter`/`QPrinter`
  `setFontEmbeddingEnabled`-tương-đương) + pin font render; **gate acceptance P2 phải chạy trên Windows/
  PyInstaller**, không chỉ macOS (metric font khác → lệch ngắt trang/footer).

## Risk Assessment
- QTextTable + `setHeaderRowCount` là điểm chốt lặp header — verify sớm bằng document nhiều trang.
- Footer per-page cần map layout→page; nếu phức tạp, fallback: dùng `QTextDocument` với block footer lặp cuối, hoặc chấp nhận số trang chỉ khi in. Ghi chú rõ trade-off.
- Font Times New Roman phải có trên máy đích/đóng gói (Windows có sẵn).
