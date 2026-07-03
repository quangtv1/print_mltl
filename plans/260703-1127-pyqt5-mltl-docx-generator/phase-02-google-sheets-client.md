---
phase: 2
title: "Google Sheets Client"
status: done
effort: ""
---

# Phase 2: Google Sheets Client

## Overview
Module kết nối Google Sheet bằng service-account do user chọn, liệt kê worksheet, lấy cột + DataFrame. Thay `oauth2client` (deprecated) bằng `google-auth`, scope read-only. Xử lý bảo mật key.

## Requirements
- Functional: authorize từ file `.json`; `list_worksheets(url)`; `get_headers(ws)`; `read_df(ws)`; validate header (không rỗng/không trùng).
- Non-functional: lỗi rõ ràng (creds sai, không quyền, URL hỏng, sheet trống); scope tối thiểu.

## Architecture
```
app/core/sheets_client.py
  authorize(creds_path) -> gspread.Client   # google.oauth2.service_account.Credentials
  open_spreadsheet(client, url)
  list_worksheets(ss) -> [str]
  read_df(ss, ws_name) -> pandas.DataFrame   # strip header, '' cho ô rỗng
  get_headers(df) -> [str]
```
Tái dùng logic `lay_du_lieu_google_sheet` cũ (`get_all_records` + strip cột), thêm liệt kê worksheet + validate.

## Related Code Files
- Create: `app/core/sheets_client.py`
- Reuse (logic): `print_mltl_parallel.py::lay_du_lieu_google_sheet`
- Security: thu hồi key trong `get_link_pdf_.json`, tạo key mới; **không** commit/bundle; app đọc key từ đường dẫn user chọn.

## Implementation Steps
1. `authorize(creds_path)`: `Credentials.from_service_account_file(path, scopes=[".../spreadsheets.readonly", ".../drive.readonly"])` → `gspread.authorize`.
2. `list_worksheets`: `open_by_url(url).worksheets()` → tên.
3. `read_df`: `worksheet.get_all_records()` → DataFrame, `df.columns.str.strip()`; nếu trống trả None + thông báo.
4. Validate header: phát hiện cột trùng/rỗng (giới hạn của `get_all_records`), báo lỗi cụ thể.
5. Bọc lỗi mạng/xác thực thành thông điệp tiếng Việt cho UI.

## Success Criteria
- [ ] Chọn key hợp lệ + URL mẫu → `list_worksheets` trả đúng danh sách, `read_df` khớp dữ liệu (đối chiếu script cũ).
- [ ] Key sai / URL hỏng / sheet trống → báo lỗi rõ, không traceback.
- [ ] Không còn phụ thuộc `oauth2client`.

## Risk Assessment
- `get_all_records` yêu cầu header duy nhất dòng 1 → validate sớm, hướng dẫn user sửa sheet.
- Chia sẻ Drive: service account phải được share quyền xem sheet → thông điệp lỗi gợi ý.
