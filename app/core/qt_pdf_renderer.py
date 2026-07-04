"""Engine render Native Qt: `template_html` + 1 nhóm record → PDF A4.

Luồng: thay token thân (doc fields/settings/auto — **escape mọi giá trị Excel**,
1-pass regex), dựng `QTextDocument`, **nhân dòng bảng ở tầng object** (QTextTable,
không cắt chuỗi HTML), lặp header bảng qua trang (`setHeaderRowCount`), in
**page-by-page** để vẽ footer "Trang x/y".

Tách rõ 2 tầng để P5 preview tái dùng:
- `build_document(...)` → `QTextDocument` (không in) — dùng cho preview & PDF.
- `render_page_image(...)` → `QImage` 1 trang (cùng clip/translate/footer) — preview phân trang.
- `render_group_pdf(...)` → ghi PDF (dùng lại build + vẽ từng trang).

Chạy được trong QThread (không đụng widget), miễn có `QApplication` sống.
"""

from __future__ import annotations

import html
import re
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List

import pandas as pd

from PyQt5.QtCore import QMarginsF, QRectF, QSizeF, Qt
from PyQt5.QtGui import (
    QColor,
    QFont,
    QImage,
    QPageLayout,
    QPageSize,
    QPainter,
    QPdfWriter,
    QTextDocument,
    QTextTable,
)

from app.core import variables as V
from app.models.style import StyleConfig

# ---------------------------------------------------------------------------
# Token substitution (1-pass, escape mọi giá trị — Red Team #8/#9)
# ---------------------------------------------------------------------------

_TOKEN_RE = re.compile(r"\{(\w+)\}")

# Ký tự cấm trong tên file Windows.
_WINDOWS_FORBIDDEN = r'\/:*?"<>|'
_FORBIDDEN_RE = re.compile(f"[{re.escape(_WINDOWS_FORBIDDEN)}]")

# Lề A4 mặc định (mm): trái/phải/trên/dưới.
_MARGIN_MM = QMarginsF(19.0, 13.0, 13.0, 13.0)


def _esc(v: Any) -> str:
    """Escape giá trị để chèn an toàn vào HTML (tương đương autoescape cũ)."""
    return html.escape(str(v), quote=True)


def substitute(text: str, values: Dict[str, str], *, escape: bool) -> str:
    """Thay `{token}` **1 lần** bằng giá trị đã resolve.

    - Token footer-only (`{trang_so}`/`{tong_so_trang}`) trong THÂN → xoá (Red Team #9).
    - Token có trong `values` → thay (escape HTML nếu `escape`).
    - Token lạ → giữ nguyên literal (dễ soi lỗi; validate gate ở P5 chặn phát hành).
    """
    def repl(m: "re.Match[str]") -> str:
        key = m.group(1)
        if key in V.FOOTER_VARS:
            return ""
        if key in values:
            return _esc(values[key]) if escape else str(values[key])
        return m.group(0)

    return _TOKEN_RE.sub(repl, text)


# ---------------------------------------------------------------------------
# Grouping / records (ported từ docx_renderer, tên symbol giữ nguyên — Red Team #7)
# ---------------------------------------------------------------------------


def group_dataframe(style: StyleConfig, df: pd.DataFrame) -> Dict[Any, pd.DataFrame]:
    """Nhóm `df` theo `style.grouping_column`, giữ thứ tự xuất hiện đầu tiên."""
    col = style.grouping_column
    if col not in df.columns:
        raise KeyError(
            f"Không có cột gom nhóm '{col}' trong dữ liệu. "
            f"Các cột hiện có: {', '.join(map(str, df.columns))}"
        )
    return {val: df[df[col] == val] for val in df[col].unique()}


def df_to_records(df: pd.DataFrame) -> List[Dict[str, Any]]:
    """DataFrame → list dict, ô rỗng/NaN → ''."""
    return df.fillna("").astype(object).to_dict("records")


