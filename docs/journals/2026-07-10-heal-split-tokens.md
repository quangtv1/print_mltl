# 2026-07-10 — Fix: variables split across Word runs never filled at render

## Symptom
Template `Phôi bằng tốt nghiệp.docx` + data `THACSI.Q2020.xlsx` (header row 2): Step-2 showed 14/14 variables mapped, but the Step-3 preview left ~7 header variables empty — `{chuyen_nganh_dao_tao}`, `{ngay_thang_nam_sinh}`, `{so_quyet_dinh_thanh_lap_hoi_dong}`, `{dia_danh_noi_co_quan_cap_bang_dat_tru_so}`, `{ngay_thang_nam_cap_bang}`, `{so_vao_so_goc_cap_van_bang}`, `{ho_chu_dem_va_ten}`.

## Root cause (empirically confirmed)
`OpenXmlHelpers.ReplaceTokens` replaces `{var}` per-Run — a token is only matched when the whole `{name}` sits inside one `<w:t>`. Word splits long tokens across multiple runs with **different** RunProperties (e.g. one run carries an extra `<w:lang>`), and `CoalesceRuns` merges only **identical-rPr** runs, so the split survives and the placeholder is never replaced. `TemplateCompiler` detects tokens by concatenating run text, so they still appear as classified/mapped — hence "14/14 mapped" yet empty output. The 7 short tokens (fully in one run) rendered fine; the 7 long ones were exactly the split set = the user's report.

Not caused by the recent Slug/ReadStartRow work — strict matching just bound the long tokens, exposing a pre-existing render limitation.

## Fix
New `OpenXmlHelpers.HealSplitTokens(scope)`, called at the top of `ReplaceTokens` (covers body, headers, footers, and cloned table rows). Per paragraph, when a simple text run ends with an open token tail (`{` + token chars to end), it **looks ahead** across following simple text runs and merges them into the opening run **only if the token actually closes** with `}` forming a valid `{name}`. Non-text runs (image/break/field) are barriers. The opening run's formatting is kept for the whole token; surrounding runs (labels, other formatting) are untouched. Look-ahead is non-destructive, so a stray/literal `{` in template text or Excel data can never trigger a formatting merge.

## Verification
Core-only repro against the real template + data: without fix → 7 leftover `{tokens}`; with fix → 0, header values correctly filled (birthdate 12/12/1989, decision 15/QĐ-SĐH, place "Thành phố Hà Nội", etc.). `chuyen_nganh_dao_tao` stays empty because that student's cell is genuinely empty. Core compiles clean in Release on macOS (fix is pure OpenXML, no Word needed). Code-reviewed: loop-safe, idempotent under the row+body double pass, no regressions.

## Files
`src/MucLucHoSo.Core/Templating/OpenXmlHelpers.cs` (+`HealSplitTokens`, `SimpleText`, two regexes; one call site).
