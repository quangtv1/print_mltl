# MụcLụcTàiLiệu — WinForms + WebView2 app

Desktop app (.NET 8 WinForms) that generates archival index PDFs ("mục lục hồ sơ")
from an Excel file, reproducing the `design_v3` prototype. Architecture: **WinForms shell
+ WebView2 core** — the A4 sheet (editor + preview) is the prototype's HTML/CSS/JS hosted
in WebView2; PDFs are produced with `CoreWebView2.PrintToPdfAsync` so preview == PDF.

## Solution layout

| Project | TFM | Role |
|---|---|---|
| `MucLucTaiLieu.Core` | `net8.0` | Excel read (ClosedXML), models, name resolver, header match, config + seed stores, batch runner, Excel export. Cross-platform, fully unit-tested. |
| `MucLucTaiLieu.App` | `net8.0-windows` | WinForms wizard + WebView2 host + PrintToPdf renderer. `Web/` = prototype assets, `Assets/seed/` = 4 template datasets. |
| `MucLucTaiLieu.Tests` | `net8.0` | xUnit tests for Core. |

## Build & test

```bash
dotnet test MucLucTaiLieu.sln            # Core tests (runs anywhere with .NET 8)
dotnet build MucLucTaiLieu.sln           # whole solution
```

**macOS/Linux dev:** the App targets `net8.0-windows` and builds via
`EnableWindowsTargeting=true`, but WinForms + WebView2 only **run** on Windows. Behavior
(UI, PrintToPdf fidelity) must be verified on Windows. CI (`.github/workflows/build-winform.yml`,
`windows-latest`) runs the Core tests and publishes the app.

## Publish (Windows)

```bash
dotnet publish MucLucTaiLieu.App -c Release -r win-x64 --self-contained true -o publish
```

Portable self-contained build (no .NET install needed on target). `Web/` and `Assets/`
are copied next to the executable automatically.

## WebView2 runtime

Distribution = **Evergreen + bootstrapper**: the WebView2 runtime ships with Windows 10/11
in most cases; if missing, install the Evergreen runtime. A Fixed-Version bundle (~120 MB)
is only needed for fully offline environments.

## Known open items

- **Prototype bootstrap (blocker for the editor/preview):** the vendored `Web/index.html`
  is a DesignCanvas (React) component compiled by `support.js`, which requires
  `window.React` / `window.ReactDOM` / `window.Babel` plus a mount step. Those are **not**
  in the imported files. Before the WebView2 editor/preview (phases 5) and the PrintToPdf
  renderer produce real output, this bootstrap must be added (bundle React/ReactDOM/Babel
  locally + mount the component, exposing it as `window.__mltlComponent` for `bridge.js`),
  or the page reworked to a standalone render path.
- **PrintToPdf spike gate (phase 3):** confirm one multi-page hồ sơ prints identical to the
  preview before building further UI; fall back to Puppeteer if margins/scale can't be tuned.

## Differences vs the prototype

- Data comes from Excel (ClosedXML) + a C# bridge instead of the prototype's inline mock
  datasets. Seed datasets are preserved in `App/Assets/seed/*.json` for preview.
- Config persists to `%AppData%\MLTL\config.json` (namespaced per app path).