def _document_field_values(
    style: StyleConfig, records: List[Dict[str, Any]]
) -> Dict[str, str]:
    """Map biến document field (vd `ho_so_so`) → giá trị từ hàng đầu của nhóm."""
    first = records[0] if records else {}
    return {
        var: str(first.get(col, "")) for var, col in style.document_fields.items()
    }


def resolve_ho_so_so(style: StyleConfig, records: List[Dict[str, Any]]) -> str:
    """Giá trị `ho_so_so` (document field 'ho_so_so') từ hàng đầu của nhóm."""
    if not records:
        return ""
    col = style.document_fields.get("ho_so_so")
    if col and col in records[0]:
        return str(records[0][col])
    return ""


def build_context(
    style: StyleConfig, records: List[Dict[str, Any]], stt_file: int
) -> Dict[str, str]:
    """Context token THÂN (không gồm cột hàng): settings + doc fields + auto vars."""
    ctx: Dict[str, str] = {}
    for k in V.SETTINGS_BODY_KEYS:
        if k in style.settings:
            ctx[k] = str(style.settings[k])
    ctx.update(_document_field_values(style, records))
    ctx["stt_file"] = str(stt_file)
    ctx["ngay_gio"] = datetime.now().strftime("%d/%m/%Y %H:%M")
    return ctx


def _row_values(style: StyleConfig, rec: Dict[str, Any]) -> Dict[str, str]:
    """Giá trị cột hàng: biến row_mapping → ô tương ứng (chuỗi)."""
    return {var: str(rec.get(col, "")) for var, col in style.row_mapping.items()}


# ---------------------------------------------------------------------------
# Filename (format_output_name VIẾT MỚI — Red Team #1)
# ---------------------------------------------------------------------------


def sanitize_filename(name: str) -> str:
    """Bỏ ký tự cấm Windows, trim khoảng trắng/dấu chấm cuối. Rỗng → 'unnamed'."""
    cleaned = _FORBIDDEN_RE.sub("", str(name)).strip().rstrip(". ")
    return cleaned or "unnamed"


def format_output_name(pattern: str, ctx: Dict[str, str], stem_max: int = 120) -> str:
    """Expand ĐẦY ĐỦ token trong `pattern` → sanitize → cắt độ dài an toàn.

    Khác `resolve_output_name` cũ (chỉ thay `{ho_so_so}`): thay mọi token có trong
    `ctx` (`{ho_so_so}`, `{stt_file}`, `{ngay_gio}`…), xoá token còn sót, cắt stem.
    """
    filled = substitute(pattern or "MLHS_{ho_so_so}.pdf", ctx, escape=False)
    filled = _TOKEN_RE.sub("", filled)  # token lạ còn lại → bỏ
    name = sanitize_filename(filled)
    p = Path(name)
    suffix = p.suffix or ".pdf"
    stem = p.stem[:stem_max].strip() or "unnamed"
    return stem + suffix


def make_unique_path(out_dir, filename: str, used: set) -> Path:
    """Đường dẫn không trùng trong `used` (hậu tố `_2`, `_3`...). Cập nhật `used`."""
    stem = Path(filename).stem
    suffix = Path(filename).suffix or ".pdf"
    candidate = filename
    n = 2
    while candidate.lower() in used:
        candidate = f"{stem}_{n}{suffix}"
        n += 1
    used.add(candidate.lower())
    return Path(out_dir) / candidate


# ---------------------------------------------------------------------------
# Data-table marker + nhân dòng ở tầng object (Red Team #15)
# ---------------------------------------------------------------------------


class TemplateError(Exception):
    """Template không hợp lệ (không tìm thấy bảng dữ liệu / dòng-mẫu)."""


def _iter_tables(doc: QTextDocument) -> List[QTextTable]:
    """Liệt kê mọi QTextTable trong tài liệu (duyệt frame con)."""
    tables: List[QTextTable] = []
    stack = [doc.rootFrame()]
    while stack:
        frame = stack.pop()
        for child in frame.childFrames():
            if isinstance(child, QTextTable):
                tables.append(child)
            stack.append(child)
    return tables


