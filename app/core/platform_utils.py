"""Tiện ích đa nền (macOS/Windows/Linux): mở file bằng app mặc định + resolve
đường dẫn tài nguyên (`styles/`) cho dev và PyInstaller frozen.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path


def open_with_default(path) -> None:
    """Mở file/thư mục bằng ứng dụng mặc định của OS."""
    p = str(path)
    if sys.platform == "win32":
        os.startfile(p)  # type: ignore[attr-defined]  # chỉ có trên Windows
    elif sys.platform == "darwin":
        subprocess.run(["open", p], check=False)
    else:
        subprocess.run(["xdg-open", p], check=False)


def resource_base_dir() -> Path:
    """Thư mục gốc chứa tài nguyên (`styles/`, ảnh…).

    - Dev: thư mục gốc project (cha của `app/`).
    - Frozen (PyInstaller): thư mục chứa exe, để `styles/` đọc/ghi được cạnh exe.
    """
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[2]


def styles_root() -> Path:
    """Đường dẫn thư mục `styles/` (dev hoặc frozen)."""
    return resource_base_dir() / "styles"
