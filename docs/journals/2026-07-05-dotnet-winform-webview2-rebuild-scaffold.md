# .NET Pivot P1+P2: Bounding Scope to What macOS Can Actually Verify

**Date**: 2026-07-05 05:40
**Severity**: Normal
**Component**: `MucLucTaiLieu` solution scaffold + `MucLucTaiLieu.Core` (net8.0)
**Status**: Done (P1+P2 of 7)

## What Happened

Started executing the WinForms + WebView2 rebuild plan (`plans/260705-0504-winform-webview2-mota3-clone/`) via `/cook`. Deleted the entire PyQt5 codebase, scaffolded a 3-project .NET 8 solution, imported the `design_v3` prototype into `App/Web/`, extracted its seed datasets to JSON, and built the whole `Core` layer (Excel reader, models, name resolver, header match, config + seed stores) test-first. 42 xUnit tests green.

## The Decision That Shaped Everything

The dev machine is macOS. The plan's own risk table flagged it: **WinForms + WebView2 cannot build a runnable UI here, and `PrintToPdf` can't be exercised at all.** Phases 3–7 are Windows-only.

Rather than barrel through all 7 phases producing code I couldn't run, I stopped at the plan-review gate and put the constraint in front of the user with three concrete scope options. We agreed: **do P1+P2 only** — the cross-platform `net8.0` core that `dotnet test` genuinely verifies — and defer the UI phases to Windows/CI.

This is the direct descendant of yesterday's lesson (green tests validated a 110-dpi phantom while the 300-dpi PDF was broken). The correction this time wasn't a better test — it was refusing to claim "done" for anything I couldn't observe running. The App project compiles on macOS via `EnableWindowsTargeting=true`; compiling is not verifying, and I didn't let the build's success stand in for behavior it can't demonstrate.

## Technical Details

- **Toolchain**: only Homebrew .NET 9 was on PATH. Installed .NET 8 SDK to `~/.dotnet` (per user's choice to match CI's `net8.0` exactly rather than roll-forward). All commands use `$HOME/.dotnet/dotnet`.
- **Branch**: committed the untracked `design_v3` + plans on the old branch first (nothing lost), then cut `feat/winform-webview2` off `main` and pulled the prototype + plans back in as import source.
- **Seed extraction**: the prototype's `templates` and `datasets` are JS object literals inline in `design3.html`, containing `{token}` braces *inside string values*. A naïve brace matcher would miscount. Wrote a string-aware matcher (skips brace chars inside quoted strings) in Node, `eval`'d the literals, emitted `mau01–04.json` + `templates.json`. Faithful copy, no hand-transcription.
- **JSON mapping**: seed keys are snake_case (`so_ho_so`, `so_ky_hieu`). .NET 8's `JsonNamingPolicy.SnakeCaseLower` maps them straight onto PascalCase models — zero per-property attributes.
- **Testable fail-fast**: `ClosedXmlReader` takes injectable `maxFileBytes` / `maxCellChars`. Tests trigger the "file too large" / "cell too long" guards with a 10-byte limit instead of fabricating a 50 MB workbook.

## Lessons Learned

1. **Compiles ≠ verified.** `EnableWindowsTargeting` makes the Windows app build on macOS, which is convenient and misleading. A green build of a UI you can't launch proves the types resolve, nothing more. Name that boundary out loud instead of letting the checkmark imply more.
2. **Bound scope to the observability of the environment.** The honest unit of "done" is what the current machine can actually run. Splitting the plan along the cross-platform/Windows seam was the whole game.
3. **Extract data, don't retype it.** A 30-line string-aware parser beats transcribing four datasets of Vietnamese archival records by hand — and it stays faithful when the prototype changes.

## Next Steps

- P3 (Windows): the `PrintToPdf` spike-gate — prove one multi-page hồ sơ prints identical to the WebView2 preview *before* building Steps 1–6. If it can't, switch to Puppeteer immediately (plan's stated fallback).
- P3–P7 need a Windows box or CI runner; `Core` stays macOS-testable throughout.
- Stale `.github/workflows/build-windows.yml` still targets the deleted Python build — P7 owns replacing it.

---

**Status**: DONE
**Summary**: Rebuilt from PyQt5 to a .NET 8 solution; deleted all Python, imported the design_v3 prototype, built + tested the cross-platform Core (42 tests green). Key call: scoped `/cook` to P1+P2 because macOS can't verify the WinForms/WebView2 UI — refused to conflate "compiles" with "works," deferring P3–P7 to Windows.