def _cell_text(table: QTextTable, row: int, col: int) -> str:
    """Văn bản trong 1 ô (gồm token `{...}` nếu có)."""
    cell = table.cellAt(row, col)
    cur = cell.firstCursorPosition()
    cur.setPosition(cell.lastCursorPosition().position(), cur.KeepAnchor)
    return cur.selectedText()


def _find_data_table(doc: QTextDocument, row_vars: List[str]) -> QTextTable:
    """Bảng dữ liệu = bảng ĐẦU TIÊN có ô chứa token cột (`{var}` với var∈row_vars)."""
    tokens = {f"{{{v}}}" for v in row_vars}
    for table in _iter_tables(doc):
        for r in range(table.rows()):
            joined = "".join(_cell_text(table, r, c) for c in range(table.columns()))
            if any(t in joined for t in tokens):
                return table
    raise TemplateError(
        "Không tìm thấy bảng dữ liệu (bảng chứa token cột như "
        f"{sorted(tokens)[:2]}…) trong mẫu."
    )


def _find_template_row(table: QTextTable, row_vars: List[str]) -> int:
    """Chỉ số dòng-mẫu = dòng chứa token cột. Bắt buộc **đúng 1** (Red Team #15)."""
    tokens = {f"{{{v}}}" for v in row_vars}
    matches = [
        r
        for r in range(table.rows())
        if any(
            t in "".join(_cell_text(table, r, c) for c in range(table.columns()))
            for t in tokens
        )
    ]
    if len(matches) != 1:
        raise TemplateError(
            f"Bảng dữ liệu phải có ĐÚNG 1 dòng-mẫu chứa token cột, "
            f"đang có {len(matches)}. Kiểm tra lại mẫu."
        )
    return matches[0]


def _fill_data_table(
    doc: QTextDocument,
    style: StyleConfig,
    records: List[Dict[str, Any]],
) -> None:
    """Nhân dòng-mẫu thành N dòng dữ liệu (object-level) + bật header lặp.

    Giữ định dạng/căn lề của dòng-mẫu từng cột (char + block format). 0 record →
    xoá dòng-mẫu (bảng chỉ còn header).
    """
    row_vars = V.row_tokens(style)
    if not row_vars:
        return
    table = _find_data_table(doc, row_vars)
    tr = _find_template_row(table, row_vars)
    ncols = table.columns()

    # Chụp "khuôn" từng cột của dòng-mẫu: text chứa token + định dạng char/block.
    tpl_text: List[str] = []
    tpl_char = []
    tpl_block = []
    for c in range(ncols):
        cell = table.cellAt(tr, c)
        cur = cell.firstCursorPosition()
        tpl_char.append(cur.charFormat())
        tpl_block.append(cur.blockFormat())
        tpl_text.append(_cell_text(table, tr, c))

    # Lặp header bảng qua trang: mọi dòng TRÊN dòng-mẫu là header (đa dòng header
    # cũng lặp đúng). Không biểu diễn được trong HTML → set lập trình (Review M2).
    if tr > 0:
        tfmt = table.format()
        tfmt.setHeaderRowCount(tr)
        table.setFormat(tfmt)

    if not records:
        table.removeRows(tr, 1)
        return

    # Chèn thêm (N-1) dòng ngay sau dòng-mẫu; rồi đổ dữ liệu vào tr..tr+N-1.
    if len(records) > 1:
        table.insertRows(tr + 1, len(records) - 1)

    for i, rec in enumerate(records):
        values = _row_values(style, rec)
        row = tr + i
        for c in range(ncols):
            cell = table.cellAt(row, c)
            cur = cell.firstCursorPosition()
            cur.setPosition(cell.lastCursorPosition().position(), cur.KeepAnchor)
            cur.removeSelectedText()
            cur.setBlockFormat(tpl_block[c])
            cur.setCharFormat(tpl_char[c])
            cur.insertText(substitute(tpl_text[c], values, escape=False), tpl_char[c])


# ---------------------------------------------------------------------------
# Build document + render
# ---------------------------------------------------------------------------


