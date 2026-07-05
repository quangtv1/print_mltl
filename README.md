# MụcLụcTàiLiệu

Desktop app (.NET 8 WinForms + WebView2) tạo **mục lục hồ sơ** dạng PDF từ file Excel,
tái tạo prototype `design_v3`. Vỏ WinForms + lõi WebView2: tờ A4 (soạn thảo + xem trước) là
HTML/CSS/JS của prototype nhúng trong WebView2; xuất PDF bằng `CoreWebView2.PrintToPdfAsync`
→ xem trước trùng khớp PDF.

## Bắt đầu

```bash
dotnet test MucLucTaiLieu.sln     # test Core (chạy đa nền)
dotnet build MucLucTaiLieu.sln    # build cả solution
```

App chạy trên **Windows** (WinForms + WebView2). Xem chi tiết build/publish/runtime và các
hạng mục còn mở tại [`docs/winform-webview2-app.md`](docs/winform-webview2-app.md).

Kế hoạch: [`plans/260705-0504-winform-webview2-mota3-clone/plan.md`](plans/260705-0504-winform-webview2-mota3-clone/plan.md).
