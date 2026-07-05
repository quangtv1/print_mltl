---
title: "WinForms + WebView2 — clone 100% design_v3 (Tạo Mục Lục Hồ Sơ)"
description: ""
status: in-progress
priority: P2
branch: "feat/winform-webview2"
tags: []
blockedBy: []
blocks: []
created: "2026-07-04T22:11:04.808Z"
createdBy: "ck:plan"
source: skill
---

# WinForms + WebView2 — clone 100% design_v3 (Tạo Mục Lục Hồ Sơ)

## Overview

App desktop **.NET 8 WinForms** tái tạo **100%** GUI + logic của prototype `design_v3/` theo đặc tả
`design_v3/mota3.html` (13 mục — **nguồn chân lý**). Kiến trúc **"vỏ WinForms + lõi WebView2"**: khung/wizard/
Bước 1/Bước 3 = control WinForms thật; **tờ A4 (soạn thảo + xem trước) nhúng WebView2 tái dùng đúng
HTML/CSS/JS + engine phân trang của prototype** → giống tuyệt đối. Xuất PDF bằng **`CoreWebView2.PrintToPdfAsync`**
(in chính HTML đã resolve — không cần Chromium thứ 2, preview==PDF). Đọc Excel **ClosedXML**; cấu hình JSON
`%AppData%`; batch **`Parallel.ForEachAsync`** với pool WebView2 offscreen. **Gỡ toàn bộ Python** của bản cũ.

**Nguồn:** `plans/reports/brainstorm-winform-webview2-260705-0504-mota3-hundred-percent-clone-report.md` +
`design_v3/mota3.html` (đặc tả) + `design_v3/{design3.html,support.js,template/}` (prototype vendor).
**Thay thế:** plan `260704-2252-winform-questpdf` (QuestPDF/native/preview-only) = **cancelled**.

## Quyết định đã chốt (2026-07-05)
- **.NET 8 WinForms** (`net8.0-windows`) + **WebView2** cho tờ A4 (vendor prototype `design_v3`).
- PDF: **`CoreWebView2.PrintToPdfAsync`** (Puppeteer chỉ là fallback nếu thiếu kiểm soát).
- Excel: **ClosedXML**; cấu hình: **JSON `%AppData%\MLTL\`**; batch: **`Parallel.ForEachAsync`** + pool offscreen.
- Editor **WYSIWYG đầy đủ** (contentEditable trong WebView2): font/size/BIU/căn/zoom/hướng A4/chèn-xóa-kéo cột/undo/chèn+highlight biến.
- Solution **`MucLucTaiLieu.sln` ở GỐC repo**; layout theo mota3 §12 (App + Core `net8.0` + Tests).
- Nhánh mới **`feat/winform-webview2`** off `main`. **Xoá toàn bộ Python** (P1) — còn trong git history + PR #1.
- 4 mẫu mau01–04 (Quảng Trị/Quảng Ninh/Đống Đa/Vĩnh Phúc) + seed data JSON.

## Acceptance Criteria (toàn plan) = mota3 §13
- [ ] Wizard 3 bước, step header ✓/đang/chưa; nút "Tiếp theo" khóa tới khi mapping hợp lệ.
- [ ] Bước 1: Excel+sheet, Đọc dữ liệu, bảng ghép biến (auto-match, **không** cảnh báo trùng cột), banner trạng thái, cột gom nhóm hiện số file sẽ tạo.
- [ ] 4 mẫu đúng tiêu đề/số cột/bố cục; đổi mẫu cập nhật biến + seed data; footer số trang sát đáy phải.
- [ ] Bước 2: toolbar đúng thứ tự (font/size/BIU/A−A+/căn/zoom/hướng/chèn-xóa cột theo focus/undo); chèn biến tại con trỏ; xóa cả biến; kéo giãn cột; highlight biến ở preview; **phân trang đúng** bảng dài.
- [ ] Bước 3: thư mục + mẫu tên ({stt_file},{ngay_gio}); tùy chọn đa luồng(ẩn số luồng)/ghi đè/Excel/bỏ-qua-lỗi; ước tính thời gian; **một nút "Tạo Mục lục"**; tiến trình (x/y) + console log + mở thư mục + thử lại lỗi.
- [ ] Màu accent `#0043a5`, nút Đọc cyan `#00ccd6`, font/cỡ đúng; **nhớ trạng thái lần trước**.
- [ ] **PDF khổ A4 trùng khớp xem trước**.
- [ ] **Repo không còn code Python**; solution .NET build/chạy trên Windows.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Scaffold Delete-Python & Vendor Prototype](./phase-01-scaffold-delete-python-vendor-prototype.md) | Done |
| 2 | [Core Excel Models Config NameResolver](./phase-02-core-excel-models-config-nameresolver.md) | Done |
| 3 | [WebView2 Host Bridge & Template Engine](./phase-03-webview2-host-bridge-template-engine.md) | Code done (host/renderer/bridge + React bootstrap); PrintToPdf spike + render fidelity pending Windows |
| 4 | [Step 1 Input & Mapping](./phase-04-step-1-input-mapping.md) | Code done (compiles + published on CI); behavior verify on Windows |
| 5 | [Step 2 Design Editor & Preview](./phase-05-step-2-design-editor-preview.md) | Code done (toolbar/panel/nav via bridge); editor+preview verify on Windows |
| 6 | [Step 3 Run Batch PrintToPdf](./phase-06-step-3-run-batch-printtopdf.md) | Core done+tested; Step3 UI done (compiles); real PDF run verify on Windows |
| 7 | [Packaging CI & Acceptance](./phase-07-packaging-ci-acceptance.md) | CI green (test + publish win-x64 artifact) + docs done; §13 acceptance pending Windows |

