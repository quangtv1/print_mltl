---
phase: 5
title: "Step 2 Design Editor & Preview"
status: pending
priority: P1
dependencies: [3, 4]
---

# Phase 5: Step 2 Design Editor & Preview

## Overview
Bước 2 — Thiết kế & Preview (mota3 §7): trái = tờ A4 WebView2 (Chỉnh sửa/Xem trước), phải = panel biến.
Toolbar định dạng WinForms điều khiển editor qua bridge (P3). **Tái dùng contentEditable + engine phân trang
của prototype** → editor + preview giống tuyệt đối. Không viết lại editor bằng control native.

## Requirements
- Functional: tab Chỉnh sửa/Xem trước; toolbar (font/size/BIU/A−A+/căn/zoom/hướng/chèn-xóa cột theo focus/undo);
  chèn biến tại con trỏ; xóa cả biến; kéo giãn cột; ◀▶ duyệt hồ sơ; highlight biến ở preview; Lưu/Reset khi dirty.
- Non-functional: thao tác mượt; đổi Edit/Preview giữ nội dung; preview khớp PDF (P3).

## Architecture
- `Forms/Step2DesignControl.cs`:
  - Trái: `WebViewController` (P3) nạp trang A4; toolbar WinForms trên cùng (mota3 §7.1 đúng thứ tự) gọi
    lệnh editor qua `ExecuteScriptAsync` (execCommand/bridge): bold/italic/underline, fontName/fontSize, align,
    zoom (50–200%), orient (dọc/ngang), **Chèn/Xóa cột** (chỉ hiện khi focus trong ô bảng — bridge báo focus),
    **Undo** (stack ~40 trong JS). Kéo mép cột do JS prototype xử lý (ngưỡng 7px, lưu %).
  - Phải: panel biến theo nhóm (Tài liệu / Hàng / **Tự động** {trang_so},{tong_so_trang} nền xanh lá) + nút
    "Ghép biến với dữ liệu" (mở `MappingPopup` P4). Chế độ Chỉnh sửa: click chip → chèn `<span data-var>` tại con
    trỏ. Chế độ Xem trước: click chip/giá trị → **highlight vàng** cả cột/ô (bridge).
  - Thanh điều hướng hồ sơ: ◀ / "Hồ sơ {so} (i/n)" / ▶ → `setRecord` (dữ liệu Excel thật đã map, hoặc seed nếu chưa).
  - Dirty → hiện **Lưu** + **Reset mặc định** (lưu template chỉnh sửa vào config; reset về `defaultDoc(id)`).
- Bridge bổ sung (P3 `bridge.js`): `execFormat(cmd,val)`, `insertVar(name)`, `highlightVar(name)`, `insertCol/delCol`,
  `undo`, `setZoom/ setOrient`, `getFocusInTable()→bool`, `isDirty()`, `getTemplateHtml()/setTemplateHtml()`.

## Related Code Files
- Create: `Forms/Step2DesignControl.cs`
- Modify: `App/Web/bridge.js` (lệnh editor), `MainForm` (nhúng Step2)
- Reuse: P3 `WebViewController`, P4 `MappingPopup`; `design_v3` editor JS (vendored)
- Reference: `design_v3/mota3.html` §7,§8

## Implementation Steps
1. Nhúng WebView2 editor + toolbar; nối lệnh format cơ bản (BIU/font/size/align) qua bridge.
2. Panel biến (nhóm + auto) + chèn biến tại con trỏ (Edit) / highlight (Preview).
3. Chèn/Xóa cột theo focus + Undo + kéo giãn cột (dùng engine prototype); zoom/hướng.
4. ◀▶ duyệt hồ sơ (setRecord); toggle Edit/Preview; phân trang preview.
5. Dirty → Lưu (config) / Reset mặc định.

## Success Criteria
- [ ] Toolbar đúng thứ tự & hoạt động (font/size/BIU/A−A+/căn/zoom/hướng/chèn-xóa cột theo focus/undo).
- [ ] Chèn biến tại con trỏ; xóa cả biến (không xóa từng ký tự); kéo giãn cột giữ tỉ lệ.
- [ ] Xem trước: chip không mờ; click chip/giá trị → highlight vàng cả cột; phân trang đúng bảng dài.
- [ ] ◀▶ đổi hồ sơ đúng; toggle Edit/Preview giữ nội dung; Lưu/Reset hoạt động.
- [ ] Editor + preview **giống prototype** (cùng HTML/engine).

## Risk Assessment
- Phần lớn hành vi nằm ở JS prototype đã có → rủi ro chính là **cầu nối** WinForms↔JS (async, focus state, dirty).
  Giữ bridge nhỏ, message rõ ràng; verify tay trên Windows.
- "Chèn/Xóa cột chỉ hiện khi focus ô bảng": bridge phải báo focus theo `selectionchange` — debounce nhẹ.
- Undo cột giới hạn ~40 (JS) — không cần undo phía C#.
