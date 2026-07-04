---
phase: 5
title: "UI Shell & Step Input"
status: pending
priority: P1
dependencies: [1]
---

# Phase 5: UI Shell & Step Input

## Overview
Khung UI WinForms khớp `design/`: `MainForm` = brand bar + **stepper 3 bước (chevron)** + vùng bước
(Panel swap) + action bar (Quay lại / Tiếp theo + status text). Bước 1 (Đầu vào): chọn `.xlsx` + sheet +
Đọc dữ liệu (async, progress) + chọn Mẫu + bảng ghép biến↔cột (auto-match) + banner trạng thái + cột gom nhóm.

## Requirements
- Functional: điều hướng 3 bước; luồng bước 1 đầy đủ; đọc Excel qua P2 (async, không treo, progress).
- Non-functional: bám thiết kế **design2 navy `#0043a5`**; Segoe UI 12px; state dùng chung giữa các bước.

## Architecture
- `Theme.cs`: hằng số màu (accent `#0078d7`, bg `#f0f0f0`, hover `#e5f1fb`, border...) + helper style nút/panel.
  WinForms native ≈ Win32 → dùng `GroupBox` (≈ fieldset), `Button`/`TextBox`/`ComboBox` chuẩn.
- `Controls/StepHeader.cs`: custom-paint (OnPaint) 3 chip số + kicker "BƯỚC n" + tiêu đề, chevron `›`,
  chip hiện tại nền accent nhạt (khớp design/). Brand bar = Panel + badge "T" + tên app.
- `MainForm.cs`: `AppState { StyleConfig Style; DataTable Df; string ExcelPath, Sheet, OutDir;
  int DataVersion; }`; `Panel` host + swap `StepInput/StepPreview/StepRun` (UserControl); action bar.
  `GoTo(i)` gọi `OnEnter()` mỗi bước; `BumpDataVersion()` khi đầu vào đổi (recompute ở bước sau).
- `Controls/StepInput.cs` (UserControl):
  - "Nguồn dữ liệu Excel": TextBox path + Duyệt (OpenFileDialog .xlsx) → `ExcelReader.ListSheets` đổ
    ComboBox sheet → "Đọc dữ liệu" → `await ExcelReader.ReadAsync(..., Progress, cts)` + ProgressBar +
    status "✓ OK đọc N dòng · M cột".
  - "Mẫu (template)": ComboBox 4 mẫu (`StyleStore.LoadAll`) + badge số biến.
  - "Ghép biến ↔ cột": `DataGridView` cột (# | Cột Excel [ComboBox cell] | → | {token} | Tự khớp);
    auto-match bằng thuật toán bỏ dấu tiếng Việt (port `text_match.normalize_header`/`auto_match`);
    ô "⚠ chưa gán" (cam) / "✓ khớp" (xanh). Ghi vào `style.rowMapping`/`documentFields`.
  - **Banner trạng thái ghép biến** (chưa đọc = vàng / đủ = xanh / thiếu = đỏ) — **có** (design2). Panel+Label
    màu theo state, đặt dưới hàng nguồn Excel.
  - "Cột gom nhóm hồ sơ": ComboBox → `style.groupingColumn`; hint.
  - `ValidateNext()`: chặn nếu chưa đọc dữ liệu / chưa chọn cột gom nhóm / cột gom nhóm không có trong df.

## Related Code Files
- Create: `MucLucHoSo/MainForm.cs`, `Theme.cs`, `Controls/{StepHeader,StepInput}.cs`,
  `MucLucHoSo.Core/Text/HeaderMatch.cs` (normalize + auto_match)
- Reference: `design2/design2.html` (bố cục navy — canonical), Python `text_match.py`

## Implementation Steps
1. `Theme` + `StepHeader` (custom paint) + `MainForm` khung 3 bước + action bar + state.
2. `StepInput`: khối Excel (browse/sheet/đọc async + progress) qua P2.
3. `HeaderMatch` (normalize bỏ dấu + auto_match) — có thể test đơn vị ở Core.
4. DataGridView mapping (ComboBox cell) + auto-match + ✓/⚠; đồng bộ StyleConfig.
5. ComboBox gom nhóm + gate ValidateNext.

## Success Criteria
- [ ] App mở 3 bước khớp `design/` (brand bar, stepper chevron, fieldset, action bar).
- [ ] Bước 1: chọn `.xlsx` → sheet → Đọc (async, progress, không treo) → status N dòng·M cột; lỗi header báo rõ.
- [ ] Bảng mapping auto-khớp; sửa tay được (ComboBox cell); ✓/⚠ đúng; lưu vào StyleConfig.
- [ ] Chọn Mẫu đổi được; cột gom nhóm set đúng; gate chặn khi thiếu.

## Red Team Fixes + Design2 (áp dụng 2026-07-04)
- **Thiết kế = `design2/` navy** (đổi từ design/): accent `#0043a5`, bg `#f0f2f5`, hover `#e6eefb`;
  nút "Đọc dữ liệu" **teal `#00ccd6`**; nút khác trắng bo góc 5px; **banner trạng thái ghép biến** (chưa đọc =
  vàng, đủ = xanh, thiếu = đỏ); ô "Cột gom nhóm" **viền trái accent + icon 🗂**; mapping có cột `→` + token
  xanh mono + "✓ khớp"/"⚠ chưa gán". *(Trước đây phase này ghi "không thêm banner navy" — **đảo lại**.)*
- **#18 — cancel discard:** `await ExcelReader.ReadAsync` bắt `OperationCanceledException` → **giữ nguyên
  `AppState.Df`** (không commit bảng cụt); mỗi lần đổi file huỷ CTS cũ, chống hoàn tất lệch thứ tự.
- **#19 — đ/Đ:** `HeaderMatch.Normalize` thay `đ→d`,`Đ→D` **trước** `Normalize(FormD)`+strip `NonSpacingMark`;
  **test Core** với header chứa đ ("Đống Đa", "Hồ sơ số", "đơn vị") assert khớp Python `text_match.py`.
- **Gate mapping (đồng bộ design2):** chặn "Tiếp" nếu còn biến "⚠ chưa gán" (như bản PyQt design2).

## Risk Assessment
- Đọc file lớn: dùng `async/await` + `IProgress` + `CancellationTokenSource` (hủy khi đổi file) — không block UI thread.
- DataGridView ComboBox cell nhiều dòng: chỉ có ~10 biến/mẫu → nhẹ.
- Custom-paint stepper/banner/nút bo góc: WinForms cần owner-draw cho stepper chevron + nút bo góc + banner
  màu (native ~75-80%, custom-paint ~90-95% khớp design2). Giữ đơn giản nếu OnPaint rườm rà.
