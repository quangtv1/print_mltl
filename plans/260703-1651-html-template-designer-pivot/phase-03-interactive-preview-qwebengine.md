---
phase: 3
title: "Interactive Preview (QWebEngine)"
status: pending
effort: ""
---

# Phase 3: Preview Widget (sau abstraction; engine chốt ở P6)

<!-- Updated: Validation Session 1 - preview sau abstraction; bind CHÍNH qua panel (P4); click-canvas = bonus WebEngine; engine chốt P6 -->

## Overview
Widget preview A4 **sau một abstraction** để P4/P5 không phụ thuộc engine cụ thể. Hai backend:
`QWebEngineView` (fidelity + click-canvas qua QWebChannel) hoặc `QTextBrowser` (nhẹ, chỉ hiển thị).
**Chốt backend ở P6** sau test đóng gói. Gán biến **chính qua panel** (P4) — hoạt động cả 2 backend;
**click-trên-canvas** chỉ bật khi backend = QWebEngine (bonus).

**2 mode hiển thị:**
- **Edit:** render template **thô** — các vùng hiện token `{ten_bien}`, **không** nhồi giá trị (dựng
  context toàn placeholder `{var}` để thấy rõ biến ở đâu).
- **Preview:** nhồi dữ liệu record thật (dùng `html_renderer` P1) với tiến/lùi hồ sơ.
- **Chọn biến ở panel (P4) → nổi bật vùng** của biến đó trên A4 (đổi style zone: nền vàng + outline),
  hoạt động ở **cả 2 mode**. Cột trong bảng lặp → nổi bật cả cột.

## Requirements
- Functional: interface `PreviewBackend.set_html(html, mode)`, `set_mode('edit'|'preview')`; điều hướng record;
  tín hiệu tùy chọn `zone_clicked(zone_id)` (chỉ WebEngine).
- Non-functional: không treo UI; import engine có guard; app không sập nếu backend không sẵn.

## Architecture
```
app/ui/preview_backend.py
  class PreviewBackend(QWidget)            # interface chung: set_html/set_mode/records nav
  make_preview_backend() -> PreviewBackend # chọn WebEngine nếu import được, else QTextBrowser (quyết cuối ở P6)
app/ui/preview_webengine.py               # QWebEngineView + QWebChannel + bridge; zone_clicked; JS overlay
app/ui/preview_textbrowser.py             # QTextBrowser hiển thị-only (không zone_clicked)
app/ui/preview_assets.py                  # JS/CSS overlay (inline) cho backend WebEngine
```
Bind KHÔNG phụ thuộc `zone_clicked`: P4 gán biến↔vùng qua panel bên; `zone_clicked` chỉ để "chọn nhanh"
vùng khi có WebEngine.

## Related Code Files
- Create: `app/ui/preview_backend.py`, `app/ui/preview_webengine.py`, `app/ui/preview_textbrowser.py`, `app/ui/preview_assets.py`
- Depends: P1 (`html_renderer` dựng HTML record)
- Dep engine (PyQtWebEngine) **quyết ở P6**; code viết để cả 2 backend cùng chạy

## Implementation Steps
1. Định nghĩa interface `PreviewBackend` (set_html/set_mode + điều hướng record).
2. `preview_webengine`: QWebEngineView + QWebChannel bridge (`@pyqtSlot(str) receive_zone_click`); JS overlay highlight `[data-zone]` + onclick; phát `zone_clicked`.
3. `preview_textbrowser`: QTextBrowser `setHtml` (hiển thị-only; không overlay/onclick).
4. `make_preview_backend`: thử import WebEngine → dùng; lỗi → QTextBrowser. (P6 chốt bằng cách quyết có ship PyQtWebEngine không.)
5. Mode Preview: render HTML record thật (P1) không overlay; Edit (WebEngine) bật overlay.

## Success Criteria
- [ ] Cả 2 backend: `set_html` hiển thị đúng; đổi record → cập nhật; toggle Edit/Preview.
- [ ] WebEngine: click vùng phát `zone_clicked` đúng id (bonus).
- [ ] Không có PyQtWebEngine → tự dùng QTextBrowser, app không sập; bind qua panel vẫn chạy.

## Risk Assessment
- QWebChannel init/timing (WebEngine) → mẫu chuẩn: channel set trước loadFinished, JS chờ `qwebchannel.js`.
- QTextBrowser CSS hạn chế → preview xấp xỉ; bản PDF cuối (wkhtmltopdf) mới là chuẩn.
