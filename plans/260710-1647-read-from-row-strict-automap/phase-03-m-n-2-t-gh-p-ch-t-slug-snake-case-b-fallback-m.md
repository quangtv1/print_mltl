---
phase: 3
title: "Màn 2 tự-ghép chặt Slug snake_case (bỏ fallback mờ)"
status: code-done
effort: ""
dependencies: []
---

# Phase 3: Màn 2 tự-ghép chặt Slug snake_case

## Overview
Đổi luật tự-ghép biến↔cột ở Màn Ghép biến sang khớp chính xác theo dạng snake_case (giữ `_`), bỏ hẳn fallback mờ `.Contains` để tránh tự-ghép sai.

## Requirements
- Functional:
  - `TextUtil.Slug(s)`: về thường, bỏ dấu tiếng Việt (đ→d), thay **mỗi cụm ký tự không phải chữ/số** bằng một `_`, trim `_` đầu/cuối. VD "Ngày, tháng, năm sinh" → `ngay_thang_nam_sinh`.
  - Tự-ghép chỉ khi `Slug(tên_cột) == Slug(tên_biến)` (chính xác). Không trùng → để trống.
  - **Bỏ** nhánh fallback `.Contains`.
- Non-functional: giữ `TextUtil.Normalize` cho heuristic cột gom nhóm (không đổi); không chạm Core.

## Architecture
- **TextUtil.Slug** (mới, cạnh `Normalize`):
  ```csharp
  // Bỏ dấu + thường + cụm ký tự không chữ/số -> "_", trim "_". Dùng để so khớp cột<->biến theo snake_case.
  public static string Slug(string? s)
  {
      if (string.IsNullOrEmpty(s)) return "";
      var t = s.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD);
      var sb = new StringBuilder(t.Length);
      bool prevSep = false;
      foreach (var ch in t)
      {
          if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
          if (char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); prevSep = false; }
          else if (!prevSep && sb.Length > 0) { sb.Append('_'); prevSep = true; }
      }
      var r = sb.ToString();
      return r.EndsWith('_') ? r[..^1] : r;   // trim '_' cuối; đầu đã tránh nhờ sb.Length>0
  }
  ```
- **Step2MappingViewModel.BuildBindings.Add:** thay 2 dòng match:
  ```csharp
  var match = cols.FirstOrDefault(c => TextUtil.Slug(c) == TextUtil.Slug(v));
  if (match != null) row.Value = match;
  ```
  (bỏ dòng `?? cols.FirstOrDefault(... Contains ...)`).

## Related Code Files
- Modify: `src/MucLucHoSo.App/Shared/TextUtil.cs` (thêm `Slug`)
- Modify: `src/MucLucHoSo.App/ViewModels/Step2MappingViewModel.cs` (đổi luật match, bỏ Contains)

## Implementation Steps
1. Thêm `TextUtil.Slug` (giữ `Normalize` nguyên vẹn).
2. Sửa `BuildBindings.Add`: match bằng `Slug(c)==Slug(v)`, bỏ fallback `.Contains`.
3. Rà: cột gom nhóm mặc định (dùng `Normalize`.Contains("hoso")) không bị ảnh hưởng.

## Success Criteria
- [ ] Cột "Ngày, tháng, năm sinh" tự khớp biến `{ngay_thang_nam_sinh}`.
- [ ] Biến `{ngaythang}` KHÔNG tự khớp cột "Ngày tháng" (khác `_`) — đúng luật chặt.
- [ ] Không còn tự-ghép mờ kiểu chứa-một-phần.
- [ ] Cột gom nhóm mặc định vẫn hoạt động như cũ.
- [ ] Build Release trên Windows OK.

## Risk Assessment
- **Giảm tự-ghép ở template cũ** (biến không có `_`) → người dùng ghép tay; đã được chấp nhận khi brainstorm.
- **Slug rỗng** (cột/biến toàn ký tự đặc biệt) → trả "" ; tránh khớp nhầm "" == "" bằng điều kiện: chỉ set khi `Slug(v)` khác rỗng (thêm guard nếu cần).
- macOS không verify UI → test Windows; logic Slug thuần .NET có thể kiểm bằng tay/console.
