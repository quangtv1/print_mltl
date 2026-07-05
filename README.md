# MụcLụcHồSơ — Tạo Mục lục hồ sơ từ Excel/CSV + Template DOCX

Ứng dụng **.NET 8 Desktop (WPF / MVVM)** đọc metadata văn bản từ **Excel (XLSX/XLSB)** hoặc **CSV**,
gom nhóm theo **hồ sơ** (một hồ sơ nhiều văn bản, các dòng cùng hồ sơ nằm **liên tiếp**), và với mỗi hồ sơ
sinh một **Mục lục** từ **Template DOCX** do người dùng thiết kế bằng Microsoft Word.
Mặc định xuất **DOCX**; tùy chọn xuất đồng thời **PDF**. Đọc **streaming** nên chịu được dữ liệu rất lớn với RAM thấp.

> Đặc tả bàn giao: `DacTa_BuildReady_v1.1_MucLucHoSo.html` (v1.1, chốt 05/07/2026). README này bám theo đặc tả đó.

## Nguyên tắc cốt lõi
- **KHÔNG** load toàn bộ Excel vào RAM; **KHÔNG** dùng `DataTable`/`DataSet` (đọc theo dòng).
- Biên dịch Template **một lần** (Runtime Template bất biến), không parse lại DOCX cho mỗi hồ sơ.
- **Preview = Output**: xem trước và kết quả đi qua **đúng cùng một đường render** (DOCX → Word), không có "Preview Engine" riêng.
- **DOCX là nguồn chân lý duy nhất**; PDF và Preview đều phái sinh từ chính DOCX đó qua chính Word.

## Công nghệ
| Lớp | Thư viện / kỹ thuật |
|-----|---------------------|
| UI | .NET 8, WPF, MVVM (`CommunityToolkit.Mvvm`) |
| Đọc dữ liệu | `ExcelDataReader` (XLSX/XLSB streaming) · `CsvHelper` (CSV) |
| Merge DOCX | `DocumentFormat.OpenXml` — thuần .NET, **không cần Office** |
| Xuất PDF | **Microsoft Word Interop** (late-bound COM) — `ExportAsFixedFormat`, cần Word cài trên máy chạy |
| Xem trước | Word → PDF, rồi rasterize trang bằng `Windows.Data.Pdf` (Windows 10+), hiển thị bằng `Image` |
| Đa luồng | `System.Threading.Channels` + TPL |
| Logging | `Serilog` (job.log / audit) |

*Vì sao Word Interop (không QuestPDF / không LibreOffice):* mẫu do người dùng thiết kế trong Word, nên convert bằng
chính Word cho fidelity cao nhất và tận dụng Office sẵn có; QuestPDF không convert DOCX và license Community không cho
cơ quan nhà nước dùng. PDF nằm sau interface `IPdfConverter` để sau này cắm backend khác (vd LibreOffice) mà không sửa lõi.

## Cấu trúc solution
```
MucLucHoSo.sln
├─ src/MucLucHoSo.Core            (net8.0 — KHÔNG phụ thuộc Office, build/test mọi nền tảng)
│   ├─ Reading/     IRowReader streaming (ExcelDataReader, CsvHelper) + ReaderFactory
│   ├─ Grouping/    GroupEngine — gom nhóm liên tiếp, streaming
│   ├─ Templating/  TemplateCompiler, RuntimeTemplate, DocxMerger, OpenXmlHelpers  ← lõi merge OpenXML
│   ├─ Output/      IPdfConverter (interface), FileNameBuilder
│   ├─ Pipeline/    GeneratePipeline (2 tầng), JobState (Resume), GenerateResult
│   └─ Validation/  Validator
├─ src/MucLucHoSo.Pdf.WordInterop (net8.0-windows — WordInteropPdfConverter, cần Word)
└─ src/MucLucHoSo.App             (net8.0-windows, WPF — Wizard 4 bước, MVVM)
templates/  Template01..04.docx      (đã dựng từ 4 mẫu thật; Mẫu 04 dựng lại từ PDF)
sample/     Biên_mục_Quảng_trị.xlsx  (sheet KhanhLinh = dữ liệu Mẫu 01, 326 dòng / 22 hồ sơ)
```

## Yêu cầu
- **.NET 8 SDK**. Cần **Windows** để build `Pdf.WordInterop` + `App` (WPF & COM Word).
- `Core` build/test được trên **mọi nền tảng** (Linux/macOS/Windows).
- Xuất **PDF** và **Xem trước** cần **Microsoft Word** cài trên máy chạy. Không có Word vẫn **xuất DOCX** bình thường (chỉ mất PDF + Preview).

