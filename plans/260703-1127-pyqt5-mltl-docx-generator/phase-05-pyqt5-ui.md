---
phase: 5
title: "PyQt5 UI"
status: done
effort: ""
---

# Phase 5: PyQt5 UI

## Overview
Giao diện PyQt5 5 bước nối các module core: kết nối → lấy biến → template+mapping → settings+preview → (generate ở P6). Đây là lớp trình bày, không chứa nghiệp vụ.

## Requirements
- Functional: 5 tab/wizard theo luồng brainstorm; mapping tự khớp + sửa tay; chọn cột gom nhóm; form settings; preview PDF nhúng.
- Non-functional: thao tác nặng (kết nối, preview) không treo UI (chạy QThread/async nhẹ); state lưu vào `style.json`.

## Architecture
```
app/ui/main_window.py     # QMainWindow chứa QTabWidget/QStackedWidget
app/ui/connect_tab.py     # Browse creds .json + URL + nút Kết nối + dropdown worksheet
app/ui/mapping_tab.py     # bảng mapping (auto-match qua template_introspect + get_headers), dropdown cột gom nhóm
app/ui/settings_tab.py    # form text settings (cơ quan, người ký, tiêu đề...), ảnh chữ ký Browse, toggle footer
app/ui/preview_widget.py  # QScrollArea hiển thị QPixmap từ pdf_preview; chọn hồ sơ để preview
```
Auto-match: giao `get_template_variables` (P3) × `get_headers` (P2), khớp tên (chuẩn hóa dấu/space), còn lại dropdown. Lưu về `style.row_mapping`/`document_fields`.

## Related Code Files
- Create: `app/ui/*` (5 file), hoàn thiện `main.py`
- Depends: P1 (style_config, platform_utils), P2 (sheets_client), P3 (renderer, introspect), P4 (pdf_preview)

## Implementation Steps
1. `main_window`: khung tab + nạp/lưu `StyleConfig` hiện hành.
2. `connect_tab`: Browse key, ô URL, nút Kết nối (QThread gọi `sheets_client`), đổ dropdown worksheet.
3. `mapping_tab`: sau khi có headers → bảng mapping auto-match + dropdown sửa; dropdown cột gom nhóm (mặc định `Tiêu đề hồ sơ`).
4. `settings_tab`: form buộc 2 chiều với `style.settings`; Browse ảnh; toggle footer; nút Lưu → `save_style`.
5. `preview_widget`: dropdown chọn giá trị nhóm → gọi renderer(P3)+pdf_preview(P4) trong QThread → hiển thị ảnh trang; bắt `LibreOfficeNotFound` → hộp thoại gợi ý cài / mở bằng Word (`open_with_default`).

## Success Criteria
- [ ] Chạy `python main.py` trên macOS: đi hết 5 bước với sheet thật → preview hiển thị đúng.
- [ ] Mapping tự khớp đúng cột trùng tên, sửa tay lưu được vào `style.json`.
- [ ] UI không treo khi kết nối/preview.

## Risk Assessment
- Ghép QThread + tín hiệu dễ lỗi → dùng worker + signal chuẩn, không đụng widget ngoài main thread.
- Chuẩn hóa tên khi auto-match (dấu tiếng Việt) → hàm normalize riêng, có test.
