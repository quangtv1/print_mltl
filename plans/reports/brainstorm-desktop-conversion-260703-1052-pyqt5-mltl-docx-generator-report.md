# Brainstorm: Chuyển script Mục Lục Tài Liệu → App Desktop PyQt5

- **Ngày:** 2026-07-03
- **Nguồn:** `print_mltl_parallel.py` (script CLI hiện tại)
- **Modes:** (không dùng --html/--wiki)
- **Trạng thái:** Đã thống nhất thiết kế; sẵn sàng chuyển `/ck:plan`.

## 1. Vấn đề & mục tiêu

Script CLI hiện sinh file docx "Mục lục văn bản, tài liệu" từ Google Sheet, nhưng **toàn bộ style hardcode** trong Python (tên cơ quan, người ký, cột, font, độ rộng, ảnh chữ ký, tên sheet). Muốn:

- App desktop PyQt5 chọn & đổi được biến đầu vào.
- B1: kết nối Google Sheet, chọn worksheet, lấy danh sách biến = tiêu đề cột.
- B2: nhồi biến vào style/template hồ sơ có sẵn.
- Preview kết quả docx sau khi nhồi biến.
- Chọn nhiều style, lưu style ra file cấu hình portable để chạy máy khác.

## 2. Quyết định đã chốt

| Chủ đề | Quyết định |
|---|---|
| Template engine | **Hybrid**: `template.docx` (docxtpl) cho layout + `style.json` cho settings & mapping |
| Preview | **LibreOffice headless → PDF, nhúng trong app** (render bằng PyMuPDF) |
| OS & đóng gói | **Dev trên macOS, target chạy chính Windows 11**; đóng gói `.exe` bằng PyInstaller (build trên Windows) |
| Google auth | **Người dùng tự chọn file service-account `.json`** (không bundle key) |
| Mapping cột↔biến | Tự khớp theo tên **có gợi ý mặc định** + cho sửa tay; lưu trong `style.json` |
| Cột gom nhóm | **Linh hoạt chọn được**, mặc định `Tiêu đề hồ sơ`; lưu trong `style.json` |
| Excel tổng hợp | Giữ, có checkbox bật/tắt |
| Quản lý template | MVP 1 template, cấu trúc `styles/` sẵn cho nhiều |
| Khôi phục phiên (URL/worksheet) | Không làm (lựa chọn đã lưu trong `style.json` là đủ) |
| Footer số trang | **"Trang x/y" canh phải** bằng Word field `PAGE`/`NUMPAGES` trong footer template; toggle trong `style.json`. Mỗi hồ sơ = 1 file nên y = tổng trang đúng file đó. Word tự tính, LibreOffice preview render đúng |

## 3. Luồng app (5 bước)

1. **Kết nối:** Browse file `.json` + dán URL Sheet → liệt kê worksheet → chọn 1.
2. **Lấy biến:** đọc hàng tiêu đề → hiện danh sách cột.
3. **Template + mapping:** chọn template `.docx`; map cột sheet ↔ placeholder (tự khớp + sửa tay); chọn cột gom nhóm (hiện: `Tiêu đề hồ sơ`) → mỗi nhóm = 1 docx.
4. **Settings + Preview:** sửa các giá trị text (tên cơ quan 2 dòng, người ký, chức danh, tiêu đề), ảnh chữ ký (Browse), toggle footer số trang (lưu `style.json`). Layout cột/độ rộng/viền sửa trong Word ở `template.docx`. → Preview 1 hồ sơ (PDF nhúng).
5. **Generate hàng loạt:** sinh tất cả docx (+ Excel tùy chọn) vào thư mục chọn; chạy nền QThread.

## 4. Kiến trúc & module (Python snake_case)

```
main.py                         # entry QApplication
app/ui/
  main_window.py                # QMainWindow, tabs/wizard
  connect_tab.py                # B1: creds + url + worksheet
  mapping_tab.py                # B2-3: columns + template + mapping + grouping
  settings_tab.py               # B4: form style settings
  preview_widget.py             # PDF preview (fitz -> QPixmap)
app/core/
  sheets_client.py              # gspread: list_worksheets, read_df, headers
  style_config.py               # load/save style.json
  docx_renderer.py              # docxtpl fill: df_group -> docx (render autoescape=True)
  excel_exporter.py             # openpyxl aggregate (từ code cũ)
  pdf_preview.py                # docx->pdf (libreoffice) -> ảnh trang
  platform_utils.py             # đa nền: resolve_soffice(), open_with_default(), temp dir
  batch_generator.py            # QThread worker lặp qua nhóm
app/models/style.py             # dataclasses: StyleConfig, ColumnMapping
styles/van-phong-dat-dai/
  template.docx                 # layout + {{...}}, {%tr for r in rows%}
  style.json                    # settings + mapping + cột gom nhóm
requirements.txt
```

