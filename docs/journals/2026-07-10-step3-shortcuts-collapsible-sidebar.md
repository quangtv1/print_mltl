# 2026-07-10 — Màn 3: phím tắt điều hướng + ẩn/hiện cột biến

## Yêu cầu
Ở màn Xem trước (Bước 3): Ctrl+→ hồ sơ sau, Ctrl+← hồ sơ trước; Ctrl+Enter = nút tiến ("Tiếp theo"), Shift+Enter = "Quay lại"; thêm icon góc trên phải cột "Biến của mẫu…" để ẩn/hiện, mở rộng vùng preview.

## Phát hiện khi scout
- Ctrl+←/→ (đổi hồ sơ) và Ctrl +/−/0 (zoom) **đã có sẵn** trong `Step3PreviewView.xaml.cs OnPreviewKeyDown`. Lý do người dùng thấy "thiếu": `PreviewKeyDown` chỉ chạy khi keyboard focus nằm trong view, mà sau khi bấm "Tiếp theo" để vào Bước 3 thì focus ở nút dưới chân (thuộc MainWindow) → phím tắt không tới.
- `StepViewModel.Wizard` là `protected` → code-behind không gọi được lệnh wizard trực tiếp.

## Thay đổi
1. **Focus**: `Step3PreviewView` đặt `Focusable="True"` (UserControl mặc định Focusable=false → `Focus()` vô hiệu); constructor gọi `FocusSelfDeferred()` (hoãn qua Dispatcher/Input) khi `Loaded` và khi `IsVisibleChanged`→visible → phím tắt chạy ngay không cần click.
2. **Ctrl/Shift+Enter**: mở rộng `OnPreviewKeyDown`; VM lộ cầu nối `public ICommand WizardNext => Wizard.NextCommand; WizardBack => Wizard.BackCommand;`. Enter không kèm Ctrl/Shift được bỏ qua (không set Handled) → ô "nhảy hồ sơ" vẫn Enter=Jump như cũ.
3. **Ẩn/hiện cột biến**: VM thêm `SidebarCollapsed`, `SidebarWidth` (GridLength 0/320), `SidebarToggleGlyph` (ChevronLeft/Right, Segoe MDL2), `ToggleSidebarCommand`. XAML: **cả hai** cột thứ 2 (toolbar + nội dung) bind `SidebarWidth`; nút icon nổi góc trên phải (`Grid.ColumnSpan=2`, right-align); tiêu đề + Border sidebar ẩn khi thu gọn; nút "Tạo file" chừa lề phải 44 để không đè icon khi thu gọn.

## Review (code-reviewer) — 2 lỗi High đã sửa
- Cột thứ 2 của **grid nội dung** ban đầu vẫn `Width=320` (chỉ grid toolbar được bind) → thu gọn để lại khoảng trắng 320px, preview không nở. Đã bind cả hai.
- `Focus()` trên UserControl vô hiệu vì `Focusable` mặc định false → đã đặt `Focusable="True"`.
Trade-off ghi nhận: khi con trỏ trong ô "nhảy hồ sơ", Ctrl+←/→ làm đổi hồ sơ thay vì nhảy chữ (chấp nhận, đúng phạm vi màn 3).

## Verify
App là WPF `net8.0-windows` → **không build/chạy được trên macOS**. Đã kiểm tra tĩnh: mọi binding/ân tham chiếu XAML↔VM↔code-behind khớp (grep), 2 lỗi runtime do review chỉ ra đã sửa. **Cần build + test trên Windows** để xác nhận focus và layout.

## File
`Step3PreviewViewModel.cs`, `Step3PreviewView.xaml`, `Step3PreviewView.xaml.cs`.
