---
phase: 7
title: "Packaging CI & Parity"
status: pending
priority: P2
dependencies: [4, 6]
---

# Phase 7: Packaging CI & Parity

## Overview
Đóng gói **1 file `.exe` self-contained** (win-x64), dựng GitHub Actions build+test trên `windows-latest`,
và **đo parity** với bản Python (đúng dữ liệu + nhanh hơn). Cập nhật README/docs cho app WinForms.

## Requirements
- Functional: `dotnet publish` ra 1 exe chạy sạch trên Win10/11 không cần cài runtime; CI xanh.
- Non-functional: exe khởi động nhanh; kèm `styles/` cạnh exe (đọc/ghi được).

## Architecture
- `MucLucHoSo.csproj` publish props: `PublishSingleFile=true`, `SelfContained=true`,
  `RuntimeIdentifier=win-x64`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`.
  QuestPDF cần font — Times New Roman có trên Windows (không bundle). `styles/` copy cạnh exe (không nhồi single-file
  để đọc/ghi được — `<None ... CopyToOutputDirectory>` hoặc bước copy ở workflow).
- `.github/workflows/build-winform.yml`: `windows-latest` → `dotnet test` (Core.Tests) → `dotnet publish
  -c Release -r win-x64 --self-contained` → upload artifact `MucLucHoSo-winform`.
- **Parity check (thủ công/script):** cùng 1 `.xlsx` thật → chạy WinForms exe và app Python → so **số PDF**,
  **số trang mỗi hồ sơ**, **nội dung bảng**; đo **thời gian đọc + tổng thời gian** (kỳ vọng WinForms nhanh hơn).
- Docs: `docs/winform-app.md` (chạy, build, license QuestPDF, khác biệt Preview-only) + cập nhật README mục WinForms.

## Related Code Files
- Create: `.github/workflows/build-winform.yml`, `docs/winform-app.md`
- Modify: `winform/MucLucHoSo/MucLucHoSo.csproj` (publish props), `README.md` (thêm mục app WinForms)

## Implementation Steps
1. Thêm publish props vào csproj; `dotnet publish -r win-x64 --self-contained` (chạy trên Windows/CI).
2. Viết workflow build+test+artifact.
3. Chạy exe trên Windows: đi 3 bước với Excel thật → xuất PDF; kiểm `styles/` cạnh exe hoạt động.
4. Parity: so kết quả + đo tốc độ vs bản Python; ghi số liệu vào `docs/winform-app.md`.
5. Cập nhật README.

## Success Criteria
- [ ] `dotnet publish` ra **1 exe self-contained** chạy sạch Win10/11 (không cài .NET runtime).
- [ ] CI `windows-latest` xanh: test core + publish artifact.
- [ ] Parity: cùng Excel → **cùng số PDF + cùng phân trang + nội dung khớp** bản Python; **đọc + xuất nhanh hơn** (số liệu ghi lại).
- [ ] `styles/` đọc/ghi được cạnh exe; 4 mẫu chọn được.
- [ ] README + `docs/winform-app.md` cập nhật (gồm ghi chú license QuestPDF + Preview-only).

## Red Team Fixes (áp dụng 2026-07-04)
- **#8 — parity = TƯƠNG ĐƯƠNG DỮ LIỆU, không phải số trang:** so **đúng N file + records/giá trị + thứ tự
  cột + có khối chữ ký**. **KHÔNG** yêu cầu trùng số trang mỗi hồ sơ (QuestPDF vs Qt là 2 engine layout khác
  → phân trang khác là bình thường). Ghi rõ pagination là engine-dependent.
- **#10 — UI có người kiểm + CI test UI:** CI thêm bước **build UI** (`dotnet build` project WinForms, có
  `EnableWindowsTargeting`), và **checklist test tay** (đi 3 bước, mapping auto-match, preview, generate) do
  **owner Windows** thực hiện — nêu tên máy/owner. Logic test được (HeaderMatch, engine, batch) đã ở Core.Tests.
- **#14 — license:** ghi **xác định pháp lý QuestPDF Community** (licensee, doanh thu, nguồn) vào
  `docs/winform-app.md`; nếu không đủ điều kiện → chuyển MigraDoc (quyết định **trước** khi khoá engine).
- **#21 — ký + extract path:** kế hoạch **Authenticode code-sign** `.exe` (thêm vào CI + success criteria) để
  tránh SmartScreen/AV chặn ở cơ quan; pin `DOTNET_BUNDLE_EXTRACT_BASE_DIR` per-user (không world-writable);
  ghi chú ảnh hưởng SmartScreen cho bên triển khai.
- **#3 — asset chữ ký:** copy `chuky_tung.png` (ảnh chữ ký mẫu Đông Hà) cạnh exe / vào `styles/`.

## Risk Assessment
- PublishSingleFile + native libs (SkiaSharp của QuestPDF) → `IncludeNativeLibrariesForSelfExtract=true`;
  test khởi động sạch (không thiếu native dll). **Exe chưa ký → SmartScreen/AV chặn** (Red Team #21) → ký Authenticode.
- Kích thước exe self-contained lớn (~60–150MB) — chấp nhận (đổi lấy "chạy không cần cài gì").
- Font Times New Roman thiếu trên máy đích hiếm gặp (Windows có sẵn) — fallback font trong template nếu cần.
- Dev trên macOS **không** publish/chạy UI được → **bắt buộc** publish + test UI + parity trên Windows (CI hoặc máy thật).
