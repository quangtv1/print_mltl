---
phase: 1
title: "HTML Renderer & Template Format"
status: pending
effort: ""
---

# Phase 1: HTML Renderer & Template Format

<!-- Updated: Validation Session 1 - ảnh chữ ký = tham chiếu đường dẫn (không data-URI); output HTML (PDF ở P2) -->

## Overview
Định nghĩa format template HTML (Jinja2 + CSS A4) + schema `style.json` mở rộng (vùng/binding),
và `html_renderer.render_group()` process-safe. Lõi nghiệp vụ mới, test headless không cần UI.

## Requirements
- Functional: `render_group(style_dict, records, out_path)` → file `.html` nhồi biến theo mapping;
  bảng `{% for r in rows %}`; ảnh chữ ký `<img src>` tham chiếu đường dẫn; footer số trang qua CSS `@page`.
- Non-functional: **autoescape=True** (Jinja2); process-safe (chỉ nhận dict/list/str, tự dựng
  `jinja2.Environment` bên trong); tên file xác định trước theo `ho_so_so`.

## Architecture
```
app/core/html_renderer.py
  build_context(style, records) -> dict          # settings + document_fields + rows + anh_chu_ky (đường dẫn)
  render_group(style_dict, records, out_path)    # jinja2 render template.html -> .html; process-safe
  _signature_src(style, out_dir) -> str|None     # đường dẫn ảnh (tương đối out_dir hoặc file://abs); None nếu thiếu
  sanitize_filename / resolve_output_name / make_unique_path   # bê từ docx_renderer (đổi đuôi .html)
app/core/html_introspect.py
  get_template_variables(template_html) -> set    # jinja2 meta.find_undeclared_variables
  get_template_zones(template_html) -> [ZoneInfo] # parse data-zone="doc:x"/"col:y" (regex/html.parser)
styles/van-phong-dat-dai/template.html            # tái tạo layout mục lục bằng HTML/CSS
```
`style.json` thêm khối `zones`/`bindings` (zone id → biến) và `template_file: "template.html"`;
giữ `settings`, `row_mapping`, `document_fields`, `grouping_column`, `columns`.

## Template HTML (tái tạo mục lục)
- `@page { size: A4; margin: ...; @bottom-right { content: "Trang " counter(page) "/" counter(pages) } }`.
- Header 2 cột (`data-zone="doc:co_quan_dong1/2"` | quốc hiệu cố định), tiêu đề `data-zone="doc:tieu_de"`,
  dòng `Số ký hiệu hồ sơ: {{ ho_so_so }}`.
- Bảng 7 cột (`<th>` nhãn + `data-zone="col:stt"`...), thân `{% for r in rows %}<td>{{ r.stt }}</td>...`.
- Khối chữ ký: `{{ chuc_danh_ky }}`, `{% if anh_chu_ky %}<img src="{{ anh_chu_ky }}">{% endif %}`, `{{ nguoi_ky }}`.
- Font Times New Roman, độ rộng cột theo `columns[].width` (CSS `width: %`).

## Related Code Files
- Create: `app/core/html_renderer.py`, `app/core/html_introspect.py`, `styles/van-phong-dat-dai/template.html`,
  `build_default_html_template.py` (sinh template.html mặc định, tương tự build_default_template.py cũ)
- Modify: `app/models/style.py` + `app/core/style_config.py` (thêm `zones`/`bindings`, `template_file` mặc định .html)
- Reuse (bê logic): `docx_renderer.sanitize_filename/resolve_output_name/make_unique_path/df_to_records`

## Implementation Steps
1. Thiết kế `template.html` + CSS A4 khớp bố cục mục lục cũ; script `build_default_html_template.py` sinh ra.
2. `html_renderer.build_context`: settings + document fields (ho_so_so từ record[0]) + rows theo row_mapping;
   `anh_chu_ky` = **đường dẫn ảnh** (copy ảnh vào out_dir hoặc dùng `file://` tuyệt đối để wkhtmltopdf/preview
   nạp được — P2 truyền base path). None nếu thiếu. Ép `str`, autoescape lo `& < >`.
3. `render_group`: `jinja2.Environment(autoescape=True).from_string(template_html).render(ctx)` → ghi `.html` utf-8.
   Process-safe: nhận `style_dict` (+ `_style_dir` để đọc template.html), tự đọc template bên trong.
4. `html_introspect`: liệt kê biến (jinja2 meta) + zone (`data-zone`) cho UI mapping/bind (P4).
5. Mở rộng schema `style.json`: `zones` (khai báo vùng + loại doc/col), `bindings` (zone→biến).
6. `resolve_output_name`: pattern đổi `MLHS_{ho_so_so}.html`.

## Success Criteria
- [ ] Render 1 nhóm mẫu → `.html` mở trình duyệt hiển thị đúng bố cục (header, bảng, chữ ký, tiêu đề).
- [ ] Dữ liệu `& < >` render đúng (autoescape); bảng nhiều hàng liệt kê đủ.
- [ ] Ảnh chữ ký thiếu → vẫn tạo, không lỗi, không chữ "None".
- [ ] In thử (Ctrl+P) → footer "Trang x/y" đúng; bảng dài sang trang không vỡ.
- [ ] `render_group` chạy trong `ProcessPoolExecutor` nhỏ không lỗi pickle.

## Risk Assessment
- Fidelity HTML vs bản docx cũ khác đôi chút (bình thường, khác engine) → chấp nhận, ưu tiên đúng dữ liệu + in đẹp.
- CSS `@page` counter chỉ hiện khi in → ghi chú; screen preview không cần số trang.
