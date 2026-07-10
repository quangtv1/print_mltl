---
title: "Màn 1 'Đọc từ:' (dòng header) + Màn 2 tự-ghép chặt snake_case"
description: ""
status: in-progress
priority: P2
branch: "main"
tags: []
blockedBy: []
blocks: []
created: "2026-07-10T09:56:22.606Z"
createdBy: "ck:plan"
source: skill
---

# Màn 1 'Đọc từ:' (dòng header) + Màn 2 tự-ghép chặt snake_case

## Overview

2 tính năng độc lập cho app WPF/MVVM `print_mltl`. Nguồn: [brainstorm report](../reports/brainstorm-260710-1647-step1-read-from-row-step2-strict-automap-report.md).

**F#1 — Màn 1 "Đọc từ:"** (chạm Core): chọn dòng bắt đầu đọc (N = dòng header, bỏ N−1 dòng trên), 1-based, mặc định 1. Áp cho **cả preview + validate + generate** qua `SessionState.ReadStartRow`. UI: ô "Đọc từ" thế chỗ nút, chuyển nút "Đọc dữ liệu" xuống hàng dưới, nhãn inline "đã đọc" (ẩn mặc định).

**F#2 — Màn 2 tự-ghép chặt** (chỉ VM/TextUtil): `TextUtil.Slug` (thường + bỏ dấu + cụm ký tự không chữ/số → một `_` + trim) → khớp **chính xác** `Slug(cột)==Slug(biến)`; **bỏ hẳn fallback `.Contains`**.

## Quyết định đã chốt (từ brainstorm)
- Nhãn "đã đọc" = nhãn inline mới cạnh nút.
- Match Màn 2 = chặt theo `_` (Slug giữ underscore).
- Bỏ hoàn toàn fallback `.Contains`.
- Start row N = dòng header, áp mọi điểm đọc.

## Acceptance chung
- macOS **không build WPF** → build & test thủ công trên Windows (xem [[dotnet-rebuild-toolchain-macos]]). Tầng Core (skip dòng, Slug) có thể unit-test nếu thêm test project (ngoài scope).
- Consistency: mọi `ReaderFactory.Open` dùng cùng `ReadStartRow`.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Core hỗ trợ dòng bắt đầu đọc (readers+factory+CoreService+SessionState)](./phase-01-core-h-tr-d-ng-b-t-u-c-readers-factory-coreservice-sessionst.md) | Code done · Core build ✓ |
| 2 | [Màn 1 UI/VM ô 'Đọc từ' + chuyển nút + nhãn đã đọc](./phase-02-m-n-1-ui-vm-c-t-chuy-n-n-t-nh-n-c.md) | Code done · chờ verify Windows |
| 3 | [Màn 2 tự-ghép chặt Slug snake_case (bỏ fallback mờ)](./phase-03-m-n-2-t-gh-p-ch-t-slug-snake-case-b-fallback-m.md) | Code done · chờ verify Windows |

## Trạng thái thực thi (2026-07-10)
- Đã code cả 3 phase. Core project build Release sạch (0 warn/0 err) trên macOS.
- App (WPF `net8.0-windows`) không build được trên macOS → **cần build + test thủ công trên Windows** ([[dotnet-rebuild-toolchain-macos]]).
- Code-review (subagent): logic đúng & không hồi quy; đã sửa 2 lỗi debounce (High: modal bật khi giá trị không hợp lệ lúc gõ; Medium: đua ghi đè kết quả đọc cũ). CTS-dispose (Low) bỏ qua theo KISS.

## Dependencies

- Phase 2 phụ thuộc Phase 1 (VM Màn 1 cần `ReadStartRow` + `ReadHead(headerRow)`).
- Phase 3 độc lập (chỉ TextUtil + Step2 VM).
- Không cross-plan: plan 260707 (var-panel) đã completed, khác concern.

## Validation Log

### Session 1 (2026-07-10)

**Verification Results**
- Claims checked: 8 (file paths + symbols) · Verified: 8 · Failed: 0 · Unverified: 0 · Tier: Standard
- Xác nhận: `ExcelRowReader.ReadHeaderRow:34`, `CsvRowReader:27`, `ReaderFactory.Open`, `CoreService.ReadHead/Validate/ReaderFactoryFor:30/49/64`, `Step2MappingViewModel:47-48`, `TextUtil.Normalize`.

**Quyết định phỏng vấn**
1. **Đọc lại khi đổi "Đọc từ"/"Số dòng đọc"** → **Tự đọc lại ngay, có debounce** (không phải thủ công). Ảnh hưởng Phase 2.
2. **Nghĩa của N ("Đọc từ")** → **Bỏ qua dòng trống rồi mới đếm**: header = dòng KHÔNG-trống thứ N (bỏ các dòng trống phía trên/xen giữa). Ảnh hưởng Phase 1 (skip-logic đếm theo dòng có dữ liệu, không raw-skip).
3. **Heuristic cột gom nhóm** (`Normalize.Contains("hoso")`) → **Giữ nguyên**, ngoài phạm vi. Phase 3 không đổi.

**Whole-Plan Consistency Sweep**
- Phase 1: đổi skip-logic sang đếm dòng-không-trống + guard hết dòng.
- Phase 2: đổi invalidation thủ công → auto-reread debounce (chỉ khi đã có file+sheet).
- Phase 3: không đổi.
- Không còn mâu thuẫn tồn đọng.
