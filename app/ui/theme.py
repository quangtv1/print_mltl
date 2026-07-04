"""Theme Windows Fluent (sáng) cho app + widget stepper 3 bước.

Accent `#0078d7`, nền `#f0f0f0`/trắng, chọn `#e5f1fb`, xác nhận xanh `#107c10`,
font "Segoe UI". Nút hành động chính đặt `objectName("primary")`; nhãn tiêu đề
section đặt property `section="true"`; vùng log `objectName("log")`.
"""

from __future__ import annotations

from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import QFrame, QHBoxLayout, QLabel, QVBoxLayout, QWidget

# --- Design tokens (Fluent) ---
ACCENT = "#0078d7"
ACCENT_HOVER = "#106ebe"
ACCENT_PRESSED = "#005a9e"
BG = "#f0f0f0"
SURFACE = "#ffffff"
TEXT = "#201f1e"
MUTED = "#605e5c"
BORDER = "#d1d1d1"
SELECT_BG = "#e5f1fb"
SUCCESS = "#107c10"
DISABLED_BG = "#e8e8e8"
DISABLED_TEXT = "#a19f9d"
RADIUS = "4px"

FONT_STACK = '"Segoe UI", "Helvetica Neue", "Arial", "Noto Sans", sans-serif'

QSS = f"""
* {{
    font-family: {FONT_STACK};
    font-size: 14px;
    color: {TEXT};
}}

QMainWindow, QWidget#Root {{ background: {BG}; }}
QWidget#Header {{ background: {SURFACE}; border-bottom: 1px solid {BORDER}; }}
QWidget#ActionBar {{ background: {SURFACE}; border-top: 1px solid {BORDER}; }}

QLabel[section="true"] {{
    font-size: 13px;
    font-weight: 700;
    color: {MUTED};
    text-transform: uppercase;
}}
QLabel[hint="true"] {{ color: {MUTED}; font-size: 12.5px; }}
QLabel[ok="true"] {{ color: {SUCCESS}; font-weight: 600; }}
QLabel[info="true"] {{ color: {ACCENT}; font-weight: 600; }}

/* ---- Cards / group boxes ---- */
QGroupBox {{
    background: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: {RADIUS};
    margin-top: 12px;
    padding: 16px 14px 12px 14px;
    font-weight: 700;
}}
QGroupBox::title {{
    subcontrol-origin: margin;
    subcontrol-position: top left;
    left: 10px;
    padding: 0 6px;
    color: {TEXT};
}}

/* ---- Inputs ---- */
QLineEdit, QComboBox, QSpinBox {{
    background: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: {RADIUS};
    padding: 6px 9px;
    selection-background-color: {ACCENT};
    selection-color: #ffffff;
}}
QLineEdit:focus, QComboBox:focus {{ border: 1px solid {ACCENT}; }}
QLineEdit:disabled, QComboBox:disabled {{ background: {DISABLED_BG}; color: {DISABLED_TEXT}; }}
QComboBox::drop-down {{ border: none; width: 20px; }}
QComboBox QAbstractItemView {{
    background: {SURFACE};
    border: 1px solid {BORDER};
    selection-background-color: {SELECT_BG};
    selection-color: {TEXT};
    outline: none;
}}

/* ---- Buttons ---- */
QPushButton {{
    background: {SURFACE};
    color: {TEXT};
    border: 1px solid {BORDER};
    border-radius: {RADIUS};
    padding: 7px 16px;
    font-weight: 600;
}}
QPushButton:hover {{ background: #f3f3f3; border-color: #b3b3b3; }}
QPushButton:pressed {{ background: #ececec; }}
QPushButton:disabled {{ background: {DISABLED_BG}; color: {DISABLED_TEXT}; border-color: {BORDER}; }}
QPushButton#primary {{
    background: {ACCENT}; color: #ffffff; border: 1px solid {ACCENT}; padding: 8px 22px;
}}
QPushButton#primary:hover {{ background: {ACCENT_HOVER}; border-color: {ACCENT_HOVER}; }}
QPushButton#primary:pressed {{ background: {ACCENT_PRESSED}; }}
QPushButton#primary:disabled {{ background: {DISABLED_BG}; color: {DISABLED_TEXT}; border-color: {BORDER}; }}

/* ---- Toolbar buttons (editor) ---- */
QPushButton[toolbtn="true"] {{ padding: 5px 10px; min-width: 0; }}
QPushButton[toolbtn="true"]:checked {{ background: {SELECT_BG}; border-color: {ACCENT}; color: {ACCENT}; }}

/* ---- Table ---- */
QTableWidget {{
    background: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: {RADIUS};
    gridline-color: #ebebeb;
    selection-background-color: {SELECT_BG};
    selection-color: {TEXT};
}}
QTableWidget::item {{ padding: 3px 6px; }}
QHeaderView::section {{
    background: #f7f7f7;
    color: {MUTED};
    border: none;
    border-bottom: 1px solid {BORDER};
    padding: 7px;
    font-weight: 700;
}}

/* ---- Checkbox ---- */
QCheckBox {{ spacing: 8px; }}
QCheckBox::indicator {{
    width: 16px; height: 16px;
    border: 1px solid #9a9a9a; border-radius: 3px; background: {SURFACE};
}}
QCheckBox::indicator:checked {{ background: {ACCENT}; border-color: {ACCENT}; }}
QCheckBox:disabled {{ color: {DISABLED_TEXT}; }}

/* ---- Progress ---- */
QProgressBar {{
    background: {DISABLED_BG}; border: none; border-radius: 4px;
    height: 10px; text-align: center; color: {TEXT};
}}
QProgressBar::chunk {{ background: {ACCENT}; border-radius: 4px; }}

/* ---- Log / preview ---- */
QTextEdit#log {{ font-family: "Consolas", "SF Mono", "Menlo", monospace; font-size: 12.5px; background: {SURFACE}; }}
QTextEdit, QTextBrowser {{ background: {SURFACE}; border: 1px solid {BORDER}; border-radius: {RADIUS}; }}

/* ---- Scrollbars ---- */
QScrollBar:vertical {{ background: transparent; width: 12px; margin: 2px; }}
QScrollBar::handle:vertical {{ background: #c8c8c8; border-radius: 6px; min-height: 30px; }}
QScrollBar::handle:vertical:hover {{ background: #b0b0b0; }}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
QScrollArea {{ border: none; background: transparent; }}
"""


