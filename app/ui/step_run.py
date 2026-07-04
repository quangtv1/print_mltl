"""Bước 3 — Chạy: chọn thư mục + mẫu tên PDF, tùy chọn (đa luồng/ghi đè/Excel),
progress + **log realtime**, mở thư mục kết quả.

Chạy `BatchController` (P3) trong 1 QThread; nối `progress`/`log`/`finished`/`failed`.
Checkbox "đa luồng" **disabled ở MVP** (render serialize — xem P3/Red Team #4).
"""

from __future__ import annotations

import copy
from pathlib import Path

from PyQt5.QtCore import QThread
from PyQt5.QtWidgets import (
    QCheckBox,
    QFileDialog,
    QGridLayout,
    QGroupBox,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QProgressBar,
    QPushButton,
    QTextEdit,
    QVBoxLayout,
    QWidget,
)

from app.core import qt_pdf_renderer as R
from app.core.batch_generator import BatchController
from app.core.platform_utils import open_with_default


class StepRun(QWidget):
    def __init__(self, main):
        super().__init__()
        self.main = main
        self._thread = None
        self._ctl = None

        root = QVBoxLayout(self)
        root.setSpacing(12)
        root.addWidget(self._build_output_group())
        root.addWidget(self._build_options_group())
        root.addWidget(self._build_progress_group())
        root.addWidget(self._build_log_group(), 1)

    # ------------------------------------------------------------ build UI
    def _build_output_group(self) -> QGroupBox:
        box = QGroupBox("Thư mục & tên file xuất")
        g = QGridLayout(box)
        g.setColumnStretch(1, 1)

        g.addWidget(QLabel("Thư mục:"), 0, 0)
        self.dir_edit = QLineEdit()
        self.dir_edit.setPlaceholderText("Chọn thư mục lưu PDF…")
        g.addWidget(self.dir_edit, 0, 1)
        browse = QPushButton("Duyệt…")
        browse.clicked.connect(self._on_browse_dir)
        g.addWidget(browse, 0, 2)

        g.addWidget(QLabel("Tên PDF:"), 1, 0)
        self.pattern_edit = QLineEdit()
        self.pattern_edit.textChanged.connect(self._update_example)
        g.addWidget(self.pattern_edit, 1, 1, 1, 2)

        self.example = QLabel("")
        self.example.setProperty("hint", "true")
        g.addWidget(self.example, 2, 1, 1, 2)
        return box

    def _build_options_group(self) -> QGroupBox:
        box = QGroupBox("Tùy chọn chạy")
        v = QVBoxLayout(box)
        self.chk_parallel = QCheckBox("Chạy đa luồng (đang thử nghiệm)")
        self.chk_parallel.setEnabled(False)  # MVP: serialize (Red Team #4)
        self.chk_parallel.setToolTip("Sẽ bật khi có bản đa luồng kiểm chứng.")
        self.chk_overwrite = QCheckBox("Ghi đè file đã tồn tại")
        self.chk_excel = QCheckBox("Xuất kèm Excel tổng hợp")
        for c in (self.chk_parallel, self.chk_overwrite, self.chk_excel):
            v.addWidget(c)
        return box

    def _build_progress_group(self) -> QGroupBox:
        box = QGroupBox("Tiến trình chạy")
        v = QVBoxLayout(box)
        self.progress = QProgressBar()
        self.progress.setValue(0)
        v.addWidget(self.progress)
        row = QHBoxLayout()
        self.btn_generate = QPushButton("▶  Bắt đầu tạo PDF")
        self.btn_generate.setObjectName("primary")
        self.btn_generate.clicked.connect(self._on_generate)
        self.btn_open = QPushButton("📂 Mở thư mục")
        self.btn_open.setEnabled(False)
        self.btn_open.clicked.connect(self._on_open_dir)
        row.addWidget(self.btn_generate)
        row.addWidget(self.btn_open)
        row.addStretch(1)
        v.addLayout(row)
        return box

    def _build_log_group(self) -> QGroupBox:
        box = QGroupBox("Nhật ký (realtime)")
        v = QVBoxLayout(box)
        self.log = QTextEdit()
        self.log.setObjectName("log")
        self.log.setReadOnly(True)
        v.addWidget(self.log)
        return box

    # --------------------------------------------------------------- enter
    def on_enter(self) -> None:
        st = self.main.state
        if st.style and not self.pattern_edit.text():
            self.pattern_edit.setText(st.style.output_filename_pattern)
        if not self.dir_edit.text():
            default = str(Path.home() / "Documents")
            self.dir_edit.setText(default if Path(default).is_dir() else str(Path.home()))
        self._update_example()

    def _update_example(self) -> None:
        st = self.main.state
        if st.df is None or st.style is None or not st.style.grouping_column:
            self.example.setText("")
            return
        try:
            groups = R.group_dataframe(st.style, st.df)
            first = next(iter(groups.values()))
            records = R.df_to_records(first)
            ctx = R.build_context(st.style, records, 1)
            name = R.format_output_name(self.pattern_edit.text(), ctx)
            self.example.setText(f"Ví dụ tên file: {name}")
        except Exception:  # noqa: BLE001 - ví dụ tên chỉ là gợi ý
            self.example.setText("")

    # ------------------------------------------------------------- actions
    def _on_browse_dir(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Chọn thư mục lưu PDF")
        if path:
            self.dir_edit.setText(path)

    def _on_open_dir(self) -> None:
        d = self.dir_edit.text().strip()
        if d and Path(d).is_dir():
            open_with_default(d)

    def _on_generate(self) -> None:
        st = self.main.state
        if st.df is None:
            self.main.show_error("Chưa có dữ liệu. Hãy quay lại Bước 1.")
            return
        out_dir = self.dir_edit.text().strip()
        if not out_dir:
            self.main.show_error("Hãy chọn thư mục lưu PDF.")
            return
        st.out_dir = out_dir

        self.log.clear()
        self.progress.setValue(0)
        self.btn_generate.setEnabled(False)
        self.btn_open.setEnabled(False)
        self.main.set_nav_locked(True)  # khóa điều hướng khi đang chạy (Review M1)

        self._thread = QThread()
        # Snapshot style để chỉnh sửa ở bước khác không ảnh hưởng mẻ đang chạy (Review M1).
        self._ctl = BatchController(
            copy.deepcopy(st.style),
            st.df,
            out_dir,
            export_excel=self.chk_excel.isChecked(),
            overwrite=self.chk_overwrite.isChecked(),
            filename_pattern=self.pattern_edit.text().strip() or None,
        )
        self._ctl.moveToThread(self._thread)
        self._thread.started.connect(self._ctl.run)
        self._ctl.progress.connect(self._on_progress)
        self._ctl.log.connect(self._append_log)
        self._ctl.finished.connect(self._on_finished)
        self._ctl.failed.connect(self._on_failed)
        self._ctl.finished.connect(self._thread.quit)
        self._ctl.failed.connect(self._thread.quit)
        self._thread.finished.connect(self._thread.deleteLater)
        self._thread.start()

    # ------------------------------------------------------------- signals
    def _on_progress(self, done: int, total: int) -> None:
        self.progress.setMaximum(total)
        self.progress.setValue(done)

    def _append_log(self, line: str) -> None:
        self.log.append(line)

    def _on_finished(self, summary) -> None:
        self.btn_generate.setEnabled(True)
        self.btn_open.setEnabled(True)
        self.main.set_nav_locked(False)
        self._append_log(
            f"\n— Xong: {summary.succeeded} tạo · {summary.skipped} bỏ qua · "
            f"{summary.failed} lỗi · {summary.total} tổng · {summary.elapsed_sec}s"
        )
        if summary.excel_path:
            self._append_log(f"Excel: {summary.excel_path}")

    def _on_failed(self, message: str) -> None:
        self.btn_generate.setEnabled(True)
        self.main.set_nav_locked(False)
        self._append_log(f"❌ Lỗi: {message}")
        self.main.show_error(message)
