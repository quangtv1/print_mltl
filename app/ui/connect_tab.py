"""Tab 1 — Kết nối: chọn khóa service-account, nhập URL, kết nối, chọn worksheet,
tải dữ liệu. Thao tác mạng chạy trên QThread (không treo UI).
"""

from __future__ import annotations

from PyQt5.QtWidgets import (
    QComboBox,
    QFileDialog,
    QFormLayout,
    QGroupBox,
    QHBoxLayout,
    QLabel,
    QLineEdit,
    QPushButton,
    QVBoxLayout,
    QWidget,
)

from app.core import sheets_client
from app.ui.workers import run_async


class ConnectTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self.window = window
        self._build_ui()

    def _build_ui(self) -> None:
        layout = QVBoxLayout(self)

        creds_box = QGroupBox("Khóa & URL")
        form = QFormLayout(creds_box)

        self.creds_edit = QLineEdit()
        self.creds_edit.setPlaceholderText("Chọn file khóa service-account (.json)")
        creds_btn = QPushButton("Chọn...")
        creds_btn.clicked.connect(self._browse_creds)
        creds_row = QHBoxLayout()
        creds_row.addWidget(self.creds_edit)
        creds_row.addWidget(creds_btn)
        form.addRow("File khóa:", self._wrap(creds_row))

        self.url_edit = QLineEdit()
        self.url_edit.setPlaceholderText("https://docs.google.com/spreadsheets/d/...")
        form.addRow("URL Sheet:", self.url_edit)

        self.connect_btn = QPushButton("Kết nối")
        self.connect_btn.clicked.connect(self._connect)
        form.addRow("", self.connect_btn)

        layout.addWidget(creds_box)

        ws_box = QGroupBox("Worksheet")
        ws_layout = QHBoxLayout(ws_box)
        self.ws_combo = QComboBox()
        self.ws_combo.setEnabled(False)
        self.load_btn = QPushButton("Tải dữ liệu")
        self.load_btn.setEnabled(False)
        self.load_btn.clicked.connect(self._load_data)
        ws_layout.addWidget(QLabel("Chọn sheet:"))
        ws_layout.addWidget(self.ws_combo, 1)
        ws_layout.addWidget(self.load_btn)
        layout.addWidget(ws_box)

        self.status = QLabel("Chưa kết nối.")
        layout.addWidget(self.status)
        layout.addStretch(1)

    @staticmethod
    def _wrap(inner_layout) -> QWidget:
        w = QWidget()
        w.setLayout(inner_layout)
        return w

    def _browse_creds(self) -> None:
        path, _ = QFileDialog.getOpenFileName(
            self, "Chọn file khóa service-account", "", "JSON (*.json)"
        )
        if path:
            self.creds_edit.setText(path)

    def _connect(self) -> None:
        creds = self.creds_edit.text().strip()
        url = self.url_edit.text().strip()
        if not creds or not url:
            self.window.show_error("Hãy chọn file khóa và nhập URL Sheet.")
            return

        self.connect_btn.setEnabled(False)
        self.status.setText("Đang kết nối...")

        def task():
            client = sheets_client.authorize(creds)
            ss = sheets_client.open_spreadsheet(client, url)
            names = sheets_client.list_worksheets(ss)
            return ss, names

        def done(result):
            ss, names = result
            self.window.state.creds_path = creds
            self.window.state.spreadsheet = ss
            self.ws_combo.clear()
            self.ws_combo.addItems(names)
            self.ws_combo.setEnabled(True)
            self.load_btn.setEnabled(bool(names))
            self.connect_btn.setEnabled(True)
            self.status.setText(f"Đã kết nối. {len(names)} worksheet.")

        def error(msg):
            self.connect_btn.setEnabled(True)
            self.status.setText("Kết nối thất bại.")
            self.window.show_error(msg)

        run_async(self, task, done, error)

    def _load_data(self) -> None:
        ws_name = self.ws_combo.currentText()
        ss = self.window.state.spreadsheet
        if not ws_name or ss is None:
            return

        self.load_btn.setEnabled(False)
        self.status.setText(f"Đang tải '{ws_name}'...")

        def task():
            df = sheets_client.read_df(ss, ws_name)
            if df is None:
                raise sheets_client.SheetsError(f"Sheet '{ws_name}' trống.")
            return df

        def done(df):
            self.window.state.df = df
            self.window.state.headers = sheets_client.get_headers(df)
            self.load_btn.setEnabled(True)
            self.status.setText(
                f"Đã tải {len(df)} dòng, {len(self.window.state.headers)} cột."
            )
            self.window.data_loaded.emit()
            self.window.tabs.setCurrentWidget(self.window.mapping_tab)

        def error(msg):
            self.load_btn.setEnabled(True)
            self.status.setText("Tải dữ liệu thất bại.")
            self.window.show_error(msg)

        run_async(self, task, done, error)
