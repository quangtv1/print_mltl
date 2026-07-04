"""Theme cổ điển Windows (khớp prototype design/) + brand bar + stepper 3 bước.

Đặc trưng: nền `#f0f0f0`, font Segoe UI **12px**, group box kiểu *fieldset* (viền
`#cfcfcf` vuông, legend nằm trên viền), input viền `#7a7a7a` cao 24px, nút xám
`#e1e1e1`/`#adadad` hover `#e5f1fb`, accent `#0078d7`. Nút primary đặt
`objectName("primary")`; vùng log `objectName("log")` (terminal tối).
"""

from __future__ import annotations

from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import QHBoxLayout, QLabel, QVBoxLayout, QWidget

# --- Design tokens (khớp prototype design2 — HUCE navy) ---
ACCENT = "#0043a5"
ACCENT_HOVER = "#0a52c0"
ACCENT_PRESSED = "#003480"
TEAL = "#00ccd6"
TEAL_HOVER = "#00b3bc"
BG = "#f0f2f5"
SURFACE = "#ffffff"
TEXT = "#000000"
MUTED = "#666666"
FIELD_BORDER = "#7a7a7a"
GROUP_BORDER = "#cfcfcf"
BTN_BG = "#ffffff"
BTN_BORDER = "#d9d9d9"
HOVER_BG = "#e6eefb"
TOKEN_BLUE = "#0057b7"
SUCCESS = "#107c10"
WARN = "#c47a00"
DANGER = "#c42b1c"
STEP_ACTIVE_BG = "#e6eefb"

FONT_STACK = "'Segoe UI', Tahoma, Geneva, 'Noto Sans', sans-serif"

