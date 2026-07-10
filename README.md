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
1 file `MLHS.exe`, kèm `templates/`).

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
- **Biến ảnh (chữ ký / logo / con dấu):** chèn một ảnh vào Word đúng vị trí/kích thước/kiểu bao chữ (kể cả "Behind Text"
  cho con dấu đè chữ), rồi đặt **Alt Text** của ảnh bắt đầu bằng `image` (VD `image_chu_ky`, `image_logo`, `image_con_dau`).
  Ở Bước 2, mỗi biến ảnh chọn **Hằng** (Duyệt 1 file, áp mọi hồ sơ — logo/chữ ký/con dấu) hoặc **Theo cột**
  (đường dẫn ảnh lấy từ một cột Excel, **mỗi hồ sơ một ảnh** — vd QR). Ô rỗng/file thiếu → giữ ảnh gốc, bỏ qua.
  Nên dùng **PNG nền trong suốt**. Đổi ruột ảnh thuần OpenXML (không cần Word).
- **Cách 2 — token chữ `{image...}`:** gõ thẳng token bắt đầu bằng `image` (vd `{image_qr}`) vào Word; **giá trị** bạn
  ghép (cột/hằng) chính là **đường dẫn ảnh** → engine chèn ảnh inline ngay tại token, **kích thước = tự nhiên của ảnh**.
  Đơn giản khi không cần canh vị trí/đè chữ (vd QR mỗi hồ sơ). Đặt token trong **hàng mẫu** → ảnh theo từng dòng.

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

### v1.12.11
- **Màn Nguồn**: dòng gợi ý hiển thị **đúng số dòng đã nhập** ở ô "Đọc từ dòng" (`Giá trị dòng x: …`), không còn lệch thành x+1; thêm khoảng cách nhỏ tách khỏi checkbox "Dùng CSV Cache".

### v1.12.10
- **Màn Nguồn**: ô "Đọc từ dòng" nay theo **số dòng Excel vật lý** (nhập đúng số dòng thấy trong Excel; bỏ dòng trống dẫn đầu vẫn tính) — sửa lệch +1 khi file có dòng trống ở trên.
- Dòng gợi ý đổi thành **"Giá trị dòng {x}: …"** (x = số dòng thật của dòng dữ liệu đầu), giá trị **in đậm**, tối đa 2 dòng rồi "…", căn trái thẳng nhãn "Sheet:"; bỏ khoảng trống thừa xuống checkbox.

### v1.12.9
- **Màn Nguồn**: sau khi Đọc dữ liệu / đổi "Đọc từ dòng", hiện gợi ý **giá trị dòng dữ liệu đầu tiên** (`Giá trị: …`) ngay dưới nút Đọc — gói 1 dòng, tràn tự cắt `…`; nhãn định dạng chữ thường (`xlsx`/`xlsb`/`csv`), đổi "Đọc từ" → **"Đọc từ dòng:"**.
- **Đọc Excel (xlsx/xlsb)**: format ô theo InvariantCulture — ngày ra `dd/MM/yyyy` (bỏ đuôi `00:00:00`), số nguyên bỏ ký hiệu mũ/dấu phân tách, hết phụ thuộc locale máy.
- **Đọc CSV**: tự dò encoding UTF-8 vs **Windows-1258** khi không có BOM → sửa lỗi mojibake chữ có dấu ở CSV kiểu ANSI (Excel VN).

### v1.11.0
- **Màn Nguồn**: nút Import gộp thành dòng cuối ComboBox mẫu (`➕ Import DOCX…`); vùng biến có trạng thái rỗng ("Hãy chọn mẫu để tiếp tục"), cảnh báo khi mẫu không có biến và **khoá nút "Tiếp theo"**; token `{image…}` gom vào nhóm **Biến ảnh** (khử trùng khỏi Biến tự do/trong bảng).
- **Màn Xem trước**: sidebar biến đồng bộ với màn Nguồn (chip biến trang gộp vào Biến tự do, thêm nhóm Biến ảnh); cảnh báo mềm khi máy không có Microsoft Word (vẫn xuất DOCX bình thường).

### v1.10.1
- Cảnh báo khi một tên vừa là token `{image…}` vừa là Alt Text ảnh (nhắc đổi tên để tránh trùng).

### v1.10.0
- **Biến ảnh theo cột**: đường dẫn ảnh lấy từ một cột Excel → **mỗi hồ sơ một ảnh** (vd QR); toggle Hằng / Theo cột ở Bước 2.
- **Token ảnh `{image...}`**: gõ token bắt đầu bằng `image`, giá trị ghép (cột/hằng) là đường dẫn → chèn ảnh inline tại token, kích thước tự nhiên.

