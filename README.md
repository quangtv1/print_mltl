# print_html

App desktop (PyQt5) tạo **"Mục lục văn bản, tài liệu"** dạng `.docx` từ Google
Sheet — cấu hình được qua `template.docx` + `style.json`, xuất hàng loạt song
song, xem trước PDF trong app. Dev trên macOS, chạy chính trên **Windows 11**
(đóng gói `.exe` qua GitHub Actions).

## Chạy từ mã nguồn

```bash
pip install -r requirements.txt
python main.py
```

## Build .exe cho Windows

Đóng gói qua GitHub Actions `windows-latest` (PyInstaller không cross-compile từ
macOS). Xem chi tiết: [`docs/deployment-windows.md`](docs/deployment-windows.md).

- Push tag `v*` **hoặc** chạy tay workflow **Build Windows EXE** (tab Actions).
- Tải artifact `MucLucHoSo-windows`, giải nén, chạy `MucLucHoSo.exe`.

## Cấu trúc

- `app/core/` — nghiệp vụ: đọc Google Sheet, render docx, export Excel, preview PDF, xuất song song.
- `app/ui/` — giao diện PyQt5 5 bước (kết nối → mapping → thiết lập → xem trước → xuất hàng loạt).
- `styles/<tên>/` — mỗi style gồm `template.docx` + `style.json` (copy sang máy khác dùng lại).
- `packaging/mltl.spec`, `.github/workflows/build-windows.yml` — đóng gói Windows.

## Lưu ý

- **Không** kèm khóa Google trong repo. Người dùng tự chọn file service-account
  `.json` (scope chỉ đọc) khi chạy app; chia sẻ Sheet cho email service-account.
- Xem trước cần **LibreOffice** trên máy đích; thiếu thì mở bản docx bằng Word.
- Ảnh chữ ký (`chuky_tung.png`) không nằm trong repo — đặt cạnh app nếu cần nhúng.
