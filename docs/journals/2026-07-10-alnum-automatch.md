# 2026-07-10 — Đổi luật ghép tự động: khớp theo dạng chỉ chữ+số

## Yêu cầu
Ghép tự động biến template ↔ cột Excel bằng cách đưa **cả** tên cột và tên token về: bỏ dấu → chữ thường → bỏ hết ký tự đặc biệt + khoảng trắng, **chỉ giữ chữ và số**, rồi so bằng nhau. Trùng → tự khớp nhồi dữ liệu; không trùng → chọn tay.

## Thay đổi
`Step2MappingViewModel.BuildBindings`: luật khớp đổi từ `Slug` (giữ `_` giữa các cụm) sang `TextUtil.Normalize` (chỉ chữ+số, bỏ mọi phân tách):
```csharp
var nv = TextUtil.Normalize(v);
var match = nv.Length == 0 ? null : cols.FirstOrDefault(c => TextUtil.Normalize(c) == nv);
if (match != null) row.Value = match;
```
`TextUtil.Slug` gỡ bỏ (chỉ dùng cho luật cũ, nay là dead code). `Normalize` đã tồn tại sẵn (dùng cho heuristic cột gom nhóm), không đổi.

## Khác biệt so với luật cũ (Slug)
Bỏ luôn dấu phân tách nên khớp **lỏng hơn ở phần ngăn cách**: `{ngaythang}`, `{NgayThangNamSinh}`, `{sohieubang}` giờ tự khớp "Ngày tháng"/"Ngày, tháng, năm sinh"/"Số hiệu bằng" (trước không). Vẫn là **so bằng tuyệt đối** trên dạng chuẩn hoá, **không có** fallback mờ `.Contains`.

## Kiểm chứng (thực nghiệm trên header + template thật)
- 35 header của `THACSI.Q2020.xlsx`: **không cột nào trùng dạng chuẩn hoá** → không có nguy cơ khớp nhầm trên file này.
- 14 token của `Phôi bằng tốt nghiệp.docx`: kết quả khớp **y hệt** luật Slug (0 token đổi cột) → không hồi quy; token đã tự khớp trước vẫn khớp đúng cột đó.
- Demo tổng hợp: token bỏ dấu ngăn cách nay khớp đúng; token không có cột tương ứng vẫn để trống (chọn tay).

## Đánh đổi (đã chấp nhận theo yêu cầu)
Khớp lỏng hơn → tăng khả năng đụng độ nếu hai cột khác nhau cùng rút gọn về một chuỗi chữ+số (vd "Số hiệu" vs "Số, hiệu"). Không xảy ra với bộ header hiện tại; là lựa chọn có chủ đích của người dùng.

## File
`src/MucLucHoSo.App/ViewModels/Step2MappingViewModel.cs` (đổi luật), `src/MucLucHoSo.App/Shared/TextUtil.cs` (gỡ `Slug`). App là WPF `net8.0-windows` → cần build/test trên Windows; luật khớp thuần .NET đã kiểm bằng thực nghiệm trên macOS.
