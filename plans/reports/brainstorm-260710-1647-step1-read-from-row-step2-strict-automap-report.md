# Brainstorm — Màn 1 "Đọc từ:" (dòng header) + Màn 2 tự-ghép chặt snake_case

- Date: 2026-07-10
- Scope: WPF/MVVM app `print_mltl`. 2 tính năng độc lập.
- Modes: (none)

## Problem statement
1. **Màn 1:** File Excel thường có dòng tiêu đề/ghi chú phía trên hàng header thật → cần chọn dòng bắt đầu đọc (header). Thêm ô "Đọc từ:" (mặc định 1 = header), đổi giá trị → đọc lại theo vị trí; chuyển nút "Đọc dữ liệu" xuống hàng dưới; nhãn "đã đọc" inline cạnh nút (ẩn mặc định).
2. **Màn 2:** Tự-ghép biến↔cột hiện dùng normalize (bỏ hết ký tự không chữ/số) + fallback mờ `.Contains` → dễ ghép sai. Muốn khớp CHẶT: header → snake_case giữ `_` (VD "Ngày, tháng, năm sinh" → `ngay_thang_nam_sinh`) rồi so khớp chính xác với tên biến; bỏ fallback mờ.

## Quyết định đã chốt
- Nhãn "đã đọc" = **nhãn inline mới** cạnh nút, ẩn mặc định.
- Match Màn 2 = **chặt theo `_`** (Slug giữ underscore, so khớp chính xác 2 phía).
- **Bỏ hoàn toàn** fallback `.Contains`.
- Start row = **N là dòng header** (bỏ N−1 dòng trên), 1-based; **áp cho cả preview + validate + generate** (bắt buộc, nhất quán).

## Thiết kế chốt

### Feature #1 — "Đọc từ:"
- Core: `ExcelRowReader`/`CsvRowReader` thêm `int headerRow=1`, bỏ qua `headerRow−1` dòng trước khi đọc header (+ guard hết dòng). `ReaderFactory.Open(...,headerRow=1)`. `CoreService.ReadHead/Validate/ReaderFactoryFor` dùng `S.ReadStartRow`.
- `SessionState.ReadStartRow=1`.
- Màn 1 VM: `ReadFromText="1"` → parse ≥1 trong `ReadDataAsync`; đổi giá trị → `DataLoaded=false` + ẩn nhãn (giống ô "Số dòng đọc", không tự đọc lại theo phím); `HasReadInfo`/`ReadInfoText` cho nhãn inline.
- Màn 1 XAML: hàng Sheet → `[ComboBox] Đọc từ:[ô]`; hàng mới → `[Đọc dữ liệu] ✓nhãn`.

### Feature #2 — Slug chặt
- `TextUtil.Slug(s)`: thường + bỏ dấu (đ→d) + cụm ký tự không chữ/số → một `_` + trim `_`.
- `Step2MappingViewModel.BuildBindings`: `match = cols.FirstOrDefault(c => Slug(c)==Slug(v))`, bỏ `.Contains`. Giữ `Normalize` cho heuristic cột gom nhóm.

## Files
Core: `ExcelRowReader.cs`, `CsvRowReader.cs`, `ReaderFactory.cs`.
App: `SessionState.cs`, `CoreService.cs`, `Step1SourceViewModel.cs`, `Step1SourceView.xaml`, `Step2MappingViewModel.cs`, `TextUtil.cs`.

## Rủi ro
- Edge case bỏ dòng vượt dữ liệu → guard + báo rõ.
- Nhất quán 3 điểm đọc (preview/validate/generate) — cùng `ReadStartRow`.
- Khớp chặt hơn → template biến không có `_` sẽ ngừng tự-ghép (đã chấp nhận).
- macOS không build WPF → test trên Windows; tầng Core (reader skip, Slug) có thể unit-test được nếu sau này thêm test project.

## Unresolved
- Không có.
