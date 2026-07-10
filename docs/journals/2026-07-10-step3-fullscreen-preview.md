# 2026-07-10 — Màn 3: xem ảnh render toàn màn hình

## Yêu cầu
Thêm icon "toàn màn hình" ngay sau nút zoom "+" ở thanh công cụ Bước 3. Bấm → mở ảnh render full màn hình, giữ nguyên tỉ lệ; Esc thoát; ←/→ (kèm hoặc không kèm Ctrl) chuyển ảnh trước/sau.

## Thiết kế (đã nêu rõ với người dùng)
Cửa sổ `FullscreenPreviewWindow` **không viền, maximized**, **dùng chung `Step3PreviewViewModel`** (`DataContext = Vm`) → điều hướng hồ sơ tự cập nhật `PreviewPages` nên ảnh full-screen theo. "Ảnh trước/sau" = **hồ sơ trước/sau** (dùng lại `Prev/NextCommand`), nhất quán với Ctrl+←/→ ở màn thường. Ảnh fit vùng xem bằng `Stretch=Uniform` + `MaxWidth/MaxHeight` bind theo kích thước ScrollViewer (giữ tỉ lệ); nhiều trang thì cuộn dọc.

## Thành phần
- Nút toolbar (glyph `` E740) sau nút "+", `IsEnabled={Binding HasPreview}`, `Click=OnFullscreenClick` → `new FullscreenPreviewWindow{ DataContext=Vm, Owner=... }.ShowDialog()`.
- `FullscreenPreviewWindow.xaml/.cs`: thanh trên (NavText + hướng dẫn + nút X); `OnKeyDown`: Esc→Close, Left→PrevCommand, Right→NextCommand.
- `SubtractConverter` (mới, đăng ký key `Subtract`): trừ lề khi fit ảnh để 1 trang vừa khít, không sinh thanh cuộn thừa.

## Review (code-reviewer) — 2 lỗi runtime đã sửa
- **High — đua render**: `RefreshAsync` không tuần tự hoá; giữ ←/→ (auto-repeat) làm ảnh lệch với `NavText`. Thêm **thẻ thế hệ** `_refreshGen`: chỉ áp kết quả của lần refresh mới nhất (bỏ kết quả cũ). Lợi cho cả màn thường.
- **Medium — fit tràn 40px**: lề (ItemsControl 0,12 + Border 0,8) làm 1 trang luôn có thanh cuộn. Dùng `Subtract` trừ 40 (dọc)/24 (ngang) khỏi ràng buộc kích thước → fit trọn.
Không sửa: nhãn header có thể chồng nếu NavText dài (NavText luôn ngắn "3/207"); nút X dùng chrome mặc định — thuần thẩm mỹ.

## Verify
App WPF `net8.0-windows` → không build/chạy trên macOS. Đã kiểm tra tĩnh: converter/binding/khóa tài nguyên khớp (grep), csproj SDK auto-include file XAML mới, code-review xác nhận wiring/threading/modal đúng (ShowDialog + await chạy trên nested loop, bitmap đã Freeze nên chia sẻ 2 cửa sổ an toàn). **Cần build/test Windows** để xác nhận fit + phím.

## File
`FullscreenPreviewWindow.xaml/.cs` (mới), `Step3PreviewView.xaml/.cs`, `Step3PreviewViewModel.cs` (_refreshGen), `Converters/Converters.cs` (+SubtractConverter), `App.xaml` (đăng ký).
