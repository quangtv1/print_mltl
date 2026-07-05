# Tạo mục lục hồ sơ — SaoMai

Ứng dụng .NET 8 (WPF/MVVM) đọc metadata văn bản từ **Excel/CSV**, gom nhóm theo **hồ sơ**,
và với mỗi hồ sơ sinh một **Mục lục** từ **Template DOCX**. Mặc định xuất **DOCX**; tùy chọn xuất **PDF**.

Xem đặc tả đầy đủ: `DacTa_BuildReady_v1.1_MucLucHoSo.html`.

## Cấu trúc solution
```
MucLucHoSo.sln
├─ src/MucLucHoSo.Core            (net8.0 — KHÔNG phụ thuộc Office, test được mọi nền tảng)
│   ├─ Reading/    IRowReader streaming (ExcelDataReader, CsvHelper)
│   ├─ Grouping/   GroupEngine — gom nhóm liên tiếp, streaming
│   ├─ Templating/ TemplateCompiler, RuntimeTemplate, DocxMerger, OpenXmlHelpers  ← lõi merge OpenXML
│   ├─ Output/     IPdfConverter (interface), FileNameBuilder
│   ├─ Pipeline/   GeneratePipeline (2 tầng), JobState (resume), GenerateResult
│   └─ Validation/ Validator
├─ src/MucLucHoSo.Pdf.WordInterop (net8.0-windows — WordInteropPdfConverter, cần Word)
└─ src/MucLucHoSo.App             (net8.0-windows, WPF — KHUNG: nút chạy Vertical Slice)
templates/  Template01..04.docx     (đã tạo từ file kết quả; 04 dựng lại từ PDF)
sample/     Biên_mục_Quảng_trị.xlsx (sheet KhanhLinh = dữ liệu Mẫu 01)
```

## Yêu cầu
- .NET 8 SDK. Windows để build `Pdf.WordInterop` + `App` (dùng WPF & COM Word).
- `Core` build/test được trên mọi nền tảng (Linux/macOS/Windows).
- Xuất PDF cần **Microsoft Word** cài trên máy chạy.

## Build
```bash
dotnet restore
dotnet build -c Release
# chỉ Core (không cần Windows/Word):
dotnet build src/MucLucHoSo.Core/MucLucHoSo.Core.csproj -c Release
```

## Vertical slice (chứng minh cả chuỗi — đường DOCX, KHÔNG cần Word)
Mẫu 01 + sheet `KhanhLinh` → đọc → gom nhóm (22 hồ sơ) → merge → 22 file DOCX.
1. Chạy `MucLucHoSo.App`, bấm **“Chạy Vertical Slice”**.
2. Chọn `templates/Template01.docx`, rồi `sample/Biên_mục_Quảng_trị.xlsx`.
3. Kết quả 22 file `MLHS_*.docx` trong thư mục `Output/` cạnh file Excel.

Hoặc gọi trực tiếp Core (đường DOCX):
```csharp
var rt  = TemplateCompiler.Compile("templates/Template01.docx");
var map = SampleData.MauMotMapping();               // so_ho_so←"Tiêu đề hồ sơ", stt←"STT"…; don_vi/chi_nhanh/nguoi_lap = HẰNG
var opt = new GenerateOptions { OutputDirectory = "Output", ExportPdf = false };
var pipe = new GeneratePipeline(rt, map, opt, () => new NullPdfConverter(), Log.Logger);
await pipe.RunAsync(() => ReaderFactory.Open("sample/Biên_mục_Quảng_trị.xlsx", "KhanhLinh"), "Output/job.state.json");
```
Bật PDF: `ExportPdf = true` và truyền `() => new WordInteropPdfConverter()` (cần Word).

## Quy tắc template (quan trọng)
- Biến hồ sơ/hằng: `{so_ho_so}`, `{don_vi}`, `{nguoi_lap}`…
- **Bảng KHÔNG cần đánh dấu vòng lặp.** Chỉ để **một hàng mẫu** chứa biến cấp dòng
  (`{stt}`, `{so_ky_hieu}`…); engine tự nhân bản hàng đó theo từng văn bản của hồ sơ.
- `{trang_so}`/`{tong_so_trang}` → engine chèn thành field Word **PAGE/NUMPAGES** (Word tự tính khi render).

## Danh mục biến 4 template — xem §5 của đặc tả (mỗi mẫu cột/biến khác nhau).

## Giao diện Wizard 4 bước (đã có)

App giờ là wizard đầy đủ (MVVM, CommunityToolkit.Mvvm):
1. **Nguồn** — chọn XLSX/XLSB/CSV, chọn sheet, "Đọc dữ liệu" (đọc 100 dòng xem nhanh); chọn/Import Template, xem danh mục biến (tự do / trong bảng / tự động).
2. **Ghép biến** — tự khớp cột↔biến; mỗi biến chọn **Cột** hoặc gõ **Hằng**; chọn cột gom nhóm; nút **Validation** (chạy full streaming).
3. **Xem trước** — render **chính DOCX** qua **Word → PDF**, hiển thị bằng **WebView2**; chuyển "Xem Template / Xem trước", điều hướng ◀ hồ sơ ▶, panel giá trị biến.
4. **Tạo mục lục** — chọn thư mục/mẫu tên, bật PDF/ghi đè, tùy chọn đa luồng/Resume/Audit/Bỏ qua lỗi; tiến trình + nhật ký realtime + mở thư mục kết quả.

Yêu cầu thêm cho UI:
- **WebView2 Runtime** để xem Preview (đã có sẵn trên đa số Win10/Win11; nếu thiếu, tải "Microsoft Edge WebView2 Runtime"). Không có Word/WebView2 vẫn xuất DOCX bình thường, chỉ mất phần Preview.
- 4 template trong `templates/` được **tự chép** vào thư mục chạy nên ComboBox ở Bước 1 tự liệt kê.

## Mở bằng VS Code
Thư mục `.vscode/` đã có sẵn `tasks.json` + `launch.json`. Mở thư mục gốc solution → cài **C# Dev Kit** → nhấn **F5** (chọn cấu hình "Chạy MucLucHoSo.App").

## Còn phải làm (theo đặc tả)
- Spike: ổn định Word Interop batch 500–1000 file; fidelity merge 4 template (nhất là Mẫu 03 9 cột); độ trễ Preview.
- Lắp UI wizard 4 bước vào khung App; Preview = render DOCX qua Word.
- Bộ test cho GroupEngine / DocxMerger / Validator.

> Ghi chú: code viết cho .NET 8 nhưng **chưa build-test trong môi trường tạo** (không có .NET SDK ở đây).
> Bước đầu tiên của dev: `dotnet build` + chạy vertical slice ở trên.
