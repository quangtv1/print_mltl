"""Mockup GUI 3 bước cho pivot HTML — bản thiết kế v2 (sidebar + action bar).

Chạy:  python plans/260703-1651-html-template-designer-pivot/demo_ui_mockup.py
KHÔNG backend — dummy data. Bản phác thảo dùng một lần để duyệt thiết kế.

Hướng thiết kế: sidebar dọc (3 bước) + vùng nội dung sáng + thanh hành động dưới
(1 primary CTA/màn). Gỡ rối: bỏ top-bar chen chúc, bỏ emoji-icon, tăng whitespace.
"""

import sys
from pathlib import Path

_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(_ROOT))

from PyQt5.QtCore import Qt, QTimer
from PyQt5.QtWidgets import (
    QApplication, QButtonGroup, QCheckBox, QComboBox, QFormLayout, QFrame,
    QHBoxLayout, QHeaderView, QLabel, QLineEdit, QProgressBar, QPushButton,
    QStackedWidget, QTableWidget, QTableWidgetItem, QTextBrowser, QTreeWidget,
    QTreeWidgetItem, QVBoxLayout, QWidget,
)

# ---- Design tokens (bản v2) ----
ACCENT = "#2563eb"
DEMO_QSS = f"""
* {{ font-family:"Segoe UI","Helvetica Neue",Arial,sans-serif; font-size:14px; color:#1f2933; }}
QWidget#Root {{ background:#f4f6fa; }}

/* Sidebar tối */
QWidget#Sidebar {{ background:#222834; }}
QLabel#brand {{ color:#ffffff; font-size:16px; font-weight:700; }}
QLabel#brandtag {{ color:#7c869a; font-size:10px; font-weight:700; letter-spacing:2px; }}
QLabel#foot {{ color:#5c6577; font-size:11px; }}
QPushButton[nav="true"] {{
  text-align:left; padding:11px 16px 11px 15px; border:none; border-left:3px solid transparent;
  background:transparent; color:#c3cbd9; font-weight:600;
}}
QPushButton[nav="true"]:hover {{ background:rgba(255,255,255,0.06); color:#ffffff; }}
QPushButton[nav="true"]:checked {{
  background:rgba(37,99,235,0.22); color:#ffffff; border-left:3px solid {ACCENT};
}}

/* Header + card */
QLabel[h1="true"] {{ font-size:22px; font-weight:700; }}
QLabel[muted="true"] {{ color:#6b7280; font-size:13px; }}
QLabel[eyebrow="true"] {{ color:#8a93a3; font-size:11px; font-weight:700; letter-spacing:1.5px; }}
QFrame[card="true"] {{ background:#ffffff; border:1px solid #e3e8ef; border-radius:12px; }}

/* Inputs */
QLineEdit, QComboBox, QTextBrowser {{
  background:#ffffff; border:1px solid #d9dee7; border-radius:8px; padding:8px 10px;
  selection-background-color:{ACCENT}; selection-color:#fff;
}}
QLineEdit:focus, QComboBox:focus {{ border:1px solid {ACCENT}; }}
QLineEdit:disabled, QComboBox:disabled {{ background:#eef1f5; color:#9aa3af; }}
QComboBox::drop-down {{ border:none; width:22px; }}

/* Buttons */
QPushButton {{ background:#ffffff; border:1px solid #d9dee7; border-radius:8px; padding:8px 16px; font-weight:600; }}
QPushButton:hover {{ border-color:{ACCENT}; color:{ACCENT}; }}
QPushButton#primary {{ background:{ACCENT}; color:#fff; border:1px solid {ACCENT}; padding:9px 22px; }}
QPushButton#primary:hover {{ background:#1d4ed8; color:#fff; }}
QPushButton#ghost {{ background:transparent; border:1px solid #d9dee7; color:#4b5563; }}

/* Segmented control */
QPushButton[seg="left"], QPushButton[seg="right"] {{ background:#fff; border:1px solid #d9dee7; color:#6b7280; padding:7px 18px; font-weight:600; }}
QPushButton[seg="left"] {{ border-top-left-radius:8px; border-bottom-left-radius:8px; border-right:none; }}
QPushButton[seg="right"] {{ border-top-right-radius:8px; border-bottom-right-radius:8px; }}
QPushButton[seg="left"]:checked, QPushButton[seg="right"]:checked {{ background:{ACCENT}; color:#fff; border-color:{ACCENT}; }}

/* Record pill */
QPushButton[pill="true"] {{ background:#eef1f6; border:1px solid #e3e8ef; color:#4b5563; padding:6px 10px; font-weight:700; }}
QLabel#recpill {{ background:#eef1f6; border:1px solid #e3e8ef; border-radius:8px; padding:6px 14px; font-weight:700; color:#374151; }}

/* Table / tree */
QTableWidget, QTreeWidget {{ background:#fff; border:1px solid #e3e8ef; border-radius:10px; }}
QHeaderView::section {{ background:#f2f5f9; color:#6b7280; border:none; border-bottom:1px solid #e3e8ef; padding:8px; font-weight:600; }}
QTreeWidget::item {{ padding:4px 2px; }}
QCheckBox {{ spacing:8px; }}

/* Progress + log */
QProgressBar {{ background:#e7eaef; border:none; border-radius:6px; height:10px; text-align:center; color:#374151; }}
QProgressBar::chunk {{ background:{ACCENT}; border-radius:6px; }}
QTextBrowser#log {{ font-family:"SF Mono",Consolas,Menlo,monospace; font-size:12.5px; }}

/* Action bar */
QFrame#actionbar {{ background:#ffffff; border-top:1px solid #e3e8ef; }}
"""

