"""Tab 4 — Xem trước: chọn 1 hồ sơ → render docx tạm → LibreOffice→PDF→ảnh hiển
thị. Thiếu LibreOffice → hỏi mở bằng Word (open_with_default). Chạy trên QThread.
"""

from __future__ import annotations

import shutil
from pathlib import Path

from PyQt5.QtCore import Qt
from PyQt5.QtGui import QImage, QPixmap
from PyQt5.QtWidgets import (
    QComboBox,
    QHBoxLayout,
    QLabel,
    QMessageBox,
    QPushButton,
    QScrollArea,
    QVBoxLayout,
    QWidget,
)

from app.core import docx_renderer as R
from app.core import pdf_preview
from app.core.platform_utils import make_temp_dir, open_with_default
from app.ui.workers import run_async


class PreviewWidget(QWidget):
    def __init__(self, window):
        super().__init__()
        self.window = window
        self._temp_dir: Path | None = None
        self._last_docx: Path | None = None
        self._build_ui()
        self.window.data_loaded.connect(self._refresh_groups)

    def _build_ui(self) -> None:
        layout = QVBoxLayout(self)

        top = QHBoxLayout()
        top.addWidget(QLabel("Chọn hồ sơ:"))
        self.group_combo = QComboBox()
        top.addWidget(self.group_combo, 1)
        self.preview_btn = QPushButton("Xem trước")
        self.preview_btn.clicked.connect(self._preview)
        top.addWidget(self.preview_btn)
        layout.addLayout(top)

        self.status = QLabel("")
        layout.addWidget(self.status)

        self.scroll = QScrollArea()
        self.scroll.setWidgetResizable(True)
        self._pages_holder = QWidget()
        self._pages_layout = QVBoxLayout(self._pages_holder)
        self._pages_layout.setAlignment(Qt.AlignTop)
        self.scroll.setWidget(self._pages_holder)
        layout.addWidget(self.scroll, 1)

    def _refresh_groups(self) -> None:
        style = self.window.state.style
        df = self.window.state.df
        self.group_combo.clear()
        if style is None or df is None:
            return
        col = style.grouping_column
        if col in df.columns:
            values = [str(v) for v in df[col].unique()]
            self.group_combo.addItems(values)

    def _clear_pages(self) -> None:
        while self._pages_layout.count():
            item = self._pages_layout.takeAt(0)
            w = item.widget()
            if w:
                w.deleteLater()

    def _preview(self) -> None:
        style = self.window.state.style
        df = self.window.state.df
        style_dir = self.window.state.style_dir
        group_value = self.group_combo.currentText()
        if style is None or df is None or not group_value:
            self.window.show_error("Chưa có dữ liệu hoặc chưa chọn hồ sơ.")
            return

        col = style.grouping_column
        df_group = df[df[col].astype(str) == group_value]
        if df_group.empty:
            self.window.show_error("Không tìm thấy dữ liệu cho hồ sơ đã chọn.")
            return

        records = R.df_to_records(df_group)
        # Thư mục tạm mới mỗi lần preview (dọn thư mục cũ).
        self._cleanup_temp()
        self._temp_dir = make_temp_dir(prefix="preview_ui_")
        docx_path = self._temp_dir / "preview.docx"
        style_dict, recs, out = R.build_task(style, style_dir, records, docx_path)

        self.preview_btn.setEnabled(False)
        self.status.setText("Đang tạo bản xem trước...")
        self._clear_pages()

        def task():
            R.render_group(style_dict, recs, out)
            pages = pdf_preview.preview_docx(out)
            return pages

        def done(pages):
            self._last_docx = docx_path
            self.preview_btn.setEnabled(True)
            self._show_pages(pages)
            self.status.setText(f"{len(pages)} trang.")

        def error(msg):
            self._last_docx = docx_path
            self.preview_btn.setEnabled(True)
            self._handle_preview_error(msg, docx_path)

        run_async(self, task, done, error)

    def _handle_preview_error(self, msg: str, docx_path: Path) -> None:
        """Thiếu LibreOffice → hỏi mở bằng Word; lỗi khác → báo lỗi."""
        # preview_docx ném LibreOfficeNotFound (message chứa 'LibreOffice').
        if "LibreOffice" in msg or "soffice" in msg:
            self.status.setText("Không có LibreOffice để xem trước trong app.")
            reply = QMessageBox.question(
                self,
                "Thiếu LibreOffice",
                msg + "\n\nMở bản xem trước bằng Word/ứng dụng mặc định?",
                QMessageBox.Yes | QMessageBox.No,
                QMessageBox.Yes,
            )
            if reply == QMessageBox.Yes and docx_path.is_file():
                open_with_default(docx_path)
        else:
            self.status.setText("Xem trước thất bại.")
            self.window.show_error(msg)

    def _show_pages(self, pages) -> None:
        self._clear_pages()
        for png_bytes in pages:
            img = QImage.fromData(png_bytes, "PNG")
            label = QLabel()
            label.setPixmap(QPixmap.fromImage(img))
            label.setAlignment(Qt.AlignCenter)
            self._pages_layout.addWidget(label)

    def _cleanup_temp(self) -> None:
        if self._temp_dir and self._temp_dir.exists():
            shutil.rmtree(self._temp_dir, ignore_errors=True)
        self._temp_dir = None
