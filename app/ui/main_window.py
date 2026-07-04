"""Cửa sổ chính: wizard **3 bước** (Đầu vào → Thiết kế & Preview → Chạy).

`QStackedWidget` giữ 3 step; `StepHeader` hiển thị tiến trình; action bar dưới có
Quay lại / Tiếp theo. State dùng chung (StyleConfig + df + Excel path + out_dir)
sống ở đây; mỗi step đọc/ghi qua `self.main`. `data_version` tăng khi đầu vào đổi
(file/sheet/cột gom nhóm/mapping) → step sau **recompute** trên `on_enter` (Red Team
invalidation), không cache 1 lần.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional

import pandas as pd
from PyQt5.QtWidgets import (
    QHBoxLayout,
    QMainWindow,
    QMessageBox,
    QPushButton,
    QStackedWidget,
    QVBoxLayout,
    QWidget,
)

from app.core.platform_utils import styles_root
from app.core.style_config import list_styles, load_style
from app.models.style import StyleConfig
from app.ui.theme import StepHeader


@dataclass
class AppState:
    """State dùng chung giữa 3 bước."""

    style: Optional[StyleConfig] = None
    style_dir: Optional[Path] = None
    excel_path: str = ""
    sheet: str = ""
    df: Optional[pd.DataFrame] = None
    headers: List[str] = field(default_factory=list)
    out_dir: str = ""
    # Tăng mỗi khi đầu vào đổi → step sau biết cần dựng lại groups/records/panel.
    data_version: int = 0


class MainWindow(QMainWindow):
    """Cửa sổ chính chứa state + 3 step + điều hướng."""

    STEP_TITLES = ("Đầu vào", "Thiết kế & Preview", "Chạy")

    def __init__(self):
        super().__init__()
        self.setWindowTitle("Tạo Mục Lục Hồ Sơ")
        self.resize(1040, 760)

        self.state = AppState()
        self._nav_locked = False
        self._load_default_style()

        root = QWidget()
        root.setObjectName("Root")
        outer = QVBoxLayout(root)
        outer.setContentsMargins(0, 0, 0, 0)
        outer.setSpacing(0)

        self.header = StepHeader(self.STEP_TITLES)
        outer.addWidget(self.header)

        self.stack = QStackedWidget()
        body = QWidget()
        body_l = QVBoxLayout(body)
        body_l.setContentsMargins(20, 16, 20, 16)
        body_l.addWidget(self.stack)
        outer.addWidget(body, 1)

        outer.addWidget(self._build_action_bar())
        self.setCentralWidget(root)

        # Import trễ để tránh vòng import.
        from app.ui.step_design import StepDesign
        from app.ui.step_input import StepInput
        from app.ui.step_run import StepRun

        self.step_input = StepInput(self)
        self.step_design = StepDesign(self)
        self.step_run = StepRun(self)
        for w in (self.step_input, self.step_design, self.step_run):
            self.stack.addWidget(w)

        self._index = 0
        self.go_to(0)

    # ------------------------------------------------------------------ nav
    def _build_action_bar(self) -> QWidget:
        bar = QWidget()
        bar.setObjectName("ActionBar")
        lay = QHBoxLayout(bar)
        lay.setContentsMargins(24, 12, 24, 12)
        self.btn_back = QPushButton("◀  Quay lại")
        self.btn_back.clicked.connect(self._go_back)
        self.btn_next = QPushButton("Tiếp theo  ▶")
        self.btn_next.setObjectName("primary")
        self.btn_next.clicked.connect(self._go_next)
        lay.addWidget(self.btn_back)
        lay.addStretch(1)
        lay.addWidget(self.btn_next)
        return bar

    def go_to(self, index: int) -> None:
        """Chuyển tới bước `index`: cập nhật stack/header/action bar + gọi on_enter."""
        index = max(0, min(index, self.stack.count() - 1))
        self._index = index
        self.stack.setCurrentIndex(index)
        self.header.set_current(index)
        self.btn_back.setEnabled(index > 0)
        self.btn_next.setVisible(index < self.stack.count() - 1)

        widget = self.stack.widget(index)
        if hasattr(widget, "on_enter"):
            widget.on_enter()

    def _go_back(self) -> None:
        if self._index > 0:
            self.go_to(self._index - 1)

    def _go_next(self) -> None:
        widget = self.stack.widget(self._index)
        # validate_next trả None nếu OK, hoặc chuỗi lỗi để chặn.
        if hasattr(widget, "validate_next"):
            err = widget.validate_next()
            if err:
                self.show_error(err)
                return
        self.go_to(self._index + 1)

    def bump_data_version(self) -> None:
        """Đánh dấu đầu vào đã đổi để step sau dựng lại state."""
        self.state.data_version += 1

    def set_nav_locked(self, locked: bool) -> None:
        """Khóa/mở điều hướng (dùng khi đang chạy batch để không sửa state đang render)."""
        self._nav_locked = locked
        self.btn_back.setEnabled(not locked and self._index > 0)
        self.btn_next.setEnabled(not locked)

    # --------------------------------------------------------------- helpers
    def _load_default_style(self) -> None:
        root = styles_root()
        names = list_styles(root)
        if names:
            self.state.style_dir = root / names[0]
            self.state.style = load_style(self.state.style_dir)

    def show_error(self, message: str, title: str = "Lỗi") -> None:
        QMessageBox.critical(self, title, message)

    def show_info(self, message: str, title: str = "Thông báo") -> None:
        QMessageBox.information(self, title, message)