### Bootstrap resolved (2026-07-05, session 2)
Vendored `Web/index.html` is a DesignCanvas **React** component compiled by `support.js`.
Resolved by bundling pinned React/ReactDOM/@babel-standalone UMD in `Web/lib/` (support.js's
CDN loaders short-circuit when the globals exist) and exposing the mounted instance as
`window.__mltlComponent` for `bridge.js`. **Not yet verified live** — this env's Chrome is
network-isolated and macOS has no WebView2; confirm render + the PrintToPdf spike on Windows.

## Dependencies
Chuỗi: **P1 → P2, P3**; **(P2 + P3) → P4, P5, P6**; **(P4+P5+P6) → P7**.
Core P2 + logic batch P6 theo **TDD** (xUnit, `net8.0`, chạy đa nền). UI/WebView2 (P3–P6) verify tay trên Windows.

**Cross-plan:** thay `260704-2252-winform-questpdf` (đã cancelled). Các plan Python đã completed/cancelled.
Không blocking cứng.

## Rủi ro chính
| Rủi ro | Giảm thiểu |
|---|---|
| WebView2 runtime thiếu máy đích | Evergreen sẵn Win10/11; Fixed-Version bundle nếu offline (chốt ở P7). |
| PrintToPdf ít kiểm soát lề hơn Puppeteer | `CoreWebView2PrintSettings` (A4/margin/scale); phân trang do template làm → PrintToPdf chỉ "chụp" HTML đã chia trang. Fallback Puppeteer nếu thiếu. |
| Batch nhiều WebView2 offscreen tốn RAM | Pool giới hạn (=ProcessorCount), tái dùng+dispose; đo ~1000 hồ sơ. |
| Vendor JS engine prototype khó bảo trì | Vendor nguyên trạng `Web/`; chỉ thay data mock bằng cầu nối C#; giữ đúng thuật toán. |
| Xoá Python phá working tree đang sửa | Commit/stash trước; Python ở git history + PR #1. |
| Dev macOS không build WinForms/WebView2 | Core `net8.0` test đa nền; App+WebView2+PrintToPdf build/test **Windows CI**. |

## Validation Log

### Verification Results (2026-07-05)
- Claims checked: nguồn phụ thuộc plan — VERIFIED: `design_v3/{design3.html,support.js,mota3.html,template/}` tồn tại;
  21 file `.py` tracked (P1 xoá); mota3 §0–13 đủ; `dotnet 9.0.109` trên dev (build `net8.0` được, UI cần Windows).
- Tier: Full (7 phase). **Failed: 0** → plan đủ điều kiện triển khai.

### Session 1 (2026-07-05) — 4 quyết định chốt
1. **Thử nghiệm PrintToPdf TRƯỚC ở P3** (gate): chứng minh 1 hồ sơ nhiều trang in ra **khớp y hệt preview**
   (lề/scale/ngắt trang) **trước** khi dựng Bước 1–6. Hỏng → chuyển Puppeteer ngay. *(Propagate: P3)*
2. **Quy mô batch = vài trăm / chưa rõ** → **serialize-first**, pool nhỏ; không đầu tư pool phức tạp/throttle
   upfront; chỉ tăng song song khi có nhu cầu thật. *(Propagate: P6)*
3. **Seed = dataset có sẵn trong prototype** → tách `App/Assets/seed/*.json` từ JS prototype. *(P1, P2)*
4. **WebView2 = Evergreen + bootstrapper** (không Fixed-Version mặc định). *(P7)*

### Whole-Plan Consistency Sweep (sau Session 1)
PrintToPdf spike-first nhất quán P3↔plan; serialize-first + pool nhỏ nhất quán P6↔plan (bỏ nhấn "đa luồng
nhanh hơn"); seed-from-prototype nhất quán P1↔P2; Evergreen nhất quán P7. **0 mâu thuẫn tồn đọng.**

## Câu hỏi còn treo
- Đóng gói: portable self-contained vs MSI → mặc định **portable**, chốt ở P7 (không chặn triển khai).
