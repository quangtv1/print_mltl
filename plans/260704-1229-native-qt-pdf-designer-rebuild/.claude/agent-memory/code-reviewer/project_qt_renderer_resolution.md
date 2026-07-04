---
name: project-qt-renderer-resolution
description: qt_pdf_renderer lays out font point-sizes at a fixed metric DPI while the page box scales with the `resolution` arg, so physical text size and pagination differ between preview and PDF
metadata:
  type: project
---

In `app/core/qt_pdf_renderer.py`, `build_document(..., resolution=)` sizes the page box via
`_a4_body_size_px(resolution)` (device px at that DPI), but `QTextDocument` lays out font
**point** sizes against a fixed internal metric DPI (~96). Net effect: `documentLayout().documentSize()`
height is roughly constant regardless of `resolution`, so physical content size = docSize/resolution.

Measured: identical 10-row doc = 5.01 in tall at resolution=110 vs 1.77 in at resolution=300.
Preview uses ~110 dpi, PDF export uses 300 dpi → PDF text renders ~2.7x too small and crams many
more rows per page; preview pagination and footer "Trang x/y" totals disagree with the actual PDF.
No data is lost (all rows render), but output is not WYSIWYG and physical sizing on A4 is wrong.

**Why:** point sizes are physical units and must be resolution-independent; only the page box scales.
**How to apply:** when reviewing/rendering changes here, verify physical text size and page count are
identical across resolutions (assert docSize.height/resolution is constant). A correct fix sets the
document's layout paint device / DPI to the target resolution, or keeps one resolution for both paths.
Do not trust `page_count()` (default 110 dpi) as evidence of the exported PDF's page count.
