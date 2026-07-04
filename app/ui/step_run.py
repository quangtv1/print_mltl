"""Bước 3 — Chạy (khớp prototype): thư mục + mẫu tên PDF (preset + tự do), tùy chọn,
progress + **log terminal realtime**, tự mở thư mục khi xong.

Nút chạy nằm ở action bar (main_window gọi `primary_action`). Checkbox "đa luồng"
disabled ở MVP (render serialize — Red Team #4). `BatchController` (P3) chạy trong
QThread; snapshot style để tránh sửa giữa chừng (Review M1).
"""

from __future__ import annotations

import copy
import html
from pathlib import Path

from PyQt5.QtCore import Qt, QThread
from PyQt5.QtWidgets import (
    QCheckBox,
    QComboBox,
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

_PDF_PRESETS = [
    "MLHS_{ho_so_so}.pdf",
    "{stt_file}_{ho_so_so}.pdf",
    "{ho_so_so}_{ngay_gio}.pdf",
    "MucLuc_{ho_so_so}.pdf",
]
_MONO = "font-family:Consolas,'Courier New',monospace; color:#0057b7;"


class StepRun(QWidget):
    def __init__(self, main):
        super().__init__()
        self.main = main
        self._thread = None
        self._ctl = None
        self._running = False

        root = QVBoxLayout(self)
        root.setContentsMargins(22, 16, 22, 16)
        root.setSpacing(12)

        top = QHBoxLayout()
        top.setSpacing(16)
        top.addWidget(self._build_output_group(), 3)
        top.addWidget(self._build_options_group(), 2)
        root.addLayout(top)

        root.addLayout(self._build_progress_row())
        self.progress = QProgressBar()
        self.progress.setTextVisible(False)
        self.progress.setValue(0)
        root.addWidget(self.progress)
        root.addWidget(self._build_log_group(), 1)

    # ------------------------------------------------------------ build UI
    def _build_output_group(self) -> QGroupBox:
        box = QGroupBox("Thư mục && tên file xuất")
        g = QGridLayout(box)
        g.setColumnStretch(1, 1)
        g.setHorizontalSpacing(8)

        g.addWidget(self._rlabel("Thư mục:"), 0, 0)
        self.dir_edit = QLineEdit()
        self.dir_edit.setReadOnly(True)
        self.dir_edit.setPlaceholderText("Chọn thư mục lưu PDF…")
        g.addWidget(self.dir_edit, 0, 1, 1, 2)
        browse = QPushButton("Duyệt…")
        browse.clicked.connect(self._on_browse_dir)
        g.addWidget(browse, 0, 3)

        g.addWidget(self._rlabel("Tên PDF:"), 1, 0)
        self.preset_combo = QComboBox()
        self.preset_combo.addItems(_PDF_PRESETS)
        self.preset_combo.setFixedWidth(190)
        self.preset_combo.activated.connect(
            lambda: self.pattern_edit.setText(self.preset_combo.currentText())
        )
        g.addWidget(self.preset_combo, 1, 1)
        self.pattern_edit = QLineEdit()
        self.pattern_edit.setStyleSheet(_MONO)
        self.pattern_edit.textChanged.connect(self._update_example)
        g.addWidget(self.pattern_edit, 1, 2, 1, 2)

        self.example = QLabel("")
        self.example.setProperty("hint", "true")
        self.example.setWordWrap(True)
        g.addWidget(self.example, 2, 1, 1, 3)
        return box

    def _build_options_group(self) -> QGroupBox:
        box = QGroupBox("Tùy chọn chạy")
        v = QVBoxLayout(box)
        v.setSpacing(8)
        self.chk_parallel = QCheckBox("Chạy đa luồng (song song — nhanh nhất)")
        self.chk_parallel.setEnabled(False)  # MVP: serialize (Red Team #4)
        self.chk_parallel.setToolTip("Bản MVP xuất tuần tự để đảm bảo ổn định.")
        self.chk_overwrite = QCheckBox("Ghi đè file đã tồn tại")
        self.chk_excel = QCheckBox("Xuất kèm file Excel tổng hợp")
        for c in (self.chk_parallel, self.chk_overwrite, self.chk_excel):
            v.addWidget(c)
        v.addStretch(1)
        return box

    def _build_progress_row(self) -> QHBoxLayout:
        row = QHBoxLayout()
        self.progress_title = QLabel("Tiến trình chạy")
        self.gen_status = QLabel("")
        self.gen_status.setProperty("muted", "true")
        self.btn_open = QPushButton("📁 Mở thư mục kết quả")
        self.btn_open.clicked.connect(self._on_open_dir)
        row.addWidget(self.progress_title)
        row.addStretch(1)
        row.addWidget(self.gen_status)
        row.addWidget(self.btn_open)
        return row

    def _on_open_dir(self) -> None:
        d = self.dir_edit.text().strip()
        if d and Path(d).is_dir():
            open_with_default(d)

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

    def status_text(self) -> str:
        return "Bước 3/3 — Chọn thư mục xuất và bắt đầu tạo PDF."

    def primary_label(self) -> str:
        return "▶ Bắt đầu tạo PDF"

    def primary_action(self) -> None:
        self._on_generate()

    def _update_example(self) -> None:
        st = self.main.state
        if st.df is None or st.style is None or not st.style.grouping_column:
            self.example.setText("")
            return
        try:
            groups = R.group_dataframe(st.style, st.df)
            first = next(iter(groups.values()))
            ctx = R.build_context(st.style, R.df_to_records(first), 1)
            name = R.format_output_name(self.pattern_edit.text(), ctx)
            self.example.setText(f"Ví dụ tên file: {name}")
        except Exception:  # noqa: BLE001 - ví dụ tên chỉ là gợi ý
            self.example.setText("")

    # ------------------------------------------------------------- actions
    def _on_browse_dir(self) -> None:
        path = QFileDialog.getExistingDirectory(self, "Chọn thư mục lưu PDF")
        if path:
            self.dir_edit.setText(path)

    def _on_generate(self) -> None:
        if self._running:
            return
        st = self.main.state
        if st.df is None:
            self.main.show_error("Chưa có dữ liệu. Hãy quay lại Bước 1.")
            return
        out_dir = self.dir_edit.text().strip()
        if not out_dir:
            self.main.show_error("Hãy chọn thư mục lưu PDF.")
            return
        st.out_dir = out_dir

        self._running = True
        self.log.clear()
        self.progress.setValue(0)
        self.gen_status.setText("Đang chạy…")
        self.main.set_nav_locked(True)

        self._thread = QThread()
        self._ctl = BatchController(
            copy.deepcopy(st.style),  # snapshot (Review M1)
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
        self.progress_title.setText(f"Tiến trình chạy ({done}/{total})")

    def _append_log(self, line: str) -> None:
        color = "#dcdcdc"
        if line.startswith("✅") or line.startswith("📊"):
            color = "#5ac85a"
        elif line.startswith("❌") or line.startswith("⚠️"):
            color = "#ff6b6b"
        elif line.startswith("⏭️"):
            color = "#c8a24a"
        self.log.append(f'<span style="color:{color};">{html.escape(line)}</span>')

    def _on_finished(self, summary) -> None:
        self._running = False
        self.main.set_nav_locked(False)
        self.gen_status.setText(
            f"Xong · {summary.succeeded} tạo · {summary.skipped} bỏ qua · "
            f"{summary.failed} lỗi · {summary.elapsed_sec}s"
        )
        self._append_log(
            f"— Hoàn tất: {summary.succeeded}/{summary.total} hồ sơ trong {summary.elapsed_sec}s"
        )
        if summary.excel_path:
            self._append_log(f"📊  Excel: {summary.excel_path}")
        # Tự mở thư mục kết quả.
        d = self.dir_edit.text().strip()
        if d and Path(d).is_dir():
            open_with_default(d)

    def _on_failed(self, message: str) -> None:
        self._running = False
        self.main.set_nav_locked(False)
        self.gen_status.setText("Lỗi")
        self._append_log(f"❌ Lỗi: {message}")
        self.main.show_error(message)

    @staticmethod
    def _rlabel(text: str) -> QLabel:
        lbl = QLabel(text)
        lbl.setFixedWidth(78)
        lbl.setAlignment(Qt.AlignRight | Qt.AlignVCenter)
        return lbl
