"""Nguồn duy nhất cho từ vựng biến `{token}` của template (Native Qt).

Cú pháp biến là `{single_brace}`. Từ vựng token **suy từ schema thật lúc runtime**
(không hardcode tên bịa): doc fields lấy từ `style.document_fields`, cột hàng lấy từ
`style.row_mapping`, một số khóa `settings` dùng được trong thân, cộng vài biến tự
động. Renderer (P2) và panel biến (P5) đều tham chiếu module này để nhất quán.
"""

from __future__ import annotations

from typing import TYPE_CHECKING, Dict, List, Tuple

if TYPE_CHECKING:  # tránh import vòng khi chạy thực
    from app.models.style import StyleConfig


# Biến tự động chèn được vào thân template (không đến từ dữ liệu Excel).
#   token -> mô tả cho panel biến.
AUTO_VARS: Dict[str, str] = {
    "stt_file": "Số thứ tự hồ sơ (tăng dần theo nhóm)",
    "ngay_gio": "Ngày giờ tạo file",
}

# Biến CHỈ dùng trong footer (settings.footer_format), KHÔNG chèn được vào thân —
# chúng chỉ có giá trị lúc in từng trang (xem P2/Red Team #9).
FOOTER_VARS: Dict[str, str] = {
    "trang_so": "Số trang hiện tại (chỉ dùng ở footer)",
    "tong_so_trang": "Tổng số trang (chỉ dùng ở footer)",
}

# Khóa trong `settings` được phép dùng như biến thân template (phần văn bản hiển
# thị). Các khóa cấu hình khác (font_name, footer_*, anh_chu_ky_path) không phải
# nội dung nên loại trừ.
SETTINGS_BODY_KEYS: Tuple[str, ...] = (
    "co_quan_dong1",
    "co_quan_dong2",
    "tieu_de",
    "chuc_danh_ky",
    "nguoi_ky",
)


def document_tokens(style: "StyleConfig") -> List[str]:
    """Token cấp tài liệu = khóa của `style.document_fields` (vd `ho_so_so`)."""
    return list(style.document_fields.keys())


def row_tokens(style: "StyleConfig") -> List[str]:
    """Token cột trong dòng-mẫu bảng = khóa của `style.row_mapping`."""
    return list(style.row_mapping.keys())


def settings_tokens(style: "StyleConfig") -> List[str]:
    """Token từ settings dùng được trong thân (chỉ khóa có mặt trong settings)."""
    return [k for k in SETTINGS_BODY_KEYS if k in style.settings]


def auto_tokens() -> List[str]:
    """Token tự động chèn được vào thân."""
    return list(AUTO_VARS.keys())


def body_tokens(style: "StyleConfig") -> List[str]:
    """Tất cả token hợp lệ trong THÂN template (không gồm footer-only).

    Dùng để validate template + resolve khi render. Thứ tự ổn định: doc → settings
    → row → auto.
    """
    return (
        document_tokens(style)
        + settings_tokens(style)
        + row_tokens(style)
        + auto_tokens()
    )
