---
phase: 1
title: "Core hỗ trợ dòng bắt đầu đọc (readers+factory+CoreService+SessionState)"
status: code-done
effort: ""
---

# Phase 1: Core hỗ trợ dòng bắt đầu đọc

## Overview
Thêm khả năng chọn dòng header (bỏ N−1 dòng phía trên) ở tầng đọc dữ liệu, áp dụng nhất quán cho xem nhanh, kiểm tra và sinh file.

## Requirements
- Functional:
  - `ExcelRowReader`/`CsvRowReader` nhận `int headerRow = 1`. **Ngữ nghĩa (chốt ở Validation §2): bỏ qua các dòng TRỐNG rồi mới đếm — header = dòng KHÔNG-trống thứ `headerRow`** (bỏ dòng trống phía trên và xen giữa). Dữ liệu = các dòng không-trống tiếp theo.
  - `headerRow=1` = dòng không-trống đầu tiên là header. Với file không có dòng trống ở đầu, kết quả **y hệt hành vi hiện tại** (không hồi quy).
  - Không đủ dòng không-trống để đạt `headerRow` → báo lỗi rõ ("Không đủ dữ liệu: cần ≥ N dòng có nội dung").
  - `ReaderFactory.Open`, `CoreService.ReadHead/Validate/ReaderFactoryFor` truyền `headerRow` từ `SessionState.ReadStartRow`.
- Non-functional: giữ streaming/RAM thấp; không đổi interface `IRowReader` (chỉ ctor readers + factory signature).

## Architecture
- **SessionState:** `[ObservableProperty] private int _readStartRow = 1;`
- **"Dòng trống"** = mọi ô (đã Trim) đều rỗng — cùng định nghĩa với chỗ bỏ dòng trắng trong `ReadRows` hiện tại (`ExcelRowReader.cs:58`).
- **ExcelRowReader(path, sheet, int headerRow=1):** sau `MoveToSheet`, lặp đọc dòng, **chỉ tính dòng không-trống**; bỏ qua `headerRow−1` dòng không-trống (bỏ luôn dòng trống gặp phải), rồi lấy dòng không-trống kế tiếp làm header. Guard: nếu `_reader.Read()` hết trước khi đủ → ném lỗi thiếu dữ liệu. Refactor `ReadHeaderRow` → dùng helper "đọc tới dòng không-trống kế tiếp" (trả false nếu hết).
- **CsvRowReader(path, delimiter=null, int headerRow=1):** tương tự — dùng `_csv.Read()` tiến từng bản ghi, kiểm tra bản ghi có ô không-rỗng nào không (`_csv.Parser.Record`/`GetField`); bỏ qua `headerRow−1` bản ghi không-trống, rồi `ReadHeader()` tại bản ghi không-trống thứ `headerRow`.
- **ReaderFactory.Open(path, sheet=null, csvDelimiter=null, int headerRow=1):** truyền `headerRow` xuống 2 reader.
- **CoreService:**
  - `ReadHead(path, sheet, delimiter, int maxRows=100, int headerRow=1)` → `ReaderFactory.Open(path, sheet, delimiter, headerRow)`.
  - `Validate(s)` → `ReaderFactory.Open(s.SourcePath!, s.SheetName, s.CsvDelimiter, s.ReadStartRow)`.
  - `ReaderFactoryFor(s)` → lambda `ReaderFactory.Open(..., s.ReadStartRow)`.
<!-- Updated: Validation Session 1 - N đếm theo dòng không-trống, không raw-skip dòng vật lý -->

## DRY note
- Tách 1 helper kiểm tra "dòng hiện tại có nội dung không" dùng chung cho cả skip-đếm và `ReadRows` (tránh 2 định nghĩa "dòng trống" lệch nhau).

## Related Code Files
- Modify: `src/MucLucHoSo.Core/Reading/ExcelRowReader.cs` (ctor headerRow + skip + guard)
- Modify: `src/MucLucHoSo.Core/Reading/CsvRowReader.cs` (ctor headerRow + skip + guard)
- Modify: `src/MucLucHoSo.Core/Reading/ReaderFactory.cs` (signature + truyền tham số)
- Modify: `src/MucLucHoSo.App/Shared/SessionState.cs` (ReadStartRow)
- Modify: `src/MucLucHoSo.App/Services/CoreService.cs` (ReadHead/Validate/ReaderFactoryFor)

## Implementation Steps
1. `SessionState`: thêm `ReadStartRow=1`.
2. `ExcelRowReader`: ctor thêm `headerRow`; helper `bool CurrentRowHasValue()` + `bool MoveToNextNonEmptyRow()` (Read tới dòng có nội dung, false nếu hết). Bỏ qua `headerRow−1` dòng không-trống, rồi header = dòng không-trống kế tiếp (throw nếu thiếu).
3. `CsvRowReader`: ctor thêm `headerRow`; tương tự — tiến `_csv.Read()`, bỏ qua bản ghi trống, đếm `headerRow−1` bản ghi không-trống rồi `ReadHeader()`.
4. `ReaderFactory.Open`: thêm `int headerRow=1`, truyền xuống cả 2 reader.
5. `CoreService`: cập nhật 3 chỗ gọi + chữ ký `ReadHead`.
6. Rà: `headerRow=1` + file không có dòng trống đầu → kết quả y hệt hiện tại.

## Success Criteria
- [ ] `ReadStartRow=1`, file không có dòng trống đầu → header/dữ liệu y như trước (không hồi quy).
- [ ] File có 2 dòng trống ở đầu + `ReadStartRow=1` → header vẫn là dòng có nội dung đầu tiên (bỏ dòng trống).
- [ ] `ReadStartRow=N>1` → header = dòng KHÔNG-trống thứ N; áp cho ReadHead + Validate + generate (cùng giá trị).
- [ ] Không đủ dòng không-trống → lỗi rõ ràng, không crash.
- [ ] Build Release trên Windows OK.

## Risk Assessment
- **Đếm dòng không-trống** khác raw-skip: định nghĩa "dòng trống" phải khớp `ReadRows` (dùng chung helper) để header/data nhất quán.
- **Excel/CSV hết dòng khi skip** → guard throw với thông báo rõ.
- **Lệch giữa 3 điểm đọc** → tất cả dùng chung `s.ReadStartRow` (không hardcode).
- **CsvHelper**: dùng `_csv.Read()` (không `ReadHeader`) để tiến từng bản ghi; kiểm tra bản ghi trống qua `_csv.Parser.Record`.
- macOS không verify → test Windows ([[dotnet-rebuild-toolchain-macos]]).