# --- Dummy data ---
DOC_VARS = ["co_quan_dong1", "co_quan_dong2", "tieu_de", "ho_so_so", "chuc_danh_ky", "nguoi_ky"]
ROW_VARS = ["stt", "so_ky_hieu_vb", "ngay_thang_vb", "tac_gia", "trich_yeu", "to_so", "ghi_chu"]
SHEET_COLS = ["STT", "Số, ký hiệu văn bản", "Ngày tháng văn bản", "Tác giả",
              "Trích yếu nội dung VB", "Tờ số", "Ghi chú", "Hồ sơ số", "Tiêu đề hồ sơ"]
RECORDS = [
    {"ho_so_so": "HS 12", "rows": [("1", "01/QĐ", "01/01/2024", "UBND", "Quyết định giao đất", "1", ""),
                                     ("2", "02/TB", "05/01/2024", "VP ĐKĐĐ", "Thông báo nộp phí", "3", "")]},
    {"ho_so_so": "HS 07", "rows": [("1", "10/HĐ", "12/02/2024", "Phòng CT", "Hợp đồng chuyển nhượng", "1", "Bản gốc"),
                                     ("2", "11/BB", "14/02/2024", "VP ĐKĐĐ", "Biên bản bàn giao", "2", ""),
                                     ("3", "12/QĐ", "20/02/2024", "UBND", "Quyết định phê duyệt", "4", "")]},
    {"ho_so_so": "HS 21", "rows": [("1", "30/ĐN", "03/03/2024", "Cá nhân", "Đơn đề nghị tách thửa", "1", "")]},
]


COL_LABELS = ["STT", "Số, ký hiệu VB", "Ngày tháng", "Tác giả", "Trích yếu nội dung", "Tờ số", "Ghi chú"]
COL_VARS = ["stt", "so_ky_hieu_vb", "ngay_thang_vb", "tac_gia", "trich_yeu", "to_so", "ghi_chu"]
# Giá trị mẫu cho các biến cấp tài liệu (dùng ở mode preview).
DOC_VALUES = {
    "co_quan_dong1": "VĂN PHÒNG ĐĂNG KÝ ĐẤT ĐAI TỈNH Q.TRỊ",
    "co_quan_dong2": "CHI NHÁNH THÀNH PHỐ ĐÔNG HÀ",
    "tieu_de": "MỤC LỤC VĂN BẢN, TÀI LIỆU",
    "chuc_danh_ky": "Người lập", "nguoi_ky": "Nguyễn Công Tùng",
}


def _hl(var, hl):
    """Style nổi bật vùng khi biến `var` đang được chọn ở sidebar."""
    return "background:#ffe28a;outline:2px solid #f59e0b;border-radius:3px;" if var == hl else ""


def _fld(var, value, edit, hl):
    """Edit → hiện token {var}; Preview → nhồi giá trị. Bọc highlight nếu được chọn."""
    txt = "{" + var + "}" if edit else (value if value != "" else "&nbsp;")
    s = _hl(var, hl)
    return f'<span style="{s}{"padding:0 2px;" if s else ""}">{txt}</span>'


