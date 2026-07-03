"""Tab 3 — Thiết lập: form text settings (cơ quan, tiêu đề, người ký...), ảnh
chữ ký, toggle footer. Buộc 2 chiều với `style.settings`; Lưu → save_style.
"""

from __future__ import annotations

from PyQt5.QtWidgets import (
    QCheckBox,
    QFileDialog,
    QFormLayout,
    QHBoxLayout,
    QLineEdit,
    QPushButton,
    QVBoxLayout,
    QWidget,
)

# (khóa settings, nhãn hiển thị) cho các trường text.
_TEXT_FIELDS = [
    ("co_quan_dong1", "Cơ quan (dòng 1)"),
    ("co_quan_dong2", "Cơ quan (dòng 2)"),
    ("tieu_de", "Tiêu đề"),
    ("chuc_danh_ky", "Chức danh ký"),
    ("nguoi_ky", "Người ký"),
    ("font_name", "Font chữ"),
    ("footer_format", "Định dạng footer"),
]


class SettingsTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self.window = window
        self._edits = {}
        self._build_ui()
        self.load_from_style()

    def _build_ui(self) -> None:
        layout = QVBoxLayout(self)
        form = QFormLayout()

        for key, label in _TEXT_FIELDS:
            edit = QLineEdit()
            self._edits[key] = edit
            form.addRow(label + ":", edit)

        # Ảnh chữ ký
        self.sig_edit = QLineEdit()
        sig_btn = QPushButton("Chọn...")
        sig_btn.clicked.connect(self._browse_signature)
        sig_row = QHBoxLayout()
        sig_row.addWidget(self.sig_edit)
        sig_row.addWidget(sig_btn)
        sig_wrap = QWidget()
        sig_wrap.setLayout(sig_row)
        form.addRow("Ảnh chữ ký:", sig_wrap)

        # Footer toggle
        self.footer_check = QCheckBox("Hiện số trang ở footer")
        form.addRow("", self.footer_check)

        layout.addLayout(form)

        btn_row = QHBoxLayout()
        btn_row.addStretch(1)
        self.save_btn = QPushButton("Lưu thiết lập")
        self.save_btn.clicked.connect(self._save)
        btn_row.addWidget(self.save_btn)
        layout.addLayout(btn_row)
        layout.addStretch(1)

    def load_from_style(self) -> None:
        style = self.window.state.style
        if not style:
            return
        s = style.settings
        for key, edit in self._edits.items():
            edit.setText(str(s.get(key, "")))
        self.sig_edit.setText(str(s.get("anh_chu_ky_path", "")))
        self.footer_check.setChecked(bool(s.get("footer_page_number", True)))

    def _browse_signature(self) -> None:
        path, _ = QFileDialog.getOpenFileName(
            self, "Chọn ảnh chữ ký", "", "Ảnh (*.png *.jpg *.jpeg)"
        )
        if path:
            self.sig_edit.setText(path)

    def _save(self) -> None:
        style = self.window.state.style
        if not style:
            return
        for key, edit in self._edits.items():
            style.settings[key] = edit.text()
        style.settings["anh_chu_ky_path"] = self.sig_edit.text()
        style.settings["footer_page_number"] = self.footer_check.isChecked()

        from app.core.style_config import save_style

        save_style(style, self.window.state.style_dir)
        self.window.show_info("Đã lưu thiết lập vào style.json.")
