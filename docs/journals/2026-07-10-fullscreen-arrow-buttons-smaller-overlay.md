# 2026-07-10 — Nút mũi tên bấm được ở màn full + thu nhỏ icon overlay

## Yêu cầu
1. Icon toàn màn hình (overlay góc trên phải màn Xem trước): **nhỏ đi 40%** + **trong suốt hơn**.
2. Trong chế độ toàn màn hình: bấm chữ **←** = lùi hồ sơ, bấm **→** = sang hồ sơ phải — **song song** phím tắt ←/→ sẵn có. Nút để trong suốt, **đậm hơn khi rê chuột**.

## Thay đổi
### Icon overlay (`Step3PreviewView.xaml`)
- 40×40 → **24×24** (giảm 40%), glyph E740 font 18 → **11**, `Opacity` 0.35 → **0.2**, `CornerRadius` 6 → 4. Glyph vẫn căn giữa qua `ContentPresenter`.

### Cửa sổ toàn màn hình (`FullscreenPreviewWindow.xaml` + `.xaml.cs`)
- Thêm `Window.Resources` style `FsArrow`: nền trong suốt luôn, `Foreground` xám nhạt, trigger `IsMouseOver` → trắng + `FontWeight=Bold`.
- Thay dòng gợi ý tĩnh bằng `StackPanel` giữa thanh trên: nút `←` (U+2190) + chữ "Esc để thoát · bấm mũi tên đổi hồ sơ" + nút `→` (U+2192).
- Code-behind: `OnPrevClick`/`OnNextClick` gọi `Exec(Vm.PrevCommand)`/`Exec(Vm.NextCommand)` — **dùng chung lệnh** với `OnKeyDown`, null-safe + `CanExecute`-guard (bấm ← ở hồ sơ đầu / → ở hồ sơ cuối tự vô hiệu).

## Review (code-reviewer): sạch — không lỗi Critical/High/Medium
- Xác nhận: 40→24 = giảm đúng 40%; click tái dùng đúng lệnh của phím tắt; z-order/hit-test đúng (NavText không nền nên không cướp click); char refs hợp lệ; `FsArrow` khai báo trước khi dùng.
- Cosmetic (chấp nhận): hover Bold có thể nhích chữ gợi ý 1–2px — đúng bản chất "bolder khi hover".

## Verify
App WPF `net8.0-windows` không build trên macOS → kiểm tĩnh + code-review. Test tay Windows: icon overlay nhỏ/mờ hơn + hover rõ; trong full bấm ←/→ đổi hồ sơ (kèm hover đậm), phím tắt ←/→/Esc vẫn chạy.

## File
`Step3PreviewView.xaml`, `FullscreenPreviewWindow.xaml/.xaml.cs`, `MucLucHoSo.App.csproj` (1.12.5→1.12.6).