def a4_html(rec, edit, hl=None):
    ho = "{ho_so_so}" if edit else rec["ho_so_so"]
    ho_s = _hl("ho_so_so", hl)
    hdr = "".join(f'<th style="border:1px solid #555;padding:4px;{_hl(COL_VARS[i], hl)}">{lab}</th>'
                  for i, lab in enumerate(COL_LABELS))
    if edit:  # 1 hàng token đại diện cho vòng lặp
        body = "<tr>" + "".join(
            f'<td style="border:1px solid #555;padding:4px;text-align:center;{_hl(v, hl)}">{{{v}}}</td>'
            for v in COL_VARS) + "</tr>"
        caption = '<div style="color:#888;font-size:10px;font-style:italic">↑ hàng lặp theo từng văn bản</div>'
    else:
        body = "".join("<tr>" + "".join(
            f'<td style="border:1px solid #555;padding:4px;text-align:center;{_hl(COL_VARS[i], hl)}">{c or "&nbsp;"}</td>'
            for i, c in enumerate(r)) + "</tr>" for r in rec["rows"])
        caption = ""
    sig_img = "{anh_chu_ky}" if edit else '<i style="color:#999">[ảnh chữ ký]</i>'
    return f"""<div style="font-family:'Times New Roman';background:white;padding:28px">
      <table width="100%" style="margin-bottom:8px"><tr>
        <td width="50%" align="center">{_fld('co_quan_dong1', DOC_VALUES['co_quan_dong1'], edit, hl)}<br>
          <b><u>{_fld('co_quan_dong2', DOC_VALUES['co_quan_dong2'], edit, hl)}</u></b></td>
        <td width="50%" align="center">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM<br><b><u>Độc lập - Tự do - Hạnh phúc</u></b></td>
      </tr></table>
      <h3 align="center">{_fld('tieu_de', DOC_VALUES['tieu_de'], edit, hl)}</h3>
      <p align="center"><i>Số, ký hiệu hồ sơ (đơn vị bảo quản): <span style="{ho_s}{'padding:0 2px' if ho_s else ''}">{ho}</span></i></p>
      <table width="100%" cellspacing="0" style="border-collapse:collapse;font-size:12px">
        <tr style="background:#eef">{hdr}</tr>{body}
      </table>{caption}
      <table width="100%" style="margin-top:22px"><tr><td width="60%"></td>
        <td width="40%" align="center"><b>{_fld('chuc_danh_ky', DOC_VALUES['chuc_danh_ky'], edit, hl)}</b><br><br>
          <span style="{_hl('anh_chu_ky', hl)}">{sig_img}</span><br><br>
          <b>{_fld('nguoi_ky', DOC_VALUES['nguoi_ky'], edit, hl)}</b></td>
      </tr></table>
      <p align="right" style="color:#999;font-size:11px">Trang 1/1 &nbsp;<i>(số trang hiện khi in/PDF)</i></p></div>"""


def card(inner: QVBoxLayout) -> QFrame:
    f = QFrame(); f.setProperty("card", "true")
    f.setLayout(inner); inner.setContentsMargins(18, 16, 18, 16); inner.setSpacing(10)
    return f


def eyebrow(t):
    lb = QLabel(t); lb.setProperty("eyebrow", "true"); return lb


