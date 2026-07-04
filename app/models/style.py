"""Dataclass ánh xạ schema `style.json`.

`from_dict` khoan dung (bỏ qua khóa lạ, điền mặc định cho khóa thiếu) để tương
thích khi schema mở rộng; `to_dict` roundtrip đầy đủ để `save`→`load` không mất
dữ liệu. Xem `styles/van-phong-dat-dai/style.json` làm tham chiếu.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Dict, List


# Khóa settings text mặc định (giữ thứ tự để UI hiển thị ổn định).
DEFAULT_SETTINGS: Dict[str, Any] = {
    "co_quan_dong1": "",
    "co_quan_dong2": "",
    "tieu_de": "",
    "chuc_danh_ky": "",
    "nguoi_ky": "",
    "anh_chu_ky_path": "",
    "font_name": "Times New Roman",
    "footer_page_number": True,
    # Footer render lúc in từng trang; `{trang_so}`/`{tong_so_trang}` là biến
    # footer-only (không chèn được vào thân template — xem app/core/variables.py).
    "footer_format": "Trang {trang_so}/{tong_so_trang}",
}


@dataclass
class ColumnDef:
    """Một cột bảng dữ liệu: nhãn hiển thị, biến template, độ rộng (tỉ lệ)."""

    header: str
    var: str
    width: float = 0.0

    @classmethod
    def from_dict(cls, d: Dict[str, Any]) -> "ColumnDef":
        return cls(
            header=str(d.get("header", "")),
            var=str(d.get("var", "")),
            width=float(d.get("width", 0.0) or 0.0),
        )

    def to_dict(self) -> Dict[str, Any]:
        return {"header": self.header, "var": self.var, "width": self.width}


@dataclass
class StyleConfig:
    """Cấu hình một style (một loại mục lục)."""

    name: str = ""
    template_file: str = "template.docx"
    grouping_column: str = ""
    output_filename_pattern: str = "MLHS_{ho_so_so}.pdf"
    settings: Dict[str, Any] = field(default_factory=lambda: dict(DEFAULT_SETTINGS))
    # Nội dung soạn thảo WYSIWYG (QTextEdit → HTML): header + bảng-mẫu (đúng 1
    # dòng-mẫu chứa token cột) + khối chữ ký. Chứa `{token}`. Rỗng với style cũ.
    template_html: str = ""
    # biến template cấp tài liệu -> tên cột trong file Excel
    document_fields: Dict[str, str] = field(default_factory=dict)
    # biến template trong dòng-mẫu bảng -> tên cột trong file Excel
    row_mapping: Dict[str, str] = field(default_factory=dict)
    columns: List[ColumnDef] = field(default_factory=list)

    @classmethod
    def from_dict(cls, d: Dict[str, Any]) -> "StyleConfig":
        settings = dict(DEFAULT_SETTINGS)
        settings.update(d.get("settings") or {})
        return cls(
            name=str(d.get("name", "")),
            template_file=str(d.get("template_file", "template.docx")),
            grouping_column=str(d.get("grouping_column", "")),
            output_filename_pattern=str(
                d.get("output_filename_pattern", "MLHS_{ho_so_so}.pdf")
            ),
            settings=settings,
            template_html=str(d.get("template_html", "") or ""),
            document_fields=dict(d.get("document_fields") or {}),
            row_mapping=dict(d.get("row_mapping") or {}),
            columns=[ColumnDef.from_dict(c) for c in (d.get("columns") or [])],
        )

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "template_file": self.template_file,
            "grouping_column": self.grouping_column,
            "output_filename_pattern": self.output_filename_pattern,
            "settings": self.settings,
            "template_html": self.template_html,
            "document_fields": self.document_fields,
            "row_mapping": self.row_mapping,
            "columns": [c.to_dict() for c in self.columns],
        }
