---
phase: 2
title: "Batch Generator HTML"
status: pending
effort: ""
---

# Phase 2: Batch Generator HTML + PDF-on-demand

<!-- Updated: Validation S1 - html_to_pdf qua wkhtmltopdf. S2 - batch CHỈ HTML (nhanh); PDF gọi on-demand (P4) -->

## Overview
`batch_generator`: batch **chỉ sinh `.html`** (P1) song song → nghìn hồ sơ vài giây (né nút cổ chai PDF).
Module `html_to_pdf` (wkhtmltopdf) tạo ở đây nhưng **gọi theo yêu cầu** (P4: 1 hồ sơ, hoặc "xuất PDF tất cả"),
không nằm trong worker batch mặc định. Giữ khung song song + cô lập lỗi + Excel tổng hợp.

## Requirements
- Functional: batch worker sinh `.html`; Excel như cũ; log per-file. `html_to_pdf(html, pdf)` dùng lại được
  cho on-demand đơn lẻ **và** batch-PDF song song (P4).
- Non-functional: song song đa tiến trình; lỗi 1 hồ sơ không dừng mẻ; picklable; `freeze_support` giữ;
  thiếu wkhtmltopdf → báo lỗi rõ **chỉ khi** dùng chức năng PDF (batch HTML không cần wkhtmltopdf).

## Architecture
```
app/core/platform_utils.py (modify)
  resolve_wkhtmltopdf(override=None) -> str|None    # như resolve_soffice: PATH → path chuẩn OS → bundled cạnh exe
app/core/html_to_pdf.py (create)
  html_to_pdf(html_path, pdf_path, wk=None)         # subprocess wkhtmltopdf --enable-local-file-access ... ; timeout
  class WkhtmltopdfNotFound(Exception)
app/core/batch_generator.py (modify)
  render_group_worker(task) -> (ok, group, path|err)  # CHỈ render_group (HTML) — không PDF
  build_tasks(...)                                     # tên .html; make_unique_path giữ
  BatchController                                      # signals giữ; thêm timing
  # PDF-theo-yêu-cầu (dùng ở P4):
  pdf_one(html_path) -> pdf_path                       # gọi html_to_pdf 1 file (bước Preview)
  PdfAllController(QObject)                             # tùy chọn: xuất PDF tất cả .html song song + progress
```
`excel_exporter.export_excel` **giữ nguyên** (dựa records, không phụ thuộc định dạng output).

## Related Code Files
- Create: `app/core/html_to_pdf.py`
- Modify: `app/core/batch_generator.py`, `app/core/platform_utils.py`
- Reuse nguyên: `app/core/excel_exporter.py`, `app/core/workers.py`

## Implementation Steps
1. `resolve_wkhtmltopdf` (đối xứng `resolve_soffice`): PATH → path chuẩn OS → binary bundled cạnh exe (P6).
2. `html_to_pdf`: `subprocess.run([wk, "--enable-local-file-access", "--quiet", html, pdf], timeout=...)`;
   kiểm file PDF ra; base path để nạp ảnh chữ ký cục bộ. (Dùng cho on-demand đơn lẻ + batch-PDF.)
3. `render_group_worker`: **chỉ** `render_group` (HTML); trả path HTML. Bắt lỗi per-file.
4. `build_tasks`: filename `.html` (`resolve_output_name` từ P1).
5. Giữ aggregation Excel ở main process; thêm timing per-batch để P4 hiển thị "thời gian tạo".
6. `pdf_one(html_path)` + `PdfAllController` (ProcessPoolExecutor gọi `html_to_pdf` trên các .html) cho P4.

## Success Criteria
- [ ] Batch (vài trăm–nghìn) → đủ N `.html` + Excel; **song song, nghìn hồ sơ trong vài giây**; progress chạy.
- [ ] `html_to_pdf` 1 file: PDF đúng (bảng dài chảy qua trang; footer "Trang x/y"; ảnh chữ ký hiện).
- [ ] `PdfAllController`: xuất PDF tất cả song song, progress + đo thời gian; 1 hồ sơ lỗi không dừng mẻ.
- [ ] Thiếu wkhtmltopdf → chỉ chức năng PDF báo lỗi rõ; **batch HTML vẫn chạy bình thường**.
- [ ] Batch chạy đúng qua `ProcessPoolExecutor` spawn (không lỗi pickle).

## Risk Assessment
- wkhtmltopdf resolve path dev vs frozen → `resolve_wkhtmltopdf` + bundle ở P6; test sớm.
- Batch HTML tách khỏi PDF → tốc độ batch không còn phụ thuộc wkhtmltopdf (nút cổ chai chỉ khi user chủ động xuất PDF hàng loạt).
