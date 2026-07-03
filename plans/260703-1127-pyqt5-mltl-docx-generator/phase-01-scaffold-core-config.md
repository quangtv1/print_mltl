---
phase: 1
title: "Scaffold & Core Config"
status: done
effort: ""
---

# Phase 1: Scaffold & Core Config

## Overview
Dựng khung project, dependencies, và 2 module nền: `style_config` (đọc/ghi `style.json`) + `platform_utils` (đa nền macOS/Windows). Nền cho mọi phase sau.

## Requirements
- Functional: load/save `style.json` với validate; resolve đường dẫn `soffice` + mở file mặc định theo OS.
- Non-functional: không hardcode path (dùng `pathlib`/`tempfile`); mọi IO file dùng `encoding="utf-8"`.

## Architecture
```
main.py
app/__init__.py
app/models/style.py        # dataclass StyleConfig, ColumnDef, RowMapping
app/core/style_config.py   # load_style(dir)->StyleConfig, save_style(cfg,dir)
app/core/platform_utils.py # resolve_soffice(override), open_with_default(path), make_temp_dir()
styles/van-phong-dat-dai/  # đã có template.docx + style.json (PoC)
requirements.txt
```
`StyleConfig` phản ánh đúng schema `style.json` hiện có (settings, document_fields, row_mapping, columns, grouping_column, output_filename_pattern, template_file).

## Related Code Files
- Create: `main.py`, `app/models/style.py`, `app/core/style_config.py`, `app/core/platform_utils.py`, `requirements.txt`, `app/__init__.py`, `app/core/__init__.py`, `app/models/__init__.py`
- Reuse: `styles/van-phong-dat-dai/style.json` (schema tham chiếu)

## Implementation Steps
1. `requirements.txt`: PyQt5, gspread, google-auth, docxtpl, python-docx, openpyxl, PyMuPDF, pandas. (PyInstaller để phase 7, chỉ dev.)
2. `app/models/style.py`: dataclasses ánh xạ `style.json`; `from_dict`/`to_dict`.
3. `app/core/style_config.py`: `load_style(style_dir)` đọc `style.json` + validate khóa bắt buộc; `save_style()`; `list_styles(styles_root)` quét thư mục con.
4. `app/core/platform_utils.py`: `resolve_soffice(override=None)` (shutil.which → path chuẩn theo `sys.platform` → None); `open_with_default(path)`; `make_temp_dir()`.
5. `main.py`: entry tối thiểu khởi tạo QApplication + cửa sổ rỗng (placeholder, hoàn thiện ở P5). Gọi `multiprocessing.freeze_support()` ngay đầu `__main__` (cần cho generate song song + exe — P6/P7).

## Success Criteria
- [ ] `load_style("styles/van-phong-dat-dai")` trả về StyleConfig đúng, roundtrip `save`→`load` không mất dữ liệu.
- [ ] `resolve_soffice()` trả path đúng khi có LibreOffice, `None` khi không (test trên macOS).
- [ ] `python main.py` mở cửa sổ rỗng không lỗi.

## Risk Assessment
- Schema `style.json` đổi sau này → giữ `from_dict` khoan dung (bỏ qua khóa lạ), validate khóa bắt buộc.
