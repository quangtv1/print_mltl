---
title: "Brainstorm — WinForms + WebView2 100% clone of design_v3 (mota3 spec)"
date: 2026-07-05
type: brainstorm
source_spec: design_v3/mota3.html (full 13-section .NET WinForms spec)
source_prototype: design_v3/design3.html + support.js + template/
supersedes: plans/260704-2252-winform-questpdf-mltl-rebuild (QuestPDF/native/preview-only)
---

# Brainstorm — Ứng dụng "Tạo Mục lục hồ sơ" .NET 8 WinForms, clone 100% design_v3

## 1. Vấn đề & bối cảnh
- User cung cấp **prototype mới `design_v3/`** + **đặc tả kỹ thuật đầy đủ `mota3.html`** (13 mục): tái tạo
  **100%** GUI + luồng nghiệp vụ của bản mẫu HTML thành app desktop **.NET 8 WinForms**.
- Yêu cầu kèm: **gỡ toàn bộ code/thư mục Python** của version cũ (PyQt5).
- `mota3.html` **tự chốt kiến trúc**: *"vỏ WinForms + lõi trang WebView2"* — khung app dựng bằng control
  WinForms thật; **vùng tờ A4 (soạn thảo + xem trước) nhúng WebView2 tái dùng đúng HTML/CSS/JS template**
  của prototype → đạt độ giống tuyệt đối.

## 2. Problem-first (rút gọn)
- User mang **giải pháp đã specced đầy đủ**. Vấn đề nền: cần app desktop Windows **giống hệt** prototype
  (GUI + logic editor/phân trang) + xuất PDF khớp preview + chạy hàng loạt.
- Bản `mota3` đã **invert đúng**: thay vì dựng lại contentEditable/phân trang bằng control native (bất khả
  100% — đã phân tích ở phiên trước), **nhúng chính prototype** trong WebView2 → xoá rủi ro lớn nhất.
- Không cần tranh luận feasibility nữa; chỉ tinh chỉnh engine PDF + phạm vi dọn dẹp.

