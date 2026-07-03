"""Tiện ích đa nền (macOS/Windows/Linux): tìm LibreOffice, mở file mặc định,
tạo thư mục tạm, và resolve đường dẫn tài nguyên (dev vs PyInstaller frozen).
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import List, Optional

# Đường dẫn `soffice` chuẩn theo OS khi không có trong PATH.
_SOFFICE_CANDIDATES = {
    "darwin": [
        "/Applications/LibreOffice.app/Contents/MacOS/soffice",
    ],
    "win32": [
        r"C:\Program Files\LibreOffice\program\soffice.exe",
        r"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
    ],
    "linux": [
        "/usr/bin/soffice",
        "/usr/bin/libreoffice",
        "/snap/bin/libreoffice",
    ],
}


def resolve_soffice(override: Optional[str] = None) -> Optional[str]:
    """Trả về đường dẫn tới `soffice`, hoặc None nếu không tìm thấy.

    Ưu tiên: override do user chọn → PATH (`shutil.which`) → path chuẩn theo OS.
    """
    if override:
        p = Path(override)
        return str(p) if p.exists() else None

    for name in ("soffice", "libreoffice"):
        found = shutil.which(name)
        if found:
            return found

    for cand in _SOFFICE_CANDIDATES.get(sys.platform, []):
        if Path(cand).exists():
            return cand
    return None


def open_with_default(path) -> None:
    """Mở file bằng ứng dụng mặc định của OS (fallback khi thiếu LibreOffice)."""
    p = str(path)
    if sys.platform == "win32":
        os.startfile(p)  # type: ignore[attr-defined]  # chỉ có trên Windows
    elif sys.platform == "darwin":
        subprocess.run(["open", p], check=False)
    else:
        subprocess.run(["xdg-open", p], check=False)


def make_temp_dir(prefix: str = "mltl_") -> Path:
    """Tạo thư mục tạm (caller tự dọn). Trả về `Path`."""
    return Path(tempfile.mkdtemp(prefix=prefix))


def resource_base_dir() -> Path:
    """Thư mục gốc chứa tài nguyên (`styles/`, ảnh...).

    - Dev: thư mục gốc project (cha của `app/`).
    - Frozen (PyInstaller): thư mục chứa exe, để `styles/` đọc/ghi được cạnh exe.
    """
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[2]


def styles_root() -> Path:
    """Đường dẫn thư mục `styles/` (dev hoặc frozen)."""
    return resource_base_dir() / "styles"
