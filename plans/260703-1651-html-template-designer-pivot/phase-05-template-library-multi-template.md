---
phase: 5
title: "Template Library (Multi-template)"
status: pending
effort: ""
---

# Phase 5: Template Library (Multi-template)

## Overview
Cho phép quản lý **nhiều mẫu** HTML: liệt kê `styles/*` có `template.html`, chọn/đổi mẫu trong app,
mỗi mẫu có bindings/settings riêng. Ship thêm 1 mẫu thứ 2 để chứng minh đa mẫu.

## Requirements
- Functional: dropdown chọn style; đổi style nạp lại template.html + bindings + settings + preview;
  lưu độc lập từng style.
- Non-functional: copy thư mục `styles/<tên>/` sang máy khác dùng lại không sửa code.

## Architecture
```
app/core/style_config.py  # list_styles đã có; đảm bảo quét template.html
app/ui/step_design.py     # thêm dropdown "Mẫu" (đầu bước 2) → đổi StyleConfig hiện hành
styles/<mẫu-2>/{template.html, style.json}   # mẫu thứ 2 (bố cục/CSS khác)
```

## Related Code Files
- Modify: `app/core/style_config.py` (nếu cần), `app/ui/step_design.py` (dropdown chọn mẫu)
- Create: `styles/<mẫu-2>/template.html` + `style.json`

## Implementation Steps
1. Dropdown chọn mẫu ở đầu bước Thiết kế; đổi mẫu → nạp StyleConfig + template + preview.
2. Đảm bảo bindings/settings lưu theo từng style dir (độc lập).
3. Tạo mẫu thứ 2 (vd bố cục khác/đổi cột) để verify đa mẫu.

## Success Criteria
- [ ] Chọn mẫu khác → preview + bindings đổi theo; xuất ra đúng mẫu đã chọn.
- [ ] Copy `styles/<tên>/` sang máy khác → dùng lại được, không sửa code.

## Risk Assessment
- Thấp–TB: cơ chế styles/ đã có từ v1; chủ yếu thêm UI chọn mẫu + 1 mẫu mẫu.
