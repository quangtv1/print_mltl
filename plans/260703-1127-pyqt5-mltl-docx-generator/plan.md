---
title: "App Desktop PyQt5 - Tạo Mục Lục Hồ Sơ"
description: ""
status: implemented
priority: P2
branch: ""
tags: []
blockedBy: []
blocks: []
created: "2026-07-03T04:29:56.470Z"
createdBy: "ck:plan"
source: skill
---

# App Desktop PyQt5 - Tạo Mục Lục Hồ Sơ

## Overview

> **Ghi chú (2026-07-03):** Engine docx (P3 `docx_renderer`, P4 preview docx→PDF) của plan này
> **được thay thế** bởi pivot HTML — xem `plans/260703-1651-html-template-designer-pivot/`.
> Các phần sheets/excel/batch/UI-khung vẫn được tái dùng.

Chuyển script CLI `print_mltl_parallel.py` (sinh docx "Mục lục văn bản, tài liệu" từ Google Sheet, style hardcode) thành app desktop PyQt5 cấu hình được. Kiến trúc **hybrid**: layout ở `template.docx` (docxtpl), settings/mapping ở `style.json` portable. Preview docx→PDF nhúng qua LibreOffice + PyMuPDF. Dev trên macOS, target chạy chính **Windows 11**, đóng gói `.exe` (PyInstaller).

**Nguồn thiết kế:** `plans/reports/brainstorm-desktop-conversion-260703-1052-pyqt5-mltl-docx-generator-report.md`

**Đã có (PoC verify khớp 100% output cũ):** `build_default_template.py`, `styles/van-phong-dat-dai/{template.docx, style.json}`.

## Acceptance Criteria (toàn plan)

- [ ] Chọn creds `.json` + URL → liệt kê worksheet → lấy danh sách cột.
- [ ] Map cột↔biến (tự khớp + sửa tay), chọn cột gom nhóm (mặc định `Tiêu đề hồ sơ`).
- [ ] Sinh docx nhồi biến **giống output script cũ** + footer "Trang x/y".
- [ ] Preview 1 hồ sơ ra PDF hiển thị trong app.
- [ ] Generate hàng loạt N docx (+ Excel tùy chọn) chạy nền, không treo UI.
- [ ] Copy `styles/` sang máy khác dùng lại không sửa code.
- [ ] Đóng gói `.exe` chạy trên Windows 11 sạch (không cài Python).
- [ ] Key `get_link_pdf_.json` được thu hồi, KHÔNG bundle vào app.

## Constraints & Decisions (chốt từ brainstorm)

- Template engine: **hybrid** (template.docx + style.json).
- Mapping: tự khớp theo tên + sửa tay; lưu trong `style.json`.
- Cột gom nhóm: linh hoạt, mặc định `Tiêu đề hồ sơ`.
- Layout cột/độ rộng/viền sửa trong Word (không qua settings UI) → tránh patch grid.
- Auth: user chọn file service-account; scope **read-only**; bỏ `oauth2client` → `google.oauth2.service_account`.
- Render **bắt buộc `autoescape=True`**; vòng lặp bảng dùng 2 hàng marker `{%tr%}`.
- MVP: 1 template, cấu trúc `styles/` sẵn cho nhiều. **Generate song song (ProcessPoolExecutor) ngay từ đầu** (quy mô hàng nghìn hồ sơ — xác nhận ở Validation).
- Preview: **có fallback mở bằng Word** khi máy đích thiếu LibreOffice.
- Đóng gói: build `.exe` qua **GitHub Actions `windows-latest`** (đường chính).

## Validation Log

### Session 1 (2026-07-03)

Verification: 8/8 claim VERIFIED, 0 FAILED (artifact tồn tại, 2 hàm cũ tái dùng, khóa `style.json` khớp, toàn bộ thư viện đã cài gồm PyQt5+PyMuPDF). Tier: Full.

Quyết định chốt:
1. **Concurrency (P6):** quy mô ~hàng nghìn hồ sơ → **song song `ProcessPoolExecutor` ngay** (kèm `freeze_support` cho exe). Kéo theo P3: `render_group` phải process-safe (worker mở `DocxTemplate` riêng, truyền StyleConfig dạng dict vì DocxTemplate không pickle được).
2. **Preview fallback (P4/P5):** thiếu LibreOffice → **fallback generate file tạm + `open_with_default`** (mở bằng Word/app mặc định).
3. **Windows build (P7):** dùng **GitHub Actions `windows-latest`** build artifact `.exe`; test trên máy đích thật sau.

Phase propagation: P6 (viết lại song song), P3 (render_group process-safe + tiêu chí pickle), P4 (fallback Word), P5 (đã sẵn nhánh fallback), P7 (CI windows-latest + rủi ro spawn), P1 (`freeze_support` trong main.py).

### Whole-Plan Consistency Sweep
Re-đọc plan.md + 7 phase: không còn "MVP tuần tự" mâu thuẫn; chữ ký `render_group(style_dict, records, out_path)` nhất quán P3↔P6; fallback preview nhất quán P4↔P5; `freeze_support` xuất hiện P1/P6/P7. **0 mâu thuẫn tồn đọng.** Tên file phase-06 giữ "qthread" (QThread vẫn điều phối pool) — không cần đổi.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Scaffold & Core Config](./phase-01-scaffold-core-config.md) | Done |
| 2 | [Google Sheets Client](./phase-02-google-sheets-client.md) | Done (chờ test live) |
| 3 | [Docx Renderer & Excel Export](./phase-03-docx-renderer-excel-export.md) | Done |
| 4 | [PDF Preview (LibreOffice)](./phase-04-pdf-preview-libreoffice.md) | Done (convert chờ máy có LibreOffice) |
| 5 | [PyQt5 UI](./phase-05-pyqt5-ui.md) | Done |
| 6 | [Batch Generator (QThread)](./phase-06-batch-generator-qthread.md) | Done |
| 7 | [Windows Packaging](./phase-07-windows-packaging.md) | Done (build/test chờ CI+máy Windows) |

## Dependencies

Chuỗi: P1 → (P2, P3, P4 song song được) → P5 → P6 → P7.
Không có cross-plan dependency (chưa có plan nào khác).

## Tech Stack

PyQt5 · gspread + `google-auth` · docxtpl + python-docx · openpyxl · PyMuPDF (fitz) · LibreOffice (ngoài, cho preview) · PyInstaller · pandas.
