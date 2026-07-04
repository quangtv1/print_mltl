---
phase: 6
title: "Step Preview & Step Run"
status: pending
priority: P1
dependencies: [3, 5]
---

# Phase 6: Step Preview & Step Run

## Overview
Bước 2 (**Preview-only**) và Bước 3 (Chạy). Bước 2: chọn mẫu + ◀▶ duyệt hồ sơ + hiển thị **ảnh trang A4**
(QuestPDF `ToImages` từ P3) cuộn dọc — **không** editor. Bước 3: thư mục + pattern tên PDF + tùy chọn +
progress + **log realtime** + Generate (batch song song P4) + mở thư mục.

## Requirements
- Functional: preview render group hiện tại (khớp PDF xuất ra); batch chạy nền + progress/log; mở thư mục.
- Non-functional: preview không treo (render nền, đẩy ảnh về UI); Generate không treo UI.

## Architecture
- `Controls/StepPreview.cs` (UserControl):
  - Trên: ComboBox chọn mẫu (đồng bộ với bước 1) + ◀ recLabel ▶ (Hồ sơ k/N).
  - Thân: `FlowLayoutPanel`/`Panel` cuộn dọc chứa `PictureBox` mỗi trang = `Renderer.ToImages(template, group)`
    (byte[]→Image). Render nền (Task) → `BeginInvoke` cập nhật ảnh. Cache theo group index.
  - `OnEnter`: recompute groups (`Grouping.GroupByColumn`) theo DataVersion; render group 0.
  - **Không** toolbar sửa mẫu (Preview-only theo quyết định).
- `Controls/StepRun.cs` (UserControl):
  - "Thư mục & tên file xuất": TextBox dir + Duyệt (FolderBrowserDialog) + ComboBox preset pattern +
    TextBox pattern (mono) + ví dụ tên.
  - "Tùy chọn chạy": **Chạy đa luồng** (checked, ENABLE — QuestPDF thread-safe, khác bản Python),
    **Ghi đè**, **Xuất kèm Excel**.
  - "Tiến trình chạy": ProgressBar + Generate → `BatchGenerator.RunAsync(..., Progress<BatchProgress>, cts)`.
  - "Nhật ký (realtime)": RichTextBox nền tối, append theo log (màu theo ✅/⏭️/❌). Summary + nút "Mở thư mục".
  - Khóa điều hướng (MainForm) khi đang chạy; snapshot StyleConfig trước khi dispatch.

## Related Code Files
- Create: `MucLucHoSo/Controls/{StepPreview,StepRun}.cs`
- Reuse: P3 `Renderer.ToImages`/`Grouping`, P4 `BatchGenerator`, `Platform.OpenFolder` (Process.Start explorer)
- Modify: `MainForm.cs` (nhúng 2 UserControl; nút primary bước 3 = "Bắt đầu tạo PDF" gọi StepRun.Generate)

## Implementation Steps
1. `StepPreview`: groups + ◀▶ + render ảnh nền + hiển thị PictureBox; đồng bộ mẫu với bước 1.
2. `StepRun`: khối thư mục/pattern/tùy chọn + progress + log; nối `BatchGenerator.RunAsync`.
3. Nối action bar: bước 3 primary → Generate; khóa nav khi chạy; mở thư mục khi xong.
4. Chạy thử full 3 bước với Excel thật.

## Success Criteria
- [ ] Bước 2: chọn mẫu + ◀▶ đổi hồ sơ; ảnh trang A4 hiển thị **khớp PDF xuất ra** (cùng engine P3).
- [ ] Bước 3: Generate → N PDF; progress tới total; log realtime từng hồ sơ + tổng + thời gian; mở thư mục.
- [ ] Đa luồng ON chạy đúng (song song); ghi đè/Excel hoạt động.
- [ ] UI không treo khi preview/generate; đổi mẫu/đổi hồ sơ mượt.

## Red Team Fixes (áp dụng 2026-07-04)
- **#13 — cache invalidate:** cache ảnh preview key theo **`(DataVersion, templateSlug, groupIndex)`** (hoặc
  clear sạch trong `OnEnter` khi DataVersion/mẫu đổi). **Clamp/reset** record index về [0,N) mỗi lần dựng lại groups.
- **#4 — preview == PDF:** ảnh preview dùng **cùng engine + DPI pin** với `ToPdf` (P3); tin cậy vào test P3
  so ảnh-count == PDF-page-count. Không tự đo phân trang trên preview.
- **#12 — nav unlock đối xứng:** handler Generate bọc `try/finally` → **luôn** `set_nav_locked(false)` khi kết
  thúc (xong/cancel/lỗi fatal). Lỗi fatal (thư mục read-only, đĩa đầy, ClosedXML lỗi) → MessageBox + mở khoá.
  `RunAsync` không ném cho lỗi từng-file; UI mở khoá ở **mọi** kết cục.
- **#1 — checkbox đa luồng disabled:** "Chạy đa luồng" **disabled ở MVP** (serial-first, xem P4); bật khi có
  bản kiểm chứng thread-safe.
- **#17 — mở thư mục an toàn:** dùng `Process.Start(new ProcessStartInfo{ FileName = dir, UseShellExecute =
  true })` (tương đương `os.startfile`), **không** `Process.Start("explorer.exe", dir)` (arg không quote →
  explorer hiểu nhầm `/select,` v.v.). Validate `dir` là thư mục tồn tại trước khi mở.

## Risk Assessment
- Preview nhiều trang: render nền + cache **có invalidate theo DataVersion/mẫu**; đừng render lại toàn bộ khi chỉ đổi group nhỏ.
- Marshal ảnh về UI thread bằng `Control.BeginInvoke`; hủy render cũ khi user đổi group nhanh (CTS).
- Mở thư mục: `ProcessStartInfo{ FileName=dir, UseShellExecute=true }` (Red Team #17).