**Portable:** copy nguyên thư mục `styles/` sang máy khác là dùng lại; key Google chọn riêng vì bảo mật.

## 5. Đường di trú (migration)

- `build_default_template.py`: tái dùng code python-docx hiện tại **đúng 1 lần** để sinh `template.docx` với placeholder + loop → template mặc định **giống hệt output hiện nay**, sau đó luồng chạy chỉ nhồi biến (bỏ ~400 dòng OOXML khỏi runtime). Đồng thời chèn **footer "Trang x/y"** (field `PAGE`/`NUMPAGES`, canh phải) vào section.
- `ghi_excel_mot_lan_toi_uu` → `excel_exporter.py` (gần như giữ nguyên).
- `lay_du_lieu_google_sheet` → `sheets_client.py` + thêm `list_worksheets()`.
- Thay `oauth2client` (deprecated) bằng `google-auth`.

## 4b. Đa nền: macOS (dev) + Windows 11 (target)

Dev/test trên macOS bằng `python main.py`; sản phẩm chạy chính Windows 11. Cô lập khác biệt OS trong `platform_utils.py`.

| Vấn đề | macOS | Windows 11 | Xử lý |
|---|---|---|---|
| Path `soffice` | `/Applications/LibreOffice.app/Contents/MacOS/soffice` | `C:\Program Files\LibreOffice\program\soffice.exe` | `resolve_soffice()`: `shutil.which` → path chuẩn theo OS → override trong settings |
| Đóng gói exe | **không cross-compile được** | build `.exe` tại đây | dev bằng `python main.py`; build exe trên Windows/CI `windows-latest` |
| Mở file mặc định | `open` | `os.startfile` | `open_with_default()` rẽ theo `sys.platform` |
| Đường dẫn | dùng `pathlib`+`tempfile` | idem | không hardcode; bỏ path WSL cũ |
| Font TNR trong preview | LibreOffice thay Liberation Serif nếu thiếu | có sẵn | preview lệch nhẹ; bản cuối mở Word đúng |
| Thư mục cấu hình app | `~/Library/Application Support` | `%APPDATA%` | `QStandardPaths`; `styles/` để cạnh app cho portable |

Lệnh preview 2 OS như nhau: `soffice --headless --convert-to pdf --outdir <tmp> <docx>` (subprocess + timeout). macOS cần `brew install --cask libreoffice`.

## 5b. Đã kiểm chứng (PoC render thử)

Đã build `template.docx` + `style.json` và render thử với dữ liệu mẫu, so sánh với output bản cũ (`ghi_df_ra_word`):

- ✅ Độ rộng 7 cột (twips), nhãn header, số hàng, font/size/bold **khớp 100%** bản cũ.
- ✅ Header cơ quan, tiêu đề, hồ sơ số, khối chữ ký + ảnh nhúng đúng; footer "Trang x/y" có field PAGE/NUMPAGES.
- ✅ Không sót tag jinja.

**2 phát hiện bắt buộc cho `docx_renderer.py`:**
1. **Vòng lặp bảng:** tag `{%tr%}` xóa NGUYÊN hàng chứa nó → `for`/`endfor` phải ở **2 hàng marker riêng** kẹp hàng dữ liệu (không đặt chung 1 hàng, sẽ mangle).
2. **Bắt buộc `tpl.render(ctx, autoescape=True)`** — nếu không, dữ liệu chứa `&`, `<`, `>` bị hỏng (vd "Sở TN&MT" → "Sở TN").

## 6. Đánh giá phương án (tóm tắt)

- **Template WYSIWYG (A):** portable, thêm mẫu không sửa code, bỏ OOXML — nhưng cần học cú pháp loop.
- **JSON-param code cũ (B):** giữ output 100% nhưng chỉ 1 layout, mâu thuẫn ý "nhiều style".
- **Hybrid (C) — ĐÃ CHỌN:** layout bằng template + settings/mapping bằng JSON; linh hoạt nhất.

## 7. Rủi ro & giảm thiểu