def apply_theme(app) -> None:
    """Áp theme QSS lên QApplication."""
    app.setStyleSheet(QSS)


class StepHeader(QWidget):
    """Thanh stepper ngang: N chip 'số + BƯỚC n + tiêu đề', đánh dấu bước hiện tại.

    `set_current(i)`: bước <i = xong (xanh ✓), =i = hiện tại (accent), >i = chờ (xám).
    """

    def __init__(self, steps, parent=None):
        super().__init__(parent)
        self.setObjectName("Header")
        self._titles = list(steps)
        self._chips = []  # (circle QLabel, kicker QLabel, title QLabel)

        row = QHBoxLayout(self)
        row.setContentsMargins(24, 12, 24, 12)
        row.setSpacing(0)

        for i, title in enumerate(self._titles):
            chip = QHBoxLayout()
            chip.setSpacing(10)

            circle = QLabel(str(i + 1))
            circle.setFixedSize(30, 30)
            circle.setAlignment(Qt.AlignCenter)
            chip.addWidget(circle)

            texts = QVBoxLayout()
            texts.setSpacing(0)
            kicker = QLabel(f"BƯỚC {i + 1}")
            kicker.setStyleSheet(f"font-size:10px; font-weight:700; color:{MUTED};")
            name = QLabel(title)
            name.setStyleSheet("font-size:14px; font-weight:600;")
            texts.addWidget(kicker)
            texts.addWidget(name)
            chip.addLayout(texts)

            row.addLayout(chip)
            self._chips.append((circle, kicker, name))

            if i < len(self._titles) - 1:
                line = QFrame()
                line.setFrameShape(QFrame.HLine)
                line.setFixedHeight(2)
                line.setStyleSheet(f"background:{BORDER}; border:none;")
                row.addSpacing(16)
                row.addWidget(line, 1)
                row.addSpacing(16)

        self.set_current(0)

    def set_current(self, index: int) -> None:
        for i, (circle, kicker, name) in enumerate(self._chips):
            if i < index:  # đã xong
                circle.setText("✓")
                circle.setStyleSheet(
                    f"background:{SUCCESS}; color:#fff; border-radius:15px; font-weight:700;"
                )
                name.setStyleSheet(f"font-size:14px; font-weight:600; color:{TEXT};")
            elif i == index:  # hiện tại
                circle.setText(str(i + 1))
                circle.setStyleSheet(
                    f"background:{ACCENT}; color:#fff; border-radius:15px; font-weight:700;"
                )
                name.setStyleSheet(f"font-size:14px; font-weight:700; color:{ACCENT};")
            else:  # chờ
                circle.setText(str(i + 1))
                circle.setStyleSheet(
                    f"background:#e1e1e1; color:{MUTED}; border-radius:15px; font-weight:700;"
                )
                name.setStyleSheet(f"font-size:14px; font-weight:600; color:{MUTED};")
