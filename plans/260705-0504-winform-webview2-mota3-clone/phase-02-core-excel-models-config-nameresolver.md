---
phase: 2
title: "Core Excel Models Config NameResolver"
status: done
priority: P1
dependencies: [1]
---

# Phase 2: Core Excel Models Config NameResolver

## Overview
Lớp `MucLucTaiLieu.Core` thuần .NET (test đa nền): models (`HoSo`/`VanBan`/`TemplateDef`/`AppConfig`),
đọc Excel (ClosedXML), `NameResolver` (mẫu tên PDF + token + sanitize), `ConfigStore` (JSON %AppData%),
seed loader. **TDD** — viết test trước cho toàn bộ logic ở đây.

## Requirements
- Functional: đọc `.xlsx` (sheet, header dòng 1, mọi dòng); resolve tên file với `{stt_file}`/`{ngay_gio}`/biến
  mẫu + sanitize ký tự cấm; load/save config; auto-match cột↔biến (bỏ dấu tiếng Việt, xử lý đ/Đ).
- Non-functional: `net8.0`, không ref WinForms/WebView2; picklable-free; test chạy trên macOS CI.

## Architecture
`MucLucTaiLieu.Core/`:
- `Models/`: `HoSo`(SoHoSo,DonVi,ChiNhanh,TieuDe,NguoiLap,Rows), `VanBan`(Stt,SoKyHieu,NgayThang,TacGia,
  TrichYeu,ToSo,GhiChu,LoaiVb,SlTrang), `TemplateDef`(Id,Name,Vars[],SeedFile), `AppConfig`(TemplateId,ColMap,
  GroupCol,PdfPattern,RunOptions…) — khớp mota3 §4.
- `Excel/IExcelReader` → `ClosedXmlReader`: `ListSheets(path)`; `Read(path, sheet)` → header[] + `List<Dictionary<string,string>>`;
  ô trống → ""; ép chuỗi văn hoá cố định (số/ngày ổn định). Cận kích thước file + per-cell fail-fast (lỗi tiếng Việt).
- `Text/HeaderMatch`: `Normalize` (thay `đ→d`,`Đ→D` **trước** NFD + strip dấu + lower + bỏ ký tự không chữ-số);
  `AutoMatch(vars, headers)` → gợi ý cột (mota3 §6.3: auto-match, không cảnh báo trùng).
- `Templating/NameResolver`: `Build(pattern, hoSo, index)` → expand `{so_ho_so}`… + `{stt_file}` (3 chữ số, 1-based)
  + `{ngay_gio}` (`YYYYMMDD_HHmm`); thay ký tự cấm `\ / : * ? " < > |` → `_`; tự thêm `.pdf`; **chặn path-rooted/`..`
  + reserved names** (CON/PRN/…); `MakeUnique(dir, name, used)`.
- `Config/ConfigStore`: load/save `AppConfig` JSON tại `%AppData%\MLTL\config.json`; **lọc bỏ mapping cũ không
  hợp lệ** khi nạp (mota3 §11); khoá namespace theo đường dẫn app.
- `Seed/SeedStore`: đọc 4 JSON seed (mau01–04) từ Assets → `HoSo[]` xem trước.

## Tests (viết trước — TDD)
- `ClosedXmlReaderTests`: tạo `.xlsx` tạm → đọc đúng header/dòng; ô trống→""; số không ra "12.0"; sheet/file lỗi → exception tiếng Việt; file quá lớn/ô quá dài → fail-fast.
- `HeaderMatchTests`: `Normalize("Đống Đa")`/`"đơn vị"`/`"Hồ sơ số"` khớp kỳ vọng (đ/Đ đúng); `AutoMatch` gợi ý đúng cột; **không** cảnh báo/loại trùng.
- `NameResolverTests`: `{so_ho_so}` khác nhau → tên khác; `{stt_file}`=`001`,`002`; `{ngay_gio}` định dạng; ký tự cấm → `_`; reserved `CON`→ đổi; rooted/`..` bị chặn; `MakeUnique` → `_2`,`_3`.
- `ConfigStoreTests`: save→load round-trip; mapping cũ tham chiếu cột không tồn tại bị lọc bỏ.
- `SeedStoreTests`: 4 seed load đúng số hồ sơ/records.

## Implementation Steps
1. Viết models + test skeleton (RED).
2. `NameResolver` + tests (token/sanitize/reserved/unique).
3. `ClosedXmlReader` + tests (tạo xlsx bằng ClosedXML trong test).
4. `HeaderMatch` (đ/Đ) + tests.
5. `ConfigStore` + `SeedStore` + tests. `dotnet test` xanh.

## Success Criteria
- [ ] Đọc `.xlsx` đúng (header + dòng; ô trống ""; số đúng định dạng); lỗi báo tiếng Việt.
- [ ] `NameResolver` expand `{stt_file}`/`{ngay_gio}`/biến + sanitize + reserved + unique; chặn path traversal.
- [ ] `HeaderMatch` xử lý đúng đ/Đ; auto-match hợp lý; không cảnh báo trùng.
- [ ] `ConfigStore` round-trip + lọc mapping cũ; `SeedStore` load 4 mẫu.
- [ ] Tất cả test Core xanh trên macOS (`net8.0`).

## Risk Assessment
- ClosedXML đọc file cực lớn (20k+ dòng) chậm/nặng RAM → đọc theo `RowsUsed()` streaming; đo, đặt cận.
- `{ngay_gio}` trong batch: tính **một lần** đầu mẻ, truyền vào để mọi file cùng mốc (tránh lệch giây).
- Reserved-name/path-traversal: dùng `Path.GetInvalidFileNameChars` + kiểm `Path.GetFullPath().StartsWith(outDir)`.
