# QTextDocument DPI Bug: Green Tests Didn't Verify Actual Output

**Date**: 2026-07-04 16:33
**Severity**: Critical
**Component**: Qt PDF renderer (`app/core/qt_pdf_renderer.py`), layout engine
**Status**: Resolved

## What Happened

Completed a full 6-phase rebuild of the archival-index app: swapped docx+Google Sheets for a native Qt engine (QTextDocument → QPdfWriter) with a 3-step wizard, Excel input, PDF+Excel output. All phase tests passed. Code review caught a showstopper bug in the layout logic that my tests had silently missed.

## The Brutal Truth

Shipped a beta with green tests that didn't actually validate the exported PDF. My test harness was measuring the wrong code path: I verified pagination at 110 dpi (preview resolution) but the actual PDF exports at 300 dpi. The user-facing output was physically broken — ~180 rows crammed onto a single A4 page with ~4pt text — while my smoke tests showed clean pagination on the preview. That's a confidence failure, not a code failure, but the damage is the same: the core artifact was wrong.

## Technical Details

QTextDocument layout engine works in logical points (96 dpi metric) with point-size fonts (e.g., 11pt × 96 dpi ≈ 14.67 pixels). My code built the page box in *device pixels* at the target output DPI:

```python
# WRONG: page_box sized in 300-dpi device pixels
page_height_pixels = 11.69 * dpi  # A4 height in inches × 300
doc.setPageSize(QSizeF(width_pixels, page_height_pixels))
```

At 300 dpi, that page box was ~3.5× the size of the layout engine's internal metric, so text meant to fit one page got crushed into a corner and the footer bled off-screen. At 110 dpi (preview), the ratio was closer, so the preview lied about the actual output.

PyMuPDF inspection confirmed: exported PDF had 1 page with illegible text instead of the expected 3 pages. Preview page_count returned 3.

## What We Tried

- Smoke testing (screenshot + visual check) — passed at preview, didn't catch the 300-dpi delta.
- Pagination tests via `page_count()` — measured preview layout, not exported PDF.
- Unit tests for row-multiplication and header logic — all passed; they didn't exercise DPI scaling.

All paths were green but measured phantom behavior.

## Root Cause Analysis

Conflated two DPI regimes:
1. **Layout DPI**: QTextDocument internally assumes ~96 dpi when rendering point-sized fonts.
2. **Output DPI**: The target device (PDF writer, printer) may use a different DPI.

I sized the page container in output DPI device pixels but fed point-sized fonts and layout math scaled to 96 dpi. The mismatch was silent and invisible until the PDF was opened.

Code reviewer (subagent) caught this by reading the scaling logic and asking "what resolution is the actual PDF?" — immediately exposing the preview/export delta.

## Lessons Learned

1. **Green tests can validate a phantom path.** Test the actual artifact. For PDF, open the file or parse it with a real PDF reader (PyMuPDF here). Screenshots and preview-path metrics are not proxies for export fidelity.

2. **QTextDocument print fidelity requires resolution-agnostic layout + painter scaling.** Lay out once at a fixed logical DPI (96), then scale only the QPainter's coordinate system when drawing to the output device. Never mix device-pixel page sizing with point-size layout.

3. **Code review earned its keep.** This bug was invisible to functional tests and hard to spot by inspection (the code *looked* reasonable). A second set of eyes asking "what actually gets exported?" was the kill shot.

4. **Document the DPI assumptions.** For a print-fidelity app, DPI is not incidental. Added comments in the renderer: "QTextDocument layout assumes 96 dpi; painter scale handles output DPI."

## Next Steps

- Verification: PyMuPDF + fitz now verifies page count on the *actual exported PDF*, not the preview proxy. Test passes: preview page_count == PDF page count at 150 and 300 dpi.
- Fixed M1 (mid-run shared-state race — style snapshot + nav lock), M2 (repeat multi-row headers), L1 (no quotes on numeric strings in injection guard).
- Beta ready after PR #1 merge (commit 0023c4b already pushed to feat/native-qt-pdf-designer).

---

**Status**: DONE
**Summary**: QTextDocument DPI scaling bug silently broke page layout in export (not preview); code review caught it where functional tests and screenshots lied. Fixed by decoupling layout DPI (96) from output DPI (painter scale only); verified with PDF introspection.
