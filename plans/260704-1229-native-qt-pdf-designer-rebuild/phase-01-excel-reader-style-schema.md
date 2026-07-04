---
phase: 1
title: "Excel Reader & Style Schema"
status: completed
priority: P1
dependencies: []
---

# Phase 1: Excel Reader & Style Schema

## Overview
Lớp dữ liệu nền: đọc file `.xlsx` (chọn file → liệt kê sheet → đọc DataFrame, validate header) và
mở rộng `StyleConfig` cho mô hình template mới (Native Qt): `template_html`, cú pháp biến
`{single_brace}`, danh sách biến tự động. Không đụng UI/engine render.

## Requirements
- Functional: `list_sheets(path)`, `read_df(path, sheet)` (dùng `pandas.read_excel` engine openpyxl),
  validate header rỗng/trùng → lỗi rõ ràng. StyleConfig có trường lưu template WYSIWYG + auto vars.
- Non-functional: đọc file lớn không treo (gọi qua `workers.run_async` ở P4); giữ picklable đơn giản.

## Architecture
- `app/core/excel_reader.py`:
  - `list_sheets(path) -> list[str]` (đọc names qua `pd.ExcelFile`).
  - `read_df(path, sheet) -> pd.DataFrame` (fillna(""), astype str an toàn cho render).
  - `validate_headers(df) -> None|raise` (header rỗng/`Unnamed`/trùng → `ValueError` tiếng Việt rõ).
- `app/models/style.py` mở rộng `StyleConfig`:
  - Thêm `template_html: str = ""` (nội dung QTextEdit — header + bảng-mẫu + footer, chứa `{token}`).
  - `output_filename_pattern` đổi mặc định `.pdf` = **`MLHS_{ho_so_so}.pdf`** (token thật là
    `{ho_so_so}`, **không** `{so_ho_so}` — xem Red Team #1/#6).
  - `settings.footer_format` giữ `"Trang {trang_so}/{tong_so_trang}"` (đổi từ `{PAGE}/{NUMPAGES}`);
    footer là **field settings**, render lúc in, **không** phải token chèn được vào body (Red Team #9).
  - **Từ vựng token = suy từ schema thật lúc runtime** (không hardcode tên bịa):
    - Doc fields: **key của `style.document_fields`** (mẫu Đông Hà hiện chỉ có `ho_so_so`).
    - Row/cột: **key của `style.row_mapping`** (7 cột: `stt`, `so_ky_hieu`, `ngay_thang`, `tac_gia`,
      `trich_yeu`, `to_so`/`trang`, `ghi_chu`… theo style.json thật).
    - Settings-derived: `co_quan_dong1/2`, `tieu_de`, `chuc_danh_ky`, `nguoi_ky` (là **settings**, không phải doc_field).
    - Auto-vars **MỚI** (hằng số module `AUTO_VARS`): `{stt_file}` (STT tăng dần theo group),
      `{ngay_gio}`. (`{trang_so}`/`{tong_so_trang}` chỉ dùng trong footer settings, không nằm AUTO_VARS body.)
    - 1 nguồn duy nhất (`app/core/variables.py` hoặc `style.py::AUTO_VARS`) để P2 render + P5 panel dùng chung.
- `app/core/style_config.py`: `load/save/list` giữ nguyên chữ ký; serialize thêm `template_html`.

## Related Code Files
- Create: `app/core/excel_reader.py`
- Modify: `app/models/style.py`, `app/core/style_config.py`
- Reuse: `app/core/text_match.py` (dùng ở P4 mapping — không sửa ở đây)
- Reference (mô hình grouping/records giữ lại): logic `group_dataframe`/`df_to_records` trong `docx_renderer.py` (sẽ chuyển sang `qt_pdf_renderer.py` ở P2)

## Implementation Steps
1. Viết `excel_reader.py` 3 hàm + validate; thông báo lỗi tiếng Việt (liệt kê header hỏng).
2. Mở rộng `StyleConfig`: thêm `template_html`, đổi mặc định output `.pdf`, đổi `footer_format`.
   Cập nhật `from_dict`/`to_dict` (đảm bảo backward: thiếu `template_html` → "").
3. Tạo module hằng số auto-vars (vd `app/models/style.py::AUTO_VARS` hoặc `app/core/variables.py`)
   để P2/P5 tham chiếu 1 nguồn.
4. Cập nhật `style_config` serialize/deserialize field mới; giữ `load/save/list` API.

## Success Criteria
- [ ] `list_sheets` + `read_df` đọc đúng `uploads/Mau02_QuangNinh.docx`-tương-đương `.xlsx` thật (dùng file Excel người dùng cung cấp ở test).
- [ ] `validate_headers` báo lỗi rõ khi header rỗng/trùng.
- [ ] `StyleConfig` round-trip (`to_dict`→`from_dict`) giữ `template_html` + pattern `.pdf`.
- [ ] Không import gspread/google-auth/docx ở các file phase này.

## Red Team Fixes (áp dụng 2026-07-04)
- **#3 (Critical) — Tạo `template_html` mặc định cho CẢ 4 MẪU** (user giữ đa mẫu). Đây là deliverable
  bắt buộc, không được để trống:
  - Dựng 4 style dir: `styles/van-phong-dat-dai/` (Mẫu 01 Đông Hà — đã có), thêm `styles/quang-ninh/`
    (Mẫu 02), `styles/dong-da/` (Mẫu 03), `styles/vinh-phuc/` (Mẫu 04).
  - Mỗi dir: `style.json` (name, document_fields, row_mapping, columns, settings, `output_filename_pattern`,
    `grouping_column`) + `template_html` = header + **1 bảng có đúng 1 dòng-mẫu chứa token cột** + marker
    bảng dữ liệu (xem P2/P5) khớp `row_mapping` của mẫu đó.
  - Nguồn dựng HTML: `Mau02_QuangNinh.docx`, `Mau03_DongDa.docx` (đọc cấu trúc bằng python-docx **một
    lần lúc dựng seed**, không phải runtime), `Mau04_VinhPhuc.pdf` (PDF → tham chiếu thị giác, soạn tay HTML).
  - "Đặt lại mặc định" (P5) nạp lại `template_html` gốc này.
- **#6 (High)** — bỏ mọi tên token bịa; panel biến (P5) sinh từ schema thật (đã sửa Architecture ở trên).
- **#1 (Critical)** — chuẩn hoá `{ho_so_so}` trong pattern mặc định (đã sửa ở trên).

## Risk Assessment
- Header trùng tên → pandas tự thêm `.1`; validate phải bắt trước khi mapping lệch. Mitigation: kiểm tra trên tên gốc qua `pd.ExcelFile` header row.
- **Chuyển 4 docx/pdf → template_html là việc thủ công/bán tự động đáng kể** (nhất là Mẫu 04 từ PDF); ước lượng riêng, đừng gộp chung "1 dòng".
- Backward compat style.json cũ (docx) → chỉ cần đọc được, không cần chạy; P6 dọn.
- **Excel không tin cậy:** `read_df` nên có cận kích thước/row (fail-fast lỗi tiếng Việt) tránh treo GUI khi file khổng lồ (Red Team minor).
