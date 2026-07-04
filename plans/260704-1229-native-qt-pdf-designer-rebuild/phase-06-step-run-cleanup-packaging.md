---
phase: 6
title: "Step Run & Cleanup Packaging"
status: completed
priority: P2
dependencies: [3, 5]
---

# Phase 6: Step Run & Cleanup Packaging

## Overview
Bước 3 — Chạy: thư mục + mẫu **Tên PDF**, tùy chọn (đa luồng / ghi đè / Excel tổng hợp), progress +
**log realtime**, mở thư mục. Sau đó **dọn code cũ** (5 tab docx + Google Sheet), tỉa `requirements`,
và **test đóng gói PyInstaller** cho engine PDF Qt.

## Requirements
- Functional: chạy batch (P3) trong QThread; progress + log realtime; mở thư mục kết quả.
- Non-functional: exe Windows chạy sạch không cần Python; không dep native khó.

## Architecture
- `app/ui/step_run.py` (create):
  - "Thư mục & tên file xuất": Duyệt thư mục + `QLineEdit` mẫu Tên PDF (`{stt_file}`, `{ho_so_so}`,
    `{ngay_gio}`…) + ví dụ preview tên.
  - "Tùy chọn chạy": checkbox **Chạy đa luồng** (<!-- Updated: Validation Session 1 --> **disabled ở MVP** —
    serialize; bật lại khi có bản đa luồng kiểm chứng, xem P3), **Ghi đè file đã tồn tại**, **Xuất kèm Excel tổng hợp**.
  - "Tiến trình chạy": `QProgressBar` + nút Generate → `BatchController` (P3) qua QThread; nối
    `progress`/`log`/`finished`/`failed`.
  - "Nhật ký (realtime)": `QTextBrowser`/list append theo `log` signal; summary tổng + thời gian; nút mở thư mục (`open_with_default`).
- **Cleanup:** xoá `connect_tab.py`, `mapping_tab.py`, `settings_tab.py`, `preview_widget.py`,
  `generate_tab.py`, `sheets_client.py`, `docx_renderer.py`, `pdf_preview.py`, `template_introspect.py`.
  Gỡ import/ref còn sót. `platform_utils`: bỏ `resolve_soffice` nếu không còn dùng.
- **requirements.txt:** bỏ `docxtpl`, `python-docx`, `PyMuPDF`, `gspread`, `google-auth`; giữ `pandas`,
  `openpyxl`, `PyQt5`; **THÊM `pyinstaller`** (hiện chưa có trong requirements — Red Team #7/minor). Không có
  entry wkhtmltopdf để bỏ (chưa từng có).
- **Packaging:** cập nhật `.spec`/`print_mltl_parallel.py` nếu cần; test build exe → chạy 3 bước + xuất PDF thật.
- Đánh dấu plan `260703-1651-html-template-designer-pivot` = superseded/cancelled.

## Related Code Files
- Create: `app/ui/step_run.py`
- Modify: `app/ui/main_window.py` (nhúng step_run), `requirements.txt`, `main.py` (dọn `freeze_support`), `.spec` (tạo mới)
- Delete: các module docx/Google Sheet liệt kê trên + **`print_mltl_parallel.py`** + **`get_link_pdf_.json`** (khóa Google)
- Reuse: `app/core/batch_generator.py` (P3), `app/core/platform_utils.open_with_default`

## Implementation Steps
1. `step_run.py`: khối thư mục + Tên PDF + tùy chọn + progress + log + Generate.
2. Nối `BatchController` (P3) qua QThread; log realtime; summary; mở thư mục.
3. Xoá các module cũ; grep sạch import còn sót; app chạy không lỗi import.
4. Tỉa `requirements.txt`; cài lại sạch trong venv thử → `python main.py` chạy full 3 bước.
5. Test PyInstaller build exe (hoặc mô phỏng) → chạy xuất PDF; kiểm plugin in ấn Qt có mặt.
6. Đánh dấu plan 1651 superseded; cập nhật `plan.md` phases → completed qua `ck plan check`.

## Success Criteria
- [ ] Bước 3: Generate → N PDF; progress tới total; log realtime từng hồ sơ + tổng + thời gian; mở thư mục.
- [ ] Tùy chọn đa luồng/ghi đè/Excel hoạt động đúng.
- [ ] Không còn import docxtpl/python-docx/PyMuPDF/gspread/google-auth/QWebEngine/wkhtmltopdf trong repo luồng chính.
- [ ] `python main.py` chạy full 3 bước với Excel thật → PDF đúng.
- [ ] Build exe (test) chạy sạch, xuất được PDF.

## Red Team Fixes (áp dụng 2026-07-04)
- **#12 (High/bảo mật) — XOÁ khóa Google:** xoá `get_link_pdf_.json` (service-account thật, project
  `dongda-42850`, có `private_key` — còn trên đĩa); **khuyến nghị revoke/rotate key** (coi như đã lộ vì nằm
  plaintext trên máy dev). **Xoá hẳn** `print_mltl_parallel.py` (import `gspread`/`oauth2client`, hardcode
  creds ở dòng ~561) — quyết định dứt khoát XOÁ, không "hoặc".
- **#14 (Med) — inventory import trước khi xoá** (offender đã xác minh): `batch_generator.py:21`
  (`from app.core import docx_renderer as R`), `preview_widget.py:23-24` (`docx_renderer`, `pdf_preview`),
  `main_window.py:66-89` (5 tab cũ). Xoá theo thứ tự: rewrite importer (P3/P4/P5) xong mới xoá module.
  **Gate thoát:** `python main.py` mở + `python -c "import app.ui.main_window, app.core.batch_generator"` sạch.
- **main.py:** dọn `multiprocessing.freeze_support()` (P3 bỏ ProcessPool → thành dead code); cập nhật docstring.
- **Packaging:** `.spec` **chưa tồn tại → tạo mới**; PyInstaller cần plugin in ấn/`platforms` của Qt +
  nhúng font → thêm hook/`--add-data`; **test build trên Windows sớm** (không chỉ macOS).

## Risk Assessment
- Xoá module cũ có thể sót ref → grep toàn repo trước xoá; chạy app kiểm import.
- PyInstaller thiếu plugin `platforms`/in ấn Qt → thêm hook/`--add-data`; test sớm, đừng để cuối.
- `print_mltl_parallel.py` (script cũ) có thể phụ thuộc engine docx → cập nhật hoặc xoá nếu không còn dùng.
