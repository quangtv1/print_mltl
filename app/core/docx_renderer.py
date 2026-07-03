"""Engine sinh docx từ 1 nhóm hồ sơ qua docxtpl (nhồi biến theo mapping).

**Process-safe** cho ProcessPoolExecutor (P6): `render_group` chỉ nhận dữ liệu
picklable (dict + list-of-dict + str), tự mở `DocxTemplate` bên trong tiến trình
con (DocxTemplate/InlineImage KHÔNG pickle được). Render bắt buộc
`autoescape=True` để dữ liệu chứa `& < >` không phá cấu trúc XML.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import Any, Dict, List, Tuple

import pandas as pd

from app.core.platform_utils import resource_base_dir
from app.models.style import StyleConfig

# Ký tự bị cấm trong tên file trên Windows.
_WINDOWS_FORBIDDEN = r'\/:*?"<>|'
_FORBIDDEN_RE = re.compile(f"[{re.escape(_WINDOWS_FORBIDDEN)}]")


def group_dataframe(style: StyleConfig, df: pd.DataFrame) -> Dict[Any, pd.DataFrame]:
    """Nhóm `df` theo `style.grouping_column`, giữ thứ tự xuất hiện đầu tiên.

    Giữ thứ tự (dùng `unique()`) để khớp output script cũ (groupby mặc định sắp xếp).
    """
    col = style.grouping_column
    if col not in df.columns:
        raise KeyError(
            f"Không có cột gom nhóm '{col}' trong dữ liệu. "
            f"Các cột hiện có: {', '.join(map(str, df.columns))}"
        )
    return {val: df[df[col] == val] for val in df[col].unique()}


def df_to_records(df: pd.DataFrame) -> List[Dict[str, Any]]:
    """DataFrame → list dict picklable, ô rỗng/NaN → '' (tránh render 'nan')."""
    return df.fillna("").astype(object).to_dict("records")


def _resolve_ho_so_so(style: StyleConfig, records: List[Dict[str, Any]]) -> str:
    """Lấy giá trị `ho_so_so` (và các document field) từ hàng đầu của nhóm."""
    if not records:
        return ""
    col = style.document_fields.get("ho_so_so")
    if col and col in records[0]:
        return str(records[0][col])
    return ""


def _document_field_values(
    style: StyleConfig, records: List[Dict[str, Any]]
) -> Dict[str, str]:
    """Map biến document field (vd `ho_so_so`) → giá trị từ hàng đầu của nhóm."""
    first = records[0] if records else {}
    return {
        var: str(first.get(col, "")) for var, col in style.document_fields.items()
    }


def _resolve_signature_path(style: StyleConfig) -> Path | None:
    """Đường dẫn tuyệt đối tới ảnh chữ ký, hoặc None nếu không có/không tồn tại."""
    raw = str(style.settings.get("anh_chu_ky_path", "")).strip()
    if not raw:
        return None
    p = Path(raw)
    if not p.is_absolute():
        p = resource_base_dir() / p
    return p if p.is_file() else None


def build_context(style: StyleConfig, tpl, records: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Dựng context render: settings + document fields + rows + ảnh chữ ký.

    `tpl` là `DocxTemplate` (cần cho `InlineImage`). `rows` theo `row_mapping`
    (biến docxtpl -> giá trị ô), giá trị ép `str` để tránh lệch định dạng số.
    """
    from docx.shared import Inches
    from docxtpl import InlineImage

    ctx: Dict[str, Any] = {}
    # settings text (các khóa thừa như font_name/footer_* vô hại với jinja).
    ctx.update(style.settings)
    # document fields (ho_so_so...) lấy từ dữ liệu.
    ctx.update(_document_field_values(style, records))

    ctx["rows"] = [
        {var: str(rec.get(col, "")) for var, col in style.row_mapping.items()}
        for rec in records
    ]

    sig = _resolve_signature_path(style)
    ctx["anh_chu_ky"] = (
        InlineImage(tpl, str(sig), width=Inches(1.0)) if sig else None
    )
    return ctx


def render_group(style_dict: Dict[str, Any], records: List[Dict[str, Any]], out_path) -> str:
    """Render 1 nhóm → docx tại `out_path`. **Process-safe** (mở template bên trong).

    Args:
        style_dict: `StyleConfig.to_dict()` (picklable).
        records: list dict các hàng của nhóm (picklable).
        out_path: đường dẫn file docx đầu ra.
    Returns: đường dẫn file đã lưu.
    """
    from docxtpl import DocxTemplate

    style = StyleConfig.from_dict(style_dict)
    # `_style_dir` do main process nhúng (build_task) — không phải field dataclass.
    style._style_dir = style_dict.get("_style_dir")  # type: ignore[attr-defined]
    template_path = _template_path(style)
    tpl = DocxTemplate(str(template_path))
    ctx = build_context(style, tpl, records)
    tpl.render(ctx, autoescape=True)

    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)
    tpl.save(str(out))
    return str(out)


def _template_path(style: StyleConfig) -> Path:
    """Đường dẫn template.docx của style (đọc từ style_dir nhúng trong dict).

    `style_dict` phải kèm khóa `_style_dir` (đường dẫn thư mục style) do main
    process gắn trước khi dispatch — vì worker không biết style nằm ở đâu.
    """
    style_dir = getattr(style, "_style_dir", None)
    if style_dir:
        return Path(style_dir) / style.template_file
    # fallback: template.docx cạnh styles_root (hiếm khi tới đây).
    from app.core.platform_utils import styles_root

    return styles_root() / style.template_file


def sanitize_filename(name: str) -> str:
    """Bỏ ký tự cấm Windows, trim khoảng trắng/dấu chấm cuối. Rỗng → 'unnamed'."""
    cleaned = _FORBIDDEN_RE.sub("", str(name)).strip().rstrip(". ")
    return cleaned or "unnamed"


def resolve_output_name(style: StyleConfig, ho_so_so: str) -> str:
    """Sinh tên file từ `output_filename_pattern`, trích số từ `ho_so_so`.

    Khớp bản cũ: lấy cụm số đầu tiên trong `ho_so_so` (fallback 'UNKNOWN').
    """
    match = re.search(r"\d+", str(ho_so_so))
    clean = match.group(0) if match else "UNKNOWN"
    pattern = style.output_filename_pattern or "MLHS_{ho_so_so}.docx"
    name = pattern.replace("{ho_so_so}", clean)
    return sanitize_filename(name)


def make_unique_path(out_dir, filename: str, used: set) -> Path:
    """Trả về đường dẫn không trùng trong `used` (thêm hậu tố `_2`, `_3`...).

    `used` được cập nhật tại chỗ — main process gọi tuần tự khi dựng task list,
    nên tên xác định trước, không phụ thuộc thứ tự chạy song song.
    """
    stem = Path(filename).stem
    suffix = Path(filename).suffix or ".docx"
    candidate = filename
    n = 2
    while candidate.lower() in used:
        candidate = f"{stem}_{n}{suffix}"
        n += 1
    used.add(candidate.lower())
    return Path(out_dir) / candidate


def build_task(
    style: StyleConfig, style_dir, records: List[Dict[str, Any]], out_path
) -> Tuple[Dict[str, Any], List[Dict[str, Any]], str]:
    """Đóng gói 1 task cho worker: (style_dict + _style_dir, records, out_path).

    Nhúng `_style_dir` vào dict để worker biết template ở đâu (xem `_template_path`).
    """
    d = style.to_dict()
    d["_style_dir"] = str(style_dir)
    return d, records, str(out_path)
