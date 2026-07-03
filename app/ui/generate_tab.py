"""Tab 5 — Xuất hàng loạt: chọn thư mục, checkbox Excel, nút Generate, progress
bar, log. Chạy `BatchController` trong QThread (song song đa tiến trình bên dưới).
"""

from __future__ import annotations

from pathlib import Path

from PyQt5.QtCore import QThread
from PyQt5.QtWidgets import (
    QCheckBox,
    QFileDialog,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QMessageBox,
    QProgressBar,
    QPushButton,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.core.batch_generator import BatchController
from app.core.platform_utils import open_with_default


class GenerateTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self.window = window
        self._thread: QThread | None = None
        self._controller: BatchController | None = None
        self._out_dir = ""
        self._build_ui()

    def _build_ui(self) -> None:
        layout = QVBoxLayout(self)

        dir_row = QHBoxLayout()
        dir_row.addWidget(QLabel("Thư mục xuất:"))
        self.dir_edit = QLineEdit()
        self.dir_edit.setReadOnly(True)
        dir_btn = QPushButton("Chọn...")
        dir_btn.clicked.connect(self._browse_dir)
        dir_row.addWidget(self.dir_edit, 1)
        dir_row.addWidget(dir_btn)
        layout.addLayout(dir_row)

        self.excel_check = QCheckBox("Xuất kèm file Excel tổng hợp")
        self.excel_check.setChecked(True)
        layout.addWidget(self.excel_check)

        self.generate_btn = QPushButton("Xuất hàng loạt")
        self.generate_btn.clicked.connect(self._generate)
        layout.addWidget(self.generate_btn)

        self.progress = QProgressBar()
        self.progress.setValue(0)
        layout.addWidget(self.progress)

        self.log = QTextEdit()
        self.log.setReadOnly(True)
        layout.addWidget(self.log, 1)

    def _browse_dir(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Chọn thư mục xuất")
        if path:
            self._out_dir = path
            self.dir_edit.setText(path)

    def _generate(self) -> None:
        state = self.window.state
        if state.style is None or state.df is None:
            self.window.show_error("Chưa có dữ liệu. Hãy kết nối và tải sheet trước.")
            return
        if not self._out_dir:
            self.window.show_error("Hãy chọn thư mục xuất.")
            return

        self.generate_btn.setEnabled(False)
        self.progress.setValue(0)
        self.log.clear()
        self._log("Bắt đầu xuất...")

        # Chạy BatchController trong QThread; pool đa tiến trình nằm bên trong.
        self._thread = QThread()
        self._controller = BatchController(
            state.style,
            state.style_dir,
            state.df,
            self._out_dir,
            self.excel_check.isChecked(),
        )
        self._controller.moveToThread(self._thread)
        self._thread.started.connect(self._controller.run)
        self._controller.progress.connect(self._on_progress)
        self._controller.finished.connect(self._on_finished)
        self._controller.failed.connect(self._on_failed)
        # Dọn thread khi xong.
        self._controller.finished.connect(self._thread.quit)
        self._controller.failed.connect(self._thread.quit)
        self._thread.finished.connect(self._thread.deleteLater)
        self._thread.start()

    def _on_progress(self, done: int, total: int) -> None:
        self.progress.setMaximum(total)
        self.progress.setValue(done)

    def _on_finished(self, summary) -> None:
        self.generate_btn.setEnabled(True)
        self._log(
            f"Xong: {summary.succeeded}/{summary.total} thành công, "
            f"{summary.failed} lỗi."
        )
        if summary.excel_path:
            self._log(f"Excel: {summary.excel_path}")
        for group, msg in summary.errors:
            self._log(f"  - Lỗi [{group}]: {msg}")

        box = QMessageBox(self)
        box.setWindowTitle("Hoàn thành")
        box.setText(
            f"Đã xuất {summary.succeeded}/{summary.total} hồ sơ vào:\n{summary.out_dir}"
        )
        open_btn = box.addButton("Mở thư mục", QMessageBox.AcceptRole)
        box.addButton("Đóng", QMessageBox.RejectRole)
        box.exec_()
        if box.clickedButton() == open_btn and Path(summary.out_dir).exists():
            open_with_default(summary.out_dir)

    def _on_failed(self, msg: str) -> None:
        self.generate_btn.setEnabled(True)
        self._log(f"THẤT BẠI: {msg}")
        self.window.show_error(msg)

    def _log(self, text: str) -> None:
        self.log.append(text)
