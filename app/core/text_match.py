"""Chuẩn hóa & auto-match tên cột (bỏ dấu tiếng Việt) cho UI mapping (P5).

Tách riêng để test headless (không phụ thuộc PyQt5).
"""

from __future__ import annotations

import re
import unicodedata
from typing import Dict, List


def normalize_header(s: str) -> str:
    """Chuẩn hóa để so khớp: bỏ dấu tiếng Việt, lowercase, bỏ ký tự không chữ-số.

    Ví dụ: 'Số, ký hiệu văn bản' → 'sokyhieuvanban'; 'STT' → 'stt'.
    """
    # NFD tách dấu, bỏ ký tự combining; đ/Đ xử lý riêng vì không tách được.
    text = str(s).replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return re.sub(r"[^a-z0-9]", "", text.lower())


def auto_match(
    variables: List[str], current_map: Dict[str, str], headers: List[str]
) -> Dict[str, str]:
    """Gợi ý map biến docxtpl → tên cột sheet.

    Ưu tiên:
      1. Giá trị đang lưu (`current_map[var]`) nếu cột đó có trong `headers`.
      2. Header có dạng chuẩn hóa trùng cột đang lưu.
      3. Header có dạng chuẩn hóa trùng chính tên biến.
      4. '' nếu không khớp (user sửa tay).
    """
    norm_headers = {normalize_header(h): h for h in headers}
    header_set = set(headers)
    result: Dict[str, str] = {}

    for var in variables:
        current = current_map.get(var, "")
        if current and current in header_set:
            result[var] = current
            continue
        if current and normalize_header(current) in norm_headers:
            result[var] = norm_headers[normalize_header(current)]
            continue
        by_var = norm_headers.get(normalize_header(var))
        result[var] = by_var or ""
    return result