class StepInput(QWidget):
    def __init__(self):
        super().__init__()
        lay = QVBoxLayout(self); lay.setContentsMargins(0, 0, 0, 0); lay.setSpacing(16)

        c1 = QVBoxLayout(); c1.addWidget(eyebrow("NGUỒN DỮ LIỆU  ·  FILE EXCEL"))
        f = QFormLayout(); f.setSpacing(10); f.setContentsMargins(0, 4, 0, 0)
        xl = QHBoxLayout(); xe = QLineEdit(); xe.setText("~/Downloads/muc_luc_ho_so.xlsx"); xl.addWidget(xe)
        xl.addWidget(QPushButton("Chọn…")); f.addRow("File Excel", self._w(xl))
        sh = QComboBox(); sh.addItems(["KhanhLinh", "Sheet1", "DuLieu2024"]); f.addRow("Sheet", sh)
        outd = QHBoxLayout(); oe = QLineEdit(); oe.setText("~/MucLuc_Output"); outd.addWidget(oe)
        outd.addWidget(QPushButton("Chọn…")); f.addRow("Thư mục xuất", self._w(outd))
        c1.addLayout(f); lay.addWidget(card(c1))

        c2 = QVBoxLayout(); c2.addWidget(eyebrow("GHÉP BIẾN ↔ CỘT  ·  TỰ KHỚP, SỬA ĐƯỢC"))
        tbl = QTableWidget(8, 2); tbl.setHorizontalHeaderLabels(["Biến (template)", "Cột trong sheet"])
        tbl.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeToContents)
        tbl.horizontalHeader().setSectionResizeMode(1, QHeaderView.Stretch)
        tbl.verticalHeader().setVisible(False); tbl.setShowGrid(False)
        tbl.verticalHeader().setDefaultSectionSize(36)
        for i, (v, cc) in enumerate([("ho_so_so", "Hồ sơ số")] + list(zip(ROW_VARS, SHEET_COLS[:7]))):
            tbl.setItem(i, 0, QTableWidgetItem(v))
            cb = QComboBox(); cb.addItems(SHEET_COLS); cb.setCurrentText(cc); tbl.setCellWidget(i, 1, cb)
        tbl.setMinimumHeight(230); c2.addWidget(tbl)
        gr = QHBoxLayout(); gr.addWidget(QLabel("Cột gom nhóm")); gc = QComboBox()
        gc.addItems(SHEET_COLS); gc.setCurrentText("Tiêu đề hồ sơ"); gr.addWidget(gc, 1); c2.addLayout(gr)
        lay.addWidget(card(c2)); lay.addStretch(1)

    def _w(self, inner):
        w = QWidget(); w.setLayout(inner); inner.setContentsMargins(0, 0, 0, 0); return w


class StepDesign(QWidget):
    def __init__(self):
        super().__init__()
        self.idx = 0
        self.hl = None  # biến đang được chọn ở sidebar → nổi bật vùng trên A4
        lay = QVBoxLayout(self); lay.setContentsMargins(0, 0, 0, 0); lay.setSpacing(12)

        top = QHBoxLayout()
        top.addWidget(QLabel("Mẫu")); tp = QComboBox(); tp.addItems(["van-phong-dat-dai", "mau-rut-gon"])
        tp.setFixedWidth(190); top.addWidget(tp); top.addStretch(1)
        self.prevb = QPushButton("‹"); self.prevb.setProperty("pill", "true"); self.prevb.setFixedWidth(34)
        self.rec = QLabel(); self.rec.setObjectName("recpill")
        self.nextb = QPushButton("›"); self.nextb.setProperty("pill", "true"); self.nextb.setFixedWidth(34)
        top.addWidget(self.prevb); top.addWidget(self.rec); top.addWidget(self.nextb)
        top.addSpacing(14)
        self.seg_edit = QPushButton("Sửa vùng"); self.seg_edit.setProperty("seg", "left"); self.seg_edit.setCheckable(True)
        self.seg_prev = QPushButton("Xem trước"); self.seg_prev.setProperty("seg", "right"); self.seg_prev.setCheckable(True)
        self.seg_prev.setChecked(True)
        g = QButtonGroup(self); g.addButton(self.seg_edit); g.addButton(self.seg_prev)
        segbox = QHBoxLayout(); segbox.setSpacing(0); segbox.addWidget(self.seg_edit); segbox.addWidget(self.seg_prev)
        top.addLayout(segbox)
        lay.addLayout(top)

        body = QHBoxLayout(); body.setSpacing(12)
        self.preview = QTextBrowser(); self.preview.setStyleSheet("background:#9aa3af;border:1px solid #e3e8ef;border-radius:10px;padding:14px;")
        body.addWidget(self.preview, 3)

        panel = QVBoxLayout(); panel.setSpacing(8)
        panel.addWidget(eyebrow("BIẾN  ·  BẤM ĐỂ NỔI BẬT VÙNG"))
        tree = QTreeWidget(); tree.setHeaderHidden(True)
        for grp, items in [("TÀI LIỆU", DOC_VARS), ("HÀNG (LẶP)", ROW_VARS),
                           ("GOM NHÓM", ["Tiêu đề hồ sơ"]), ("ẢNH CHỮ KÝ", ["anh_chu_ky"])]:
            top_it = QTreeWidgetItem([grp]); tree.addTopLevelItem(top_it)
            for it in items:
                child = QTreeWidgetItem(["   " + it])
                child.setData(0, Qt.UserRole, it)  # tên biến sạch để highlight
                top_it.addChild(child)
            top_it.setExpanded(True)
        tree.itemClicked.connect(self._pick_var)
        panel.addWidget(tree, 1)
        div = QFrame(); div.setFrameShape(QFrame.HLine); div.setStyleSheet("color:#e3e8ef"); panel.addWidget(div)
        panel.addWidget(eyebrow("GÁN VÀO VÙNG"))
        zr = QHBoxLayout(); zr.addWidget(QLabel("Vùng")); zc = QComboBox()
        zc.addItems(["doc:tieu_de", "doc:ho_so_so", "col:stt", "doc:nguoi_ky"]); zr.addWidget(zc, 1); panel.addLayout(zr)
        gb = QPushButton("Gán biến → vùng"); gb.setObjectName("primary"); panel.addWidget(gb)
        pw = QFrame(); pw.setProperty("card", "true"); pw.setLayout(panel)
        panel.setContentsMargins(14, 12, 14, 14)
        body.addWidget(pw, 1)
        lay.addLayout(body, 1)

        self.seg_edit.toggled.connect(self._render)
        self.prevb.clicked.connect(lambda: self._nav(-1)); self.nextb.clicked.connect(lambda: self._nav(1))
        self._render()

    def _nav(self, d): self.idx = (self.idx + d) % len(RECORDS); self._render()

    def _pick_var(self, item, _col=0):
        var = item.data(0, Qt.UserRole)  # None nếu bấm nhóm cha
        if var:
            self.hl = var
            self._render()

    def _render(self):
        rec = RECORDS[self.idx]
        self.rec.setText(f"{rec['ho_so_so']}  ·  {self.idx + 1}/{len(RECORDS)}")
        self.preview.setHtml(a4_html(rec, self.seg_edit.isChecked(), self.hl))


