---
phase: 7
title: "Packaging CI & Acceptance"
status: pending
priority: P2
dependencies: [4, 5, 6]
---

# Phase 7: Packaging CI & Acceptance

## Overview
Đóng gói app (WebView2 distribution + portable/MSI), dựng CI `windows-latest` (test Core + build App +
publish), và **nghiệm thu theo checklist mota3 §13** (giống 100%). Cập nhật docs.

## Requirements
- Functional: publish chạy sạch trên Win10/11; CI xanh; đủ checklist §13.
- Non-functional: WebView2 runtime sẵn sàng trên máy đích; `Web/`+`Assets/` đi kèm app.

## Architecture
- **WebView2 distribution = Evergreen + bootstrapper** (đã chốt, Validation Session 1): kiểm tra/nhắc cài runtime
  nếu thiếu (đa số Win10/11 có sẵn). Fixed-Version (~120MB) chỉ dùng nếu môi trường **bắt buộc offline hoàn toàn**.
- **Publish:** `dotnet publish MucLucTaiLieu.App -c Release -r win-x64` (self-contained hoặc framework-dependent);
  copy `Web/` + `Assets/` (CopyToOutputDirectory). Mặc định **portable**; MSI (WiX/Velopack) là tùy chọn.
- **CI** `.github/workflows/build-winform.yml` (windows-latest): `dotnet test` (Core, đa nền cũng chạy được) →
  `dotnet build` App (`EnableWindowsTargeting`) → `dotnet publish` → upload artifact. UI/PDF verify tay theo checklist.
- **Acceptance:** đối chiếu từng mục **mota3 §13**; xuất vài PDF thật, so trực quan preview==PDF.
- Docs: `docs/winform-webview2-app.md` (chạy/build/WebView2 runtime/distribution/khác biệt vs prototype); cập nhật README.

## Related Code Files
- Create: `.github/workflows/build-winform.yml`, `docs/winform-webview2-app.md`
- Modify: `MucLucTaiLieu.App.csproj` (publish + copy Web/Assets), `README.md`

## Implementation Steps
1. csproj: copy `Web/`+`Assets/` ra output; cấu hình publish win-x64.
2. Workflow CI (test + build + publish + artifact).
3. Publish + chạy trên Windows: đi hết 3 bước với Excel thật → xuất PDF; kiểm WebView2 runtime.
4. **Nghiệm thu checklist mota3 §13** (từng mục); ghi kết quả.
5. Chốt distribution (Evergreen vs Fixed-Version) + đóng gói (portable vs MSI); cập nhật README/docs.

## Success Criteria
- [ ] `dotnet publish` chạy sạch trên Win10/11; `Web/`+`Assets/` đi kèm; WebView2 runtime OK (hoặc bundle).
- [ ] CI `windows-latest` xanh (test Core + build/publish App + artifact).
- [ ] **Đủ checklist mota3 §13** (wizard/4 mẫu/editor/toolbar/preview/run/màu/PDF==preview).
- [ ] Repo không còn Python; README + `docs/winform-webview2-app.md` cập nhật.

## Risk Assessment
- WebView2 Evergreen thiếu mạng lần đầu → Fixed-Version bundle (kích thước lớn hơn) — nêu rõ trade-off ở docs.
- Dev macOS **không** publish/chạy WebView2 → publish + nghiệm thu §13 **bắt buộc trên Windows** (CI/máy thật).
- exe self-contained + WebView2 fixed → dung lượng lớn; chấp nhận đổi lấy "chạy không cần cài".
- Chưa ký Authenticode → SmartScreen/AV có thể chặn ở cơ quan; cân nhắc ký (ghi ở docs).
