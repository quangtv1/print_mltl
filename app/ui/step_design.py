"""Bước 2 — Thiết kế & Preview: trình soạn thảo WYSIWYG (`QTextEdit`) + panel biến
+ **preview phân trang thật** (tái dùng renderer P2).

- Trái: `QTextEdit` (Chỉnh sửa) hoặc danh sách trang A4 render (Xem trước).
- Toolbar: B/I/U, A−/A+, màu chữ, toggle Chỉnh sửa↔Xem trước, ◀ recLabel ▶.
- Phải: panel biến theo nhóm (Tài liệu / Hàng / Gom nhóm / Tự động); footer-only bị
  khóa (chỉ dùng ở footer). Bấm biến → chèn `{token}` tại con trỏ (chỉ khi Chỉnh sửa).
- Lưu mẫu (`toHtml`→save_style, có strip `<img src=file:>`); Đặt lại mặc định (nạp
  template gốc của mẫu). Validate đúng 1 dòng-mẫu trước khi rời bước (Red Team #11).
"""

from __future__ import annotations

import re
from typing import Any, Dict, List

from PyQt5.QtCore import Qt
from PyQt5.QtGui import QColor, QFont, QPixmap, QTextCharFormat, QTextCursor
from PyQt5.QtWidgets import (
    QColorDialog,
    QFrame,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QScrollArea,
    QStackedWidget,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.core import qt_pdf_renderer as R
from app.core import variables as V
from app.core.style_config import load_style, save_style

# <img> có src KHÔNG phải data-URI (file:/đường dẫn tuyệt đối) → gỡ (Red Team #Q).
_IMG_FILE_RE = re.compile(r'<img\b[^>]*\bsrc\s*=\s*"(?!data:)[^"]*"[^>]*>', re.IGNORECASE)
_MAX_TEMPLATE_CHARS = 400_000


class StepDesign(QWidget):
    def __init__(self, main):
        super().__init__()
        self.main = main
        self._loaded_style_dir = None
        self._panel_version = -1
        self._groups: List[Any] = []  # list các records-nhóm
        self._group_idx = 0
        self._preview_mode = False

        root = QVBoxLayout(self)
        root.setSpacing(10)
        root.addLayout(self._build_toolbar())

        body = QHBoxLayout()
        body.setSpacing(12)
        body.addWidget(self._build_left(), 3)
        body.addWidget(self._build_right(), 1)
        root.addLayout(body, 1)

        root.addLayout(self._build_footer_bar())

    # ------------------------------------------------------------ build UI
    def _build_toolbar(self) -> QHBoxLayout:
        bar = QHBoxLayout()
        bar.setSpacing(6)

        def tool(text, slot, checkable=False, tip=""):
            b = QPushButton(text)
            b.setProperty("toolbtn", "true")
            b.setCheckable(checkable)
            b.setToolTip(tip)
            b.clicked.connect(slot)
            bar.addWidget(b)
            return b

        self.btn_bold = tool("B", self._toggle_bold, True, "Đậm")
        self.btn_bold.setStyleSheet("font-weight:800;")
        self.btn_italic = tool("I", self._toggle_italic, True, "Nghiêng")
        self.btn_italic.setStyleSheet("font-style:italic;")
        self.btn_underline = tool("U", self._toggle_underline, True, "Gạch chân")
        self.btn_underline.setStyleSheet("text-decoration:underline;")
        tool("A−", lambda: self._bump_size(-1), tip="Nhỏ chữ")
        tool("A+", lambda: self._bump_size(+1), tip="To chữ")
        tool("🎨 Màu", self._pick_color, tip="Màu chữ")

        bar.addStretch(1)
        self.btn_prev = tool("◀", self._prev_group, tip="Hồ sơ trước")
        self.rec_label = QLabel("Hồ sơ 0/0")
        self.rec_label.setProperty("hint", "true")
        bar.addWidget(self.rec_label)
        self.btn_next = tool("▶", self._next_group, tip="Hồ sơ sau")

        self.btn_toggle = QPushButton("Xem trước")
        self.btn_toggle.setObjectName("primary")
        self.btn_toggle.clicked.connect(self._toggle_mode)
        bar.addWidget(self.btn_toggle)
        return bar

    def _build_left(self) -> QWidget:
        self.stack = QStackedWidget()
        self.editor = QTextEdit()
        self.editor.setAcceptRichText(True)
        self.stack.addWidget(self.editor)

        self.preview_area = QScrollArea()
        self.preview_area.setWidgetResizable(True)
        self.preview_host = QWidget()
        self.preview_layout = QVBoxLayout(self.preview_host)
        self.preview_layout.setAlignment(Qt.AlignHCenter | Qt.AlignTop)
        self.preview_area.setWidget(self.preview_host)
        self.stack.addWidget(self.preview_area)
        return self.stack

    def _build_right(self) -> QWidget:
        panel = QFrame()
        panel.setFrameShape(QFrame.StyledPanel)
        outer = QVBoxLayout(panel)
        title = QLabel("Biến trong mẫu")
        title.setProperty("section", "true")
        outer.addWidget(title)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        self.var_host = QWidget()
        self.var_layout = QVBoxLayout(self.var_host)
        self.var_layout.setAlignment(Qt.AlignTop)
        scroll.setWidget(self.var_host)
        outer.addWidget(scroll, 1)

        hint = QLabel("Đặt con trỏ trong ô soạn thảo rồi bấm 1 biến để chèn.")
        hint.setProperty("hint", "true")
        hint.setWordWrap(True)
        outer.addWidget(hint)
        return panel

    def _build_footer_bar(self) -> QHBoxLayout:
        bar = QHBoxLayout()
        save = QPushButton("💾 Lưu mẫu")
        save.clicked.connect(self._save_template)
        reset = QPushButton("↺ Đặt lại mặc định")
        reset.clicked.connect(self._reset_template)
        bar.addWidget(save)
        bar.addWidget(reset)
        bar.addStretch(1)
        return bar

    # --------------------------------------------------------------- enter
    def on_enter(self) -> None:
        st = self.main.state
        if st.style is None:
            return
        # Nạp template khi đổi mẫu (không đè khi chỉ đổi dữ liệu để giữ chỉnh sửa).
        if st.style_dir != self._loaded_style_dir:
            self.editor.setHtml(st.style.template_html or "")
            self._loaded_style_dir = st.style_dir
        # Panel biến + groups recompute theo data-version (Red Team invalidation).
        if st.data_version != self._panel_version:
            self._rebuild_var_panel()
            self._recompute_groups()
            self._panel_version = st.data_version
        if self._preview_mode:
            self._render_preview()

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
        # Xóa panel cũ.
        while self.var_layout.count():
            item = self.var_layout.takeAt(0)
            if item.widget():
                item.widget().deleteLater()

        style = self.main.state.style
        self._add_var_section("Tài liệu", V.document_tokens(style), True)
        self._add_var_section("Cột bảng (dòng-mẫu)", V.row_tokens(style), True)
        self._add_var_section("Từ settings", V.settings_tokens(style), True)
        self._add_var_section("Tự động", V.auto_tokens(), True)
        self._add_var_section("Chỉ dùng ở footer", list(V.FOOTER_VARS.keys()), False)

    def _add_var_section(self, title: str, tokens: List[str], insertable: bool) -> None:
        if not tokens:
            return
        lbl = QLabel(title)
        lbl.setProperty("section", "true")
        self.var_layout.addWidget(lbl)
        for tok in tokens:
            btn = QPushButton("{" + tok + "}")
            btn.setProperty("toolbtn", "true")
            if insertable:
                btn.clicked.connect(lambda _c, t=tok: self._insert_token(t))
                btn.setToolTip("Chèn vào vị trí con trỏ (chế độ Chỉnh sửa)")
            else:
                btn.setEnabled(False)
                btn.setToolTip("Chỉ có giá trị ở footer, không chèn vào thân mẫu")
            self.var_layout.addWidget(btn)

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
        weight = QFont.Bold if self.btn_bold.isChecked() else QFont.Normal
        fmt.setFontWeight(weight)
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
        cursor = self.editor.textCursor()
        size = cursor.charFormat().fontPointSize() or 12.0
        fmt = QTextCharFormat()
        fmt.setFontPointSize(max(6.0, size + delta))
        self._merge_format(fmt)

    def _pick_color(self) -> None:
        color = QColorDialog.getColor(QColor(0, 0, 0), self, "Chọn màu chữ")
        if color.isValid():
            fmt = QTextCharFormat()
            fmt.setForeground(color)
            self._merge_format(fmt)

    # ---------------------------------------------------------- preview
    def _toggle_mode(self) -> None:
        if not self._preview_mode:
            err = self._sync_and_validate()
            if err:
                self.main.show_error(err)
                return
            self._preview_mode = True
            self.btn_toggle.setText("Chỉnh sửa")
            self.stack.setCurrentIndex(1)
            self._render_preview()
        else:
            self._preview_mode = False
            self.btn_toggle.setText("Xem trước")
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
                pix = QPixmap.fromImage(img)
                lbl = QLabel()
                lbl.setPixmap(pix)
                lbl.setStyleSheet("background:#fff; border:1px solid #c8c8c8; margin:8px;")
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
        """Đồng bộ editor→style rồi validate đúng 1 dòng-mẫu (build thử group 0/rỗng)."""
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