### v1.9.0
- **Biến ảnh** (chữ ký / logo / con dấu): đặt ảnh trong Word + Alt Text bắt đầu bằng `image`, duyệt file ở Bước 2; engine đổi ruột ảnh giữ nguyên vị trí/kích thước/đè chữ (thuần OpenXML).
- Bảng Ghép biến: sắp xếp biến theo **thứ tự xuất hiện trong template** (trái→phải, trên→xuống) thay cho A→Z; thêm nút **"Clear ghép"** (xoá trắng biến đã ghép, có xác nhận).

### v1.8.0
- Màn Nguồn: thêm ô **"Số dòng đọc"** (mặc định 100, sửa được; cảnh báo nếu không hợp lệ).
- Màn Xem trước: x/**y** lấy tổng hồ sơ đã kiểm tra ở Bước 2; gợi ý tăng số dòng đọc khi previewable ít hơn tổng; hộp thoại **"Tạo file"** ra giữa màn hình 2 trạng thái (Tạo file → Mở thư mục).
- Màn Tạo mục lục: mỗi dòng log kèm **thời gian dd/MM/yyyy HH:mm:ss**; khi xong hiện **popup thống kê** (DOCX/PDF/lỗi/thời gian).

### v1.7.0
- Màn Xem trước: thêm nút **"Tạo file"** (popup: tùy chọn kèm PDF, hiện tên DOCX/PDF, lưu vào thư mục xuất) để xuất riêng hồ sơ đang xem.
- Cụm tiến/lùi chuyển sang cạnh zoom; ô tên hồ sơ thành **ô nhập nhảy nhanh** — gõ giá trị cột gom nhóm + Enter để đến hồ sơ.

### v1.6.1
- Thêm **icon ứng dụng** (nhúng vào MLHS.exe → Explorer/taskbar hiển thị icon).
- Màn Xem trước: chip biến hiển thị đúng `{ten_bien}`; ẩn khối "Biến tự động" khi template không dùng.

### v1.6.0
- Màn Tạo mục lục: nút chính thành **Tạm dừng (đỏ) / Chạy tiếp (xanh)** khi đang chạy (tạm dừng thật, giữ nguyên job), chỉ đổi thành **Chạy lại** khi xong hết.
- Nhật ký realtime: mỗi dòng có `[n/tổng]` và **tên file đầy đủ** kèm tên `.pdf` (đúng cả hậu tố chống trùng).
- Hàng tên file gộp: tiền tố + pill "Biến gom nhóm" + xem trước trên một hàng.
- Đổi tên file thực thi thành **MLHS.exe**.

### v1.5.0
- Sửa thanh tiến trình Màn 4: tổng (`y`) lấy đúng số hồ sơ đã Validation ở Màn 2.
- Sửa treo nhật ký realtime khi hàng nghìn hồ sơ (đa luồng): cập nhật UI theo lô qua timer, giới hạn 1000 dòng, gộp cuộn.
- Mặc định tắt "chạy đa luồng".
- Màn 2: bỏ dòng "sẽ tạo x file", đưa mô tả xuống dưới select box, và **bắt buộc Validation hợp lệ** mới bật nút "Tiếp theo".

### v1.4.0
- Tinh chỉnh giao diện: khối "Cột gom nhóm hồ sơ" dịu lại (bỏ viền đậm, chỉ nền), làm mảnh thanh màu phân loại biến; đổi nhãn "Mẫu template" (Màn 1), "Template" (Màn 3); tên hồ sơ ở Màn 3 gọn còn "giá trị gom nhóm (x/y)"; Màn 4 gộp tiền tố + xem trước tên trên cùng một hàng.

### v1.3.0
- **Đặt tên file** không còn phụ thuộc biến template: tên = **tiền tố (text) + giá trị cột gom nhóm**; cột gom nhóm rỗng → tiền tố + STT; pipeline tự thêm `_2, _3…` chống trùng (không ghi đè nhầm). Màn Tạo mục lục có ô tiền tố + xem trước tên thật.
- **Màn Ghép biến:** làm nổi bật khối "Cột gom nhóm hồ sơ" (viền accent, huy hiệu BẮT BUỘC, cảnh báo chọn đúng); bỏ nhãn loại trong từng dòng và thu gọn chiều cao dòng để bớt cuộn; đồng bộ cỡ tiêu đề 16 với các màn khác.

### v1.2.0
- **Select box (ComboBox) & ô nhập** vẽ lại phẳng, bo góc, viền accent khi focus/mở, chevron riêng, dropdown bo góc + đổ bóng.
- **Màn Ghép biến** thiết kế lại thành danh sách dòng "biến ← nguồn": chip biến + nhãn loại, thanh màu theo loại, combo cột/hằng (biến tự động có ghi chú riêng), và **nhãn trạng thái** (Cột/Hằng/Chưa gán/Tự động).
- **Màn Nguồn:** dropdown mẫu hiển thị **tên file template** (đổi tên file trong `templates/` là đổi theo, không cần sửa code).
- **Màn Tạo mục lục:** nhật ký realtime tự cuộn xuống dòng mới nhất.
- Thu nhỏ thanh Bước trên cùng ~20% ở mọi màn.

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
