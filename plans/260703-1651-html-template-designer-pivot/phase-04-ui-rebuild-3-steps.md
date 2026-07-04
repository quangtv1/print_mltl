---
phase: 4
title: "UI Rebuild 3 Steps"
status: pending
effort: ""
---

# Phase 4: UI Rebuild 3 Steps

<!-- Updated: S1 - bind qua panel; click-canvas bonus. S3 - đầu vào FILE EXCEL (excel_reader) thay Google Sheet -->

## Overview
Gom UI về **3 bước** theo report: (1) Đầu vào (chọn **file Excel** + sheet + ghép biến), (2) Thiết kế
& Preview, (3) Chạy + log realtime. Tái dùng theme QSS + workers; thay QTabWidget 5 tab bằng 3 bước.
Gán biến **chính qua panel bên** (mọi backend preview); **click-trên-canvas** chỉ là lối tắt khi QWebEngine.

## Requirements
- Functional: 3 bước theo mô tả user; click-to-bind biến↔vùng; toggle Edit/Preview; tiến/lùi record;
  log realtime (hồ sơ nào xong / tổng / thời gian).
- Non-functional: thao tác nặng (connect/generate) qua QThread; state trong `style.json`.

## Architecture
```
app/core/excel_reader.py (create)       # list_sheets(path), read_df(path, sheet), validate header
app/ui/main_window.py (rewrite khung)   # 3 bước: QStackedWidget/QWizard + header
app/ui/step_input.py     # Browse .xlsx + dropdown sheet + Đọc dữ liệu + mapping biến + thư mục output
app/ui/step_design.py    # trái 3/4 preview backend (P3 make_preview_backend), phải 1/4 panel bind biến↔vùng;
                         #   góc trên phải toggle Edit/Preview + ◀ ▶ điều hướng hồ sơ + nút "Xuất PDF hồ sơ này"
app/ui/step_run.py       # thư mục xuất + checkbox Excel + Generate (HTML hàng loạt) + QProgressBar + log realtime;
                         #   nút phụ "Xuất PDF tất cả" (PdfAllController, song song) + cảnh báo thời gian
```
Panel biến (phải): nhóm **Tài liệu / Hàng / Gom nhóm / Ảnh chữ ký**. **Bấm 1 biến → nổi bật vùng của
biến đó trên A4** (Edit: highlight token `{var}`; Preview: highlight giá trị; cột bảng → cả cột). Luồng
bind **chính**: chọn vùng (dropdown/list `data-zone`) + chọn biến → cập nhật `style.bindings` → re-render.
Lối tắt (chỉ WebEngine): click vùng ở preview (`zone_clicked`) tự chọn vùng đó trong panel.

## Related Code Files
- Rewrite: `app/ui/main_window.py`
- Create: `app/core/excel_reader.py`, `app/ui/step_input.py`, `app/ui/step_design.py`, `app/ui/step_run.py`
- Reuse: `app/ui/workers.py`, `app/ui/theme.py`, `text_match`, `html_introspect`, P3 preview, P2 batch
- **Không dùng** `sheets_client` nữa (thay bằng `excel_reader`); bỏ gspread/google-auth
- Delete/deprecate (P6): `connect_tab.py`, `mapping_tab.py`, `settings_tab.py`, `preview_widget.py`, `generate_tab.py`

## Implementation Steps
1. `main_window`: khung 3 bước (QStackedWidget + nút Tiếp/Lùi) + header; nạp/lưu StyleConfig.
2. `step_input`: Browse file `.xlsx` → `excel_reader.list_sheets` đổ dropdown sheet → Đọc dữ liệu
   (`excel_reader.read_df`, QThread nếu file lớn) → bảng mapping biến↔cột (auto-match `text_match`) +
   dropdown cột gom nhóm + chọn thư mục output.
3. `step_design`: ghép preview backend (P3) + panel bind (chọn vùng + chọn biến) → `style.bindings`;
   toggle Edit/Preview; ◀▶ đổi record (dựng records theo grouping, render qua P1, đẩy vào preview).
   Nếu backend WebEngine: nối `zone_clicked` → auto-chọn vùng trong panel. Nút Lưu → save_style.
4. `step_run`: nút Generate chạy `BatchController` (P2, **HTML hàng loạt**) trong QThread; progress +
   **log realtime** từng hồ sơ; summary (tổng, thời gian) + nút mở thư mục. Nút phụ **"Xuất PDF tất cả"**
   (`PdfAllController`) có hộp cảnh báo "~phút cho nghìn hồ sơ" trước khi chạy.
4b. `step_design`: nút **"Xuất PDF hồ sơ này"** → `pdf_one` cho record đang xem (theo yêu cầu, nhanh).
5. Chuẩn hóa auto-match biến (dùng `text_match`) khi vào bước thiết kế.

## Success Criteria
- [ ] Bước 1: chọn file `.xlsx` → dropdown sheet đúng → Đọc dữ liệu → mapping tự khớp; báo lỗi rõ nếu header rỗng/trùng hoặc file hỏng.
- [ ] Chạy `python main.py`: đi hết 3 bước với file Excel thật → preview đúng, xuất HTML đúng.
- [ ] Chọn vùng + chọn biến (panel) → bind lưu vào `style.json`; preview cập nhật. (WebEngine: click vùng cũng chọn được.)
- [ ] Toggle Edit/Preview + tiến/lùi hồ sơ hoạt động; UI không treo.
- [ ] Bước 3: Generate HTML hàng loạt → log realtime hồ sơ đã tạo + tổng số + thời gian.
- [ ] "Xuất PDF hồ sơ này" (bước 2) tạo đúng 1 PDF; "Xuất PDF tất cả" (bước 3) chạy song song + cảnh báo trước.

## Risk Assessment
- Ghép QThread + QWebChannel signal dễ lỗi → worker/signal chuẩn, không đụng view ngoài main thread.
- Rewrite main_window đụng nhiều → giữ theme/workers cũ, thay khung điều hướng thôi.
