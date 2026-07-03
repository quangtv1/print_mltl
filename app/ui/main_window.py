"""Cửa sổ chính: QTabWidget nối 5 bước (kết nối → biến/mapping → settings →
preview → generate). Giữ state dùng chung (style, dữ liệu sheet) cho các tab.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import List, Optional

import pandas as pd
from PyQt5.QtCore import pyqtSignal
from PyQt5.QtWidgets import QMainWindow, QMessageBox, QTabWidget

from app.core.platform_utils import styles_root
from app.core.style_config import list_styles, load_style
from app.models.style import StyleConfig


@dataclass
class AppState:
    """State dùng chung giữa các tab."""

    style: Optional[StyleConfig] = None
    style_dir: Optional[Path] = None
    creds_path: str = ""
    spreadsheet: object = None  # gspread Spreadsheet
    df: Optional[pd.DataFrame] = None
    headers: List[str] = field(default_factory=list)


class MainWindow(QMainWindow):
    """Cửa sổ chính chứa state + các tab."""

    # Phát khi dữ liệu sheet (df/headers) đã sẵn sàng.
    data_loaded = pyqtSignal()

    def __init__(self):
        super().__init__()
        self.setWindowTitle("Tạo Mục Lục Hồ Sơ")
        self.resize(980, 720)

        self.state = AppState()
        self._load_default_style()

        self.tabs = QTabWidget()
        self.setCentralWidget(self.tabs)

        # Import trễ để tránh vòng import.
        from app.ui.connect_tab import ConnectTab
        from app.ui.mapping_tab import MappingTab
        from app.ui.preview_widget import PreviewWidget
        from app.ui.settings_tab import SettingsTab

        self.connect_tab = ConnectTab(self)
        self.mapping_tab = MappingTab(self)
        self.settings_tab = SettingsTab(self)
        self.preview_widget = PreviewWidget(self)

        self.tabs.addTab(self.connect_tab, "1. Kết nối")
        self.tabs.addTab(self.mapping_tab, "2. Biến & Mapping")
        self.tabs.addTab(self.settings_tab, "3. Thiết lập")
        self.tabs.addTab(self.preview_widget, "4. Xem trước")

        # Generate tab (P6) gắn thêm nếu có.
        try:
            from app.ui.generate_tab import GenerateTab

            self.generate_tab = GenerateTab(self)
            self.tabs.addTab(self.generate_tab, "5. Xuất hàng loạt")
        except ModuleNotFoundError:
            self.generate_tab = None

    def _load_default_style(self) -> None:
        """Nạp style đầu tiên tìm thấy trong `styles/` làm style hiện hành."""
        root = styles_root()
        names = list_styles(root)
        if names:
            self.state.style_dir = root / names[0]
            self.state.style = load_style(self.state.style_dir)

    def show_error(self, message: str, title: str = "Lỗi") -> None:
        QMessageBox.critical(self, title, message)

    def show_info(self, message: str, title: str = "Thông báo") -> None:
        QMessageBox.information(self, title, message)