## Build
```bash
dotnet restore
dotnet build MucLucHoSo.sln -c Release          # cần Windows cho App + Pdf.WordInterop
# chỉ Core (không cần Windows/Word):
dotnet build src/MucLucHoSo.Core/MucLucHoSo.Core.csproj -c Release
```
CI (`.github/workflows/`) build + publish trên `windows-latest`; tag `v*` tạo GitHub Release (self-contained win-x64,
1 file `MucLucHoSo.exe`, kèm `templates/`). Bản phát hành: **v1.0.0**.

## Wizard 4 bước
Một `Window` WPF (~1200×780), 3 vùng dọc **StepHeader · ContentHost · NavBar**; accent `#0043a5`, cyan `#00ccd6`;
font UI Segoe UI, nội dung tài liệu Times New Roman. Nhớ lựa chọn gần nhất.

1. **Nguồn** — chọn XLSX/XLSB/CSV, chọn sheet, **Đọc dữ liệu** (header + 100 dòng xem nhanh, RAM < 50 MB);
   chọn/Import Template DOCX, xem danh mục biến (tự do / trong bảng / tự động); Xem nhanh placeholder.
2. **Ghép biến** — tự khớp cột↔biến; mỗi biến chọn **Cột** Excel hoặc gõ **Hằng**; chọn **cột gom nhóm**;
   nút **Kiểm tra dữ liệu** (Validation chạy full streaming).
3. **Xem trước** — render **chính DOCX** của hồ sơ đang xem qua **Word → PDF**, hiển thị bằng ảnh trang;
   segmented **Xem Template / Xem trước**, zoom, điều hướng ◀ Hồ sơ (i/n) ▶, panel giá trị biến. Không có trình soạn thảo.
4. **Tạo mục lục** — thư mục + mẫu tên (`MLHS_{so_ho_so}`), DOCX luôn bật, PDF/ghi đè tùy chọn;
   tùy chọn đa luồng / Resume / Audit / Bỏ qua hồ sơ lỗi; tiến trình + nhật ký realtime + mở thư mục kết quả.

## Template DOCX — quy tắc
Cú pháp trong Word: biến hồ sơ/hằng `{so_ho_so}`, `{don_vi}`, `{nguoi_lap}`…; biến cấp dòng `{stt}`, `{so_ky_hieu}`…;
tự động `{trang_so}` / `{tong_so_trang}` (engine chèn thành field Word **PAGE/NUMPAGES**, Word tự tính khi render).

- **Bảng KHÔNG cần đánh dấu vòng lặp.** Để **một hàng mẫu** chứa các biến cấp dòng; `TemplateCompiler` tự nhận diện
  hàng đó là prototype (hàng có nhiều biến cấp dòng nhất) và nhân bản theo từng văn bản của hồ sơ.
- Biên dịch một lần → Runtime Template (byte[] bất biến + phân tích). Merge từng hồ sơ = clone byte[] →
  thay biến header → nhân bản prototype row → xoá prototype. Clone riêng nên **an toàn đa luồng**.

## Ràng buộc biến = Cột HOẶC Hằng
Dữ liệu thật thường thiếu cột cho một số biến (vd `{don_vi}`, `{chi_nhanh}`, `{nguoi_lap}`). Mỗi biến bind theo:
- **Cột Excel** — theo dòng (biến trong bảng) hoặc theo dòng đầu nhóm (biến cấp hồ sơ);
- **Hằng** — người dùng gõ một giá trị áp cho mọi hồ sơ;
- **Tự động** — `{trang_so}` / `{tong_so_trang}` (Word tính).

**Gom nhóm:** chọn cột gom nhóm; các dòng cùng hồ sơ **phải liên tiếp** (kiểm 1-pass streaming, gặp lại key đã đóng → dừng và báo).
Trường cấp hồ sơ lấy từ dòng đầu nhóm.

## Danh mục biến 4 template thật
| Mẫu | Bảng | Cấp hồ sơ / hằng | Biến trong bảng (loop) |
|-----|------|------------------|------------------------|
| **01 – Quảng Trị** | 7 cột | `{so_ho_so}` · hằng `{don_vi}` `{chi_nhanh}` `{nguoi_lap}` | `{stt}` `{so_ky_hieu}` `{ngay_thang}` `{tac_gia}` `{trich_yeu}` `{to_so}` `{ghi_chu}` |
| **02 – Quảng Ninh** | 7 cột | `{ho_so_so}` `{muc_luc_hs_so}` `{phong_so}` `{tieu_de_tap}` | `{tt}` `{so_ky_hieu}` `{ngay_thang}` `{tac_gia}` `{trich_yeu}` `{trang_tu_den}` `{ghi_chu}` |
| **03 – Đống Đa** | 9 cột | `{ho_so_so}` | `{stt}` `{so_ky_hieu}` `{ngay_ky}` `{loai_vb}` `{tac_gia}` `{trich_yeu}` `{to_so}` `{sl_trang}` `{ghi_chu}` |
| **04 – Vĩnh Phúc** | 7 cột | `{so_ho_so}` `{ten_ho_so}` | `{stt}` `{so_ky_hieu}` `{ngay_thang}` `{trich_yeu}` `{tac_gia}` `{to_so}` `{ghi_chu}` |

