---
title: "Brainstorm — WinForms (.NET 8 + QuestPDF) rebuild of Mục Lục Hồ Sơ"
date: 2026-07-04
type: brainstorm
modes: []
source_prototype: design/ (classic blue #0078d7)
supersedes_platform: PyQt5 app (feat/native-qt-pdf-designer)
---

# Brainstorm — Xây lại app "Tạo Mục Lục Hồ Sơ" bằng WinForms (.NET 8 + QuestPDF)

## 1. Vấn đề & bối cảnh
- Đang có app PyQt5 chạy được (engine QTextDocument→QPdfWriter, WYSIWYG, 4 mẫu). Người dùng muốn
  **xây lại bằng WinForms/.NET** làm việc tương tự, **bám design/ (classic blue)**, và **đọc Excel nhanh hơn**.
- **Driver thật (đã xác nhận):** *ưu tiên stack C#/.NET* (maintainer làm .NET) — không phải vì Python
  chạy sai. Đây là lý do bảo trì dài hạn hợp lệ: giữ app ở stack mà người bảo trì thạo.

## 2. Problem-first (rút gọn)
- **Solution-jumping:** "viết lại WinForms" nén 3 pain: (a) tự tin đóng gói .exe Windows, (b) cảm giác
  native Win32 (prototype vốn là Win32), (c) đọc Excel chậm.
- **Sự thật quan trọng:** pain (c) **không cần** đổi nền tảng — chỉ cần thay `openpyxl`→`calamine` trong
  app Python là nhanh 10–50×. Nếu chỉ vì tốc độ đọc thì rewrite là over-correction.
- **Chi phí thật của rewrite = engine PDF WYSIWYG.** .NET **không có** tương đương `QTextDocument`
  (HTML rich-text → PDF phân trang). Quyết định "khoá 4 mẫu bằng code" **loại bỏ** rủi ro này → path sạch.
- **Kết luận:** rewrite chỉ đáng khi (driver = stack preference) **và** chấp nhận **bỏ editor WYSIWYG**.
  Cả hai đều đã xác nhận → tiến hành.

## 3. Quyết định đã chốt (2026-07-04)
| Hạng mục | Quyết định |
|---|---|
| Nền tảng | **.NET 8 WinForms**, **single-file self-contained .exe** |
| Mẫu (template) | **Khoá bằng code** — 4 lớp QuestPDF; **không** editor WYSIWYG |
| Engine PDF | **QuestPDF** (MIT/Community; thread-safe → batch **song song thật**) |
| Đọc Excel | **ExcelDataReader** (streaming, background thread + progress) — file lớn 20k+ dòng |
| Ghi Excel tổng hợp | **ClosedXML** |
| Thiết kế | **design/ classic blue `#0078d7`**, wizard **3 bước** |
| Bước 2 | **Preview-only** (chọn mẫu + ◀▶ duyệt hồ sơ + xem trang A4; bỏ toolbar sửa mẫu) |
| style.json | **chỉ metadata** (mapping mặc định, settings, cột gom nhóm, pattern tên file) — layout ở code |

## 4. Kiến trúc đề xuất
```
MucLucHoSo.sln
  MucLucHoSo/ (.NET 8 WinForms, self-contained)
    Program.cs
    Forms/MainForm.cs            # host wizard: brand bar + stepper + action bar + state chung
    Controls/
      StepInput.cs               # file+sheet+Đọc dữ liệu (async) + mẫu + DataGridView mapping + banner + gom nhóm
      StepPreview.cs             # chọn mẫu + ◀▶ record + ảnh trang A4 (QuestPDF GenerateImages)
      StepRun.cs                 # thư mục + pattern tên + tùy chọn + progress + log tối + Generate (song song)
    Core/
      ExcelReader.cs             # ExcelDataReader → DataTable; validate header rỗng/trùng; async + progress
      Grouping.cs                # nhóm theo cột gom nhóm (giữ thứ tự)
      Models/StyleConfig.cs      # đọc style.json metadata
      Templates/
        ITemplate.cs             # Compose(doc_fields, records, settings) → QuestPDF Document
        DongHaTemplate.cs, QuangNinhTemplate.cs, DongDaTemplate.cs, VinhPhucTemplate.cs
      BatchGenerator.cs          # Parallel.ForEach; dedup tên tuần tự (main thread); progress + log; ghi đè/skip
      ExcelExporter.cs           # ClosedXML summary + chống formula-injection
      FileName.cs                # expand token {ho_so_so}/{stt_file}/{ngay_gio}; sanitize; cắt độ dài
    styles/<slug>/style.json     # metadata 4 mẫu
```
**Điểm mạnh .NET tận dụng:** QuestPDF thread-safe → `BatchGenerator` chạy `Parallel.ForEach` (multithread
thật — thứ bản Python phải tắt vì Qt không an toàn). Cùng ExcelDataReader → cả **đọc** lẫn **sinh PDF** đều nhanh.

## 5. Các phương án đã cân nhắc
| Quyết định | Chọn | Loại bỏ / lý do |
|---|---|---|
| Nền tảng | WinForms .NET 8 | Giữ PyQt (đã chạy) — loại vì driver = stack C#. WPF — dư thừa cho UI Win32 cổ điển. |
| PDF | QuestPDF | MigraDoc/PdfSharp (MIT thuần, không điều khoản doanh thu) — API cũ, viết layout thủ công nhiều hơn. HTML→PDF (Puppeteer) — kèm ~150MB Chromium, phá lợi thế exe gọn. |
| Đọc Excel | ExcelDataReader | EPPlus (nhanh nhưng license thương mại cho phi-noncommercial). ClosedXML đọc chậm hơn (dùng cho GHI). |
| Editor | Bỏ (preview-only) | Giữ WYSIWYG — buộc HTML→PDF/RTF→PDF, đắt & rủi ro cao. |

## 6. Rủi ro & giảm thiểu
| Rủi ro | Giảm thiểu |
|---|---|
| **QuestPDF Community license** (miễn phí <$1M/yr doanh thu tổ chức) | App cơ quan/lưu trữ gần như đủ điều kiện; nếu lo → fallback MigraDoc. Ghi rõ trong docs. |
| **Bước 2 lệch prototype** (không có editor) | Preview-only đã chốt; giữ bố cục/thanh điều hướng, bỏ toolbar sửa. |
| **Khoá mẫu bằng code** → sửa mẫu cần dev + build lại | Chấp nhận (4 mẫu ổn định). style.json vẫn chỉnh được settings/mapping không cần build. |
| **Font Times New Roman** cho PDF | Windows có sẵn; QuestPDF dùng font hệ thống, pin tên font. |
| **File cực lớn treo UI** | Đọc async + progress + cận kích thước fail-fast (như bản Python). |
| **Fidelity phân trang** (header bảng lặp, footer x/y) | QuestPDF hỗ trợ sẵn (`Table.Header`, footer slot) — verify sớm bằng nhóm ≥3 trang. |

## 7. Tiêu chí thành công
- Chọn `.xlsx` **20k+ dòng** → đọc < vài giây, UI không treo, progress chạy.
- Đi 3 bước → xuất **N PDF** đúng dữ liệu; bảng dài chảy đúng qua trang + header lặp + footer "Trang x/y".
- Batch **song song** nhanh hơn bản Python serial (đo, kỳ vọng ≥2–4×).
- Excel tổng hợp đúng; ghi đè/skip; log realtime.
- Build ra **1 file .exe self-contained** chạy sạch trên Win10/11 không cần cài Python/.NET runtime.
- Giao diện khớp `design/` (blue #0078d7): brand bar, stepper chevron, fieldset, bảng mapping có `→` + token + banner.

## 8. Bước tiếp theo & phụ thuộc
- **Repo:** dựng solution mới `MucLucHoSo/` (thư mục con trong repo hiện tại hoặc repo riêng — chốt ở plan).
- **Seed 4 mẫu:** port cấu trúc cột từ style.json Python hiện có (đã có sẵn 4 mẫu) → 4 lớp QuestPDF.
- **CI:** GitHub Actions `windows-latest` `dotnet publish -r win-x64 --self-contained` → artifact .exe.
- **Không đụng** app PyQt5 hiện tại (giữ song song tới khi WinForms đạt parity).

## Câu hỏi còn treo
- Repo mới riêng hay thư mục con trong repo này? (chốt khi lập plan)
- Có cần installer (MSI) hay chỉ .exe portable? (mặc định: .exe portable self-contained)
