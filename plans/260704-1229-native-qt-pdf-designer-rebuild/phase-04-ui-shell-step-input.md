---
phase: 4
title: "UI Shell & Step Input"
status: completed
priority: P1
dependencies: [1]
---

# Phase 4: UI Shell & Step Input

## Overview
Khung UI mới: `main_window` = **QStackedWidget 3 bước** + **stepper ngang Windows Fluent** (theo
thiết kế), theme QSS `#0078d7`. Bước 1 (Đầu vào): chọn `.xlsx` + sheet + đọc dữ liệu + chọn Mẫu +
bảng ghép biến↔cột (auto-match) + cột gom nhóm.

## Requirements
- Functional: điều hướng 3 bước (Tiếp/Quay lại), nạp/lưu `StyleConfig`, luồng bước 1 đầy đủ.
- Non-functional: đọc Excel qua `workers.run_async` (không treo); state dùng chung giữa các bước.

## Architecture
- `app/ui/theme.py` (rewrite): QSS Fluent sáng — nền `#f0f0f0`/trắng, accent `#0078d7`, chọn
  `#e5f1fb`, check xanh `#107c10`; font "Segoe UI". Header stepper: 3 chip số + kicker "BƯỚC n" + tiêu đề.
- `app/ui/main_window.py` (rewrite): `QStackedWidget` giữ `step_input`/`step_design`/`step_run`;
  header stepper trên cùng; action bar dưới (Quay lại / Tiếp theo, 1 primary CTA/màn); giữ 1
  `StyleConfig` + `df` + `out_dir` dùng chung (đẩy vào các step qua thuộc tính/signal).
- `app/ui/step_input.py` (create):
  - "Nguồn dữ liệu Excel": `Tệp` + Duyệt (`QFileDialog` .xlsx) → `excel_reader.list_sheets` đổ
    dropdown Sheet → "Đọc dữ liệu" (`read_df` qua `run_async`) → status "OK đọc N dòng · M cột".
  - "Mẫu (template)": dropdown `list_styles(styles_root())` → `load_style`; hiển thị số biến của mẫu.
  - Bảng "Ghép biến ↔ cột": `QTableWidget` cột (# | Cột Excel | Biến mẫu | Tự khớp); auto-match bằng
    `text_match.auto_match`; combobox chọn biến; đánh dấu ✓ "Tự khớp". Ghi vào `style.row_mapping`/`document_fields`.
  - "Cột gom nhóm hồ sơ": dropdown cột → `style.grouping_column`; hint "mỗi giá trị khác nhau → 1 file PDF".

## Related Code Files
- Rewrite: `app/ui/main_window.py`, `app/ui/theme.py`
- Create: `app/ui/step_input.py`
- Reuse: `app/core/excel_reader.py` (P1), `app/core/text_match.py`, `app/ui/workers.py`,
  `app/core/style_config.py`, `app/core/platform_utils.styles_root`
- Reference (đừng import): `demo_ui_mockup.py` chỉ để tham khảo bố cục — **theme dùng bản Fluent mới**, không dùng sidebar tối
- Delete (P6): `connect_tab.py`, `mapping_tab.py`

## Implementation Steps
1. `theme.py`: viết QSS Fluent + widget stepper (hàm dựng header 3 bước, đánh dấu bước hiện tại).
2. `main_window.py`: QStackedWidget + header + action bar; state chung; nút Tiếp/Quay lại đổi trang.
3. `step_input.py`: khối Excel (browse/sheet/đọc) qua `run_async`; xử lý lỗi `validate_headers` → thông báo.
4. Khối Mẫu + bảng mapping (auto-match) + dropdown gom nhóm; đồng bộ vào `StyleConfig`.
5. Chặn "Tiếp theo" nếu chưa đọc dữ liệu / chưa chọn cột gom nhóm.

## Success Criteria
- [ ] `python main.py` mở app 3 bước Fluent; stepper hiển thị đúng bước.
- [ ] Bước 1: chọn `.xlsx` → sheet đúng → đọc → status N dòng·M cột; lỗi header báo rõ.
- [ ] Bảng mapping auto-khớp; sửa tay được; lưu vào StyleConfig.
- [ ] Chọn Mẫu đổi được; cột gom nhóm set đúng.
- [ ] UI không treo khi đọc file lớn.

## Red Team Fixes (áp dụng 2026-07-04)
- **Đa mẫu = 4 (quyết định user, #13 Reject):** dropdown "Mẫu" liệt kê 4 style dir (Đông Hà, Quảng Ninh,
  Đông Đa, Vĩnh Phúc) qua `list_styles(styles_root())`. 4 style dir + `template_html` do **P1** dựng.
- **#6 (High) — panel/mapping theo schema thật:** cột "Biến trong mẫu" sinh từ `row_mapping`/`document_fields`
  của mẫu đang chọn (không dùng tên token bịa `so_ho_so`/`nguoi_lap`/`don_vi`).
- **Invalidation state (Red Team #O/stale):** khi user Back đổi file `.xlsx` / đổi sheet / đổi
  `grouping_column` / đổi mapping → **bump 1 data-version token**; P5 & P6 **recompute** `groups`/records/
  panel trên `showEvent`/vào-bước (không cache 1 lần). Chặn "Tiếp" nếu `grouping_column` không còn trong df
  mới (tránh `KeyError` ở `group_dataframe`, `docx_renderer.py:31-35`).

## Risk Assessment
- Rewrite main_window đụng nhiều → giữ workers/style_config, chỉ thay khung điều hướng + theme.
- Đồng bộ state giữa step: 1 nguồn `StyleConfig`/`df` trong main_window; **recompute derived state mỗi lần vào bước** (không cache).
