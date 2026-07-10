# 2026-07-10 — Phím tắt Tiến/Lùi toàn cục + nút full màn hình dạng overlay

## Yêu cầu
1. Chuyển nút toàn màn hình khỏi toolbar → **nổi ở góc trên phải vùng preview**, hơi trong suốt, rõ khi rê chuột, click → full; thêm phím tắt **Ctrl+F**.
2. Ctrl+Enter = nút "Tiến lên", Shift+Enter = "Quay lại" hoạt động ở **tất cả các màn**.

## Thay đổi
### Phím tắt điều hướng toàn cục (mọi màn)
- `WizardViewModel`: thêm `[RelayCommand] Forward()` — bước cuối chạy `CurrentStep.PrimaryCommand` (Tạo mục lục/Chạy lại/Tạm dừng), bước khác chạy `NextCommand`, chỉ khi nút bấm được.
- `MainWindow.xaml` `Window.InputBindings`: thêm `Ctrl+Enter → ForwardCommand`, `Shift+Enter → BackCommand` (cạnh Alt ←/→ cũ). Vì ở cấp Window nên áp cho cả 4 bước.
- Gỡ xử lý Enter cục bộ ở `Step3PreviewView.OnPreviewKeyDown` + gỡ cầu nối `WizardNext/WizardBack` (và `using System.Windows.Input`) khỏi Step3 VM — nay dùng chung đường toàn cục (DRY).

### Nút full màn hình dạng overlay
- `Step3PreviewView.xaml`: bỏ nút full khỏi toolbar; thêm `Button` nổi trong ô preview (góc trên phải, `Margin=0,12,12,0`, `ZIndex=20`, 40×40), `Visibility` theo `HasPreview`. Style: `Opacity=0.35` mặc định, ControlTemplate = Border bo góc nền `#DD303030`, trigger `IsMouseOver → Opacity=1`; glyph E740 trắng.
- `Step3PreviewView.xaml.cs`: `OpenFullscreen()` (guard `HasPreview`) dùng chung cho `Click` và **Ctrl+F** (thêm vào nhánh Ctrl của `OnPreviewKeyDown`).

## Review (code-reviewer): Status DONE — không lỗi critical/high/medium
- Xác nhận: KeyBinding cấp Window nổ ở mọi bước (giống Alt ←/→); không xung đột (không có TextBox `AcceptsReturn`, không nút `IsDefault`); gỡ cầu nối sạch (0 tham chiếu treo); `Forward` route đúng nút hiển thị; overlay hit-test/hover đúng.
- Ghi chú thiết kế (chấp nhận): Ctrl+Enter ở Bước 4 lúc đang chạy = Tạm dừng/Chạy tiếp (đúng "nút chính" đang hiển thị); Shift/Ctrl+Enter khi đang gõ ô nhảy-hồ-sơ sẽ điều hướng; góc 40×40 dưới nút overlay không cuộn được (không đáng kể).

## Verify
App WPF `net8.0-windows` → không build trên macOS. Kiểm tĩnh + code-review + build Windows CI khi release. Cần test tay Windows: Ctrl+Enter/Shift+Enter ở cả 4 bước; overlay mờ/hover/click + Ctrl+F.

## File
`WizardViewModel.cs`, `MainWindow.xaml`, `Step3PreviewView.xaml/.cs`, `Step3PreviewViewModel.cs`.