QSS = f"""
* {{
    font-family: {FONT_STACK};
    font-size: 12px;
    color: {TEXT};
}}

QMainWindow, QWidget#Root {{ background: {BG}; }}
QWidget#BrandBar {{ background: {SURFACE}; border-bottom: 1px solid #dcdcdc; }}
QWidget#Header {{ background: {SURFACE}; border-bottom: 1px solid #dcdcdc; }}
QWidget#ActionBar {{ background: {BG}; border-top: 1px solid #dcdcdc; }}

QLabel[muted="true"] {{ color: {MUTED}; }}
QLabel[hint="true"] {{ color: #777777; font-size: 11px; }}
QLabel[ok="true"] {{ color: {SUCCESS}; }}

/* ---- Info banner (trạng thái ghép biến) ---- */
QLabel[banner="warn"] {{ font-size: 11px; padding: 5px 10px; border: 1px solid #e6d9a8; border-radius: 2px; background: #fbf4e0; color: #8a6d00; }}
QLabel[banner="ok"] {{ font-size: 11px; padding: 5px 10px; border: 1px solid #a8ddb5; border-radius: 2px; background: #eafaef; color: {SUCCESS}; }}
QLabel[banner="err"] {{ font-size: 11px; padding: 5px 10px; border: 1px solid #e6b0b0; border-radius: 2px; background: #fdeaea; color: {DANGER}; }}

/* ---- Group box = fieldset cổ điển ---- */
QGroupBox {{
    border: 1px solid {GROUP_BORDER};
    background: {BG};
    border-radius: 0;
    margin-top: 7px;
    padding: 12px 12px 10px 12px;
    font-size: 12px;
}}
QGroupBox::title {{
    subcontrol-origin: margin;
    subcontrol-position: top left;
    left: 8px;
    padding: 0 5px;
    background: {BG};
    color: #1a1a1a;
}}
QGroupBox#GroupCol {{ border-left: 3px solid {ACCENT}; }}

/* ---- Inputs ---- */
QLineEdit, QComboBox {{
    min-height: 22px;
    background: {SURFACE};
    border: 1px solid {FIELD_BORDER};
    border-radius: 0;
    padding: 1px 6px;
    selection-background-color: {ACCENT};
    selection-color: #ffffff;
}}
QLineEdit:focus, QComboBox:focus {{ border: 1px solid {ACCENT}; }}
QLineEdit:disabled, QComboBox:disabled {{ background: #f5f5f5; color: #9a9a9a; }}
QComboBox::drop-down {{ border: none; width: 18px; }}
QComboBox QAbstractItemView {{
    background: {SURFACE};
    border: 1px solid {FIELD_BORDER};
    selection-background-color: {HOVER_BG};
    selection-color: {TEXT};
    outline: none;
}}

/* ---- Buttons ---- */
QPushButton {{
    min-height: 22px;
    background: {BTN_BG};
    border: 1px solid {BTN_BORDER};
    border-radius: 5px;
    padding: 2px 14px;
}}
QPushButton:hover {{ background: {HOVER_BG}; border-color: {ACCENT}; }}
QPushButton:pressed {{ background: #dbe6fa; }}
QPushButton:disabled {{ background: #f0f0f0; color: #b0b0b0; border-color: {GROUP_BORDER}; }}
QPushButton#primary {{ background: {ACCENT}; border: 1px solid {ACCENT}; color: #ffffff; font-weight: 600; padding: 3px 20px; }}
QPushButton#primary:hover {{ background: {ACCENT_HOVER}; border-color: {ACCENT_HOVER}; }}
QPushButton#primary:pressed {{ background: {ACCENT_PRESSED}; }}
QPushButton#primary:disabled {{ background: #f0f0f0; color: #b0b0b0; border-color: {GROUP_BORDER}; }}
QPushButton#teal {{ background: {TEAL}; border: 1px solid {TEAL}; color: #ffffff; }}
QPushButton#teal:hover {{ background: {TEAL_HOVER}; border-color: {TEAL_HOVER}; }}
QPushButton#teal:disabled {{ background: #f0f0f0; color: #b0b0b0; border-color: {GROUP_BORDER}; }}

/* ---- Toolbar / chip buttons (editor, palette) ---- */
QPushButton[toolbtn="true"] {{ min-height: 24px; min-width: 26px; background: #ffffff; border: 1px solid {GROUP_BORDER}; border-radius: 2px; padding: 2px 8px; }}
QPushButton[toolbtn="true"]:hover {{ background: {HOVER_BG}; border-color: {ACCENT}; }}
QPushButton[toolbtn="true"]:checked {{ background: {HOVER_BG}; border-color: {ACCENT}; color: {ACCENT}; }}
QPushButton[chip="true"] {{ min-height: 22px; background: #ffffff; border: 1px solid #c8c8c8; border-radius: 2px; padding: 2px 8px; color: {TOKEN_BLUE}; font-family: Consolas, 'Courier New', monospace; }}
QPushButton[chip="true"]:hover {{ background: {HOVER_BG}; border-color: {ACCENT}; }}
QPushButton[chip="true"]:disabled {{ color: #a8a8a8; background: #f3f3f3; }}

/* ---- Table ---- */
QTableWidget {{
    background: {SURFACE};
    border: 1px solid #a0a0a0;
    border-radius: 0;
    gridline-color: #eeeeee;
    selection-background-color: {HOVER_BG};
    selection-color: {TEXT};
}}
QTableWidget::item {{ padding: 2px 6px; }}
QHeaderView::section {{
    background: #f5f5f5;
    color: #555555;
    border: none;
    border-right: 1px solid #e2e2e2;
    border-bottom: 1px solid #d0d0d0;
    padding: 5px 8px;
    font-weight: 400;
}}

/* ---- Checkbox ---- */
QCheckBox {{ spacing: 7px; }}
QCheckBox::indicator {{ width: 14px; height: 14px; border: 1px solid #7a7a7a; border-radius: 0; background: {SURFACE}; }}
QCheckBox::indicator:checked {{ background: {ACCENT}; border-color: {ACCENT}; }}
QCheckBox:disabled {{ color: #a3a3a3; }}

/* ---- Progress ---- */
QProgressBar {{ background: #e6e6e6; border: 1px solid #a0a0a0; border-radius: 0; height: 18px; text-align: center; color: {TEXT}; }}
QProgressBar::chunk {{ background: {ACCENT}; }}

/* ---- Log terminal ---- */
QTextEdit#log {{ background: #1e1e1e; color: #dcdcdc; border: 1px solid #000000; border-radius: 0; font-family: Consolas, 'Courier New', monospace; font-size: 12px; }}
QTextEdit, QTextBrowser {{ background: {SURFACE}; border: 1px solid {FIELD_BORDER}; border-radius: 0; }}

/* ---- Scrollbars ---- */
QScrollBar:vertical {{ background: #f0f0f0; width: 16px; margin: 0; }}
QScrollBar::handle:vertical {{ background: #cdcdcd; border: 1px solid #f0f0f0; min-height: 30px; }}
QScrollBar::handle:vertical:hover {{ background: #a6a6a6; }}
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
QScrollBar:horizontal {{ background: #f0f0f0; height: 16px; margin: 0; }}
QScrollBar::handle:horizontal {{ background: #cdcdcd; border: 1px solid #f0f0f0; min-width: 30px; }}
QScrollArea {{ border: none; background: transparent; }}
"""


