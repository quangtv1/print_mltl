---
phase: 4
title: "PDF Preview (LibreOffice)"
status: done
effort: ""
---

# Phase 4: PDF Preview (LibreOffice)

<!-- Updated: Validation Session 1 - thêm fallback mở bằng Word khi thiếu LibreOffice -->

## Overview
Pipeline preview: render 1 nhóm → docx tạm → LibreOffice headless convert PDF → PyMuPDF render trang thành ảnh. Trả ảnh cho UI (phase 5) hiển thị. **Fallback:** thiếu LibreOffice (hoặc convert lỗi) → mở docx tạm bằng Word/app mặc định (`open_with_default`). Logic thuần, test headless được.

## Requirements
- Functional: `docx_to_pdf(docx, out_dir)`; `pdf_to_pixmaps(pdf, dpi)` → list ảnh trang.
- Non-functional: timeout subprocess; báo lỗi rõ khi thiếu LibreOffice; profile tạm tránh xung đột instance.

## Architecture
```
app/core/pdf_preview.py
  docx_to_pdf(docx_path, out_dir, soffice=None) -> pdf_path
    subprocess.run([soffice, "--headless",
       "-env:UserInstallation=file://<tmp_profile>",
       "--convert-to","pdf","--outdir",out_dir, docx_path], timeout=..)
  render_pdf_pages(pdf_path, dpi=120) -> [bytes/QImage source]   # PyMuPDF fitz
```
Dùng `platform_utils.resolve_soffice`. Nếu `None` → raise `LibreOfficeNotFound` (UI bắt để gợi ý cài / fallback `open_with_default`).

## Related Code Files
- Create: `app/core/pdf_preview.py`
- Depends: `app/core/platform_utils.py` (P1), `app/core/docx_renderer.py` (P3)

## Implementation Steps
1. `docx_to_pdf`: gọi soffice headless với `-env:UserInstallation` (profile tạm) + timeout; kiểm tra file PDF ra.
2. `render_pdf_pages`: `fitz.open(pdf)`, mỗi trang `get_pixmap(dpi)` → bytes PNG (UI đổ vào QPixmap ở P5).
3. Exception rõ: `LibreOfficeNotFound`, `ConversionTimeout`, `ConversionFailed`.
4. Dọn file tạm sau preview.

## Success Criteria
- [ ] Trên macOS đã cài LibreOffice: preview docx mẫu → nhận đúng số trang ảnh, footer "Trang x/y" hiển thị đúng.
- [ ] Thiếu LibreOffice → raise `LibreOfficeNotFound`; UI (P5) bắt và mở docx tạm bằng Word (`open_with_default`), không treo.
- [ ] Không rác file tạm sau nhiều lần preview.

## Risk Assessment
- soffice không chạy 2 instance đồng thời → profile tạm riêng + preview/batch không song song.
- Fidelity font trên macOS lệch nhẹ (Liberation Serif) → chấp nhận, ghi chú UI; bản cuối mở Word đúng.
- PyMuPDF AGPL → công cụ nội bộ OK.
