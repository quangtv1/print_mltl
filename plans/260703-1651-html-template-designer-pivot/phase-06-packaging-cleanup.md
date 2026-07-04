---
phase: 6
title: "Packaging & Cleanup"
status: pending
effort: ""
---

# Phase 6: Packaging, Preview-Engine Decision & Cleanup

<!-- Updated: Validation Session 1 - bundle wkhtmltopdf binary; CHỐT engine preview (WebEngine vs QTextBrowser) sau test; xóa docx -->

## Overview
Đóng gói stack HTML+PDF: +Jinja2, **bundle wkhtmltopdf binary**, **chốt engine preview** (QWebEngine vs
QTextBrowser) dựa test đóng gói thật, gỡ sạch module docx/PyMuPDF, cập nhật workflow + docs. Rủi ro cao nhất.

## Requirements
- Functional: `.exe` Windows mở app, đi hết 3 bước, preview chạy, xuất HTML **+ PDF** (wkhtmltopdf).
- Non-functional: không lib PDF native khó; không bundle khóa Google; styles/ đọc/ghi cạnh exe.

## Quyết định engine preview (chốt ở phase này)
1. Thử build với **PyQtWebEngine** (fidelity + click-canvas). Nếu bundle QtWebEngine trên Windows OK và
   kích thước chấp nhận được → **chọn WebEngine**.
2. Nếu vỡ/quá nặng → **QTextBrowser** (bỏ PyQtWebEngine; bind qua panel vẫn đủ). `make_preview_backend`
   (P3) tự xử; chỉ cần quyết có đưa PyQtWebEngine vào requirements/spec hay không.

## Architecture
- `requirements.txt`: **+Jinja2** (+PyQtWebEngine **nếu** chọn WebEngine); bỏ docxtpl, python-docx, PyMuPDF,
  **gspread, google-auth** (đầu vào Excel, không còn Google Sheet).
- `packaging/mltl.spec`: **bundle wkhtmltopdf binary** (add-binary, resolve cạnh exe qua `resolve_wkhtmltopdf`);
  nếu WebEngine → collect QtWebEngine; giữ `--onedir`, add-data `styles/`.
- `.github/workflows/build-windows.yml`: **tải wkhtmltopdf** (choco/URL) + copy vào gói; build; (kiểm QtWebEngine nếu dùng).
- Xóa module docx: `docx_renderer.py`, `pdf_preview.py`, `template_introspect.py`, `build_default_template.py`,
  5 tab UI cũ (P4 đã thay). Cập nhật `docs/deployment-windows.md` + `README.md`.

## Related Code Files
- Modify: `requirements.txt`, `packaging/mltl.spec`, `.github/workflows/build-windows.yml`, `docs/deployment-windows.md`, `README.md`, `app/core/platform_utils.py` (resolve bundled wkhtmltopdf)
- Delete: `app/core/docx_renderer.py`, `app/core/pdf_preview.py`, `app/core/template_introspect.py`,
  `app/core/sheets_client.py` (thay bằng excel_reader), `build_default_template.py`,
  `app/ui/{connect_tab,mapping_tab,settings_tab,preview_widget,generate_tab}.py`

## Implementation Steps
1. Cập nhật requirements (+jinja2, −docx/pymupdf; +PyQtWebEngine nếu chọn WebEngine).
2. Sửa spec: add-binary wkhtmltopdf; (nếu WebEngine: `--collect-all PyQt5.QtWebEngineCore`); build.
3. Workflow: tải wkhtmltopdf cho windows-latest + copy cạnh exe; build; upload artifact.
4. Test trên Windows: 3 bước + preview + generate HTML+PDF song song (kiểm freeze_support/spawn).
   → chốt engine preview theo kết quả.
5. Xóa module docx cũ + 5 tab UI cũ; cập nhật docs/README (mô tả HTML+PDF).

## Success Criteria
- [ ] `.exe` Windows chạy sạch (kèm wkhtmltopdf; + QtWebEngine nếu chọn), đi hết luồng, xuất HTML+PDF đúng.
- [ ] Không còn import docxtpl/python-docx/PyMuPDF trong luồng chính (đã xóa file).
- [ ] `styles/` đọc/ghi cạnh exe; không có khóa Google trong gói; engine preview đã chốt + ghi vào docs.

## Risk Assessment
- **Cao nhất:** bundle wkhtmltopdf + (tùy chọn) QtWebEngine trên Windows có thể vỡ → test sớm; QTextBrowser dự phòng.
- Gói nặng nếu chọn WebEngine (Chromium) → `--onedir`; loại locale/thừa nếu cần.
