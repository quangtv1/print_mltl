"""Preview docx→PDF qua LibreOffice headless, rồi render trang thành ảnh PNG.

Thiếu LibreOffice → `LibreOfficeNotFound` (UI bắt để fallback mở bằng Word).
Mỗi lần convert dùng profile tạm riêng để không xung đột instance soffice.
"""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path
from typing import List, Optional

from app.core.platform_utils import make_temp_dir, resolve_soffice

CONVERT_TIMEOUT_SEC = 120


class PdfPreviewError(Exception):
    """Lỗi nền tảng cho pipeline preview."""


class LibreOfficeNotFound(PdfPreviewError):
    """Không tìm thấy soffice → UI gợi ý cài hoặc fallback mở bằng Word."""


class ConversionTimeout(PdfPreviewError):
    """soffice chạy quá `CONVERT_TIMEOUT_SEC`."""


class ConversionFailed(PdfPreviewError):
    """soffice chạy xong nhưng không sinh ra PDF."""


def docx_to_pdf(docx_path, out_dir, soffice: Optional[str] = None) -> Path:
    """Convert docx → PDF bằng LibreOffice headless. Trả về đường dẫn PDF.

    Dùng profile tạm (`-env:UserInstallation`) để tránh xung đột khi soffice
    đang mở. `out_dir` là nơi soffice ghi PDF ra (caller tự dọn).
    """
    soffice_bin = resolve_soffice(soffice)
    if not soffice_bin:
        raise LibreOfficeNotFound(
            "Không tìm thấy LibreOffice (soffice). Cài LibreOffice để xem preview, "
            "hoặc mở file bằng Word."
        )

    docx_path = Path(docx_path)
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    profile_dir = make_temp_dir(prefix="lo_profile_")

    try:
        cmd = [
            soffice_bin,
            "--headless",
            "--norestore",
            f"-env:UserInstallation=file://{profile_dir}",
            "--convert-to",
            "pdf",
            "--outdir",
            str(out_dir),
            str(docx_path),
        ]
        try:
            proc = subprocess.run(
                cmd,
                capture_output=True,
                timeout=CONVERT_TIMEOUT_SEC,
                check=False,
            )
        except subprocess.TimeoutExpired as e:
            raise ConversionTimeout(
                f"Chuyển đổi PDF quá {CONVERT_TIMEOUT_SEC}s, đã hủy."
            ) from e

        pdf_path = out_dir / (docx_path.stem + ".pdf")
        if not pdf_path.is_file():
            err = (proc.stderr or b"").decode("utf-8", "replace").strip()
            raise ConversionFailed(
                f"LibreOffice không tạo được PDF. {err or 'Không rõ lỗi.'}"
            )
        return pdf_path
    finally:
        shutil.rmtree(profile_dir, ignore_errors=True)


def render_pdf_pages(pdf_path, dpi: int = 120) -> List[bytes]:
    """Render mỗi trang PDF thành PNG bytes (UI đổ vào QPixmap ở P5)."""
    import fitz  # PyMuPDF

    images: List[bytes] = []
    doc = fitz.open(str(pdf_path))
    try:
        for page in doc:
            pix = page.get_pixmap(dpi=dpi)
            images.append(pix.tobytes("png"))
    finally:
        doc.close()
    return images


def preview_docx(docx_path, dpi: int = 120, soffice: Optional[str] = None) -> List[bytes]:
    """Tiện ích: docx → PDF (thư mục tạm) → list ảnh PNG. Tự dọn file tạm.

    Ném `LibreOfficeNotFound` nếu thiếu soffice (UI fallback mở bằng Word).
    """
    work_dir = make_temp_dir(prefix="preview_")
    try:
        pdf_path = docx_to_pdf(docx_path, work_dir, soffice=soffice)
        return render_pdf_pages(pdf_path, dpi=dpi)
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)
