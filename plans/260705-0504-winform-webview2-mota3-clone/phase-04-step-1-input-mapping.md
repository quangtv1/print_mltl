---
phase: 4
title: "Step 1 Input & Mapping"
status: pending
priority: P1
dependencies: [2, 3]
---

# Phase 4: Step 1 Input & Mapping

## Overview
Khung wizard (`MainForm`: step header + nav bar + swap UserControl) và **Bước 1 — Đầu vào** (mota3 §2,§6):
nguồn Excel + sheet + Đọc dữ liệu (ClosedXML/P2), chọn Mẫu, banner trạng thái, bảng ghép biến↔cột (auto-match),
cột gom nhóm (hiện số file sẽ tạo), gate hợp lệ để sang Bước 2.

## Requirements
- Functional: điều hướng 3 bước; luồng Bước 1 đầy đủ; `step1Valid = dataRead && unmapped==0` (mota3 §6.5).
- Non-functional: đọc Excel async (không treo); màu/typography đúng mota3 §3 (accent `#0043a5`, nút Đọc cyan).

## Architecture
- `Forms/MainForm.cs`: step header (custom-paint chevron + badge ✓/số theo trạng thái, mota3 §2.2), vùng bước
  (Panel swap `Step1/2/3Control`), nav bar (◀ Quay lại / Tiếp theo ▶ / "Tạo Mục lục"; primary accent; disable
  theo `step1Valid`; statusMap). `AppState` chung (config, df, template, groupCol, dataVersion).
- `Forms/Step1InputControl.cs` (UserControl):
  - Fieldset "Nguồn dữ liệu Excel": Tệp + Duyệt (OpenFileDialog *.xlsx) → nạp sheet combobox; nút **Đọc dữ liệu**
    (cyan) → `ClosedXmlReader.Read` (async) → status "✓ Đã đọc N dòng · M cột".
  - Fieldset "Mẫu": combobox 4 mẫu (`TemplateCatalog`) + số biến.
  - **Banner** trạng thái ghép biến (chưa đọc vàng / đủ xanh / thiếu đỏ) — mota3 §6.2.
  - **DataGridView** 5 cột: # | Cột Excel (combobox cell) | → | Biến (chip) | Tự khớp (✓/⚠); auto-match `HeaderMatch`;
    **không** cảnh báo trùng cột; thanh cuộn dọc luôn hiện.
  - Fieldset "Cột gom nhóm" (viền trái accent): combobox cột → hiện **số file PDF** ("→ sẽ tạo N file…" theo distinct;
    "(không gom nhóm)" → 1 file).
- Popup mapping (`MappingPopup`) dùng lại ở Bước 2 (mota3 §7.3) — dựng ở đây, tái dùng P5.

## Related Code Files
- Create: `Forms/MainForm.cs`, `Forms/Step1InputControl.cs`, `Forms/MappingPopup.cs`, `Theme.cs`
- Reuse: P2 `ClosedXmlReader`/`HeaderMatch`/`ConfigStore`, P3 `TemplateCatalog`
- Reference: `design_v3/mota3.html` §2,§3,§6

## Implementation Steps
1. `Theme` (màu/typography mota3 §3) + `MainForm` (step header + swap + nav + state).
2. `Step1InputControl`: khối Excel (browse/sheet/đọc async) + status.
3. DataGridView mapping (combobox cell) + auto-match + ✓/⚠ + banner; đồng bộ `AppState`/config.
4. Cột gom nhóm + đếm distinct → số file; gate `step1Valid` khoá nút Tiếp theo.
5. Nạp config lần trước (mota3 §11): template/mapping/groupCol/pattern/options.

## Success Criteria
- [ ] App mở wizard 3 bước; step header đúng trạng thái ✓/đang/chưa; nút Tiếp theo khoá tới khi hợp lệ.
- [ ] Bước 1: chọn Excel+sheet → Đọc (async) → status N dòng·M cột; đổi mẫu cập nhật biến.
- [ ] Bảng mapping auto-khớp, sửa tay, **không** cảnh báo trùng; banner đổi màu đúng.
- [ ] Cột gom nhóm hiện số file sẽ tạo; nhớ trạng thái lần trước.

## Risk Assessment
- Custom-paint step header: giữ đơn giản (Panel+Label) nếu OnPaint rườm; ưu tiên đúng trạng thái/màu.
- DataGridView combobox-cell với ~10–12 biến → nhẹ; reserve gutter để canh cột ổn định (mota3 §6.3).
- Đọc Excel lớn: async + trạng thái; hủy khi đổi file (CTS) — giữ `AppState.Df` cũ nếu hủy.
