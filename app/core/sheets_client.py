"""Kết nối Google Sheet bằng service-account (read-only), lấy worksheet + DataFrame.

Thay `oauth2client` (deprecated) bằng `google.oauth2.service_account`. Lỗi được
bọc thành thông điệp tiếng Việt rõ ràng cho UI (creds sai, không quyền, URL hỏng,
sheet trống). Tái dùng logic `lay_du_lieu_google_sheet` cũ (get_all_records + strip).
"""

from __future__ import annotations

from typing import List, Optional

import pandas as pd

# Scope tối thiểu: chỉ đọc Sheets + Drive (Drive cần cho open_by_url).
READONLY_SCOPES = [
    "https://www.googleapis.com/auth/spreadsheets.readonly",
    "https://www.googleapis.com/auth/drive.readonly",
]


class SheetsError(Exception):
    """Lỗi thao tác Google Sheet với thông điệp tiếng Việt cho UI."""


def authorize(creds_path: str):
    """Tạo `gspread.Client` từ file service-account `.json` (scope read-only)."""
    import gspread
    from google.oauth2.service_account import Credentials

    try:
        creds = Credentials.from_service_account_file(
            creds_path, scopes=READONLY_SCOPES
        )
    except FileNotFoundError as e:
        raise SheetsError(f"Không tìm thấy file khóa: {creds_path}") from e
    except ValueError as e:
        raise SheetsError(
            "File khóa không hợp lệ (không phải service-account JSON đúng định dạng)."
        ) from e
    except Exception as e:  # noqa: BLE001 - gói mọi lỗi đọc key
        raise SheetsError(f"Không đọc được file khóa: {e}") from e

    return gspread.authorize(creds)


def open_spreadsheet(client, url: str):
    """Mở spreadsheet theo URL. Lỗi → thông điệp gợi ý chia sẻ quyền/URL."""
    from gspread.exceptions import APIError, NoValidUrlKeyFound, SpreadsheetNotFound

    try:
        return client.open_by_url(url)
    except (NoValidUrlKeyFound, ValueError) as e:
        raise SheetsError("URL Google Sheet không hợp lệ.") from e
    except SpreadsheetNotFound as e:
        raise SheetsError(
            "Không tìm thấy hoặc không có quyền xem bảng tính. "
            "Hãy chia sẻ sheet cho email của service-account (quyền Xem)."
        ) from e
    except APIError as e:
        raise SheetsError(_api_error_message(e)) from e


def list_worksheets(spreadsheet) -> List[str]:
    """Danh sách tên worksheet trong spreadsheet."""
    from gspread.exceptions import APIError

    try:
        return [ws.title for ws in spreadsheet.worksheets()]
    except APIError as e:
        raise SheetsError(_api_error_message(e)) from e


def read_df(spreadsheet, ws_name: str) -> Optional[pd.DataFrame]:
    """Đọc worksheet → DataFrame (strip header). None nếu sheet trống.

    `get_all_records` yêu cầu header duy nhất ở dòng 1 → validate rồi báo lỗi cụ thể.
    """
    from gspread.exceptions import APIError, GSpreadException, WorksheetNotFound

    try:
        ws = spreadsheet.worksheet(ws_name)
    except WorksheetNotFound as e:
        raise SheetsError(f"Không tìm thấy worksheet: {ws_name}") from e
    except APIError as e:
        raise SheetsError(_api_error_message(e)) from e

    try:
        records = ws.get_all_records()
    except GSpreadException as e:
        # get_all_records ném lỗi khi header trùng/rỗng.
        raise SheetsError(
            f"Header của sheet '{ws_name}' bị trùng hoặc rỗng. "
            f"Hãy sửa dòng tiêu đề để mỗi cột có tên duy nhất, không để trống. ({e})"
        ) from e
    except APIError as e:
        raise SheetsError(_api_error_message(e)) from e

    if not records:
        return None

    df = pd.DataFrame(records)
    df.columns = df.columns.str.strip()
    _validate_headers(list(df.columns), ws_name)
    return df


def get_headers(df: pd.DataFrame) -> List[str]:
    """Danh sách tên cột (header) của DataFrame."""
    return [str(c) for c in df.columns]


def _validate_headers(headers: List[str], ws_name: str) -> None:
    """Phát hiện cột rỗng/trùng sau khi strip → báo lỗi rõ."""
    stripped = [h.strip() for h in headers]
    if any(h == "" for h in stripped):
        raise SheetsError(
            f"Sheet '{ws_name}' có cột tiêu đề bị trống. Hãy đặt tên cho mọi cột."
        )
    seen = set()
    dups = {h for h in stripped if h in seen or seen.add(h)}
    if dups:
        raise SheetsError(
            f"Sheet '{ws_name}' có cột tiêu đề trùng: {', '.join(sorted(dups))}."
        )


def _api_error_message(err) -> str:
    """Đổi APIError của Google thành thông điệp tiếng Việt ngắn gọn."""
    text = str(err)
    if "PERMISSION_DENIED" in text or "403" in text:
        return (
            "Không có quyền truy cập. Hãy chia sẻ sheet cho email service-account "
            "(quyền Xem) và bật Google Sheets/Drive API cho project."
        )
    if "RESOURCE_EXHAUSTED" in text or "429" in text:
        return "Vượt hạn mức truy vấn Google API, thử lại sau ít phút."
    return f"Lỗi Google API: {text}"
