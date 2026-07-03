# Đóng gói & triển khai trên Windows 11

App "Tạo Mục Lục Hồ Sơ" chạy trên Windows 11 dưới dạng `.exe` (không cần cài
Python). Build qua **GitHub Actions `windows-latest`** vì PyInstaller không
cross-compile từ macOS.

## Build artifact (.exe)

1. Push tag `v*` (ví dụ `v1.0.0`) hoặc chạy tay workflow **Build Windows EXE**
   (tab Actions → Run workflow).
2. Workflow (`.github/workflows/build-windows.yml`):
   - cài `requirements.txt` + PyInstaller,
   - build theo `packaging/mltl.spec` (chế độ `--onedir`),
   - copy `styles/` và `chuky_tung.png` **cạnh exe**,
   - upload artifact `MucLucHoSo-windows`.
3. Tải artifact về, giải nén. Cấu trúc:

   ```
   MucLucHoSo/
     MucLucHoSo.exe
     styles/van-phong-dat-dai/{template.docx, style.json}
     chuky_tung.png
     _internal/            # thư viện PyInstaller (không sửa)
   ```

## Chạy trên máy đích

- Chạy `MucLucHoSo.exe`. Không cần cài Python.
- **Khóa Google KHÔNG đi kèm gói.** Người dùng tự chọn file service-account
  `.json` ở bước 1 (Kết nối). Chia sẻ Google Sheet cho email của service-account
  (quyền Xem).

### Yêu cầu để xem trước (preview) trong app

- Cài **LibreOffice** trên máy đích (dùng để chuyển docx→PDF hiển thị).
  Tải tại https://www.libreoffice.org/download.
- Nếu **thiếu LibreOffice**, app vẫn chạy: khi bấm Xem trước sẽ hỏi và **mở bản
  docx tạm bằng Word / ứng dụng mặc định** (fallback). Xuất hàng loạt không cần
  LibreOffice.
- Nên cài **Microsoft Word** để mở/chỉnh bản docx cuối (font Times New Roman
  hiển thị chuẩn nhất).

## Tùy biến & tái sử dụng style

- Toàn bộ cấu hình nằm trong `styles/<tên>/` (`template.docx` + `style.json`).
- **Copy thư mục `styles/<tên>/` sang máy khác** (đặt cạnh exe) là dùng lại được,
  không cần sửa code.
- Sửa bố cục cột / độ rộng / viền: mở `template.docx` bằng Word và chỉnh trực
  tiếp. Sửa nội dung text (cơ quan, người ký...), mapping cột, cột gom nhóm:
  làm trong app (tab Thiết lập / Mapping) → app ghi lại `style.json`.

## Ghi chú kỹ thuật

- Xuất hàng loạt chạy **song song đa tiến trình** (`ProcessPoolExecutor`).
  `main.py` gọi `multiprocessing.freeze_support()` đầu tiên — bắt buộc để pool
  hoạt động đúng trong exe (Windows dùng phương thức `spawn`).
- Đường dẫn tài nguyên (`styles/`, ảnh chữ ký) resolve theo thư mục chứa exe khi
  đóng gói (`resource_base_dir()` trong `app/core/platform_utils.py`).
