---
phase: 5
title: "Step Design Editor"
status: completed
priority: P1
dependencies: [2, 4]
---

# Phase 5: Step Design Editor

## Overview
Bước 2 — Thiết kế & Preview: trình soạn thảo **WYSIWYG (`QTextEdit`)** sửa mẫu đầy đủ (B/I/U, cỡ,
màu), panel biến bên phải (đặt con trỏ → bấm biến → **chèn `{token}` tại con trỏ**), toggle
**Chỉnh sửa↔Xem trước**, ◀▶ duyệt từng hồ sơ (render qua P2), Lưu mẫu / Đặt lại mặc định.

## Requirements
- Functional: sửa & định dạng template; chèn biến tại con trỏ; xem trước render group hiện tại; điều
  hướng group; lưu `template_html` vào style.
- Non-functional: preview không treo; đổi Edit/Preview giữ nội dung mẫu.

## Architecture
`app/ui/step_design.py`:
- **Trái (3/4):** `QTextEdit` (chế độ Chỉnh sửa = sửa `template_html`; chế độ Xem trước = read-only,
  hiển thị `QTextDocument` đã render của group hiện tại từ `qt_pdf_renderer.render_group_document`).
- **Toolbar:** B / I / U (mergeCharFormat), A− / A+ (đổi pointSize), màu chữ (`QColorDialog`), toggle
  **Chỉnh sửa / Xem trước**, ◀ recLabel ▶.
- **Phải (1/4):** panel biến theo nhóm (Tài liệu / Hàng / Gom nhóm) + **Biến tự động** (`{stt_file}`,
  `{ngay_gio}`, `{trang_so}/{tong_so_trang}`); nguồn = AUTO_VARS (P1) + mapping (P4). Bấm biến →
  `textCursor().insertText("{var}")` vào QTextEdit (chỉ khi đang Chỉnh sửa) + ghi chú hướng dẫn.
- **Điều hướng:** dựng groups = `group_dataframe(style, df)`; ◀▶ đổi index → ở Preview re-render group đó.
- **Lưu:** `style.template_html = editor.toHtml()` → `save_style`. "Đặt lại mặc định" nạp template mẫu gốc từ `styles/<mẫu>/`.
- Toggle: Chỉnh sửa lưu html hiện tại vào buffer; Xem trước build document group hiện tại (không phá buffer edit).

## Related Code Files
- Create: `app/ui/step_design.py`
- Reuse: `app/core/qt_pdf_renderer.py` (`render_group_document`, `group_dataframe`), `app/core/style_config.save_style`, AUTO_VARS (P1)
- Modify: `app/ui/main_window.py` (nhúng step_design + truyền style/df)
- Delete (P6): `settings_tab.py`, `preview_widget.py`

## Implementation Steps
1. Layout trái/phải + toolbar; QTextEdit nạp `style.template_html` (hoặc template mặc định của mẫu).
2. Nút B/I/U/cỡ/màu thao tác trên `textCursor`/`mergeCurrentCharFormat`.
3. Panel biến: dựng cây nhóm từ mapping + AUTO_VARS; click → insert token tại con trỏ (Edit mode).
4. Toggle Chỉnh sửa/Xem trước: Preview gọi `render_group_document(style, records[idx])` → set vào QTextEdit read-only.
5. ◀▶ đổi group index (chỉ ý nghĩa ở Preview); recLabel "Hồ sơ k/N".
6. Lưu mẫu (`toHtml`→save_style) + Đặt lại mặc định.

## Success Criteria
- [ ] Chỉnh sửa nội dung + định dạng B/I/U/cỡ/màu hoạt động; nội dung giữ khi toggle.
- [ ] Bấm biến (panel) chèn `{token}` đúng vị trí con trỏ ở chế độ Chỉnh sửa.
- [ ] Xem trước render đúng group hiện tại (token thay giá trị, bảng đủ dòng); ◀▶ đổi hồ sơ đúng.
- [ ] Lưu mẫu ghi `template_html` vào style.json; mở lại giữ nguyên.
- [ ] Đặt lại mặc định nạp template gốc của mẫu.

## Red Team Fixes (áp dụng 2026-07-04)
- **#3** — editor nạp `style.template_html` **do P1 dựng cho từng mẫu** (không còn rỗng); "Đặt lại mặc định"
  nạp lại template gốc của mẫu đang chọn.
- **#11 (High) — gate validate + cố định shape bảng (MVP):** cột bảng **cố định theo `row_mapping`** của
  mẫu; **không** cho user thêm/xoá cột tuỳ ý ở MVP (đồng bộ cột↔mapping). Trước khi Lưu/Tiếp: **validate
  đúng 1 dòng-mẫu** chứa đủ token cột (marker bảng dữ liệu, xem P2); 0/nhiều → chặn + báo lỗi rõ. Cho phép
  định dạng (B/I/U/màu) nhưng cấm phá cấu trúc dòng-mẫu/bảng.
- **#9** — panel biến: `{trang_so}/{tong_so_trang}` **không** insert được vào body (footer-only, xám/nhãn
  "chỉ footer"); chỉ chèn doc-fields/row-cols/`{stt_file}`/`{ngay_gio}`.
- **#10 (High) — preview PHÂN TRANG THẬT (WYSIWYG đầy đủ)** <!-- Updated: Validation Session 1 - chọn paged preview -->:
  Xem trước render **theo trang A4**, thấy đúng **footer "Trang x/y" + ngắt trang + header bảng lặp** —
  dùng lại **cùng paged renderer của P2** (render từng trang ra ảnh/`QImage` qua `doc.drawContents`+footer
  painter, hiển thị dạng danh sách trang cuộn dọc), KHÔNG phải `setHtml` vào QTextEdit cuộn liên tục.
  ⇒ P2 phải expose hàm render-trang tái dùng cho preview (không chỉ ghi ra PDF).
- **toHtml không tin cậy (Red Team #Q):** khi nạp `template_html` (4 mẫu dùng chung/chia sẻ) → **strip
  `<img src>` trỏ `file:`/đường dẫn tuyệt đối**, chỉ giữ data-URI kiểm soát; cap kích thước template lưu.

## Risk Assessment
- QTextEdit `toHtml` sinh HTML rườm rà (style inline) → render P2 phải chấp nhận HTML của Qt (cùng engine QTextDocument nên khớp về **fidelity nội dung**; footer/phân trang KHÔNG khớp — xem #10). Không cần HTML "sạch".
- Insert token khi ở Preview (read-only) phải bị chặn — chỉ cho ở Edit.
- Đồng bộ cột bảng giữa mapping (P4) và template: dòng-mẫu trong template chứa token cột; đảm bảo template mặc định của mẫu khớp cột mapping.
