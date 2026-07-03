"""Trích danh sách biến jinja trong template.docx cho UI mapping (P5)."""

from __future__ import annotations

from typing import Set


def get_template_variables(template_path) -> Set[str]:
    """Trả về tập biến chưa khai báo trong template (docxtpl).

    Bao gồm cả biến cấp tài liệu và biến trong vòng lặp (vd `rows`, `anh_chu_ky`).
    """
    from docxtpl import DocxTemplate

    tpl = DocxTemplate(str(template_path))
    return set(tpl.get_undeclared_template_variables())
