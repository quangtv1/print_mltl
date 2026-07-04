"""Bước 1 — Đầu vào: chọn file Excel + sheet + đọc dữ liệu, chọn Mẫu, ghép
biến↔cột (auto-match), chọn cột gom nhóm.

Ghi thẳng vào `main.state.style` (mapping/grouping) + `main.state.df`. Đọc file
qua `workers.run_async` để không treo UI. Đổi đầu vào → `main.bump_data_version()`.
"""

from __future__ import annotations

from pathlib import Path
from typing import List, Tuple

from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import (
    QAbstractItemView,
    QComboBox,
    QFileDialog,
    QGridLayout,
    QGroupBox,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QPushButton,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

from app.core import excel_reader
from app.core.platform_utils import styles_root
from app.core.style_config import list_styles, load_style
from app.core.text_match import auto_match
from app.ui.workers import run_async


class StepInput(QWidget):
    def __init__(self, main):
        super().__init__()
        self.main = main
        self._var_rows: List[Tuple[str, str]] = []  # (var, kind) song song với hàng bảng

        root = QVBoxLayout(self)
        root.setSpacing(14)
        root.addLayout(self._build_top_row())
        root.addWidget(self._build_mapping_group(), 1)
        root.addWidget(self._build_grouping_group())

        self._reload_style_list()

    # ------------------------------------------------------------ build UI
    def _build_top_row(self):
        row = QHBoxLayout()
        row.setSpacing(14)
        row.addWidget(self._build_excel_group(), 3)
        row.addWidget(self._build_template_group(), 2)
        return row

    def _build_excel_group(self) -> QGroupBox:
        box = QGroupBox("Nguồn dữ liệu Excel")
        g = QGridLayout(box)
        g.setColumnStretch(1, 1)

        g.addWidget(QLabel("Tệp:"), 0, 0)
        self.path_edit = QLineEdit()
        self.path_edit.setReadOnly(True)
        self.path_edit.setPlaceholderText("Chọn file .xlsx…")
        g.addWidget(self.path_edit, 0, 1)
        browse = QPushButton("Duyệt…")
        browse.clicked.connect(self._on_browse)
        g.addWidget(browse, 0, 2)

        g.addWidget(QLabel("Sheet:"), 1, 0)
        self.sheet_combo = QComboBox()
        g.addWidget(self.sheet_combo, 1, 1)
        self.read_btn = QPushButton("Đọc dữ liệu")
        self.read_btn.setObjectName("primary")
        self.read_btn.setEnabled(False)
        self.read_btn.clicked.connect(self._on_read)
        g.addWidget(self.read_btn, 1, 2)

        self.status = QLabel("Chưa đọc dữ liệu.")
        self.status.setProperty("hint", "true")
        g.addWidget(self.status, 2, 0, 1, 3)
        return box

    def _build_template_group(self) -> QGroupBox:
        box = QGroupBox("Mẫu (template)")
        v = QVBoxLayout(box)
        self.style_combo = QComboBox()
        self.style_combo.currentIndexChanged.connect(self._on_style_change)
        v.addWidget(self.style_combo)
        self.style_info = QLabel("")
        self.style_info.setProperty("info", "true")
        v.addWidget(self.style_info)
        v.addStretch(1)
        return box

    def _build_mapping_group(self) -> QGroupBox:
        box = QGroupBox("Ghép biến ↔ cột trong file Excel")
        v = QVBoxLayout(box)
        self.table = QTableWidget(0, 4)
        self.table.setHorizontalHeaderLabels(
            ["#", "Cột trong file Excel", "Biến trong mẫu", "Tự khớp"]
        )
        self.table.verticalHeader().setVisible(False)
        self.table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.table.setSelectionMode(QAbstractItemView.NoSelection)
        hh = self.table.horizontalHeader()
        hh.setSectionResizeMode(0, QHeaderView.ResizeToContents)
        hh.setSectionResizeMode(1, QHeaderView.Stretch)
        hh.setSectionResizeMode(2, QHeaderView.Stretch)
        hh.setSectionResizeMode(3, QHeaderView.ResizeToContents)
        v.addWidget(self.table)
        return box

    def _build_grouping_group(self) -> QGroupBox:
        box = QGroupBox("Cột gom nhóm hồ sơ")
        v = QVBoxLayout(box)
        self.group_combo = QComboBox()
        self.group_combo.currentTextChanged.connect(self._on_group_change)
        v.addWidget(self.group_combo)
        hint = QLabel("Mỗi giá trị khác nhau trong cột này → 1 file PDF.")
        hint.setProperty("hint", "true")
        v.addWidget(hint)
        return box

    # ------------------------------------------------------------- actions
    def _reload_style_list(self) -> None:
        self.style_combo.blockSignals(True)
        self.style_combo.clear()
        names = list_styles(styles_root())
        self._style_names = names
        for name in names:
            try:
                cfg = load_style(styles_root() / name)
                self.style_combo.addItem(cfg.name or name, name)
            except Exception:  # noqa: BLE001 - style hỏng vẫn liệt kê theo tên thư mục
                self.style_combo.addItem(name, name)
        # Chọn style đang active nếu có.
        if self.main.state.style_dir is not None:
            cur = self.main.state.style_dir.name
            idx = self.style_combo.findData(cur)
            if idx >= 0:
                self.style_combo.setCurrentIndex(idx)
        self.style_combo.blockSignals(False)
        self._on_style_change()

    def _on_browse(self) -> None:
        path, _ = QFileDialog.getOpenFileName(
            self, "Chọn file Excel", "", "Excel (*.xlsx *.xlsm)"
        )
        if not path:
            return
        self.path_edit.setText(path)
        self.main.state.excel_path = path
        self.status.setText("Đang đọc danh sách sheet…")
        try:
            sheets = excel_reader.list_sheets(path)
        except excel_reader.ExcelReadError as e:
            self.sheet_combo.clear()
            self.read_btn.setEnabled(False)
            self.main.show_error(str(e))
            self.status.setText("Không đọc được file.")
            return
        self.sheet_combo.clear()
        self.sheet_combo.addItems(sheets)
        self.read_btn.setEnabled(bool(sheets))
        self.status.setText(f"Đã tìm thấy {len(sheets)} sheet. Bấm “Đọc dữ liệu”.")

    def _on_read(self) -> None:
        path = self.path_edit.text().strip()
        sheet = self.sheet_combo.currentText()
        if not path or not sheet:
            return
        self.read_btn.setEnabled(False)
        self.status.setText("Đang đọc dữ liệu…")

        def work():
            return excel_reader.read_df(path, sheet)

        def done(df):
            self.read_btn.setEnabled(True)
            self.main.state.df = df
            self.main.state.sheet = sheet
            self.main.state.headers = [str(c) for c in df.columns]
            self.main.bump_data_version()
            self.status.setText(
                f"✓ OK đọc {len(df)} dòng · {len(df.columns)} cột"
            )
            self.status.setProperty("ok", "true")
            self.status.setProperty("hint", "false")
            self.status.style().unpolish(self.status)
            self.status.style().polish(self.status)
            self._rebuild_mapping()
            self._rebuild_grouping()

        def err(msg):
            self.read_btn.setEnabled(True)
            self.status.setText("Lỗi đọc dữ liệu.")
            self.main.show_error(msg)

        run_async(self, work, done, err)

    def _on_style_change(self) -> None:
        name = self.style_combo.currentData()
        if not name:
            return
        self.main.state.style_dir = styles_root() / name
        self.main.state.style = load_style(self.main.state.style_dir)
        nvars = len(self.main.state.style.document_fields) + len(
            self.main.state.style.row_mapping
        )
        self.style_info.setText(f"{nvars} biến đầu vào cho mẫu này")
        self.main.bump_data_version()
        if self.main.state.headers:
            self._rebuild_mapping()
            self._rebuild_grouping()

    # ------------------------------------------------------- mapping table
    def _rebuild_mapping(self) -> None:
        style = self.main.state.style
        headers = self.main.state.headers
        if style is None:
            return

        # Danh sách biến: document fields trước, rồi row mapping (giữ thứ tự).
        self._var_rows = [(v, "doc") for v in style.document_fields] + [
            (v, "row") for v in style.row_mapping
        ]
        variables = [v for v, _ in self._var_rows]
        current = {**style.document_fields, **style.row_mapping}
        matched = auto_match(variables, current, headers)

        self.table.setRowCount(len(self._var_rows))
        for r, (var, kind) in enumerate(self._var_rows):
            self.table.setItem(r, 0, self._cell(str(r + 1)))

            combo = QComboBox()
            combo.addItem("— (bỏ trống) —", "")
            for h in headers:
                combo.addItem(h, h)
            chosen = matched.get(var, "")
            idx = combo.findData(chosen) if chosen else 0
            combo.setCurrentIndex(max(0, idx))
            combo.currentIndexChanged.connect(
                lambda _i, row=r: self._on_map_change(row)
            )
            self.table.setCellWidget(r, 1, combo)

            self.table.setItem(r, 2, self._cell("{" + var + "}"))
            ok = "✓ khớp" if chosen else "—"
            item = self._cell(ok)
            if chosen:
                item.setForeground(Qt.darkGreen)
            self.table.setItem(r, 3, item)

            # Ghi ngược lựa chọn auto vào style ngay để nhất quán.
            self._write_map(var, kind, chosen)

    def _on_map_change(self, row: int) -> None:
        var, kind = self._var_rows[row]
        combo = self.table.cellWidget(row, 1)
        chosen = combo.currentData() or ""
        self._write_map(var, kind, chosen)
        item = self.table.item(row, 3)
        item.setText("✓ khớp" if chosen else "—")
        item.setForeground(Qt.darkGreen if chosen else Qt.gray)
        self.main.bump_data_version()

    def _write_map(self, var: str, kind: str, header: str) -> None:
        target = (
            self.main.state.style.document_fields
            if kind == "doc"
            else self.main.state.style.row_mapping
        )
        target[var] = header

    # ------------------------------------------------------------ grouping
    def _rebuild_grouping(self) -> None:
        headers = self.main.state.headers
        self.group_combo.blockSignals(True)
        self.group_combo.clear()
        self.group_combo.addItems(headers)
        cur = self.main.state.style.grouping_column if self.main.state.style else ""
        if cur in headers:
            self.group_combo.setCurrentText(cur)
        elif headers:
            self.main.state.style.grouping_column = self.group_combo.currentText()
        self.group_combo.blockSignals(False)

    def _on_group_change(self, text: str) -> None:
        if self.main.state.style is not None and text:
            self.main.state.style.grouping_column = text
            self.main.bump_data_version()

    # ------------------------------------------------------------- gating
    def validate_next(self):
        st = self.main.state
        if st.df is None:
            return "Hãy chọn file Excel và bấm “Đọc dữ liệu” trước."
        if not st.style or not st.style.grouping_column:
            return "Hãy chọn cột gom nhóm hồ sơ."
        if st.style.grouping_column not in st.headers:
            return (
                f"Cột gom nhóm '{st.style.grouping_column}' không có trong dữ liệu. "
                "Hãy chọn lại."
            )
        return None

    @staticmethod
    def _cell(text: str) -> QTableWidgetItem:
        item = QTableWidgetItem(text)
        item.setFlags(Qt.ItemIsEnabled)
        return item
