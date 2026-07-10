---
phase: 2
title: "Màn 1 UI/VM ô 'Đọc từ' + chuyển nút + nhãn đã đọc"
status: code-done
effort: ""
dependencies: [1]
---

# Phase 2: Màn 1 UI/VM ô "Đọc từ" + chuyển nút + nhãn đã đọc

## Overview
Thêm ô "Đọc từ:" (dòng header, mặc định 1) ở Màn Nguồn, chuyển nút "Đọc dữ liệu" xuống hàng dưới, thêm nhãn inline "đã đọc" (ẩn mặc định, hiện sau khi đọc).

## Requirements
- Functional:
  - Ô "Đọc từ:" mặc định "1"; parse số nguyên ≥1 khi bấm "Đọc dữ liệu"; giá trị không hợp lệ → cảnh báo (giống ô "Số dòng đọc").
  - Bấm "Đọc dữ liệu" dùng `S.ReadStartRow` → bảng dữ liệu đọc theo vị trí header đã chọn.
  - **Đổi giá trị "Đọc từ" (hoặc "Số dòng đọc") → TỰ đọc lại có debounce** (chốt ở Validation §1): chỉ tự đọc khi đã có `SourcePath` + `SheetName`; debounce ~400ms sau lần gõ cuối; giá trị không hợp lệ (<1) → không đọc + cảnh báo inline, ẩn nhãn.
  - Nhãn inline "✓ Đã đọc N dòng · M cột" ngay sau nút, **ẩn mặc định**, hiện sau khi đọc thành công; ẩn trong lúc đang (re)load.
  - Ô "Đọc từ" thế chỗ nút cũ (hàng Sheet); nút "Đọc dữ liệu" chuyển xuống hàng mới bên dưới.
- Non-functional: không đổi luồng chọn template; giữ `CanGoNext` hiện có.

## Architecture
- **Step1SourceViewModel:**
  - `[ObservableProperty] private string _readFromText = "1";`
  - `[ObservableProperty] private bool _hasReadInfo;` + `[ObservableProperty] private string _readInfoText = "";`
  - `ReadDataAsync`: parse `ReadFromText` (≥1, else cảnh báo) → `S.ReadStartRow = start;` truyền vào `ReadHead(..., limit, start)`. Sau khi đọc OK: `ReadInfoText = $"✓ Đã đọc {rows.Count} dòng · {headers.Count} cột"; HasReadInfo = true;`.
  - **Debounce auto-reread:** `OnReadFromTextChanged`/`OnRowLimitTextChanged` → `HasReadInfo=false`; nếu chưa có `SourcePath`/`SheetName` thì thôi. Ngược lại: huỷ `CancellationTokenSource` cũ, tạo mới, `await Task.Delay(400, token)` rồi `await ReadDataAsync()` (bọc try/catch OperationCanceledException). Nút "Đọc dữ liệu" vẫn đọc ngay (không debounce).
  - Không tự đọc lại theo từng phím — chỉ 1 lần sau khi ngừng gõ 400ms.
  - Đổi `BrowseSource` reset `HasReadInfo=false`.
- **Step1SourceView.xaml (card Nguồn dữ liệu):**
  - Hàng "Sheet:" → `[ComboBox sheets]  "Đọc từ:" [TextBox ReadFromText width~64]` (bỏ nút khỏi hàng này).
  - Thêm 1 RowDefinition mới ngay dưới: `[Button "Đọc dữ liệu"]  [ProgressBar bận]  [TextBlock nhãn đã đọc, Visibility=HasReadInfo]`.
  - Nhãn "đã đọc" màu Ok, `Visibility={Binding HasReadInfo, Converter={StaticResource BoolToVis}}`.
  - Giữ `StatusText` (hàng dưới) cho lỗi/thông báo khác.

## Related Code Files
- Modify: `src/MucLucHoSo.App/ViewModels/Step1SourceViewModel.cs`
- Modify: `src/MucLucHoSo.App/Views/Step1SourceView.xaml`

## Implementation Steps
1. VM: thêm `ReadFromText`, `HasReadInfo`, `ReadInfoText`, field `CancellationTokenSource? _reloadCts`.
2. VM `ReadDataAsync`: validate + set `S.ReadStartRow`, truyền `start` vào `ReadHead`; set nhãn sau khi OK.
3. VM: `OnReadFromTextChanged`/`OnRowLimitTextChanged` → `HasReadInfo=false`; nếu đã có file+sheet → debounce (huỷ CTS cũ, `Task.Delay(400,token)` → `ReadDataAsync`), nuốt `OperationCanceledException`.
4. XAML: chèn RowDefinition; đưa ô "Đọc từ" vào hàng Sheet; chuyển Button + ProgressBar xuống hàng mới; thêm nhãn inline.
5. Kiểm tra layout không đè "Số dòng đọc" (ở hàng Định dạng) — hai ô khác hàng.

## Success Criteria
- [ ] Ô "Đọc từ" mặc định 1; nút "Đọc dữ liệu" nằm hàng dưới; nhãn "đã đọc" ẩn khi chưa đọc.
- [ ] Đọc với "Đọc từ"=N → bảng lấy header đúng dòng-không-trống thứ N; nhãn hiện "✓ Đã đọc … dòng · … cột".
- [ ] Đổi "Đọc từ"/"Số dòng đọc" (đã có file+sheet) → sau ~400ms **tự đọc lại**, nhãn cập nhật; không giật khi gõ nhanh (debounce huỷ lần trước).
- [ ] Giá trị "Đọc từ" không hợp lệ → cảnh báo inline, không đọc.
- [ ] Build Release trên Windows OK.

## Risk Assessment
- **Debounce/huỷ tác vụ**: dùng `CancellationTokenSource` reset mỗi lần đổi; đảm bảo chạy `ReadDataAsync` trên UI context (cập nhật `PreviewRows`/nhãn). Nuốt `OperationCanceledException`.
- **Auto-read khi chưa đủ điều kiện**: chỉ đọc khi có `SourcePath`+`SheetName`; nếu không, chỉ ẩn nhãn.
- **Layout Grid** thêm hàng dễ lệch các Grid.Row phía sau → cập nhật chỉ số Row cẩn thận; kiểm tra trên Windows.
- Phụ thuộc Phase 1 (`ReadStartRow`, `ReadHead(headerRow)`).
- macOS không verify → test Windows ([[dotnet-rebuild-toolchain-macos]]).
<!-- Updated: Validation Session 1 - auto-reread có debounce thay cho invalidation thủ công -->