## 3. Quyết định đã chốt (2026-07-05)
| Hạng mục | Quyết định |
|---|---|
| Nền tảng | **.NET 8 WinForms** (`net8.0-windows`), C# 12 |
| Kiến trúc | **Vỏ WinForms + lõi WebView2** — nhúng HTML/JS/template của `design_v3` cho tờ A4 (editor+preview) |
| PDF | **WebView2 `CoreWebView2.PrintToPdfAsync`** (KHÔNG PuppeteerSharp) — in chính HTML đã resolve, **không cần Chromium thứ 2**, preview==PDF tự khớp |
| Đọc Excel | **ClosedXML** (`.xlsx`: sheet, header dòng 1, toàn bộ dòng) |
| Ghi Excel tổng hợp | ClosedXML |
| Đa luồng | `Parallel.ForEachAsync` + `MaxDegreeOfParallelism` |
| Cấu hình | JSON (`System.Text.Json`) trong `%AppData%\MLTL\` (nhớ lần cuối; namespace theo đường dẫn) |
| Editor | **Đầy đủ WYSIWYG** (contentEditable trong WebView2): B/I/U, font/size, căn lề, zoom, hướng A4, **chèn/xóa/kéo cột**, **undo**, chèn biến tại con trỏ, highlight biến ở preview |
| 4 mẫu | mau01–04 (Quảng Trị/Quảng Ninh/Đống Đa/Vĩnh Phúc) — tiêu đề/cột/bố cục riêng + seed data JSON |
| Xoá Python | **Gỡ toàn bộ** `app/`, `main.py`, `requirements.txt`, `styles/` python, packaging, plan python (còn trong git history + PR #1) |
| Vị trí | Solution **`MucLucTaiLieu.sln` ở gốc repo**; **nhánh mới `feat/winform-webview2`** off `main` |
| Plan cũ | `260704-2252-winform-questpdf` → **cancelled/superseded** (QuestPDF/native/preview-only bị thay) |

## 4. Kiến trúc (theo mota3 §12, đã tinh chỉnh engine PDF)
```
MucLucTaiLieu.sln (gốc repo, nhánh feat/winform-webview2)
├─ MucLucTaiLieu.App        (WinForms, net8.0-windows)
│  ├─ Forms/ MainForm (wizard shell + step header + nav bar)
│  │        Step1InputControl (Đầu vào) · Step2DesignControl (WebView2) · Step3RunControl (Chạy) · MappingPopup
│  ├─ Web/  (vendor: design3 template HTML/CSS + editor.js + templates.js + engine phân trang)
│  ├─ Assets/ (icon, seed JSON 4 mẫu)
│  └─ Program.cs
├─ MucLucTaiLieu.Core       (class library, net8.0 — test đa nền)
│  ├─ Models/ (HoSo, VanBan, TemplateDef, AppConfig)
│  ├─ Excel/ (IExcelReader → ClosedXmlReader) · Templating/ (NameResolver, seed)
│  ├─ Pdf/ (IPdfRenderer → WebView2PrintRenderer)  ← in offscreen WebView2
│  └─ Config/ (ConfigStore — JSON %AppData%)
└─ MucLucTaiLieu.Tests      (xUnit — Core)
```
- **Bridge WinForms↔WebView2:** `CoreWebView2.PostWebMessageAsJson` / `AddHostObjectToScript` để đẩy
  record/mapping/template vào trang, nhận HTML đã resolve + chiều cao đo (`getBoundingClientRect`) ra.
- **Batch:** mỗi hồ sơ → resolve HTML (JS engine) → 1 WebView2 offscreen `PrintToPdfAsync` → file. Song song
  bằng pool WebView2 giới hạn (`MaxDegreeOfParallelism`).

## 5. Các phương án đã cân nhắc
| Quyết định | Chọn | Loại bỏ / lý do |
|---|---|---|
| Vùng A4 | WebView2 nhúng template thật | RichTextBox/native — bất khả 100% (contentEditable/bảng/phân trang) |
| PDF | **WebView2 PrintToPdf** | PuppeteerSharp — kèm Chromium thứ 2 ~150–300MB (mota3 gợi ý nhưng dư thừa vì đã có WebView2). QuestPDF — không đạt 100%, phải code lại layout |
| Excel | ClosedXML | EPPlus ≥7 (license phi-noncommercial) |
| Xoá Python | Gỡ hẳn (git giữ lịch sử) | Giữ song song — messy, không cần (PR #1 đã lưu) |

## 6. Rủi ro & giảm thiểu
| Rủi ro | Giảm thiểu |
|---|---|
| WebView2 runtime thiếu trên máy đích | Evergreen có sẵn Win10/11; kèm bootstrapper/Fixed-Version nếu offline. Ghi ở docs. |
| `PrintToPdfAsync` ít kiểm soát lề/margin hơn Puppeteer | Đặt `CoreWebView2PrintSettings` (khổ A4, margin, scale); phân trang do CHÍNH template làm (đo trong trang) → PrintToPdf chỉ "chụp" HTML đã phân trang → khớp preview. Nếu thiếu kiểm soát → fallback Puppeteer (đã ghi). |
| Batch nhiều WebView2 offscreen tốn RAM | Pool giới hạn (vd = ProcessorCount), tái dùng instance, dispose gọn; đo với ~1000 hồ sơ. |
| Reuse JS engine của prototype (support.js/design3) | Vendor nguyên trạng vào `Web/`; chỉ thay data mock bằng cầu nối C#; giữ đúng thuật toán phân trang. |
| Xoá Python phá working tree đang có thay đổi | Commit/stash trước khi xoá; Python vẫn ở git history + PR #1 (khôi phục được). |
| Dev trên macOS không build/chạy WinForms+WebView2 | Core (`net8.0`) test đa nền; App + WebView2 + PrintToPdf build/test trên **Windows** (CI windows-latest / máy thật). |

## 7. Tiêu chí thành công
= **checklist §13 của `mota3.html`** (nguồn chân lý), tóm tắt:
- [ ] Wizard 3 bước, step header ✓/đang/chưa; nút "Tiếp theo" khóa tới khi mapping hợp lệ.
- [ ] Bước 1: Excel+sheet, Đọc dữ liệu, bảng ghép biến (auto-match, không cảnh báo trùng), banner trạng thái, cột gom nhóm hiện số file.
- [ ] 4 mẫu đúng tiêu đề/cột/bố cục; đổi mẫu cập nhật biến + seed data; footer số trang sát đáy phải.
- [ ] Bước 2: toolbar đúng thứ tự (font/size/BIU/A−A+/căn/zoom/hướng/chèn-xóa cột theo focus/undo); chèn/xóa biến; kéo giãn cột; highlight biến ở preview; phân trang đúng bảng dài.
- [ ] Bước 3: thư mục + mẫu tên ({stt_file},{ngay_gio}); tùy chọn đa luồng(ẩn số luồng)/ghi đè/Excel/bỏ-qua-lỗi; ước tính thời gian; **một nút "Tạo Mục lục"**; tiến trình (x/y) + console log + mở thư mục + thử lại lỗi.
- [ ] Màu accent `#0043a5`, nút Đọc cyan; nhớ trạng thái lần trước.
- [ ] **PDF khổ A4 trùng khớp xem trước**.
- [ ] Repo không còn code Python; solution .NET build/chạy trên Windows.

## 8. Bước tiếp theo & phụ thuộc
- Nhánh mới `feat/winform-webview2`; đánh dấu plan `260704-2252` cancelled.
- Vendor `design_v3/` (design3/support.js/template + seed 4 mẫu) vào `App/Web/`.
- CI `windows-latest`: `dotnet test` (Core) + `dotnet publish` + (thủ công) test UI/PDF.
- Xoá Python sau khi commit mốc hiện tại.

## Câu hỏi còn treo
- WebView2 phân phối: Evergreen (mặc định, cần mạng lần đầu) hay Fixed-Version bundle (offline, +~120MB)? → chốt ở plan/P đóng gói.
- Có cần installer (MSI) hay portable? → mặc định portable self-contained (như trước).
