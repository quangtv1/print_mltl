---
title: 'Màn 1 & 3: chọn mẫu trong ComboBox + panel biến có trạng thái rỗng'
description: ''
status: completed
priority: P2
branch: main
tags: []
blockedBy: []
blocks: []
created: '2026-07-07T13:59:24.886Z'
createdBy: 'ck:plan'
source: skill
---

# Màn 1 & 3: chọn mẫu trong ComboBox + panel biến có trạng thái rỗng

## Overview

Cải tổ UX chọn mẫu + panel biến ở Màn 1 (Step1Source) và đồng bộ sidebar biến ở Màn 3 (Step3Preview). App WPF/MVVM (CommunityToolkit.Mvvm). **Không sửa `Core`** — mọi khử-trùng/gom nhóm xử lý ở ViewModel.

**Quyết định đã chốt với người dùng:**
- Thiếu Word → **cảnh báo mềm ở Màn 3, KHÔNG chặn** "Tiếp theo" (README: không-Word vẫn xuất DOCX).
- Nhóm "Biến ảnh" = `ImageFields ∪ ImageTokenFields`, **bỏ token `{image...}` khỏi** Biến tự do/trong bảng (khử trùng). Hiện nhóm khi có 1 trong 2.
- "Mẫu không có biến" = **không có biến dữ liệu** (Free ∪ Table ∪ Image rỗng); bỏ qua `trang_so`/`tong_so_trang`.
- Màn 3: **gộp** chip auto (trang_so/tong_so_trang) vào "Biến tự do" như Màn 1.

**Quy tắc dedup dùng chung (cả 2 màn):**
- `FreeVars  = HeaderFields \ ImageTokenFields`
- `TableVars = RowFields    \ ImageTokenFields`
- `ImageVars = ImageFields  ∪ ImageTokenFields`
- `HasDataVars = FreeVars.Any() || TableVars.Any() || ImageVars.Any()` (không tính AutoFields)

## Acceptance chung
- macOS **không build/verify được** WPF+COM Word → build & test thủ công trên Windows (xem [[dotnet-rebuild-toolchain-macos]]).
- Không thay đổi `MucLucHoSo.Core`; converters (`BoolToVis`, `InverseBoolToVis`, `NullToVis`) đã có sẵn.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Màn 1 — template picker gộp Import + empty-state + chặn Next](./phase-01-m-n-1-template-picker-g-p-import-empty-state-ch-n-next.md) | Completed |
| 2 | [Màn 3 — sidebar biến giống màn 1 + cảnh báo thiếu Word](./phase-02-m-n-3-sidebar-bi-n-gi-ng-m-n-1-c-nh-b-o-thi-u-word.md) | Completed |

## Dependencies

<!-- Cross-plan dependencies -->
