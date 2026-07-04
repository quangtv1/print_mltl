"""Bước 2 — Thiết kế & Preview (khớp prototype).

Trái: toolbar (toggle Chỉnh sửa/Xem trước dạng segmented + ◀ recLabel ▶) + thanh
định dạng (B/I/U, A−/A+, căn lề, ô màu) + canvas xám chứa "giấy" QTextEdit hoặc
danh sách trang preview. Phải: palette 300px (header + hint + nút ghép biến + chip
biến theo nhóm + biến tự động + hàng Lưu/Đặt lại). Preview dùng renderer P2.
"""

from __future__ import annotations

import re
from typing import Any, List

from PyQt5.QtCore import QPoint, QRect, QSize, Qt
from PyQt5.QtGui import QColor, QFont, QPixmap, QTextCharFormat, QTextCursor
from PyQt5.QtWidgets import (
    QButtonGroup,
    QColorDialog,
    QFrame,
    QHBoxLayout,
    QLabel,
    QLayout,
    QPushButton,
    QScrollArea,
    QSizePolicy,
    QStackedWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.core import qt_pdf_renderer as R
from app.core import variables as V
from app.core.style_config import load_style, save_style

# <img> src không phải data-URI → gỡ (Red Team #Q).
_IMG_FILE_RE = re.compile(r'<img\b[^>]*\bsrc\s*=\s*"(?!data:)[^"]*"[^>]*>', re.IGNORECASE)
_MAX_TEMPLATE_CHARS = 400_000
_SWATCHES = ["#000000", "#c00000", "#0057b7", "#107c10", "#d97706", "#5c2d91"]


class FlowLayout(QLayout):
    """Layout cuộn dòng (chip tự xuống hàng khi hết chiều ngang)."""

    def __init__(self, spacing: int = 6):
        super().__init__()
        self._items: List[Any] = []
        self.setContentsMargins(0, 0, 0, 0)
        self.setSpacing(spacing)

    def addItem(self, item):
        self._items.append(item)

    def count(self):
        return len(self._items)

    def itemAt(self, i):
        return self._items[i] if 0 <= i < len(self._items) else None

    def takeAt(self, i):
        return self._items.pop(i) if 0 <= i < len(self._items) else None

    def expandingDirections(self):
        return Qt.Orientations(Qt.Horizontal)

    def hasHeightForWidth(self):
        return True

    def heightForWidth(self, w):
        return self._do(QRect(0, 0, w, 0), True)

    def setGeometry(self, rect):
        super().setGeometry(rect)
        self._do(rect, False)

    def sizeHint(self):
        return self.minimumSize()

    def minimumSize(self):
        s = QSize()
        for it in self._items:
            s = s.expandedTo(it.minimumSize())
        return s

    def _do(self, rect, test):
        x, y, line_h = rect.x(), rect.y(), 0
        sp = self.spacing()
        for it in self._items:
            hint = it.sizeHint()
            if x + hint.width() > rect.right() and line_h > 0:
                x = rect.x()
                y += line_h + sp
                line_h = 0
            if not test:
                it.setGeometry(QRect(QPoint(x, y), hint))
            x += hint.width() + sp
            line_h = max(line_h, hint.height())
        return y + line_h - rect.y()


class StepDesign(QWidget):
    def __init__(self, main):
        super().__init__()
        self.main = main
        self._loaded_style_dir = None
        self._panel_version = -1
        self._groups: List[Any] = []
        self._group_idx = 0
        self._preview_mode = False

        row = QHBoxLayout(self)
        row.setContentsMargins(0, 0, 0, 0)
        row.setSpacing(0)
        row.addWidget(self._build_left(), 1)
        row.addWidget(self._build_right())

    # ------------------------------------------------------------ build UI
    def _build_left(self) -> QWidget:
        panel = QWidget()
        panel.setObjectName("DesignLeft")
        panel.setStyleSheet("QWidget#DesignLeft{background:#e6e6e6;}")
        v = QVBoxLayout(panel)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(0)
        v.addWidget(self._build_toolbar())
        v.addWidget(self._build_format_bar())

        canvas = QScrollArea()
        canvas.setWidgetResizable(True)
        canvas.setStyleSheet("background:#8f8f8f; border:none;")
        self.stack = QStackedWidget()
        self.stack.setStyleSheet("background:#8f8f8f;")

        # Edit: giấy trắng.
        editor_host = QWidget()
        eh = QVBoxLayout(editor_host)
        eh.setContentsMargins(24, 24, 24, 24)
        self.editor = QTextEdit()
        self.editor.setStyleSheet("background:#fff; border:1px solid #b0b0b0;")
        eh.addWidget(self.editor)
        self.stack.addWidget(editor_host)

        # Preview: danh sách trang.
        self.preview_area = QScrollArea()
        self.preview_area.setWidgetResizable(True)
        self.preview_area.setStyleSheet("background:#8f8f8f; border:none;")
        self.preview_host = QWidget()
        self.preview_host.setStyleSheet("background:#8f8f8f;")
        self.preview_layout = QVBoxLayout(self.preview_host)
        self.preview_layout.setAlignment(Qt.AlignHCenter | Qt.AlignTop)
        self.preview_area.setWidget(self.preview_host)
        self.stack.addWidget(self.preview_area)

        canvas.setWidget(self.stack)
        v.addWidget(canvas, 1)
        return panel

    def _build_toolbar(self) -> QWidget:
        bar = QWidget()
        bar.setFixedHeight(40)
        bar.setStyleSheet("background:#f0f0f0; border-bottom:1px solid #cfcfcf;")
        h = QHBoxLayout(bar)
        h.setContentsMargins(12, 0, 12, 0)
        h.setSpacing(10)

        # Segmented Edit/Preview.
        seg = QWidget()
        sl = QHBoxLayout(seg)
        sl.setContentsMargins(0, 0, 0, 0)
        sl.setSpacing(0)
        self.btn_edit = QPushButton("Chỉnh sửa")
        self.btn_prev = QPushButton("Xem trước")
        grp = QButtonGroup(self)
        for b in (self.btn_edit, self.btn_prev):
            b.setCheckable(True)
            b.setProperty("toolbtn", "true")
            grp.addButton(b)
            sl.addWidget(b)
        self.btn_edit.setChecked(True)
        self.btn_edit.clicked.connect(lambda: self._set_mode(False))
        self.btn_prev.clicked.connect(lambda: self._set_mode(True))
        h.addWidget(seg)
        h.addStretch(1)

        prev_g = QPushButton("◀")
        prev_g.setProperty("toolbtn", "true")
        prev_g.clicked.connect(self._prev_group)
        self.rec_label = QLabel("Hồ sơ 0/0")
        self.rec_label.setMinimumWidth(120)
        self.rec_label.setAlignment(Qt.AlignCenter)
        next_g = QPushButton("▶")
        next_g.setProperty("toolbtn", "true")
        next_g.clicked.connect(self._next_group)
        h.addWidget(prev_g)
        h.addWidget(self.rec_label)
        h.addWidget(next_g)
        return bar

    def _build_format_bar(self) -> QWidget:
        bar = QWidget()
        bar.setStyleSheet("background:#fbfbfb; border-bottom:1px solid #cfcfcf;")
        h = QHBoxLayout(bar)
        h.setContentsMargins(10, 5, 10, 5)
        h.setSpacing(4)

        def tool(text, slot, checkable=False, style=""):
            b = QPushButton(text)
            b.setProperty("toolbtn", "true")
            b.setCheckable(checkable)
            if style:
                b.setStyleSheet(style)
            b.clicked.connect(slot)
            h.addWidget(b)
            return b

        self.btn_bold = tool("B", self._toggle_bold, True, "font-weight:800;")
        self.btn_italic = tool("I", self._toggle_italic, True, "font-style:italic;")
        self.btn_underline = tool("U", self._toggle_underline, True, "text-decoration:underline;")
        h.addWidget(self._sep())
        tool("A−", lambda: self._bump_size(-1))
        tool("A+", lambda: self._bump_size(+1))
        h.addWidget(self._sep())
        tool("Trái", lambda: self._set_align(Qt.AlignLeft))
        tool("Giữa", lambda: self._set_align(Qt.AlignHCenter))
        tool("Phải", lambda: self._set_align(Qt.AlignRight))
        h.addWidget(self._sep())
        lbl = QLabel("Màu chữ:")
        lbl.setProperty("hint", "true")
        h.addWidget(lbl)
        for c in _SWATCHES:
            sw = QPushButton()
            sw.setFixedSize(18, 18)
            sw.setStyleSheet(f"background:{c}; border:1px solid #888; border-radius:2px;")
            sw.clicked.connect(lambda _c, col=c: self._apply_color(QColor(col)))
            h.addWidget(sw)
        more = QPushButton("…")
        more.setProperty("toolbtn", "true")
        more.setToolTip("Chọn màu khác")
        more.clicked.connect(self._pick_color)
        h.addWidget(more)
        h.addStretch(1)
        return bar

    def _build_right(self) -> QWidget:
        panel = QWidget()
        panel.setObjectName("Palette")
        panel.setFixedWidth(300)
        panel.setStyleSheet("QWidget#Palette{background:#f0f0f0;}")
        v = QVBoxLayout(panel)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(0)

        self.pal_header = QLabel("Biến của mẫu")
        self.pal_header.setStyleSheet(
            "background:#fff; border-bottom:1px solid #dcdcdc; padding:10px 12px; font-weight:600; color:#1a1a1a;"
        )
        v.addWidget(self.pal_header)

        hint = QLabel(
            "Đặt con trỏ vào vùng soạn thảo (chế độ Chỉnh sửa), rồi bấm một biến để chèn."
        )
        hint.setWordWrap(True)
        hint.setStyleSheet(
            "background:#fafafa; border-bottom:1px solid #e4e4e4; padding:10px 12px; font-size:11px; color:#666;"
        )
        v.addWidget(hint)

        map_wrap = QWidget()
        mw = QVBoxLayout(map_wrap)
        mw.setContentsMargins(12, 10, 12, 0)
        map_btn = QPushButton("Ghép biến với dữ liệu…")
        map_btn.clicked.connect(lambda: self.main.go_to(0))
        mw.addWidget(map_btn)
        v.addWidget(map_wrap)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        self.var_host = QWidget()
        self.var_layout = QVBoxLayout(self.var_host)
        self.var_layout.setContentsMargins(12, 12, 12, 12)
        self.var_layout.setAlignment(Qt.AlignTop)
        scroll.setWidget(self.var_host)
        v.addWidget(scroll, 1)

        save_row = QWidget()
        save_row.setObjectName("SaveRow")
        save_row.setStyleSheet("QWidget#SaveRow{background:#fff; border-top:1px solid #dcdcdc;}")
        sr = QHBoxLayout(save_row)
        sr.setContentsMargins(12, 12, 12, 12)
        sr.setSpacing(8)
        save = QPushButton("Lưu mẫu")
        save.setObjectName("primary")
        save.clicked.connect(self._save_template)
        reset = QPushButton("Đặt lại mặc định")
        reset.clicked.connect(self._reset_template)
        sr.addWidget(save)
        sr.addWidget(reset)
        v.addWidget(save_row)
        return panel

    @staticmethod
    def _sep() -> QFrame:
        f = QFrame()
        f.setFrameShape(QFrame.VLine)
        f.setStyleSheet("color:#dedede;")
        f.setFixedHeight(20)
        return f

    # --------------------------------------------------------------- enter
    def on_enter(self) -> None:
        st = self.main.state
        if st.style is None:
            return
        if st.style_dir != self._loaded_style_dir:
            self.editor.setHtml(st.style.template_html or "")
            self._loaded_style_dir = st.style_dir
        self.pal_header.setText(f"Biến của mẫu · {st.style.name}")
        if st.data_version != self._panel_version:
            self._rebuild_var_panel()
            self._recompute_groups()
            self._panel_version = st.data_version
        if self._preview_mode:
            self._render_preview()

    def status_text(self) -> str:
        return "Bước 2/3 — Soạn mẫu, chèn biến và xem trước."

    def _recompute_groups(self) -> None:
        st = self.main.state
        self._groups = []
        self._group_idx = 0
        if st.df is None or not st.style.grouping_column:
            self.rec_label.setText("Hồ sơ 0/0")
            return
        try:
            groups = R.group_dataframe(st.style, st.df)
        except KeyError:
            self.rec_label.setText("Hồ sơ 0/0")
            return
        self._groups = [R.df_to_records(g) for g in groups.values()]
        self.rec_label.setText(f"Hồ sơ 1/{len(self._groups)}" if self._groups else "Hồ sơ 0/0")

    # ---------------------------------------------------------- var panel
    def _rebuild_var_panel(self) -> None:
        while self.var_layout.count():
            item = self.var_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        style = self.main.state.style
        self._add_group("Tài liệu (hồ sơ)", V.document_tokens(style) + V.settings_tokens(style), True)
        self._add_group("Hàng (bảng văn bản)", V.row_tokens(style), True)
        self._add_group("Biến tự động", V.auto_tokens(), True)
        self._add_group("Chỉ dùng ở footer", list(V.FOOTER_VARS.keys()), False)

    def _add_group(self, title: str, tokens: List[str], insertable: bool) -> None:
        if not tokens:
            return
        lbl = QLabel(title.upper())
        lbl.setStyleSheet("font-size:11px; color:#7a7a7a; letter-spacing:.4px; margin-bottom:2px;")
        self.var_layout.addWidget(lbl)

        holder = QWidget()
        flow = FlowLayout(6)
        holder.setLayout(flow)
        for tok in tokens:
            chip = QPushButton("{" + tok + "}")
            chip.setProperty("chip", "true")
            if insertable:
                chip.clicked.connect(lambda _c, t=tok: self._insert_token(t))
                chip.setToolTip("Chèn vào vị trí con trỏ (chế độ Chỉnh sửa)")
            else:
                chip.setEnabled(False)
                chip.setToolTip("Chỉ có giá trị ở footer")
            flow.addWidget(chip)
        self.var_layout.addWidget(holder)

    def _insert_token(self, token: str) -> None:
        if self._preview_mode:
            self.main.show_info("Chuyển sang chế độ Chỉnh sửa để chèn biến.")
            return
        self.editor.setFocus()
        self.editor.textCursor().insertText("{" + token + "}")

    # ------------------------------------------------------- formatting
    def _merge_format(self, fmt: QTextCharFormat) -> None:
        cursor = self.editor.textCursor()
        if not cursor.hasSelection():
            cursor.select(QTextCursor.WordUnderCursor)
        cursor.mergeCharFormat(fmt)
        self.editor.mergeCurrentCharFormat(fmt)

    def _toggle_bold(self) -> None:
        fmt = QTextCharFormat()
        fmt.setFontWeight(QFont.Bold if self.btn_bold.isChecked() else QFont.Normal)
        self._merge_format(fmt)

    def _toggle_italic(self) -> None:
        fmt = QTextCharFormat()
        fmt.setFontItalic(self.btn_italic.isChecked())
        self._merge_format(fmt)

    def _toggle_underline(self) -> None:
        fmt = QTextCharFormat()
        fmt.setFontUnderline(self.btn_underline.isChecked())
        self._merge_format(fmt)

    def _bump_size(self, delta: int) -> None:
        size = self.editor.textCursor().charFormat().fontPointSize() or 12.0
        fmt = QTextCharFormat()
        fmt.setFontPointSize(max(6.0, size + delta))
        self._merge_format(fmt)

    def _set_align(self, align) -> None:
        self.editor.setAlignment(align)

    def _apply_color(self, color: QColor) -> None:
        fmt = QTextCharFormat()
        fmt.setForeground(color)
        self._merge_format(fmt)

    def _pick_color(self) -> None:
        color = QColorDialog.getColor(QColor(0, 0, 0), self, "Chọn màu chữ")
        if color.isValid():
            self._apply_color(color)

    # ---------------------------------------------------------- preview
    def _set_mode(self, preview: bool) -> None:
        if preview == self._preview_mode:
            return
        if preview:
            err = self._sync_and_validate()
            if err:
                self.main.show_error(err)
                self.btn_edit.setChecked(True)
                return
            self._preview_mode = True
            self.stack.setCurrentIndex(1)
            self._render_preview()
        else:
            self._preview_mode = False
            self.stack.setCurrentIndex(0)

    def _render_preview(self) -> None:
        while self.preview_layout.count():
            item = self.preview_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()
        style = self.main.state.style
        if not self._groups:
            self.preview_layout.addWidget(QLabel("Chưa có dữ liệu để xem trước."))
            return
        records = self._groups[self._group_idx]
        try:
            total = R.page_count(style, records)
            for p in range(total):
                img = R.render_page_image(style, records, p, stt_file=self._group_idx + 1)
                lbl = QLabel()
                lbl.setPixmap(QPixmap.fromImage(img))
                lbl.setStyleSheet("background:#fff; border:1px solid #555; margin:8px;")
                self.preview_layout.addWidget(lbl)
        except R.TemplateError as e:
            self.preview_layout.addWidget(QLabel(f"Lỗi mẫu: {e}"))

    def _prev_group(self) -> None:
        if self._groups and self._group_idx > 0:
            self._group_idx -= 1
            self._update_rec_label()
            if self._preview_mode:
                self._render_preview()

    def _next_group(self) -> None:
        if self._groups and self._group_idx < len(self._groups) - 1:
            self._group_idx += 1
            self._update_rec_label()
            if self._preview_mode:
                self._render_preview()

    def _update_rec_label(self) -> None:
        self.rec_label.setText(f"Hồ sơ {self._group_idx + 1}/{len(self._groups)}")

    # ------------------------------------------------------------- save
    def _sync_template(self) -> None:
        html = self.editor.toHtml()
        html = _IMG_FILE_RE.sub("", html)[:_MAX_TEMPLATE_CHARS]
        self.main.state.style.template_html = html

    def _sync_and_validate(self):
        self._sync_template()
        records = self._groups[self._group_idx] if self._groups else []
        try:
            R.build_document(self.main.state.style, records, self._group_idx + 1)
        except R.TemplateError as e:
            return str(e)
        return None

    def _save_template(self) -> None:
        err = self._sync_and_validate()
        if err:
            self.main.show_error(err)
            return
        if self.main.state.style_dir is None:
            self.main.show_error("Chưa chọn mẫu để lưu.")
            return
        save_style(self.main.state.style, self.main.state.style_dir)
        self.main.show_info("Đã lưu mẫu.")

    def _reset_template(self) -> None:
        if self.main.state.style_dir is None:
            return
        disk = load_style(self.main.state.style_dir)
        self.main.state.style.template_html = disk.template_html
        self.editor.setHtml(disk.template_html or "")
        if self._preview_mode:
            self._render_preview()

    # ------------------------------------------------------------ gating
    def validate_next(self):
        return self._sync_and_validate()