Tất cả đều có `{trang_so}` `{tong_so_trang}` (tự động). Mẫu 04 dựng lại layout từ PDF (không có DOCX gốc) nên fidelity gần đúng.

## Pipeline Generate — 2 tầng
```
xlsx/csv → Reader(stream) → Group(liên tiếp) → HoSoJob{hằng, nhóm, rows}
   ├─ Merge Pool (N = ProcessorCount − 2, OpenXML, KHÔNG cần Office) → DOCX
   └─(nếu bật PDF)→ Channel<docxPath> → Word Converter (1 luồng STA, 1 instance WINWORD, tuần tự) → PDF
```
Chỉ giữ hồ sơ hiện tại trong RAM (100–300 MB bất kể số dòng). Chỉ DOCX → chạy full song song, nhanh nhất.
Có PDF → tổng thời gian ≈ số hồ sơ × ~0,3–1 s/file (trần cố hữu của Word Interop).

**Gia cố Word Interop:** tái dùng một instance cho cả job; `Visible=false`, tắt alert/security; `Fields.Update()` trước export;
đóng doc không lưu + `ReleaseComObject`; **watchdog** timeout → kill WINWORD, dựng lại instance, tiếp tục nhờ **Resume**.

## Resume Job, Audit, Validation
- **Resume**: checkpoint `lastCompletedGroupIndex` → chạy lại bỏ qua group đã xong.
- **Audit**: `job.log` (Serilog) mỗi hồ sơ; `failures.json` → "Thử lại hồ sơ lỗi". Phân loại: DataError · TemplateError · IoError · PdfError.
- **Validation** (không sinh tài liệu): file tồn tại · mapping đủ · placeholder/loop hợp lệ · hồ sơ liên tiếp · cột bắt buộc.
  Kết quả mẫu (sheet KhanhLinh): 326 dòng · 22 hồ sơ · hồ sơ lớn nhất 65 văn bản · liên tiếp: đạt · **Hợp lệ**.

## Whitelist tính năng Word (v1)
**Hỗ trợ:** định dạng text; bảng (merge cell, viền, độ rộng cột) miễn loop nằm trong một bảng với một hàng prototype;
header/footer, logo & ảnh tĩnh, page setup; field PAGE/NUMPAGES; placeholder gọn trong một vùng.
**Chưa hỗ trợ:** loop lồng, bảng lồng trong hàng loop, ảnh đổi theo dữ liệu, mail-merge field gốc, biểu thức/điều kiện trong placeholder.

## Mở bằng VS Code
Thư mục `.vscode/` có sẵn `tasks.json` + `launch.json`. Mở thư mục gốc → cài **C# Dev Kit** → **F5** (cấu hình "Chạy MucLucHoSo.App").

## Lịch sử phiên bản
Bản phát hành: [GitHub Releases](https://github.com/quangtv1/print_mltl/releases) (tag `v*` → CI build self-contained win-x64).

### v1.1.0
- **Nút bấm** đồng bộ: bo góc, trạng thái hover/pressed/disabled/focus theo brand, con trỏ tay; nút "Tạo lại" rộng bằng "Tạo mục lục".
- **Màn Xem trước:** nút zoom & tiến/lùi hồ sơ tự mờ ở biên (hết click chết); bấm "%" về 100%; **Ctrl + lăn chuột** zoom; phím tắt **Ctrl +/−/0**, **Ctrl ←/→** đổi hồ sơ; tooltip + nhãn trợ năng cho nút icon.
- Tab "Xem Template / Xem trước" có gạch chân khi chọn; chip biến đang chọn được tô sáng.
- Sửa tương phản chữ biến tự động đạt **WCAG AA**; thêm phím tắt **Alt ←/→** điều hướng wizard.

### v1.0.0
- Bản đầu: app WPF 4 bước (Nguồn → Ghép biến → Xem trước → Tạo mục lục), pipeline merge OpenXML 2 tầng + xuất PDF qua Word Interop, 4 template thật, Resume/Audit/Validation.

## Còn phải làm (theo đặc tả §16)
- **Spike:** ổn định Word Interop batch 500–1000 file; fidelity merge 4 template (nhất là Mẫu 03 chín cột); độ trễ Preview.
- **Test:** thêm project `MucLucHoSo.Tests` cho GroupEngine / DocxMerger / Validator (chưa có trong repo).
- Tiêu chí nghiệm thu đầy đủ: xem §17 của đặc tả.