class StepRun(QWidget):
    def __init__(self):
        super().__init__()
        lay = QVBoxLayout(self); lay.setContentsMargins(0, 0, 0, 0); lay.setSpacing(16)
        c = QVBoxLayout(); c.addWidget(eyebrow("XUẤT HÀNG LOẠT"))
        f = QFormLayout(); f.setSpacing(10)
        outd = QHBoxLayout(); oe = QLineEdit(); oe.setText("~/MucLuc_Output"); outd.addWidget(oe)
        outd.addWidget(QPushButton("Chọn…")); w = QWidget(); w.setLayout(outd); outd.setContentsMargins(0, 0, 0, 0)
        f.addRow("Thư mục xuất", w); c.addLayout(f)
        c.addWidget(QCheckBox("Xuất kèm file Excel tổng hợp"))
        self.bar = QProgressBar(); self.bar.setValue(0); c.addWidget(self.bar)
        lay.addWidget(card(c))
        cl = QVBoxLayout(); cl.addWidget(eyebrow("NHẬT KÝ"))
        self.log = QTextBrowser(); self.log.setObjectName("log"); self.log.setMinimumHeight(220); cl.addWidget(self.log)
        lay.addWidget(card(cl)); lay.addStretch(1)
        self._i = 0; self._t = QTimer(self); self._t.timeout.connect(self._tick)

    def simulate(self):
        self._i = 0; self.log.clear(); self.bar.setMaximum(len(RECORDS) * 400)
        self.log.append("▶ Bắt đầu xuất HTML hàng loạt (song song)…"); self._t.start(110)

    def _tick(self):
        total = len(RECORDS) * 400; self._i += 41; self.bar.setValue(min(self._i, total))
        if self._i % 400 < 41:
            n = min(self._i // 400 + 1, total // 400)
            self.log.append(f"  ✓ {RECORDS[(n-1) % len(RECORDS)]['ho_so_so']}.html   ({n}/{total//400})")
        if self._i >= total:
            self._t.stop()
            self.log.append(f"✅ Xong {total//400} hồ sơ HTML · 0.4s · Excel: Muc_Luc_Ho_So.xlsx")


class DemoWindow(QWidget):
    # Không dùng '&' (Qt hiểu là mnemonic trong nút nav).
    STEPS = [
        ("Đầu vào", "Excel và ghép biến", "Chọn file Excel, sheet dữ liệu và ghép biến ↔ cột."),
        ("Thiết kế", "Preview và gán biến", "Xem trước A4, gán biến vào vùng, lật từng hồ sơ."),
        ("Chạy", "Xuất và nhật ký", "Xuất HTML hàng loạt; PDF theo yêu cầu."),
    ]

    def __init__(self):
        super().__init__()
        self.setObjectName("Root")
        self.setWindowTitle("Trình thiết kế Mục lục HTML — mockup v2")
        self.resize(1080, 720)
        root = QHBoxLayout(self); root.setContentsMargins(0, 0, 0, 0); root.setSpacing(0)

        # Sidebar
        side = QWidget(); side.setObjectName("Sidebar"); side.setFixedWidth(238)
        sl = QVBoxLayout(side); sl.setContentsMargins(16, 20, 16, 18); sl.setSpacing(6)
        brand = QLabel("Mục lục HTML"); brand.setObjectName("brand")
        tagl = QLabel("MOCKUP · v2"); tagl.setObjectName("brandtag")
        sl.addWidget(brand); sl.addWidget(tagl); sl.addSpacing(18)
        self.navs = []; grp = QButtonGroup(self)
        for i, (short, title, _) in enumerate(self.STEPS):
            b = QPushButton(f"{i+1}   {short}\n      {title}")
            b.setProperty("nav", "true"); b.setCheckable(True); b.setChecked(i == 0)
            b.clicked.connect(lambda _, x=i: self.goto(x))
            grp.addButton(b); sl.addWidget(b); self.navs.append(b)
        sl.addStretch(1)
        foot = QLabel("Dữ liệu giả · chưa nối backend"); foot.setObjectName("foot"); foot.setWordWrap(True)
        sl.addWidget(foot)
        root.addWidget(side)

        # Content
        content = QWidget(); cl = QVBoxLayout(content); cl.setContentsMargins(28, 24, 28, 0); cl.setSpacing(4)
        self.h1 = QLabel(); self.h1.setProperty("h1", "true")
        self.hsub = QLabel(); self.hsub.setProperty("muted", "true")
        cl.addWidget(self.h1); cl.addWidget(self.hsub); cl.addSpacing(14)
        self.stack = QStackedWidget()
        self.step_run = StepRun()
        self.stack.addWidget(StepInput()); self.stack.addWidget(StepDesign()); self.stack.addWidget(self.step_run)
        cl.addWidget(self.stack, 1)

        # Action bar
        bar = QFrame(); bar.setObjectName("actionbar"); bl = QHBoxLayout(bar)
        bl.setContentsMargins(28, 12, 28, 12)
        self.back = QPushButton("← Quay lại"); self.back.setObjectName("ghost"); self.back.clicked.connect(lambda: self.goto(self.stack.currentIndex() - 1))
        self.secondary = QPushButton("Xuất PDF hồ sơ này"); self.secondary.setObjectName("ghost")
        self.primary = QPushButton(); self.primary.setObjectName("primary"); self.primary.clicked.connect(self._primary)
        bl.addWidget(self.back); bl.addStretch(1); bl.addWidget(self.secondary); bl.addWidget(self.primary)
        cl.addWidget(bar)

        root.addWidget(content, 1)  # bắt buộc: parent content vào cửa sổ (nếu quên → stack bị GC)
        self.goto(0)

    def goto(self, i):
        i = max(0, min(i, len(self.STEPS) - 1))
        self.stack.setCurrentIndex(i); self.navs[i].setChecked(True)
        short, title, desc = self.STEPS[i]
        self.h1.setText(title); self.hsub.setText(desc)
        self.back.setVisible(i > 0)
        # Nút phụ: bước 2 = xuất PDF 1 hồ sơ; bước 3 = xuất PDF tất cả (chậm).
        self.secondary.setVisible(i in (1, 2))
        self.secondary.setText("Xuất PDF hồ sơ này" if i == 1 else "Xuất PDF tất cả (chậm)")
        self.primary.setText(["Tiếp: Thiết kế →", "Tiếp: Chạy →", "Xuất HTML hàng loạt"][i])

    def _primary(self):
        i = self.stack.currentIndex()
        if i < 2:
            self.goto(i + 1)
        else:
            self.step_run.simulate()


def main():
    app = QApplication(sys.argv)
    app.setStyleSheet(DEMO_QSS)
    w = DemoWindow(); w.show()
    return app.exec_()


if __name__ == "__main__":
    sys.exit(main())
