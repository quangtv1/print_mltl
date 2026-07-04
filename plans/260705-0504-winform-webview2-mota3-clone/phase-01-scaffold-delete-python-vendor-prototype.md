---
phase: 1
title: "Scaffold Delete-Python & Vendor Prototype"
status: done
priority: P1
dependencies: []
---

# Phase 1: Scaffold Delete-Python & Vendor Prototype

## Overview
Dựng nhánh + solution .NET 8 (`MucLucTaiLieu.sln` gốc repo) theo mota3 §12, **xoá toàn bộ code Python**
bản cũ, và **vendor prototype** `design_v3` (design3 HTML/CSS + support.js + template/) vào `App/Web/` để
WebView2 (P3) tái dùng. Kết thúc: solution build sạch, `dotnet test` chạy (rỗng), repo không còn `.py`.

## Requirements
- Functional: `dotnet build` sạch; nhánh `feat/winform-webview2`; assets prototype nằm trong `App/Web/`.
- Non-functional: Core `net8.0` (đa nền để test trên macOS); App `net8.0-windows` (`EnableWindowsTargeting=true`).

## Architecture
```
(gốc repo)
MucLucTaiLieu.sln
├─ MucLucTaiLieu.App/        net8.0-windows  <UseWindowsForms>  EnableWindowsTargeting=true
│  ├─ Web/                   ← vendor: design3.html→index.html, support.js, template/*, styles.css, editor/engine JS
│  ├─ Assets/                ← icon, seed JSON 4 mẫu (trích từ prototype datasets)
│  └─ (Forms/… ở P3–P6)
├─ MucLucTaiLieu.Core/       net8.0 (Models/Excel/Templating/Pdf/Config — điền ở P2/P3)
└─ MucLucTaiLieu.Tests/      net8.0 xUnit
```
- **Xoá Python** (git rm): `app/`, `main.py`, `requirements.txt`, `build_default_template.py`, `packaging/`,
  `styles/` (python-era), `print_mltl_parallel.py` nếu còn, `docs/deployment-windows.md` (python). Giữ `plans/`,
  `docs/journals/`, `design*/` reference. (Python vẫn ở git history + PR #1 → khôi phục được.)
- **Vendor prototype:** copy `design_v3/design3.html` → `App/Web/index.html`, `support.js`, `template/` nguyên
  trạng; tách seed datasets (mau01–04 records trong prototype JS) → `App/Assets/seed/*.json`.
- NuGet: `Microsoft.Web.WebView2`, `ClosedXML` (Core).

## Related Code Files
- Create: `MucLucTaiLieu.sln`, 3 `.csproj`, `App/Web/*` (vendored), `App/Assets/seed/*.json`
- Delete: toàn bộ Python (liệt kê trên)
- Reference: `design_v3/mota3.html` §1,§12 (kiến trúc/cấu trúc), `design_v3/design3.html`+`support.js` (nguồn vendor)

## Implementation Steps
1. `git checkout -b feat/winform-webview2` (off `main`); commit mốc hiện tại trước khi xoá.
2. `git rm -r` toàn bộ Python (app/, main.py, requirements.txt, packaging/, styles/, build_default_template.py…).
3. `dotnet new sln`; `classlib` Core (`net8.0`); `winforms` App (`net8.0-windows`, +EnableWindowsTargeting); `xunit` Tests. Add refs.
4. Add NuGet (WebView2 vào App, ClosedXML vào Core).
5. Vendor `design_v3` → `App/Web/`; tách seed JSON 4 mẫu → `App/Assets/seed/`.
6. `dotnet build` sạch; commit.

## Success Criteria
- [ ] Nhánh `feat/winform-webview2`; repo **không còn file `.py`** (`git ls-files | grep .py` rỗng).
- [ ] `dotnet build MucLucTaiLieu.sln` sạch; `dotnet test` chạy (rỗng ok) trên macOS.
- [ ] `App/Web/` chứa index.html + support.js + template/; `App/Assets/seed/` có 4 JSON mẫu.
- [ ] Core **không** ref WinForms.

## Risk Assessment
- Xoá Python khi working tree đang có thay đổi → commit/stash trước; xác nhận `git status` sạch sau xoá.
- Vendor JS lớn (support.js ~1700 dòng) — giữ nguyên trạng, không refactor ở phase này.
- macOS build App `net8.0-windows`: cần `EnableWindowsTargeting=true` (build được, không chạy UI).
