---
phase: 1
title: Màn 1 — template picker gộp Import + empty-state + chặn Next
status: completed
effort: ''
---

# Phase 1: Màn 1 — template picker gộp Import + empty-state + chặn Next

## Overview
Gộp nút "Import DOCX…" thành dòng cuối của ComboBox chọn mẫu; thêm trạng thái rỗng cho vùng biến ("Hãy chọn mẫu để tiếp tục"), cảnh báo mẫu không biến, gom token ảnh vào nhóm Biến ảnh, và chặn "Tiếp theo" khi mẫu không có biến dữ liệu.

## Requirements
- Functional:
  - ComboBox mẫu có item sentinel cuối cùng "➕ Import DOCX…". Chọn → mở `OpenFileDialog` (.docx) như nút hiện tại. Chọn file → chèn item thật (IsImported) trước sentinel + select. **Hủy dialog → revert về mẫu trước đó**, không kẹt ở sentinel.
  - Xóa nút "Import DOCX…" rời khỏi XAML, nới rộng ComboBox.
  - Chưa chọn mẫu → ẩn toàn bộ list biến, hiện tiêu đề nổi bật "Hãy chọn mẫu để tiếp tục".
  - Chọn mẫu **có biến dữ liệu** → ẩn tiêu đề, hiện nhóm: Biến tự do (kèm chip auto trang), Biến trong bảng, Biến ảnh (khi `ImageVars` khác rỗng).
  - Chọn mẫu **không có biến dữ liệu** → hiện cảnh báo "Mẫu này không có biến, hãy chọn lại !" + disable "Tiếp theo".
  - Nhóm Biến ảnh gồm cả token `{image...}` (khử trùng khỏi Biến tự do/trong bảng).
- Non-functional: không sửa `Core`; giữ nguyên luồng đọc dữ liệu; guard chống đệ quy khi revert selection.

## Architecture
- **Dedup ở VM** (không đụng Core):
  - `FreeVars  = HeaderFields.Except(ImageTokenFields)`
  - `TableVars = RowFields.Except(ImageTokenFields)`
  - `ImageVars = ImageFields.Union(ImageTokenFields)`
  - `HasDataVars = FreeVars.Any() || TableVars.Any() || ImageVars.Any()`
- **Sentinel import**: `record TemplateItem(string Name, string Path, bool IsImported=false, bool IsImportAction=false)`. `LoadBuiltInTemplates` append thêm 1 sentinel `new("➕ Import DOCX…","",IsImportAction:true)` sau khi nạp mẫu.
- **Revert an toàn**: field `_prevTemplate` giữ mẫu hợp lệ gần nhất; field `_suppressSelectionHandler` (bool) chặn đệ quy khi gán lại `SelectedTemplate` trong handler.
- **Trạng thái vùng biến** (VM computed/observable):
  - `TemplateChosen => S.Runtime != null`
  - `HasDataVars` (như trên) → khi false và `TemplateChosen` true ⇒ `ShowNoVarWarning = true`.
  - `ShowEmptyPrompt = !TemplateChosen && !ShowNoVarWarning` (hiện "Hãy chọn mẫu để tiếp tục").
  - `ShowVarLists = TemplateChosen && HasDataVars`.
- **Gate Next**: `UpdateCanGoNext() => CanGoNext = S.DataLoaded && S.Runtime != null && HasDataVars;`

## Related Code Files
- Modify: `src/MucLucHoSo.App/ViewModels/Step1SourceViewModel.cs`
  - Mở rộng `TemplateItem` (thêm `IsImportAction`).
  - `LoadBuiltInTemplates`: append sentinel.
  - `OnSelectedTemplateChanged`: nhánh xử lý sentinel (mở dialog, chèn/ select hoặc revert); dedup FreeVars/TableVars/ImageVars; set `HasDataVars`, cờ empty/warning.
  - Thêm observable/notify props: `HasDataVars`, `ShowEmptyPrompt`, `ShowNoVarWarning`, `ShowVarLists` (hoặc computed + `OnPropertyChanged`).
  - `UpdateCanGoNext`: thêm `&& HasDataVars`.
  - Giữ `ImportTemplateCommand` logic dùng lại trong nhánh sentinel (tránh trùng — DRY).