# Bố cục tài liệu ở **DPI logic cố định** (độc lập thiết bị): QTextDocument dàn
# trang theo point-size trên metric ~96dpi. Ta luôn dựng ở LOGICAL_DPI rồi **chỉ
# scale painter** khi xuất ra thiết bị → preview & PDF cùng số trang/kích thước
# thật (WYSIWYG). Trước đây set page-box theo px thiết bị 300dpi khiến chữ in ra
# quá nhỏ và preview/PDF lệch số trang (Review H1).
LOGICAL_DPI = 96.0
_FOOTER_MM = 9.0  # dải footer "Trang x/y"
_FOOTER_PT = 11.0  # cỡ chữ footer


def _a4_body_size_logical() -> QSizeF:
    """Kích thước vùng in (A4 trừ lề) ở LOGICAL_DPI — hằng số, không theo thiết bị."""
    layout = QPageLayout(
        QPageSize(QPageSize.A4), QPageLayout.Portrait, _MARGIN_MM, QPageLayout.Millimeter
    )
    rect = layout.paintRectPixels(int(LOGICAL_DPI))
    return QSizeF(rect.width(), rect.height())


def _footer_h_logical(style: StyleConfig) -> float:
    """Chiều cao dải footer ở LOGICAL_DPI (0 nếu tắt số trang)."""
    if not style.settings.get("footer_page_number", True):
        return 0.0
    return (_FOOTER_MM / 25.4) * LOGICAL_DPI


def build_document(
    style: StyleConfig,
    records: List[Dict[str, Any]],
    stt_file: int,
    *,
    footer_reserve_px: float = 0.0,
) -> QTextDocument:
    """Dựng `QTextDocument` đã thay token + nhân dòng bảng (KHÔNG in).

    Dàn trang ở LOGICAL_DPI; `footer_reserve_px` (đơn vị LOGICAL_DPI) chừa chỗ cho
    footer. Preview & PDF dùng chung hàm này nên số trang khớp nhau. Gọi được trong
    QThread.
    """
    ctx = build_context(style, records, stt_file)
    filled = substitute(style.template_html, ctx, escape=True)

    doc = QTextDocument()
    doc.setDefaultFont(QFont(str(style.settings.get("font_name", "Times New Roman")), 12))
    doc.setDocumentMargin(0)
    doc.setHtml(filled)
    _fill_data_table(doc, style, records)

    body = _a4_body_size_logical()
    page_h = max(1.0, body.height() - footer_reserve_px)
    doc.setPageSize(QSizeF(body.width(), page_h))
    return doc


def render_group_document(
    style: StyleConfig, records: List[Dict[str, Any]], stt_file: int = 1
) -> QTextDocument:
    """Alias tiện dụng: build tài liệu 1 nhóm (không in)."""
    return build_document(
        style, records, stt_file, footer_reserve_px=_footer_h_logical(style)
    )


def _footer_text(style: StyleConfig, page: int, total: int) -> str:
    """Footer đã thay `{trang_so}`/`{tong_so_trang}` (chỉ ở footer, Red Team #9)."""
    fmt = str(style.settings.get("footer_format", "Trang {trang_so}/{tong_so_trang}"))
    return fmt.replace("{trang_so}", str(page)).replace("{tong_so_trang}", str(total))


def _draw_body(
    painter: QPainter,
    doc: QTextDocument,
    page_index: int,
    body: QSizeF,
    scale: float,
    ox: float,
    oy: float,
) -> None:
    """Vẽ phần thân 1 trang: dịch vào gốc thiết bị (ox,oy), scale, clip đúng trang."""
    painter.save()
    painter.translate(ox, oy)
    painter.scale(scale, scale)
    painter.translate(0, -page_index * body.height())
    clip = QRectF(0, page_index * body.height(), body.width(), body.height())
    doc.drawContents(painter, clip)
    painter.restore()


