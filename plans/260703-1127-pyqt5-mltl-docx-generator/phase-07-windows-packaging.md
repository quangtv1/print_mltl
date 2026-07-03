---
phase: 7
title: "Windows Packaging"
status: done
effort: ""
---

# Phase 7: Windows Packaging

<!-- Updated: Validation Session 1 - build qua GitHub Actions windows-latest (đường chính) -->

## Overview
Đóng gói app thành `.exe` chạy trên Windows 11 sạch (không cần Python). Build qua **GitHub Actions `windows-latest`** (đường chính — không có máy Windows sẵn; PyInstaller không cross-compile từ macOS).

## Requirements
- Functional: `.exe` mở app, đủ luồng 5 bước; `styles/` đi kèm và ghi được.
- Non-functional: không bundle key Google; hướng dẫn cài LibreOffice trên máy đích cho preview.

## Architecture
- PyInstaller spec (`--onedir` ưu tiên hơn `--onefile` vì nhẹ khởi động + dễ kèm `styles/`).
- `--add-data` gói `styles/` (hoặc đặt cạnh exe, đọc theo đường dẫn tương đối exe).
- Ẩn/không kèm `get_link_pdf_.json`.
- `docs/deployment-windows.md`: bước build + yêu cầu máy đích (LibreOffice cho preview; MS Word để mở bản cuối).

## Related Code Files
- Create: `.github/workflows/build-windows.yml` (CI `windows-latest` — đường build chính), `packaging/mltl.spec`, `docs/deployment-windows.md`
- Đảm bảo: đường dẫn `styles/`/template resolve theo `sys._MEIPASS`/thư mục exe khi đóng gói
- `main.py`: `multiprocessing.freeze_support()` (bắt buộc cho ProcessPoolExecutor trong exe — xem P6)

## Implementation Steps
1. Chuẩn hóa resolve đường dẫn tài nguyên (dev vs frozen: `getattr(sys,'_MEIPASS',...)`).
2. Viết spec PyInstaller: entry `main.py`, add-data `styles/`, ẩn key, `--onedir`.
3. `.github/workflows/build-windows.yml`: `windows-latest`, setup Python, `pip install -r requirements.txt` + pyinstaller, build, upload artifact `.exe`.
4. Tải artifact về máy Windows đích test: kết nối sheet, mapping, preview (LibreOffice hoặc fallback Word), **generate hàng loạt song song** (kiểm tra freeze_support/spawn OK).
5. `docs/deployment-windows.md`: yêu cầu máy đích (LibreOffice cho preview, MS Word mở bản cuối) + cách lấy artifact.

## Success Criteria
- [ ] `.exe` chạy trên Windows 11 không cài Python: đi hết luồng, sinh docx đúng.
- [ ] `styles/` đọc/ghi được từ exe; copy style sang máy khác dùng lại.
- [ ] Không có key Google trong gói.

## Risk Assessment
- Không build được từ macOS → CI `windows-latest` build; cần máy Windows đích để test thật (thu xếp sớm).
- **ProcessPoolExecutor trong exe**: Windows dùng spawn → cần `freeze_support()` + code an toàn re-import; test kỹ generate song song trên exe (rủi ro cao nhất của phase này).
- Preview cần LibreOffice trên máy đích → fallback `open_with_default` (Word) khi thiếu; tài liệu hóa.
- Gói nặng (PyQt5+PyMuPDF+pandas) → dùng `--onedir`, loại module thừa nếu cần.