| Rủi ro | Giảm thiểu |
|---|---|
| LibreOffice là dependency máy đích cho preview | Detect `soffice`, báo lỗi rõ; fallback mở bằng Word nếu thiếu |
| multiprocessing lỗi trong exe PyInstaller | MVP dùng 1 QThread tuần tự; thêm song song sau nếu chậm |
| Key service-account đang lộ plaintext trong repo | **Thu hồi key cũ, tạo mới**, không commit/bundle |
| docxtpl fidelity (border/width cột) | Bake sẵn trong template; verify bằng `build_default_template.py` |
| Preview LibreOffice ≠ Word chính xác | Chấp nhận lệch nhẹ; preview để kiểm tra biến, bản cuối mở bằng Word |
| Exe PyInstaller nặng | Chấp nhận; cân nhắc `--onedir` thay `--onefile` |

## 8. Tiêu chí thành công

- Chọn creds + URL → liệt kê đúng worksheet → lấy đúng danh sách cột.
- Map cột → sinh 1 docx nhồi biến **giống output script hiện tại**.
- Preview PDF hiển thị đúng trong app.
- Copy `styles/` sang máy khác chạy lại được không sửa code.
- Generate hàng loạt N hồ sơ ra N docx + 1 Excel (nếu bật).
- Đóng gói `.exe` chạy trên Windows sạch (test máy không có Python).

## 9. Bước tiếp theo / phụ thuộc

- **Phụ thuộc ngoài:** LibreOffice cài trên máy đích (preview); MS Word để mở bản cuối (tùy chọn).
- **Chuyển tiếp:** `/ck:plan` để chia phase (khuyến nghị mode mặc định — đây là app mới, không phải refactor logic nghiệp vụ cần khóa test trước).

## 10. Rà soát cuối — điểm vướng & hướng xử lý (đã chốt default)

Các gap phát hiện khi rà soát, tất cả có default hợp lý, không blocker:

| # | Điểm vướng | Hướng xử lý (default) |
|---|---|---|
| 1 | `get_all_records()` yêu cầu header dòng 1, không trùng/không rỗng | `sheets_client` validate header; báo lỗi rõ nếu trùng/rỗng |
| 2 | Sheet thiếu cột đã map / thiếu cột gom nhóm / cột `Hồ sơ số` | Validate trước khi generate; cột map thiếu → đổ rỗng + cảnh báo, không crash |
| 3 | Ảnh chữ ký thiếu / path sai | Template dùng `{% if anh_chu_ky %}`; renderer bỏ ảnh, chừa khoảng trắng (giống fallback bản cũ) |
| 4 | Tên file output chứa ký tự cấm Windows `\ / : * ? " < > \|` hoặc trùng | Sanitize tên; trùng thì thêm hậu tố `_2`. Giữ quy ước `MLHS_<số>` (trích số từ `ho_so_so`) |
| 5 | **Sửa layout/độ rộng cột** trong settings ↔ đã bake trong template.docx | **CHỐT: layout (cột, độ rộng, viền) sửa trong Word ở `template.docx`; settings UI chỉ sửa text (cơ quan, người ký, tiêu đề), toggle footer, ảnh, mapping.** Tránh phải patch grid docx (YAGNI) |
| 6 | Đổi/ thêm template → placeholder khác | Introspect bằng docxtpl `get_undeclared_template_variables()` để dựng UI mapping động |
| 7 | `oauth2client` deprecated | Dùng `google.oauth2.service_account.Credentials` + `gspread.authorize`; scope **read-only** (`spreadsheets.readonly`) |
| 8 | LibreOffice không chạy 2 instance headless đồng thời / khi user đang mở LO | Preview & batch không chạy song song; dùng profile tạm `-env:UserInstallation`; timeout + báo lỗi |
| 9 | Excel exporter bám 7 cột cố định | Cho Excel theo `row_mapping` của style thay vì hardcode |
| 10 | Kiểu dữ liệu số (STT) từ sheet | jinja render số OK; ép `str` khi cần; giữ `number_format="@"` bên Excel |
| 11 | Preview chọn hồ sơ nào | Dropdown chọn giá trị cột gom nhóm; mặc định hồ sơ đầu |
| 12 | Giấy phép PyQt5 (GPL) / PyMuPDF (AGPL) | Công cụ nội bộ cơ quan → OK; ghi chú nếu sau này phân phối thương mại |

## 11. Câu hỏi còn treo (không chặn)

1. Số hồ sơ điển hình mỗi lần chạy — MVP QThread tuần tự, thêm multiprocessing sau nếu chậm (lưu ý freeze_support khi đóng exe).
2. Có cần fallback preview mở bằng Word khi máy đích thiếu LibreOffice không? (đã có `open_with_default`, bật nếu cần).
