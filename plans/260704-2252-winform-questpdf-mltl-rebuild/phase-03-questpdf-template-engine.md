---
phase: 3
title: "QuestPDF Template Engine"
status: pending
priority: P1
dependencies: [2]
---

# Phase 3: QuestPDF Template Engine

## Overview
Engine PDF lõi (phần rủi ro nhất): `ITemplate` + 4 lớp mẫu QuestPDF sinh 1 PDF A4 cho 1 group —
header + **bảng lặp dòng theo record** + **header bảng lặp qua trang** + footer "Trang x/y".
Thêm `FileName` (expand token → tên file an toàn) và `PageRenderer` (render 1 trang → ảnh cho preview P6).

## Requirements
- Functional: `ITemplate.Compose(docFields, records, settings, sttFile) -> QuestPDF IDocument`;
  `Renderer.ToPdf(template, group, outPath)`; `Renderer.ToImages(template, group) -> IReadOnlyList<byte[]>`.
- Non-functional: **thread-safe** (không state chia sẻ giữa group → batch song song ở P4); font Times New Roman.

## Architecture
`MucLucHoSo.Core/Pdf/` (**1 lớp** `IndexTemplate`, chốt 1 chữ ký — Red Team #6/#7):
- `IndexTemplate.cs`: `void Compose(IDocumentContainer c, DocGroup g, StyleConfig s)` — helper:
  - **Substitute:** thay `{token}` trong text header/footer bằng giá trị (doc fields/settings/auto);
    **escape** không cần (QuestPDF nhận string thuần, tự xử lý) nhưng **strip footer-only token** khỏi body.
  - **Bảng dữ liệu:** `table.Header(...)` (lặp qua trang tự động) + `foreach record → row cells` theo
    `style.columns` (thứ tự + độ rộng tỉ lệ). Cột `trich_yeu` canh trái, còn lại canh giữa.
  - **Footer (Red Team #20):** dựng từ `settings.footer_format` (token-replace `{trang_so}`→`CurrentPageNumber`,
    `{tong_so_trang}`→`TotalPages`) + tôn trọng `settings.footer_page_number`; **không** hardcode "Trang x/y".
  - **Chữ ký (Red Team #3):** vùng sau bảng: `{chuc_danh_ky}`/`{nguoi_ky}`; ảnh `settings.anh_chu_ky_path` nếu có.
  - **A4 + lề + font** từ settings.
- **1 lớp `IndexTemplate`** (Red Team #7) điều khiển hoàn toàn bởi `StyleConfig` (columns/settings/documentFields);
  4 mẫu = 4 `style.json` khác data. Header/label prose lấy từ `settings` (P1). **Không** 4 subclass.
- `FileName.cs`: `Format(pattern, ctx)` expand `{ho_so_so}/{stt_file}/{ngay_gio}/...`, sanitize ký tự cấm
  Windows, cắt stem ≤120 (port từ Python `format_output_name`). `MakeUniquePath(dir, name, usedSet)`.
- `Renderer.cs`: `ToPdf(ITemplate, DocGroup, StyleConfig, outPath)`; `ToImages(...)` dùng
  `Document.Create(...).GenerateImages()` (QuestPDF render trang → PNG) cho preview P6.
- Chọn mẫu (UI) = lookup `StyleStore.LoadAll` (slug → StyleConfig); **không** cần `TemplateRegistry`.
- Set `QuestPDF.Settings.License = LicenseType.Community` ở static ctor/entrypoint.

## Related Code Files
- Create: `MucLucHoSo.Core/Pdf/{IndexTemplate,Renderer,FileName}.cs` (1 lớp template điều khiển bởi StyleConfig)
- Reference: bản Python `qt_pdf_renderer.py` (luật cột/căn lề/footer, `format_output_name`, seed 4 mẫu)

## Tests (viết trước — TDD)
- `FileNameTests`: expand `{ho_so_so}` khác nhau → tên khác nhau; sanitize ký tự cấm; cắt độ dài;
  `MakeUniquePath` → HS-001/HS-001_2 khi trùng.
- `RendererTests` (dùng PDFsharp/PdfPig để đọc lại PDF hoặc đếm trang):
  - 1 group nhiều record → PDF tồn tại, kích thước > ngưỡng.
  - **60 record → PDF nhiều trang**; đọc lại đếm **số dòng bảng = số record** (không mất/nhân đôi ở ranh trang).
  - dữ liệu chứa `& < >` không phá layout (render không lỗi).
  - `ToImages` trả ≥1 ảnh; số ảnh == số trang.
- `ThreadSafetyTests`: `Parallel.For` render 20 group đồng thời → không lỗi/không lẫn dữ liệu.

## Implementation Steps
1. Viết `FileNameTests` + `FileName` (RED→GREEN).
2. Viết `IndexTemplate` (StyleConfig-driven: bảng + header lặp + footer từ settings + chữ ký).
3. Viết `RendererTests` (đọc lại PDF bằng PdfPig đếm trang/dòng) → chỉnh tới GREEN.
4. Thêm 3 mẫu còn lại (data-driven từ style.columns).
5. `ToImages` cho preview; test số ảnh == pageCount.
6. Test thread-safe song song.

## Success Criteria
- [ ] 1 group → 1 PDF A4 đúng: header, bảng đủ dòng, cột đúng thứ tự/độ rộng, footer "Trang x/y".
- [ ] Bảng ≥2 trang: **header bảng lặp** đầu mỗi trang; **số dòng bảng == số record**.
- [ ] `& < >` an toàn; `{stt_file}`/`{ngay_gio}` đúng; footer-only token không lọt vào body.
- [ ] Render **song song** 20 group không lỗi (thread-safe).
- [ ] `ToImages` == pageCount (cho preview P6).

## Red Team Fixes (áp dụng 2026-07-04)
- **#7 — MỘT lớp template, không phải 4:** thay `ITemplate`+`TemplateBase`+4 lớp+`TemplateRegistry` bằng
  **1 lớp `IndexTemplate`** hoàn toàn điều khiển bởi `StyleConfig` (columns/settings/documentFields). Chọn mẫu =
  lookup `StyleStore.LoadAll`. Nếu sau này có biến thể **cấu trúc** thật mới subclass. Prose header/label
  riêng mẫu lấy từ `settings` (P1) — KHÔNG hardcode trong code.
- **#6 — chốt 1 chữ ký (bỏ 2 cái kia):** `void Compose(IDocumentContainer c, DocGroup g, StyleConfig s)`
  (void-into-container). `Renderer.ToPdf(IndexTemplate, DocGroup, StyleConfig, outPath)` (4 tham số, có style).
  `stt_file`/`ngay_gio` truyền qua `DocGroup`/ctx (1-based). Bỏ mọi biến thể chữ ký khác trong plan.
- **#3 — KHỐI CHỮ KÝ (bắt buộc, parity):** thêm vùng chữ ký sau bảng: `{chuc_danh_ky}` + `{nguoi_ky}`;
  mẫu Đông Hà nhúng **ảnh** `settings.anh_chu_ky_path` (`chuky_tung.png`) — resolve asset + copy khi đóng gói
  (P7). Test: PDF chứa text người ký; mẫu Đông Hà có ảnh.
- **#20 — footer từ settings:** dựng footer từ `settings.footer_format` (token `{trang_so}/{tong_so_trang}`)
  + tôn trọng `footer_page_number` (tắt thì không vẽ) — không hardcode "Trang x/y".
- **#5 — filename an toàn:** strip bằng `Path.GetInvalidFileNameChars()` (gồm separator + control 0x00–0x1F);
  chặn **reserved names** (CON/PRN/AUX/NUL/COM1-9/LPT1-9 → prefix `_`); sau `Path.Combine`, **assert
  `Path.GetFullPath(result).StartsWith(Path.GetFullPath(outDir))`**, từ chối rooted/`..` (chống footgun
  `Path.Combine` bỏ outDir khi name rooted).
- **#4/#9 — preview==PDF + đếm dòng bằng sentinel:** `RendererTests` render **PDF thật** (`ToPdf`) rồi đọc lại
  bằng **PdfPig**, đếm **token STT sentinel** mỗi record (không đếm "dòng" hình học — cell wrap làm sai);
  assert số token == số record. Thêm test: `ToImages` page count **== page count của PDF thật** (không đo
  trên chính preview). **Pin phiên bản QuestPDF + set `ImageGenerationSettings` (raster DPI cố định)** để
  preview khớp xuất (tránh bẫy DPI như bản Qt — xem journal).
- **#1 — thread-safe: chứng minh trước:** static init (License + font) ở `Program.cs` trước mọi dispatch;
  `ThreadSafetyTests` render **cùng group serial vs `Parallel.For`** rồi **so nội dung** (PdfPig text diff),
  KHÔNG chỉ "no exception". Nếu không chứng minh được → P4 mặc định serial.
- **#2 — ghi atomic:** `ToPdf` ghi ra `outPath + ".tmp"` rồi `File.Move` sang `outPath` khi xong; lỗi/cancel →
  xoá tmp. Không bao giờ để file dở ở tên cuối.

## Risk Assessment
- QuestPDF `GenerateImages` cần cùng layout + **DPI pin** với PDF → preview khớp xuất (tránh lỗi resolution như bản Qt).
- 1 lớp template điều khiển bởi `StyleConfig` (columns/settings) → tránh 4 lớp lệch nhau.
- Đọc lại PDF trong test: **PdfPig** đếm **token sentinel** (không đếm dòng hình học) — thêm vào test project.
