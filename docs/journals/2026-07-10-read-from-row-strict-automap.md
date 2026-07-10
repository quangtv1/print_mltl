# 2026-07-10 — "Đọc từ" header row + strict snake_case auto-map

Commit `8bd770e` · plan `plans/260710-1647-read-from-row-strict-automap/`

## What shipped
Two independent features:

1. **Start/header row ("Đọc từ")** — readers now take `headerRow` and locate the header by counting only NON-empty rows (blank rows above/between are skipped), so `SessionState.ReadStartRow` flows uniformly through preview (`ReadHead`), `Validate`, and generate (`ReaderFactoryFor`). Step1 gained a "Đọc từ" box, moved the read button to its own row, added a green "đã đọc" label, and auto-rereads with a 400ms debounce when the row inputs change.
2. **Strict auto-map** — new `TextUtil.Slug` (snake_case, diacritics stripped); Step2 matches column↔variable only on exact `Slug` equality and drops the old fuzzy `.Contains` fallback.

## Decisions / notes
- `headerRow=1` on a file with no leading blanks is intentionally byte-identical to the old behavior — the only new case is skipping leading/interleaved blank rows (previously a blank first row could become the "header").
- Empty-Slug guard prevents `""==""` false matches; grouping heuristic (`Normalize.Contains("hoso")`) left untouched.
- Reader "empty row" rule (all cells trim to empty) is shared between the header-skip helpers and `ReadRows` so header/data stay consistent.

## Review findings fixed
Code-reviewer flagged the debounce logic:
- **High** — `_reloadCts.Cancel()` sat *after* the invalid-value early-return, so clearing a field mid-typing left a stale valid read scheduled that later popped a modal (violates the no-modal-while-typing spec). Moved the cancel above validation.
- **Medium** — overlapping reads couldn't be cancelled once past the delay. Extracted `DoReadAsync(token)`; results are dropped and `Busy` left alone when the token is cancelled. Manual button also cancels any pending debounce.
- **Low** (CTS not disposed) — accepted as-is; no `WaitHandle` accessed, safe disposal would add `ObjectDisposedException` risk.

## Verification gap
Core builds Release clean on macOS (0/0). The App is WPF `net8.0-windows` and **cannot build/run on this macOS box** — Step1/Step2 UI + VM behavior must be built and tested on Windows against each phase's success criteria (esp. blank-row skipping, debounce-no-modal, strict Slug matching).
