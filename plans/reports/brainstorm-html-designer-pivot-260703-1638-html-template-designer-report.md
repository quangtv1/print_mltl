---
title: "Brainstorm — Pivot sang HTML + Trình thiết kế mẫu (đa mẫu, gán biến vào vùng)"
date: 2026-07-03
type: brainstorm-report
status: awaiting-approval
supersedes_partially: plans/260703-1127-pyqt5-mltl-docx-generator (engine docx P3, preview P4)
modes: []
---

# Brainstorm: Pivot HTML + Trình thiết kế mẫu

## Vấn đề gốc (problem-first)

Đề xuất của user = "canvas A4 kéo-thả biến vào tọa độ + 2 mode edit/preview + tiến/lùi record".
Vấn đề thật đằng sau = **sửa template.docx bằng Word bất tiện; muốn cấu hình + xem trước
+ gán biến ngay trong app, cho nhiều mẫu.** Kéo-thả-tọa-độ chỉ là giải pháp user tưởng tượng.

Chốt qua hỏi đáp: **đổi output sang HTML/PDF**, **gán biến vào vùng** (không tọa độ tự do),
**đa mẫu**. → HTML hợp editor trực quan hơn docx nhiều; tên repo `print_html` khớp.

## Quyết định đã chốt (từ user)

| Hạng mục | Chọn |
|---|---|
| Output | **HTML** (bỏ .docx) |
| Kéo-thả | **Gán biến vào vùng** (không tọa độ tự do) |
| Phạm vi | **Đa mẫu** (trình thiết kế mẫu) |
| Preview | Xấp xỉ trong app → với HTML = render thật, near-exact |

## Mặc định tôi chốt (vòng user chưa trả lời — cần xác nhận lại)

| Hạng mục | Mặc định | Lý do |
|---|---|---|
| Engine PDF | **Chỉ xuất HTML** (in PDF = Ctrl+P trình duyệt) | Khỏi lib native → đóng gói exe an toàn, KISS |
| Độ sâu designer | **Thư viện mẫu + gán biến** | Vừa sức; builder tự do là nhiều tháng |
| Preview engine | **QWebEngineView** (chính xác) | Đúng bản cuối; điểm duy nhất làm exe nặng hơn |
| Cơ chế gán | **Click-to-bind** (chọn biến → bấm vùng) | Đơn giản/chắc hơn kéo-thả xuyên Qt↔web; drag thật để sau |

## Kiến trúc mục tiêu

**Template = HTML + Jinja2 + CSS** (A4 `@page`). Biến tài liệu `{{ }}`; bảng `{% for r in rows %}`.
Vùng đánh dấu bằng `data-zone` (vd `data-zone="doc:co_quan_dong1"`, `data-zone="col:stt"`).
`style.json` mở rộng: `bindings` (zone→biến) + settings text/màu/font + cột (ẩn/hiện/rộng).

Preview = render HTML với record hiện tại → nạp vào QWebEngineView. Click vùng (JS→Qt qua
QWebChannel) chọn zone; chọn biến ở panel phải để gán → re-render.

### Tái dùng / thay / mới

- **Giữ nguyên:** `sheets_client`, `excel_exporter`, `style_config`+`models` (mở rộng schema),
  `text_match`, `workers`, `platform_utils`, `batch_generator` (đổi hàm render).
- **Thay:** `docx_renderer`→`html_renderer` (Jinja2); `template_introspect` (đọc zone HTML);
  `pdf_preview` (docx→pdf) → preview HTML.
- **Mới:** `styles/<tên>/template.html`; UI designer (QWebEngineView + panel biến + QWebChannel bridge).
- **Bỏ:** toàn bộ engine docx (P3 cũ), phụ thuộc docxtpl/python-docx cho render.

### Luồng UX 3 bước (theo user)

1. **Đầu vào & Kết nối:** creds .json + URL → chọn worksheet → chọn/khớp biến (mapping) →
   chọn thư mục output → Kết nối/Tải.
2. **Thiết kế & Preview:** trái 3/4 = A4 HTML preview; phải 1/4 = biến theo nhóm
   (tài liệu / hàng / gom-nhóm / ảnh chữ ký). Click-to-bind biến vào vùng. Góc trên phải:
   toggle **Edit ↔ Preview**; ở Preview có **tiến/lùi** nhồi từng hồ sơ.
3. **Chạy:** sinh N file HTML (+ Excel tùy chọn) song song; **log realtime**: hồ sơ nào xong,
   tổng số, thời gian tạo.

## Các hướng đã cân nhắc

- **A. Configurator theo vùng + preview thật (CHỌN).** Tương thích, vừa sức, đúng bản chất tài liệu bảng.
- **B. Canvas kéo-thả tọa-độ tự do + engine tọa-độ→output.** Scope khổng lồ, bảng biến thiên không hợp tọa độ tuyệt đối, rủi ro cao. Loại.
- **C. Lai (scalar tự do, bảng cố định).** Mô hình lẫn lộn, lợi ích thêm ít. Loại.

## Rủi ro & giảm thiểu

| Rủi ro | Mức | Giảm thiểu |
|---|---|---|
| Mất khả năng sửa bằng Word | Cao | User đã chấp nhận; xác nhận downstream (nộp/lưu trữ) OK với PDF/HTML |
| Đóng gói HTML→PDF trên Windows | Cao | Chọn **HTML-only** (khỏi engine PDF) |
| exe nặng do QWebEngine | TB | Chấp nhận cho preview chính xác; fallback QTextBrowser nếu cần nhẹ |
| Vứt bỏ engine docx vừa xây (P3/P4) | TB | Thực tế của pivot; sheets/excel/batch/UI khung vẫn tái dùng |
| Đa mẫu phình scope | Cao | Giới hạn "thư viện mẫu + gán biến", không builder tự do |
| Drag-drop xuyên Qt↔web phức tạp | TB | Dùng click-to-bind trước, drag thật sau |

## Tiêu chí thành công

- Đi hết 3 bước với sheet thật → xuất N file HTML đúng dữ liệu, bảng chảy đúng qua trang.
- Bước 2: click vùng + gán biến → preview cập nhật; tiến/lùi xem đúng từng hồ sơ.
- Đổi/ thêm mẫu HTML trong `styles/` dùng lại không sửa code.
- Đóng gói .exe Windows chạy sạch (không cài Python), không lib native khó.
- Excel tổng hợp + xuất song song vẫn hoạt động.

## Câu hỏi còn treo (cần user xác nhận khi quay lại)

1. Xác nhận **HTML-only** (không cần app tự xuất PDF)?
2. Xác nhận **QWebEngineView** cho preview (exe nặng hơn ~150MB) hay muốn **QTextBrowser** nhẹ?
3. Downstream (nộp/lưu trữ) **chấp nhận PDF/HTML**, không ai cần mở sửa từng hồ sơ bằng Word?
4. Giữ **Excel tổng hợp** + **xuất song song** như bản cũ? (giả định: có)
5. Có cần **giữ song song cả .docx** cho giai đoạn chuyển tiếp không? (giả định: không)
