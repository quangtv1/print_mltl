---
phase: 2
title: Màn 3 — sidebar biến giống màn 1 + cảnh báo thiếu Word
status: completed
effort: ''
---

# Phase 2: Màn 3 — sidebar biến giống màn 1 + cảnh báo thiếu Word

## Overview
Đồng bộ sidebar biến ở Màn 3 với Màn 1: gộp chip auto (trang_so/tong_so_trang) vào "Biến tự do", thêm nhóm "Biến ảnh" (gộp token + alt, khử trùng). Thiếu Word → banner cảnh báo mềm, **không chặn** "Tiếp theo".

## Requirements
- Functional:
  - Sidebar hiện: Biến tự do (kèm chip auto trang xanh), Biến trong bảng, **Biến ảnh** (chip cam, hiển thị-only) — bố cục nhóm giống Màn 1.
  - Bỏ section "Biến tự động" riêng; chip trang gộp vào ngay dưới "Biến tự do".
  - Token `{image...}` gộp vào Biến ảnh, **bỏ khỏi** Biến tự do/trong bảng.
  - Thiếu Word → banner rõ: "Không có Microsoft Word — chỉ xuất được DOCX, không Preview/PDF". Nút "Tiếp theo" **vẫn hoạt động**.
- Non-functional: giữ tính năng bấm chip để nổi sáng của Biến tự do & Biến trong bảng; không sửa `Core`.

## Architecture
- Dùng đúng quy tắc dedup của Phase 1 trong `RebuildVarGroups`:
  - `HeaderVars = HeaderFields.Except(ImageTokenFields).OrderBy(OrderOf)`
  - `RowVars    = RowFields.Except(ImageTokenFields).OrderBy(OrderOf)`
  - `ImageVars  = (ImageFields ∪ ImageTokenFields).OrderBy(OrderOf)`; `HasImageVars = ImageVars.Count>0`.
  - Chip auto vẫn nạp vào `AutoVars`; **hiển thị chung khối "Biến tự do"** (đặt ItemsControl AutoVars ngay dưới HeaderVars, không header riêng) — giống Màn 1.
- Cảnh báo Word: đã có `WordAvailable` (set trong `OnActivated`). Thêm binding banner `Visibility={Binding WordAvailable, InverseBoolToVis}`. **Không** đụng `CanGoNext` (Step3 mặc định true → Next vẫn bật).

## Related Code Files
- Modify: `src/MucLucHoSo.App/ViewModels/Step3PreviewViewModel.cs`
  - Thêm `ObservableCollection<string> ImageVars` + `[ObservableProperty] bool _hasImageVars`.
  - `RebuildVarGroups`: áp dedup Except(ImageTokenFields) cho HeaderVars/RowVars; nạp ImageVars; giữ AutoVars.
- Modify: `src/MucLucHoSo.App/Views/Step3PreviewView.xaml`
  - Chuyển ItemsControl `AutoVars` lên ngay dưới "Biến tự do" (bỏ section header "Biến tự động"; có thể giữ 1 dòng ghi chú trang_so/tong_so_trang).
  - Thêm nhóm "Biến ảnh" (chip cam hiển thị-only, mẫu chip giống Màn 1) bọc `Visibility={Binding HasImageVars, BoolToVis}`.
  - Thêm banner cảnh báo thiếu Word (đặt đầu sidebar hoặc trên khung Preview) bind `WordAvailable` (InverseBoolToVis).

## Implementation Steps
1. VM: thêm `ImageVars` + `HasImageVars`.
2. `RebuildVarGroups`: `HeaderVars`/`RowVars` dùng `.Except(S.Runtime.ImageTokenFields)`; nạp `ImageVars = ImageFields.Union(ImageTokenFields)` theo `OrderOf`; set `HasImageVars`. Giữ nạp `AutoVars`/`HasAutoVars`.
3. XAML: di chuyển khối AutoVars vào ngay dưới nhóm "Biến tự do" (chip xanh, không header riêng); giữ dòng chú thích `{trang_so}/{tong_so_trang}`.
4. XAML: thêm nhóm "Biến ảnh" chip cam (dùng lại style chip như Màn 1 `#FFFBEBD2` / `#FFB26A00`, hiển thị-only), visibility theo `HasImageVars`.
5. XAML: thêm banner cảnh báo thiếu Word (màu Warn), visibility `!WordAvailable`; đảm bảo **không** chặn Next.
6. Rà: token `{image...}` không còn ở HeaderVars/RowVars; chip auto nằm trong khối Biến tự do.

## Success Criteria
- [ ] Sidebar Màn 3 có nhóm Biến ảnh khi mẫu có ảnh/token ảnh; chip cam hiển thị-only.
- [ ] Chip trang_so/tong_so_trang nằm trong khối "Biến tự do" (không còn section "Biến tự động" riêng).
- [ ] Token `{image...}` chỉ ở nhóm Biến ảnh, không double ở Biến tự do/trong bảng.
- [ ] Bấm chip Biến tự do/Biến trong bảng vẫn nổi sáng như cũ.
- [ ] Thiếu Word: hiện banner cảnh báo; nút "Tiếp theo" **vẫn bấm được**.
- [ ] Build Release trên Windows OK; không đụng `Core`.

## Risk Assessment
- **Chip ảnh có nên bấm-nổi-sáng?** → quyết định hiển thị-only cho giống Màn 1 & tránh logic highlight cho alt-text; nếu sau muốn highlight, mở rộng sau (YAGNI).
- **Regression highlight**: chỉ đổi nguồn nạp HeaderVars/RowVars (Except token ảnh) — kiểm tra `SelectVar` vẫn khớp tên cột.
- **macOS không verify được** → build/test thủ công trên Windows ([[dotnet-rebuild-toolchain-macos]]).