def _draw_footer(
    painter: QPainter,
    style: StyleConfig,
    page: int,
    total: int,
    body: QSizeF,
    footer_h: float,
    scale: float,
    ox: float,
    oy: float,
) -> None:
    """Vẽ footer ở TỌA ĐỘ THIẾT BỊ (không scale painter → tránh nhân đôi cỡ chữ).

    Cỡ chữ đặt bằng pixel thiết bị = pt/72 × LOGICAL_DPI × scale để khớp thân.
    """
    if footer_h <= 0 or not style.settings.get("footer_page_number", True):
        return
    font = QFont(str(style.settings.get("font_name", "Times New Roman")))
    font.setPixelSize(max(1, int(round(_FOOTER_PT / 72.0 * LOGICAL_DPI * scale))))
    rect = QRectF(
        ox, oy + body.height() * scale, body.width() * scale, footer_h * scale
    )
    painter.save()
    painter.setFont(font)
    painter.setPen(QColor(0, 0, 0))
    painter.drawText(rect, int(Qt.AlignRight | Qt.AlignVCenter), _footer_text(style, page, total))
    painter.restore()


def render_group_pdf(
    style: StyleConfig,
    records: List[Dict[str, Any]],
    out_path,
    stt_file: int = 1,
    *,
    resolution: int = 300,
) -> str:
    """Render 1 nhóm → PDF A4 tại `out_path` (page-by-page, footer mỗi trang).

    Dàn trang ở LOGICAL_DPI, scale painter = resolution/LOGICAL_DPI để chữ in đúng
    cỡ vật lý. Trả về đường dẫn; lỗi template rõ ràng (`TemplateError`).
    """
    out = Path(out_path)
    out.parent.mkdir(parents=True, exist_ok=True)

    writer = QPdfWriter(str(out))
    writer.setPageSize(QPageSize(QPageSize.A4))
    writer.setPageMargins(_MARGIN_MM, QPageLayout.Millimeter)
    writer.setResolution(resolution)

    footer_h = _footer_h_logical(style)
    doc = build_document(style, records, stt_file, footer_reserve_px=footer_h)
    body = doc.pageSize()
    scale = resolution / LOGICAL_DPI
    origin = writer.pageLayout().paintRectPixels(resolution).topLeft()
    total = max(1, doc.pageCount())

    painter = QPainter(writer)
    try:
        for i in range(total):
            if i > 0:
                writer.newPage()
            _draw_body(painter, doc, i, body, scale, origin.x(), origin.y())
            _draw_footer(
                painter, style, i + 1, total, body, footer_h, scale, origin.x(), origin.y()
            )
    finally:
        painter.end()
    return str(out)


def render_page_image(
    style: StyleConfig,
    records: List[Dict[str, Any]],
    page_index: int,
    stt_file: int = 1,
    *,
    scale: float = 1.5,
) -> QImage:
    """Render 1 trang thành `QImage` cho preview (cùng bố cục LOGICAL_DPI như PDF).

    `scale` chỉ để ảnh nét trên màn hình, KHÔNG đổi số trang. Ảnh trắng nếu vượt trang.
    """
    footer_h = _footer_h_logical(style)
    doc = build_document(style, records, stt_file, footer_reserve_px=footer_h)
    body = doc.pageSize()
    total = max(1, doc.pageCount())

    img = QImage(
        max(1, int(body.width() * scale)),
        max(1, int((body.height() + footer_h) * scale)),
        QImage.Format_ARGB32,
    )
    img.fill(QColor(255, 255, 255))
    if page_index >= total:
        return img

    painter = QPainter(img)
    try:
        _draw_body(painter, doc, page_index, body, scale, 0.0, 0.0)
        _draw_footer(painter, style, page_index + 1, total, body, footer_h, scale, 0.0, 0.0)
    finally:
        painter.end()
    return img


def page_count(style: StyleConfig, records: List[Dict[str, Any]]) -> int:
    """Số trang khi render nhóm (khớp PDF vì cùng bố cục LOGICAL_DPI)."""
    footer_h = _footer_h_logical(style)
    doc = build_document(style, records, 1, footer_reserve_px=footer_h)
    return max(1, doc.pageCount())