- Modify: `src/MucLucHoSo.App/Views/Step1SourceView.xaml`
  - Xóa `Button "Import DOCX…"` (`:108-109`), nới ComboBox chiếm chỗ.
  - Bọc `ScrollViewer` list biến bằng `Visibility={Binding ShowVarLists,...BoolToVis}`.
  - Thêm block tiêu đề rỗng "Hãy chọn mẫu để tiếp tục" (`ShowEmptyPrompt`) và block cảnh báo "Mẫu này không có biến, hãy chọn lại !" (`ShowNoVarWarning`).
  - `ImageVars` binding trỏ collection đã gộp; giữ `HasImageVars` = `ImageVars.Count>0`.

## Implementation Steps
1. Sửa `TemplateItem` record thêm `bool IsImportAction=false`; cập nhật `ToString()` giữ nguyên hiển thị `Name`.
2. `LoadBuiltInTemplates`: sau vòng nạp mẫu, `Templates.Add(new TemplateItem("➕ Import DOCX…","",IsImportAction:true));`.
3. Thêm helper `PerformImport()` (tách từ `ImportTemplate` cũ): mở dialog, nếu OK → tạo item thật, `Templates.Insert(Templates.Count-1, item)` (trước sentinel), `SelectedTemplate=item`, trả `true`; nếu hủy → trả `false`.
4. `OnSelectedTemplateChanged`: nếu `_suppressSelectionHandler` → return. Nếu `value?.IsImportAction==true`: set suppress, gọi `PerformImport()`; nếu false → gán `SelectedTemplate=_prevTemplate` (revert), clear suppress, return. Nếu là mẫu thường → cập nhật `_prevTemplate=value`, tiếp tục compile như cũ.
5. Trong nhánh compile thành công: build `FreeVars/TableVars/ImageVars` theo quy tắc dedup; set `HasImageVars`, `HasDataVars`; raise `ShowEmptyPrompt/ShowNoVarWarning/ShowVarLists`.
6. Nhánh `value is null` (deselect): reset toàn bộ, `ShowEmptyPrompt=true`.
7. `UpdateCanGoNext`: `CanGoNext = S.DataLoaded && S.Runtime!=null && HasDataVars;`.
8. XAML: xóa nút Import, nới ComboBox; thêm 2 block trạng thái; bọc visibility list biến; trỏ `ImageVars` sang collection gộp.
9. Rà lại: token `{image...}` không còn xuất hiện ở Biến tự do/trong bảng.

## Success Criteria
- [ ] ComboBox có dòng cuối "➕ Import DOCX…"; chọn nó mở dialog; chọn file → mẫu mới được thêm & chọn; **hủy dialog → quay lại mẫu cũ, không kẹt**.
- [ ] Không còn nút "Import DOCX…" rời.
- [ ] Chưa chọn mẫu: chỉ thấy "Hãy chọn mẫu để tiếp tục"; list biến ẩn.
- [ ] Chọn mẫu có biến: ẩn prompt, hiện Biến tự do (+ chip trang nếu có), Biến trong bảng, Biến ảnh (khi có).
- [ ] Mẫu không biến dữ liệu: hiện "Mẫu này không có biến, hãy chọn lại !"; nút "Tiếp theo" disable.
- [ ] Token `{image...}` chỉ hiện ở nhóm Biến ảnh, không double ở Biến tự do/trong bảng.
- [ ] Build Release trên Windows OK; không đụng `Core`.

## Risk Assessment
- **Đệ quy khi revert selection**: gán lại `SelectedTemplate` trong handler kích hoạt lại handler → dùng `_suppressSelectionHandler` guard.
- **Sentinel bị compile như path rỗng**: nhánh `IsImportAction` phải return sớm trước khi gọi `Compile`.
- **macOS không verify được**: yêu cầu người dùng build/test trên Windows ([[dotnet-rebuild-toolchain-macos]]).
