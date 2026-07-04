"""Đọc/ghi/liệt kê `style.json`.

Một "style" = một thư mục con trong `styles/` chứa `style.json` + `template.docx`.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import List

from app.models.style import StyleConfig

STYLE_FILENAME = "style.json"

# Khóa bắt buộc phải có trong style.json (validate sớm để báo lỗi rõ).
# `template_file` đã bỏ khỏi bắt buộc: engine Native Qt dùng `template_html`
# nhúng trong style.json, không cần file `.docx` rời (style cũ vẫn đọc được).
REQUIRED_KEYS = ("grouping_column", "row_mapping")


class StyleConfigError(Exception):
    """Lỗi khi đọc/validate style.json."""


def load_style(style_dir) -> StyleConfig:
    """Đọc `style.json` trong `style_dir` → `StyleConfig`, validate khóa bắt buộc."""
    path = Path(style_dir) / STYLE_FILENAME
    if not path.is_file():
        raise StyleConfigError(f"Không tìm thấy {STYLE_FILENAME} trong: {style_dir}")
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except json.JSONDecodeError as e:
        raise StyleConfigError(f"{path} không phải JSON hợp lệ: {e}") from e

    missing = [k for k in REQUIRED_KEYS if k not in data]
    if missing:
        raise StyleConfigError(
            f"{path} thiếu khóa bắt buộc: {', '.join(missing)}"
        )
    return StyleConfig.from_dict(data)


def save_style(cfg: StyleConfig, style_dir) -> Path:
    """Ghi `cfg` ra `style.json` trong `style_dir` (tạo thư mục nếu cần)."""
    d = Path(style_dir)
    d.mkdir(parents=True, exist_ok=True)
    path = d / STYLE_FILENAME
    with open(path, "w", encoding="utf-8") as f:
        json.dump(cfg.to_dict(), f, ensure_ascii=False, indent=2)
    return path


def list_styles(styles_root) -> List[str]:
    """Liệt kê tên thư mục style con (có `style.json`) trong `styles_root`."""
    root = Path(styles_root)
    if not root.is_dir():
        return []
    return sorted(
        p.name for p in root.iterdir() if (p / STYLE_FILENAME).is_file()
    )
