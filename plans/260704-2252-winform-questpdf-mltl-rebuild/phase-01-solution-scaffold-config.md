---
phase: 1
title: "Solution Scaffold & Config"
status: pending
priority: P1
dependencies: []
---

# Phase 1: Solution Scaffold & Config

## Overview
Dựng solution .NET 8 (`winform/MucLucHoSo.sln`) với project WinForms + project test xUnit, thêm
NuGet (QuestPDF, ExcelDataReader, ClosedXML), và model cấu hình `StyleConfig` + 4 `style.json` metadata
(port từ style.json Python hiện có). Chưa có UI/engine — chỉ nền tảng biên dịch + test chạy được.

## Requirements
- Functional: solution build sạch `dotnet build`; `dotnet test` chạy (rỗng cũng được); `StyleConfig`
  load/round-trip từ `style.json`.
- Non-functional: đa nền cho core (chạy `dotnet` trên macOS dev); WinForms project target `net8.0-windows`.

## Architecture
```
winform/
  MucLucHoSo.sln
  MucLucHoSo/                     # net8.0-windows, WinForms, self-contained sau (P7)
    MucLucHoSo.csproj            # <UseWindowsForms>true; QuestPDF, ExcelDataReader, ClosedXML
    Core/Models/StyleConfig.cs   # name, groupingColumn, outputFilenamePattern, settings,
                                 #   documentFields{var->col}, rowMapping{var->col}, columns[]
    Core/Config/StyleStore.cs    # LoadAll(stylesRoot), Load(dir), Save(dir) — System.Text.Json
    Core/Variables.cs            # AUTO_VARS {stt_file,ngay_gio}, FOOTER_VARS {trang_so,tong_so_trang},
                                 #   SETTINGS_BODY_KEYS; BodyTokens(style)
    styles/<slug>/style.json     # 4 mẫu (metadata; KHÔNG template_html)
  MucLucHoSo.Core.Tests/         # xUnit — test core không cần WinForms
    MucLucHoSo.Core.Tests.csproj # net8.0 (đa nền), ref Core qua InternalsVisibleTo hoặc public API
```
- Tách **Core** (thuần .NET, `net8.0`) khỏi **UI** (`net8.0-windows`) để test đa nền: đặt logic
  Core trong cùng project nhưng test qua public API; hoặc tách `MucLucHoSo.Core` class-lib `net8.0`
  và UI ref nó. **Chọn:** tách `MucLucHoSo.Core` (net8.0) + `MucLucHoSo` (net8.0-windows, WinForms) +
  `MucLucHoSo.Core.Tests` (net8.0). Core không phụ thuộc WinForms.
- `style.json` port trực tiếp từ `styles/*/style.json` Python (bỏ `template_html`, `template_file`).

## Related Code Files
- Create: `winform/MucLucHoSo.sln`, `winform/MucLucHoSo.Core/*.cs`, `winform/MucLucHoSo/*.csproj`,
  `winform/MucLucHoSo.Core.Tests/*`, `winform/MucLucHoSo.Core/styles/<slug>/style.json` (×4)
- Reference (đọc, không sửa): `styles/*/style.json` (Python) để lấy mapping/columns/settings 4 mẫu

## Tests (viết trước — TDD)
- `StyleConfigTests`: load `style.json` mẫu → đúng name/groupingColumn/rowMapping/columns; round-trip
  save→load giữ nguyên; thiếu khoá → default an toàn.
- `VariablesTests`: `BodyTokens` gồm doc fields + settings keys + row vars + auto; **loại** footer vars.

## Implementation Steps
1. `dotnet new sln`; `dotnet new classlib -f net8.0` (Core); `dotnet new winforms -f net8.0-windows` (UI);
   `dotnet new xunit -f net8.0` (Tests). Add refs.
2. Add NuGet: QuestPDF, ExcelDataReader, ExcelDataReader.DataSet, ClosedXML (vào Core hoặc UI theo dùng).
3. Viết `StyleConfig` + `StyleStore` (System.Text.Json, `JsonSerializerOptions{PropertyNameCaseInsensitive}`).
4. Viết `Variables` (hằng số token). Copy 4 `style.json` metadata từ Python (bỏ template_html/template_file).
5. Viết tests; `dotnet test` xanh.

## Success Criteria
- [ ] `dotnet build winform/MucLucHoSo.sln` sạch (0 warning nghiêm trọng).
- [ ] `dotnet test` xanh (StyleConfig + Variables).
- [ ] 4 `style.json` load được, mapping/columns đúng 4 mẫu.
- [ ] Core project **không** ref WinForms (đa nền).

## Red Team Fixes (áp dụng 2026-07-04)
- **#7 — prose-in-settings:** khi bỏ `template_html`, **chuyển text riêng từng mẫu vào `settings`** trước
  khi xoá: nhãn dòng hồ sơ (vd Đông Hà `"Số, ký hiệu hồ sơ (đơn vị bảo quản): {ho_so_so}"` vs 3 mẫu kia
  `"Số, ký hiệu hồ sơ: {ho_so_so}"`), tiêu đề/cơ quan. Thêm khoá settings: `ho_so_line_format`,
  `co_quan_dong1/2`, `tieu_de` (đã có). StyleConfig gồm cả `settings.anh_chu_ky_path` (ảnh chữ ký).
- **#10 — build đa nền:** thêm `<EnableWindowsTargeting>true</EnableWindowsTargeting>` vào csproj UI để
  `dotnet build` chạy trên macOS dev (build được, KHÔNG chạy UI). Core + Tests target `net8.0` thuần.
- **#14 — QuestPDF license:** trước khi khởi động P3, **ghi rõ xác định pháp lý** (ai là licensee, mức doanh
  thu, nguồn) vào `docs/winform-app.md`; nếu không chắc → chọn MigraDoc từ đầu. **Pin phiên bản QuestPDF**
  cố định trong NuGet (không floating) để test/parity ổn định.
- **#6 — DocGroup contract:** kiểu dữ liệu nhóm dùng chung định nghĩa **1 lần ở P2** (`DocGroup`), P1 chỉ
  cần biết StyleConfig; đừng định nghĩa trùng.

## Risk Assessment
- QuestPDF cần đăng ký license type khi khởi động (`QuestPDF.Settings.License = LicenseType.Community`)
  — set 1 lần ở `Program.cs` **trước mọi render/dispatch song song** (Red Team #1). Ghi chú.
- ExcelDataReader cần `System.Text.Encoding.CodePages` register cho `.xls` cũ (không cần cho `.xlsx`).
