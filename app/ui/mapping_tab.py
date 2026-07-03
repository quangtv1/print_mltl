"""Tab 2 — Biến & Mapping: bảng map biến docxtpl ↔ cột sheet (auto-match + sửa
tay), dropdown chọn cột gom nhóm. Lưu về `style.row_mapping`/`document_fields`.
"""

from __future__ import annotations

from typing import Dict, List

from PyQt5.QtWidgets import (
    QComboBox,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.core.text_match import auto_match

_NONE_LABEL = "— (bỏ trống) —"


class MappingTab(QWidget):
    def __init__(self, window):
        super().__init__()
        self.window = window
        # var -> QComboBox chọn cột sheet
        self._combos: Dict[str, QComboBox] = {}
        self._build_ui()
        self.window.data_loaded.connect(self.refresh)

    def _build_ui(self) -> None:
        layout = QVBoxLayout(self)
        layout.addWidget(
            QLabel(
                "Ghép mỗi biến trong template với một cột trong sheet. "
                "App tự khớp theo tên; bạn có thể sửa lại."
            )
        )

        self.table = QTableWidget(0, 2)
        self.table.setHorizontalHeaderLabels(["Biến (template)", "Cột trong sheet"])
        self.table.horizontalHeader().setSectionResizeMode(
            0, QHeaderView.ResizeToContents
        )
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        self.table.verticalHeader().setVisible(False)
        layout.addWidget(self.table, 1)

        group_row = QHBoxLayout()
        group_row.addWidget(QLabel("Cột gom nhóm:"))
        self.group_combo = QComboBox()
        group_row.addWidget(self.group_combo, 1)
        layout.addLayout(group_row)

        btn_row = QHBoxLayout()
        btn_row.addStretch(1)
        self.save_btn = QPushButton("Lưu mapping")
        self.save_btn.clicked.connect(self._save)
        btn_row.addWidget(self.save_btn)
        layout.addLayout(btn_row)

    def _template_variables(self) -> List[str]:
        """Biến cần map = document_fields + row_mapping (giữ thứ tự ổn định)."""
        style = self.window.state.style
        if not style:
            return []
        return list(style.document_fields.keys()) + list(style.row_mapping.keys())

    def refresh(self) -> None:
        """Dựng lại bảng mapping khi có dữ liệu sheet mới."""
        style = self.window.state.style
        headers = self.window.state.headers
        if not style or not headers:
            return

        variables = self._template_variables()
        current = {**style.document_fields, **style.row_mapping}
        suggested = auto_match(variables, current, headers)

        self._combos.clear()
        self.table.setRowCount(len(variables))
        options = [_NONE_LABEL] + list(headers)
        for i, var in enumerate(variables):
            self.table.setItem(i, 0, QTableWidgetItem(var))
            combo = QComboBox()
            combo.addItems(options)
            match = suggested.get(var, "")
            combo.setCurrentText(match if match in headers else _NONE_LABEL)
            self._combos[var] = combo
            self.table.setCellWidget(i, 1, combo)

        # Cột gom nhóm
        self.group_combo.clear()
        self.group_combo.addItems(headers)
        if style.grouping_column in headers:
            self.group_combo.setCurrentText(style.grouping_column)

    def _save(self) -> None:
        style = self.window.state.style
        if not style:
            return

        doc_vars = set(style.document_fields.keys())
        new_doc: Dict[str, str] = {}
        new_row: Dict[str, str] = {}
        for var, combo in self._combos.items():
            value = combo.currentText()
            col = "" if value == _NONE_LABEL else value
            if var in doc_vars:
                new_doc[var] = col
            else:
                new_row[var] = col

        style.document_fields = new_doc
        style.row_mapping = new_row
        style.grouping_column = self.group_combo.currentText()

        from app.core.style_config import save_style

        save_style(style, self.window.state.style_dir)
        self.window.show_info("Đã lưu mapping vào style.json.")
