---
phase: 3
title: "Docx Renderer & Excel Export"
status: done
effort: ""
---

# Phase 3: Docx Renderer & Excel Export

<!-- Updated: Validation Session 1 - render_group phải process-safe cho ProcessPoolExecutor (P6) -->

## Overview
Engine sinh docx từ 1 nhóm hồ sơ qua docxtpl (nhồi biến theo mapping) + export Excel tổng hợp. Đây là lõi nghiệp vụ, test headless không cần UI. Bọc lại logic PoC `render_test.py` thành module chuẩn. **Phải chạy được trong tiến trình con** (P6 dùng ProcessPoolExecutor).

## Requirements
- Functional: group DataFrame theo `grouping_column`; mỗi nhóm → 1 docx nhồi biến; sanitize tên file; Excel tổng hợp theo `row_mapping`.
- Non-functional: **`render(ctx, autoescape=True)`**; xử lý ảnh chữ ký thiếu; tên file hợp lệ Windows; không sót tag jinja. **Process-safe**: `render_group` chỉ nhận dữ liệu picklable (dict/records/str), tự mở `DocxTemplate` bên trong (DocxTemplate/InlineImage KHÔNG pickle được). Tên file **xác định trước** theo `ho_so_so` (không phụ thuộc thứ tự chạy → an toàn khi song song).

## Architecture
```
app/core/docx_renderer.py
  build_context(style, df_group, ho_so_so) -> dict     # settings + document_fields + rows(list dict theo row_mapping) + anh_chu_ky
  render_group(style_dict, records, out_path)          # process-safe: mở DocxTemplate bên trong; render(ctx, autoescape=True); InlineImage nếu có ảnh
  group_dataframe(style, df) -> {group_value: df}      # theo grouping_column
  sanitize_filename(name), resolve_output_name(style, ho_so_so)
app/core/excel_exporter.py
  export_excel(all_groups, out_path, style)            # openpyxl, cột theo row_mapping (bỏ hardcode 7 cột)
app/core/template_introspect.py
  get_template_variables(template_path) -> set         # docxtpl get_undeclared_template_variables cho UI mapping
```
Template dùng 2 hàng marker `{%tr%}` (đã fix ở PoC). Ảnh: `{% if anh_chu_ky %}` trong template; renderer truyền `InlineImage` hoặc bỏ trống (giống fallback bản cũ).

## Related Code Files
- Create: `app/core/docx_renderer.py`, `app/core/excel_exporter.py`, `app/core/template_introspect.py`
- Reuse: `build_default_template.py` (đã có), `styles/van-phong-dat-dai/*`, logic `ghi_excel_mot_lan_toi_uu` cũ
- Cập nhật: `styles/van-phong-dat-dai/template.docx` thêm `{% if anh_chu_ky %}...{% endif %}` quanh ảnh (sửa `build_default_template.py`)

## Implementation Steps
1. `group_dataframe`: nhóm theo `style.grouping_column`; lấy `ho_so_so` từ `document_fields` mapping.
2. `build_context`: dựng `rows=[{var: row[col]}...]` theo `row_mapping`; document fields; `anh_chu_ky=InlineImage(tpl, path, Inches(1.0))` nếu file tồn tại, else None.
3. `render_group`: `DocxTemplate(template).render(ctx, autoescape=True)` → save. Assert không còn `{{`/`{%`.
4. `sanitize_filename`: bỏ ký tự cấm Windows `\/:*?"<>|`; `resolve_output_name` trích số từ `ho_so_so` theo pattern; chống trùng (hậu tố `_2`).
5. Sửa `build_default_template.py`: bọc ảnh bằng `{% if anh_chu_ky %}`, regen template.
6. `excel_exporter`: bê `ghi_excel_mot_lan_toi_uu` nhưng headers/cột lấy từ `row_mapping`.
7. `template_introspect.get_template_variables` cho phase 5.

## Success Criteria
- [ ] Render 1 nhóm mẫu → docx khớp 100% output cũ (đối chiếu như PoC: width cột, header, dữ liệu, font, footer x/y).
- [ ] Dữ liệu chứa `&<>` render đúng (autoescape).
- [ ] Ảnh chữ ký thiếu → docx vẫn tạo, chừa chỗ, không crash.
- [ ] Tên file có ký tự cấm/trùng → sanitize + không đè nhau.
- [ ] Excel tổng hợp sinh đúng theo `row_mapping`.
- [ ] `render_group(style_dict, records, out_path)` chạy được khi gọi từ tiến trình con (test bằng `ProcessPoolExecutor` nhỏ) — không lỗi pickle.

## Risk Assessment
- docxtpl fidelity đã verify ở PoC → rủi ro thấp.
- Kiểu số (STT) → ép `str` khi cần để tránh lệch định dạng.
