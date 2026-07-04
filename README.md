# print_mltl

App desktop (PyQt5) tạo **"Mục lục văn bản, tài liệu"** dạng **PDF** từ **file Excel
`.xlsx`** — trình soạn thảo mẫu **WYSIWYG** + xuất hàng loạt (mỗi hồ sơ 1 PDF) kèm
Excel tổng hợp. Engine **Native Qt** (`QTextDocument` → `QPdfWriter`), không phụ thuộc
docx/Google Sheet/LibreOffice. Dev trên macOS, chạy chính trên **Windows 11** (đóng
gói `.exe` qua GitHub Actions).

## Chạy từ mã nguồn

```bash
pip install -r requirements.txt
python main.py
```

## Luồng 3 bước

1. **Đầu vào** — chọn file `.xlsx` + sheet → đọc dữ liệu (validate header rỗng/trùng),
   chọn Mẫu, ghép biến ↔ cột (auto-match), chọn cột gom nhóm hồ sơ.
2. **Thiết kế & Preview** — soạn mẫu WYSIWYG (B/I/U, cỡ, màu), chèn biến `{token}` tại
   con trỏ, xem trước **phân trang A4 thật** (footer "Trang x/y", header bảng lặp qua
   trang), duyệt từng hồ sơ, Lưu mẫu / Đặt lại mặc định.
3. **Chạy** — chọn thư mục + mẫu tên PDF, tùy chọn (ghi đè / Excel tổng hợp), xuất
   hàng loạt (tuần tự) với log realtime + tiến trình + mở thư mục kết quả.

## Build .exe cho Windows

Đóng gói qua GitHub Actions `windows-latest` (PyInstaller không cross-compile từ
macOS). Push tag `v*` hoặc chạy tay workflow, tải artifact, chạy `MucLucHoSo.exe`.
Spec: [`packaging/mltl.spec`](packaging/mltl.spec).

## Cấu trúc

- `app/core/` — nghiệp vụ: `excel_reader` (đọc `.xlsx`), `qt_pdf_renderer` (render PDF
  Native Qt), `batch_generator` (xuất hàng loạt), `excel_exporter`, `variables` (từ
  vựng `{token}`), `style_config`, `text_match`.
- `app/ui/` — giao diện PyQt5 3 bước: `main_window` + `step_input` / `step_design` /
  `step_run`; `theme` (Fluent `#0078d7`).
- `styles/<tên>/style.json` — mỗi mẫu gồm mapping + `template_html` (nội dung WYSIWYG,
  chứa `{token}`). Có sẵn 4 mẫu: Đông Hà, Quảng Ninh, Đống Đa, Vĩnh Phúc.

## Lưu ý

- Cú pháp biến: `{single_brace}`. Từ vựng token suy từ schema mẫu (document fields +
  cột `row_mapping` + biến tự động `{stt_file}`/`{ngay_gio}`). `{trang_so}`/
  `{tong_so_trang}` **chỉ dùng ở footer**.
- Không còn phụ thuộc `docxtpl`/`python-docx`/`PyMuPDF`/`gspread`/`google-auth` trong
  luồng chính. Không kèm khóa/credential nào trong repo.
- Font mẫu mặc định **Times New Roman** (có sẵn trên Windows).
