---
phase: 4
title: "Batch Generator & Excel Export"
status: pending
priority: P1
dependencies: [3]
---

# Phase 4: Batch Generator & Excel Export

## Overview
`BatchGenerator` sinh **N PDF song song** (QuestPDF thread-safe) qua `Renderer` (P3), progress + log
realtime + tùy chọn ghi đè/skip; tên file chốt **tuần tự** (chống trùng) trước khi dispatch. `ExcelExporter`
(ClosedXML) ghi Excel tổng hợp có chống formula-injection. Đây là lợi thế tốc độ so với bản Python serial.

## Requirements
- Functional: `RunAsync(style, template, groups, outDir, options, IProgress<BatchProgress>, CancellationToken)
  -> BatchSummary`; lỗi 1 hồ sơ không dừng cả mẻ; Excel tổng hợp tùy chọn.
- Non-functional: song song (`Parallel.ForEachAsync`/`MaxDegreeOfParallelism`); UI không treo; hủy được.

## Architecture
`MucLucHoSo.Core/Batch/`:
- `BatchOptions.cs`: `Overwrite`, `ExportExcel`, `FilenamePattern`, `MaxWorkers` (mặc định = CPU count).
- `BatchSummary.cs`: Total, Succeeded, Skipped, Failed, Errors[(hồ sơ,msg)], OutDir, ExcelPath, ElapsedSec.
- `BatchProgress.cs`: Done, Total, LogLine.
- `BatchGenerator.cs`:
  - **BuildTasks (tuần tự, main thread):** với mỗi group theo thứ tự → docFields, records, sttFile=index,
    `outPath = MakeUniquePath(outDir, FileName.Format(pattern, ctx), used)` — **chốt tên duy nhất trước**
    (chống ghi đè im lặng, kể cả trùng ho_so_so). Runnable chỉ render.
  - **Render song song:** `Parallel.ForEachAsync(tasks, opts{MaxDegreeOfParallelism})`:
    - nếu `outPath` tồn tại và !Overwrite → skip + log "bỏ qua".
    - else `Renderer.ToPdf(...)`; try/catch cô lập lỗi từng hồ sơ; report progress+log (thread-safe: dùng
      `Interlocked`/`lock` cộng dồn, `IProgress` marshal về UI thread).
  - Sau batch: nếu ExportExcel → `ExcelExporter.Export(successGroups, xlsxPath, style)`.
- `ExcelExporter.cs` (ClosedXML): mỗi hồ sơ 2 dòng tiêu đề + header bảng + data; cột lấy từ `style.rowMapping`;
  **SanitizeCell**: ô chuỗi mở đầu `= + - @` **và không phải số** → prefix `'` (chống formula injection).

## Related Code Files
- Create: `MucLucHoSo.Core/Batch/{BatchGenerator,BatchOptions,BatchSummary,BatchProgress}.cs`,
  `MucLucHoSo.Core/Excel/ExcelExporter.cs`
- Reference: bản Python `batch_generator.py` (dedup tuần tự, cô lập lỗi), `excel_exporter.py` (layout + sanitize)

## Tests (viết trước — TDD)
- `BatchGeneratorTests`:
  - 3 group (2 trùng ho_so_so) → **3 file phân biệt** (HS-001, HS-001_2, HS-002).
  - Overwrite=false chạy lại → skip hết; Overwrite=true → ghi lại.
  - 1 group lỗi (template ném) → Failed=1, batch tiếp tục, Summary đếm đúng.
  - **Song song thật:** N=50 group với MaxWorkers>1 → tất cả file tạo, không lỗi; đo nhanh hơn MaxWorkers=1.
  - progress tới (N,N); log có N dòng.
- `ExcelExporterTests`: ghi file → đọc lại (ClosedXML) đúng số dòng; ô `=1+1` → `'=1+1`; số âm `-5` **không** bị bọc.

## Implementation Steps
1. Viết `BatchGeneratorTests` dedup + skip/overwrite (RED).
2. `BuildTasks` tuần tự + `MakeUniquePath`; `Parallel.ForEachAsync` render; cô lập lỗi; progress/log.
3. `ExcelExporter` + `SanitizeCell` + test injection/số âm.
4. Test song song (50 group) + đo thời gian song song vs serial (chống hồi quy tốc độ).

## Success Criteria
- [ ] N group (kể cả trùng ho_so_so) → **N file phân biệt**; ghi đè/skip đúng.
- [ ] Lỗi 1 hồ sơ → log + batch tiếp tục; Summary đếm đúng.
- [ ] Mặc định **serial** (MaxWorkers=1) đúng; đa luồng là opt-in **chỉ sau khi** P3 chứng minh thread-safe.
- [ ] Excel tổng hợp đúng; formula-injection bị chặn; số âm giữ nguyên.
- [ ] progress/log realtime; cancel được.

## Red Team Fixes (áp dụng 2026-07-04)
- **#1/#11 — SERIAL-FIRST:** `MaxWorkers` **mặc định = 1** (render tuần tự, khớp bản Python). Đa luồng
  (`Parallel.ForEachAsync`) **chỉ bật khi P3 đã chứng minh** QuestPDF thread-safe (test so nội dung serial vs
  song song). "Nhanh hơn serial ≥2×" **bỏ khỏi acceptance** — hạ thành tối ưu hậu-parity có đo riêng.
  Checkbox "đa luồng" ở UI (P6) **disabled** tới khi có bản kiểm chứng.
- **#2 — ghi atomic + cancel đúng:** dùng `Renderer.ToPdf` (đã ghi tmp+rename ở P3). `BuildTasks` chốt tên
  duy nhất tuần tự (giữ). Kiểm `ct.IsCancellationRequested` **trước mỗi** render (dừng dispatch, không cắt
  file đang ghi). Cancel/lỗi → không để file dở ở tên cuối.
- **#6 — chữ ký thật:** `RunAsync(StyleConfig, IndexTemplate, IReadOnlyList<DocGroup>, outDir, options,
  IProgress<BatchProgress>, ct) -> BatchSummary`. Gán `stt_file` (1-based) ở **BuildTasks** (main thread).
  P6 gọi đúng chữ ký này.
- **#16 — formula-injection chặt hơn:** `SanitizeCell` **trim leading whitespace trước**, rồi kiểm ký tự đầu
  ∈ `= + - @ \t \r \n` (và không phải số) → prefix `'`. Test thêm ca leading-space và tab (không chỉ `=1+1`).

## Risk Assessment
- `IProgress<T>` callback chạy trên thread bắt (SynchronizationContext) — ở UI (P6) tạo `Progress<T>` trên
  UI thread để marshal an toàn; ở test dùng callback trực tiếp.
- Song song + ClosedXML ghi Excel: **ghi Excel sau khi** batch xong (1 thread) — không ghi song song.
- Đa luồng **mặc định tắt** (Red Team #1/#11); khi bật, `MaxDegreeOfParallelism` mặc định = CPU count.
