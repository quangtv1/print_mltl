---
title: "WinForms .NET 8 + QuestPDF rebuild — Tạo Mục Lục Hồ Sơ"
description: ""
status: cancelled
priority: P2
branch: "feat/native-qt-pdf-designer"
tags: []
blockedBy: []
blocks: []
created: "2026-07-04T16:09:43.975Z"
createdBy: "ck:plan"
source: skill
---

# WinForms .NET 8 + QuestPDF rebuild — Tạo Mục Lục Hồ Sơ

> **CANCELLED / SUPERSEDED (2026-07-05):** thay bằng bản `mota3.html` (design_v3) — kiến trúc **WebView2**
> (nhúng prototype thật) + **WebView2 PrintToPdf** + editor WYSIWYG đầy đủ, thay cho QuestPDF/native/preview-only
> ở plan này. Xem `plans/reports/brainstorm-winform-webview2-260705-0504-mota3-hundred-percent-clone-report.md`.

## Overview

Xây lại app "Tạo Mục Lục Hồ Sơ" bằng **WinForms (.NET 8)** — làm việc tương tự app PyQt5 hiện tại
(đọc Excel → gom nhóm hồ sơ → xuất N PDF mục lục + Excel tổng hợp), **bám thiết kế `design/` (classic
blue `#0078d7`)**, wizard **3 bước**. Engine PDF = **QuestPDF** (4 mẫu **khoá bằng code**, không editor
WYSIWYG → **Bước 2 = Preview-only**). Đọc Excel = **ExcelDataReader** (streaming, async, progress) cho
file lớn 20k+ dòng. Ghi Excel tổng hợp = **ClosedXML**. Batch **mặc định serial** (an toàn); đa luồng bật khi
đã kiểm chứng QuestPDF thread-safe (Red Team #1).
Đóng gói **1 file .exe self-contained**.

**Nguồn:** `plans/reports/brainstorm-winform-questpdf-260704-2252-mltl-dotnet-rebuild-report.md`
**Driver (đã chốt):** ưu tiên stack C#/.NET (bảo trì). **Không đụng** app PyQt5 hiện tại tới khi WinForms đạt parity.

## Quyết định đã chốt (2026-07-04)
- .NET 8 WinForms, single-file self-contained `.exe` (portable — **không** MSI ở vòng này).
- **Vị trí repo:** thư mục con **`winform/`** trong repo hiện tại (solution `winform/MucLucHoSo.sln`).
- PDF: **QuestPDF**; 4 mẫu code-defined (Đông Hà, Quảng Ninh, Đống Đa, Vĩnh Phúc).
- Đọc Excel: **ExcelDataReader**; ghi: **ClosedXML**.
- `style.json` = **metadata** (mapping mặc định, settings **gồm cả prose header/label theo mẫu**, cột gom
  nhóm, pattern tên file) — layout ở code. *(Red Team #7: text riêng từng mẫu phải nằm trong settings.)*
- Bước 2 = **Preview-only** (chọn mẫu + ◀▶ duyệt hồ sơ + ảnh trang A4; bỏ toolbar sửa mẫu).
- Thiết kế: **`design2/` navy** — accent `#0043a5`, nút Đọc **teal `#00ccd6`**, bg `#f0f2f5`, hover `#e6eefb`,
  **banner trạng thái ghép biến**, ô gom nhóm **viền trái accent + icon 🗂**. (Bước 2 khác design2: bỏ toolbar sửa.)

## Tài liệu đích
"MỤC LỤC VĂN BẢN TRONG HỒ SƠ" = header (cơ quan, quốc hiệu, tiêu đề, "Hồ sơ số…") + **1 bảng** (cột từ
mapping) + footer "Trang x/y". Bảng **lặp dòng** theo record trong group; **header bảng lặp qua trang**.

## Acceptance Criteria (toàn plan)
- [ ] Chọn `.xlsx` **20k+ dòng** → đọc < vài giây, UI không treo, progress chạy; validate header rỗng/trùng.
- [ ] Đi hết 3 bước với Excel thật → xuất **N PDF** đúng dữ liệu; bảng dài chảy đúng qua trang, header bảng lặp, footer "Trang x/y".
- [ ] **Khối chữ ký** (chức danh/người ký; **ảnh chữ ký** cho mẫu Đông Hà) render đúng — điều kiện parity.
- [ ] Bước 2: chọn mẫu + ◀▶ duyệt hồ sơ + xem trang A4; **preview == PDF xuất ra** (có test so trực tiếp, không đo trên chính preview).
- [ ] Bước 3: batch (mặc định **serial** an toàn; đa luồng chỉ bật khi đã kiểm chứng QuestPDF thread-safe) + log realtime + ghi đè + Excel tổng hợp.
- [ ] Thư viện 4 mẫu chọn được; mapping auto-match (**xử lý đúng đ/Đ**) + gate "⚠ chưa gán".
- [ ] Build **1 file `.exe` self-contained** chạy sạch trên Win10/11 (không cần cài .NET/Python).
- [ ] Giao diện khớp `design2/` (navy): brand bar, stepper chevron, fieldset, **teal Đọc**, **banner trạng thái**, bảng mapping (`→` + token + ✓/⚠), ô gom nhóm accent + icon.
- [ ] **Parity = tương đương dữ liệu** (đúng N file + records + thứ tự cột), **KHÔNG** yêu cầu trùng số trang (engine khác).

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Solution Scaffold & Config](./phase-01-solution-scaffold-config.md) | Pending |
| 2 | [Excel Reader & Grouping](./phase-02-excel-reader-grouping.md) | Pending |
| 3 | [QuestPDF Template Engine](./phase-03-questpdf-template-engine.md) | Pending |
| 4 | [Batch Generator & Excel Export](./phase-04-batch-generator-excel-export.md) | Pending |
| 5 | [UI Shell & Step Input](./phase-05-ui-shell-step-input.md) | Pending |
| 6 | [Step Preview & Step Run](./phase-06-step-preview-step-run.md) | Pending |
| 7 | [Packaging CI & Parity](./phase-07-packaging-ci-parity.md) | Pending |

## Dependencies
Chuỗi: **P1 → P2 → P3 → P4**; **P1 → P5 → P6**; **(P4 + P6) → P7**.
Core P2/P3/P4 theo **TDD** (xUnit tests-first). P5/P6 UI. P7 đóng gói + đo parity.

**Cross-plan:** độc lập với các plan Python (`260704-1229-native-qt-pdf-designer-rebuild` = completed). App
PyQt5 giữ nguyên chạy song song; plan này **không** sửa nó. Không có quan hệ blocking cứng.

## Rủi ro chính
| Rủi ro | Giảm thiểu |
|---|---|
| QuestPDF Community license (<$1M/yr) | App cơ quan đủ điều kiện; fallback MigraDoc nếu cần; ghi rõ ở docs. |
| Bước 2 lệch prototype (không editor) | Preview-only đã chốt; giữ bố cục, bỏ toolbar sửa. |
| Khoá mẫu bằng code → sửa cần build | Chấp nhận; settings/mapping vẫn ở style.json không cần build. |
| File cực lớn treo UI | Đọc async + progress + cận kích thước fail-fast. |
| Fidelity phân trang (header lặp/footer) | QuestPDF `Table.Header`/footer sẵn — verify sớm bằng nhóm ≥3 trang. |
| Tester .NET trên máy macOS dev | `dotnet` chạy đa nền cho core/tests; build `.exe` win-x64 + test UI trên Windows CI. |

## Red Team Review

### Session — 2026-07-04
**Findings:** 31 thô (4 reviewer: Security/Failure-Mode/Assumption/Scope) → dedupe **21 accept**, 1 acknowledged
(quyết định user: rewrite vì stack preference). Mọi finding có `file:line`.
**Severity:** 4 Critical · 11 High · 6 Medium.

| # | Sev | Finding | Áp vào |
|---|-----|---------|--------|
| 1 | Crit | QuestPDF thread-safe **chưa kiểm chứng** (test không bắt được glyph corruption thầm lặng; Python đã tắt đa luồng vì lý do này) → **serial-first** | P3, P4 |
| 2 | Crit | Cancel/crash để lại PDF dở ở tên cuối → skip-if-exists khoá vĩnh viễn | P3, P4 |
| 3 | Crit | **Khối chữ ký bị bỏ sót** khỏi engine → parity bất khả | P3 |
| 4 | Crit | **Preview(ToImages) ≠ export(ToPdf)** — đúng bẫy DPI dự án từng dính (journal) | P3, P6 |
| 5 | High | Filename path-traversal (sanitizer chưa nêu + `Path.Combine` rooted footgun + reserved names) | P3, P4 |
| 6 | High | **Contract drift** `Compose`/`ToPdf`/`DocGroup` 3 chữ ký mâu thuẫn → P3↔P4 không build | P2, P3, P4 |
| 7 | High | "4 lớp mẫu data-driven" **sai** — khác biệt chỉ ở `columns`; prose header nằm ở `template_html` (P1 xoá) → **1 lớp** + prose vào settings | P1, P3 |
| 8 | High | Gate "cùng số trang bản Python" **bất khả** (engine khác) → parity = tương đương dữ liệu | P7 |
| 9 | High | Test PdfPig "đếm dòng == record" **không hiện thực được** → dùng sentinel token | P3 |
| 10 | High | macOS không build/test WinForms (`EnableWindowsTargeting` thiếu; CI không test UI; không owner) | P1, P7 |
| 11 | High | Batch song song tối ưu **sai chỗ** (I/O-bound; Python serial) → serial-first, đa luồng opt-in | P4 |
| 12 | High | Nav lock không unlock khi lỗi fatal (thiếu `finally`) → treo UI | P6 |
| 13 | High | Preview cache theo group-index **không** invalidate theo DataVersion → ảnh cũ | P6 |
| 14 | High | QuestPDF license: chưa xác định pháp lý cụ thể + không có điểm quyết định fallback | P1, P7 |
| 15 | Med | Excel zip-bomb / per-cell cap (chỉ có MAX_ROWS) | P2 |
| 16 | Med | Formula-injection bỏ sót leading space/tab/CR | P4 |
| 17 | Med | `explorer.exe` arg thay vì `UseShellExecute` (thoái lui so với `os.startfile`) | P6 |
| 18 | Med | Cancelled read chưa có hợp đồng discard → có thể commit dữ liệu cụt vào AppState | P2, P5 |
| 19 | Med | đ/Đ normalization: NFD không tách `đ` → auto-match lệch nếu port ngây thơ | P5 |
| 20 | Med | Footer hardcode, bỏ qua `settings.footer_format`/`footer_page_number` | P3 |
| 21 | Med | Self-extract native chưa ký (SmartScreen/AV) + DLL-plant path | P7 |

**Acknowledged (không phải lỗi):** rewrite chỉ vì stack preference — trade-off đã ghi ở brainstorm §2 (đã
loại phương án rẻ hơn calamine cho tốc độ đọc; user chấp nhận).

Chi tiết fix ở mục **"Red Team Fixes"** trong từng phase file.

### Whole-Plan Consistency Sweep
Sau khi áp: serial-first nhất quán P3↔P4↔plan; 1 lớp template + prose-in-settings nhất quán P1↔P3;
`DocGroup` 1 kiểu (định nghĩa ở P2) nhất quán P2↔P3↔P4; parity = data-equivalence nhất quán P3↔P7;
design2 navy nhất quán plan↔P5. **0 mâu thuẫn tồn đọng.**

## Câu hỏi còn treo
Không còn (đã chốt qua brainstorm + defaults: subfolder `winform/`, portable exe; design2 navy; serial-first).