def apply_theme(app) -> None:
    """Áp theme QSS lên QApplication."""
    app.setStyleSheet(QSS)


class BrandBar(QWidget):
    """Dải thương hiệu trên cùng: badge accent 'T' + tên ứng dụng (khớp title bar mock)."""

    def __init__(self, title: str, parent=None):
        super().__init__(parent)
        self.setObjectName("BrandBar")
        self.setFixedHeight(31)
        row = QHBoxLayout(self)
        row.setContentsMargins(9, 0, 12, 0)
        row.setSpacing(8)
        badge = QLabel("T")
        badge.setFixedSize(15, 15)
        badge.setAlignment(Qt.AlignCenter)
        badge.setStyleSheet(
            f"background:{ACCENT}; color:#fff; font-size:10px; font-weight:700;"
        )
        name = QLabel(title)
        name.setStyleSheet("font-size:12px; color:#1a1a1a;")
        row.addWidget(badge)
        row.addWidget(name)
        row.addStretch(1)


class StepHeader(QWidget):
    """Stepper ngang: chip 'số + BƯỚC n + tiêu đề', phân tách bằng chevron ›.

    `set_current(i)`: <i xong (badge xanh ✓), =i hiện tại (badge accent + nền nhạt),
    >i chờ (badge xám).
    """

    def __init__(self, steps, parent=None):
        super().__init__(parent)
        self.setObjectName("Header")
        self._titles = list(steps)
        self._chips = []  # (wrap QWidget, badge, title)

        row = QHBoxLayout(self)
        row.setContentsMargins(18, 10, 18, 10)
        row.setSpacing(6)

        for i, title in enumerate(self._titles):
            wrap = QWidget()
            wl = QHBoxLayout(wrap)
            wl.setContentsMargins(8, 5, 10, 5)
            wl.setSpacing(9)

            badge = QLabel(str(i + 1))
            badge.setFixedSize(24, 24)
            badge.setAlignment(Qt.AlignCenter)
            wl.addWidget(badge)

            texts = QVBoxLayout()
            texts.setSpacing(0)
            kicker = QLabel(f"BƯỚC {i + 1}")
            kicker.setStyleSheet("font-size:10px; color:#8a8a8a;")
            name = QLabel(title)
            texts.addWidget(kicker)
            texts.addWidget(name)
            wl.addLayout(texts)

            row.addWidget(wrap)
            self._chips.append((wrap, badge, name))

            if i < len(self._titles) - 1:
                sep = QLabel("›")  # ›
                sep.setStyleSheet("font-size:16px; color:#b8b8b8;")
                row.addSpacing(6)
                row.addWidget(sep)
                row.addSpacing(6)

        row.addStretch(1)
        self.set_current(0)

    def set_current(self, index: int) -> None:
        for i, (wrap, badge, name) in enumerate(self._chips):
            if i < index:
                badge.setText("✓")  # ✓
                badge.setStyleSheet(
                    f"background:{SUCCESS}; color:#fff; border-radius:12px; font-weight:700;"
                )
                wrap.setStyleSheet("background:transparent; border-radius:3px;")
                name.setStyleSheet("font-size:13px; color:#1a1a1a;")
            elif i == index:
                badge.setText(str(i + 1))
                badge.setStyleSheet(
                    f"background:{ACCENT}; color:#fff; border-radius:12px; font-weight:700;"
                )
                wrap.setStyleSheet(f"background:{STEP_ACTIVE_BG}; border-radius:3px;")
                name.setStyleSheet(f"font-size:13px; font-weight:700; color:{ACCENT};")
            else:
                badge.setText(str(i + 1))
                badge.setStyleSheet(
                    "background:#d6d6d6; color:#666; border-radius:12px; font-weight:700;"
                )
                wrap.setStyleSheet("background:transparent; border-radius:3px;")
                name.setStyleSheet("font-size:13px; color:#8a8a8a;")
