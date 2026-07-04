---
phase: 2
title: "Excel Reader & Grouping"
status: pending
priority: P1
dependencies: [1]
---

# Phase 2: Excel Reader & Grouping

## Overview
Lớp đọc dữ liệu nhanh: `ExcelReader` (ExcelDataReader streaming) liệt kê sheet + đọc sheet → `DataTable`,
validate header rỗng/`Unnamed`/trùng; đọc **async + progress** cho file lớn (20k+ dòng). `Grouping` gom
record theo cột gom nhóm (giữ thứ tự). Đây là phần "tăng tốc đọc dữ liệu" cốt lõi.

## Requirements
- Functional: `ListSheets(path)`, `ReadAsync(path, sheet, IProgress<int>, CancellationToken) -> DataTable`,
  `ValidateHeaders(rawHeaders)`; `GroupByColumn(rows, col)` giữ thứ tự xuất hiện.
- Non-functional: 20k dòng đọc < ~2s; không giữ toàn workbook trong RAM lâu; hủy được (cancel).

## Architecture
`MucLucHoSo.Core/Excel/`:
- `ExcelReader.cs`:
  - `ListSheets(path)`: mở `ExcelReaderFactory.CreateReader(stream)` → duyệt `Result.Tables` names.
  - `ReadAsync(...)`: đọc **streaming** theo row (`reader.Read()` vòng lặp), row đầu = header (validate
    **trên header thô** trước khi map — bắt trùng/rỗng như bản Python `_raw_headers`), các row sau → DataRow.
    Báo `IProgress<int>` mỗi ~1000 dòng. Cận `MAX_ROWS` (vd 200k) fail-fast lỗi tiếng Việt.
    Ô trống/null → `""`; ép chuỗi an toàn (số → chuỗi theo culture cố định, tránh "nan"/định dạng lệch).
  - `ExcelReadException` (thông báo tiếng Việt).
- `Grouping.cs`: `GroupByColumn(DataTable, string col) -> IReadOnlyList<GroupRecords>` (list dict giữ thứ tự;
  `GroupRecords` = { GroupValue, Rows: List<Dictionary<string,string>> keyed by header }).
- Chạy `ReadAsync` trên thread nền (Task) → UI (P5) await, cập nhật progress bar.

## Related Code Files
- Create: `MucLucHoSo.Core/Excel/ExcelReader.cs`, `Grouping.cs`, `ExcelReadException.cs`
- Reference: bản Python `app/core/excel_reader.py` (logic validate header thô) + `qt_pdf_renderer.group_dataframe`

## Tests (viết trước — TDD)
- `ExcelReaderTests`:
  - đọc file `.xlsx` mẫu (tạo trong test bằng ClosedXML) → đúng số dòng/cột, ô trống → "".
  - header rỗng → `ExcelReadException` (vị trí cột); header trùng → exception (tên cột).
  - file thiếu → exception; sheet không tồn tại → exception.
  - **perf smoke:** sinh 20k dòng → đọc < ngưỡng (assert thời gian nới lỏng, chỉ chống hồi quy thô).
  - progress: `IProgress<int>` được gọi ≥1 lần, giá trị tăng.
- `GroupingTests`: 3 group (có 2 group trùng giá trị ho_so_so ở cột khác) → đúng số nhóm, thứ tự giữ nguyên.

## Implementation Steps
1. Viết test tạo `.xlsx` tạm bằng ClosedXML (header + N rows) → assert đọc đúng (RED).
2. Viết `ExcelReader.ListSheets` + `ReadAsync` streaming + `ValidateHeaders(rawHeaders)` → GREEN.
3. Viết `Grouping.GroupByColumn` + test giữ thứ tự.
4. Perf smoke 20k dòng; nếu chậm → chỉ đọc cột cần / bỏ trim thừa.

## Success Criteria
- [ ] `ReadAsync` đọc đúng dữ liệu; ô trống → ""; validate header rỗng/trùng báo lỗi tiếng Việt rõ.
- [ ] 20k dòng đọc nhanh (< ~2s trên máy CI), progress cập nhật, cancel được.
- [ ] `GroupByColumn` giữ thứ tự xuất hiện; nhóm đúng.
- [ ] Tests xanh.

## Red Team Fixes (áp dụng 2026-07-04)
- **#6 — định nghĩa `DocGroup` (nguồn duy nhất):** `record DocGroup { string GroupValue;
  IReadOnlyDictionary<string,string> DocFields; IReadOnlyList<IReadOnlyDictionary<string,string>> Rows }`.
  `GroupByColumn` dựng luôn `DocFields` (từ `style.documentFields` áp lên hàng đầu nhóm). P3/P4 consume
  **đúng kiểu này** (bỏ tên `GroupRecords`). `stt_file` **KHÔNG** nằm ở đây — do batch gán (1-based, P4).
- **#15 — chống zip-bomb/OOM:** thêm **cận kích thước file thô** (vd 100MB) + **cận độ dài mỗi ô** (vd 32KB,
  vượt → cắt/từ chối) ngoài `MAX_ROWS`; fail-fast lỗi tiếng Việt. Không dùng `AsDataSet`.
- **#18 — hợp đồng cancel:** hủy → **ném `OperationCanceledException`** (`ct.ThrowIfCancellationRequested()`),
  KHÔNG trả bảng cụt; dispose `IExcelDataReader`/stream trong `using`/`finally`. Caller (P5) bắt và **giữ
  nguyên `AppState.Df`**. Chống hoàn tất lệch thứ tự khi read mới thay read cũ.
- **#19 (một phần) — `đ/Đ`:** normalize header phải thay `đ→d`,`Đ→D` **trước** NFD (NFD không tách `đ`) —
  dùng chung `HeaderMatch` (P5) đặt ở Core để test.
- **Memory (#Failure-7):** grouping **index vào DataTable** (giữ tham chiếu hàng) thay vì copy mọi ô sang
  dict, HOẶC hạ `MAX_ROWS` xuống mức đã **đo** footprint thực; ghi ceiling vào perf test.
- **#20/perf gate:** "20k < 2s" đổi thành **ngưỡng tương đối** đo 1 lần trên runner đích; sinh fixture
  **ngoài** vùng bấm giờ (không tính thời gian ClosedXML tạo file).

## Risk Assessment
- ExcelDataReader trả kiểu theo cell (double/date) → ép chuỗi theo `CultureInfo.InvariantCulture` +
  format ngày ổn định; verify cột số (Tờ số) không ra "12.0".
- `.xlsx` lớn: dùng `AsDataSet` sẽ nạp hết RAM → **tránh**, đọc streaming từng row.
