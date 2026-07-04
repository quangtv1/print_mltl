"""Đọc file Excel `.xlsx` đầu vào (thay nguồn Google Sheet cũ).

`list_sheets` liệt kê tên sheet; `read_df` đọc 1 sheet thành DataFrame (ô rỗng/NaN
→ chuỗi rỗng, ép chuỗi an toàn cho render). `validate_headers` bắt sớm các header
rỗng/`Unnamed`/trùng để mapping không lệch. Thông báo lỗi bằng tiếng Việt.
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path
from typing import List

import pandas as pd

# Cận an toàn tránh treo GUI khi mở file khổng lồ (Red Team minor). Vượt ngưỡng →
# báo lỗi rõ thay vì đọc hết vào RAM.
MAX_ROWS = 200_000


class ExcelReadError(Exception):
    """Lỗi khi đọc/validate file Excel (thông báo tiếng Việt)."""


def list_sheets(path) -> List[str]:
    """Liệt kê tên các sheet trong file `.xlsx`."""
    p = Path(path)
    if not p.is_file():
        raise ExcelReadError(f"Không tìm thấy file Excel: {path}")
    try:
        with pd.ExcelFile(p, engine="openpyxl") as xls:
            return list(xls.sheet_names)
    except Exception as e:  # noqa: BLE001 - gói lỗi openpyxl thành lỗi tiếng Việt
        raise ExcelReadError(f"Không đọc được file Excel '{p.name}': {e}") from e


def read_df(path, sheet) -> pd.DataFrame:
    """Đọc 1 sheet → DataFrame; ô rỗng/NaN → '' và ép chuỗi an toàn cho render.

    Validate header ngay sau khi đọc (rỗng/`Unnamed`/trùng → lỗi rõ).
    """
    p = Path(path)
    if not p.is_file():
        raise ExcelReadError(f"Không tìm thấy file Excel: {path}")

    # Validate trên header GỐC trước: pandas âm thầm đổi cột trùng → `.1` và cột
    # trống → `Unnamed: N`, nên phải soát hàng tiêu đề thô để không bỏ sót.
    validate_headers(_raw_headers(p, sheet))

    try:
        df = pd.read_excel(p, sheet_name=sheet, engine="openpyxl", dtype=object)
    except Exception as e:  # noqa: BLE001 - gói lỗi openpyxl thành lỗi tiếng Việt
        raise ExcelReadError(
            f"Không đọc được sheet '{sheet}' trong '{p.name}': {e}"
        ) from e

    if len(df) > MAX_ROWS:
        raise ExcelReadError(
            f"File quá lớn: {len(df)} dòng (giới hạn {MAX_ROWS}). "
            "Hãy tách nhỏ dữ liệu trước khi xử lý."
        )

    # Ô rỗng/NaN → '' và ép chuỗi (elementwise để tránh downcast object của
    # fillna+astype), giúp render không ra 'nan'/số lệch định dạng.
    return df.map(lambda v: "" if pd.isna(v) else str(v))


def _raw_headers(path, sheet) -> List[str]:
    """Đọc hàng tiêu đề THÔ (chưa qua dedup của pandas). Ô trống → ''."""
    try:
        head = pd.read_excel(
            path, sheet_name=sheet, engine="openpyxl", header=None, nrows=1
        )
    except Exception as e:  # noqa: BLE001 - gói lỗi thành lỗi tiếng Việt
        raise ExcelReadError(
            f"Không đọc được tiêu đề sheet '{sheet}': {e}"
        ) from e
    if head.empty:
        return []
    return ["" if pd.isna(v) else str(v).strip() for v in head.iloc[0].tolist()]


def validate_headers(headers: List[str]) -> None:
    """Bắt lỗi header rỗng/trùng trên danh sách tiêu đề GỐC; hợp lệ → không làm gì.

    Nhận list tên cột thô (từ `_raw_headers`), không phải DataFrame — để soát trước
    khi pandas kịp mangle tên trùng/trống.
    """
    if not headers:
        raise ExcelReadError("Sheet không có dòng tiêu đề.")

    empty = [i + 1 for i, c in enumerate(headers) if str(c).strip() == ""]
    if empty:
        raise ExcelReadError(
            "Có cột thiếu tiêu đề ở vị trí: "
            + ", ".join(map(str, empty))
            + ". Hãy điền tiêu đề cho mọi cột."
        )

    dup = [name for name, n in Counter(headers).items() if n > 1]
    if dup:
        raise ExcelReadError(
            "Có tiêu đề cột bị trùng: " + ", ".join(dup) + ". Hãy đặt tên cột duy nhất."
        )
