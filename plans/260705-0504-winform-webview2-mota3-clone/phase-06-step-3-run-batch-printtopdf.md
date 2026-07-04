---
phase: 6
title: "Step 3 Run Batch PrintToPdf"
status: pending
priority: P1
dependencies: [2, 3]
---

# Phase 6: Step 3 Run Batch PrintToPdf

## Overview
Bước 3 — Chạy (mota3 §9,§10): thư mục + mẫu tên PDF, tùy chọn (đa luồng/ghi đè/Excel/bỏ-qua-lỗi), **một nút
"Tạo Mục lục"**, tiến trình (x/y) + console log tối + mở thư mục + thử lại lỗi. Batch sinh N PDF bằng
`WebView2PrintRenderer` (P3) trong **pool offscreen** song song. Logic điều phối batch theo **TDD**.

## Requirements
- Functional: `BatchRunner.RunAsync(hoSoList, template, mapping, outDir, options, IProgress, ct) → BatchSummary`;
  đơn/đa luồng; ghi đè/bỏ qua; bỏ-qua-lỗi (không dừng) hoặc dừng cả mẻ; retry hồ sơ lỗi; Excel tổng hợp.
- Non-functional: UI không treo; ~1000 hồ sơ trong thời gian hợp lý; pool WebView2 giới hạn (RAM).

## Architecture
- `Core/Batch/BatchRunner`: nhận danh sách hồ sơ + `IPdfRenderer` (inject) → **tên file chốt tuần tự**
  (`NameResolver.Build` + `MakeUnique`, `{ngay_gio}` tính 1 lần đầu mẻ) → `Parallel.ForEachAsync`
  (`MaxDegreeOfParallelism = multi ? ProcessorCount : 1`): ghi đè/skip theo `File.Exists`; try/catch mỗi hồ sơ;
  `skipErrors` false → hủy CTS + log "■ Đã dừng"; report qua `IProgress<BatchProgress>` (done/total/log line).
  Gom danh sách lỗi để **retry**. Sau mẻ: nếu bật → `ExcelExporter` (ClosedXML) tổng hợp.
- `App/Pdf/WebView2RendererPool`: <!-- Updated: Validation Session 1 - quy mô vài trăm → serialize-first -->
  **quy mô mục tiêu = vài trăm hồ sơ** → **mặc định serial (MaxDOP=1, 1 renderer offscreen tái dùng)**; giữ
  tham số MaxDOP nhưng **KHÔNG** đầu tư pool phức tạp/throttle upfront. Chỉ mở song song (pool nhỏ = 2–ProcessorCount)
  khi đo thấy cần. (`IPdfRenderer` để test batch bằng fake renderer.)
- `Forms/Step3RunControl.cs`: khối thư mục (FolderBrowserDialog) + mẫu tên (combobox preset + textbox mono +
  ví dụ tên); tùy chọn (đa luồng — **ẩn số luồng**; ghi đè; Excel; bỏ-qua-lỗi); dòng ⏱ ước tính; progress bar
  (nền `#e6e6e6`, fill accent) + "Tiến trình chạy (x/y)" chỉ hiện khi chạy/xong; **console log tối** cuộn; nút
  📁 Mở thư mục + ↻ Thử lại hồ sơ lỗi. Nút chạy = **"Tạo Mục lục"** ở nav bar ("Đang tạo…"→"Tạo lại").

## Tests (viết trước — TDD, dùng fake `IPdfRenderer`)
- `BatchRunnerTests`: N hồ sơ (2 trùng so_ho_so) → **N file phân biệt**; ghi đè off → skip tồn tại, on → ghi lại;
  1 hồ sơ lỗi + skipErrors on → Failed=1, chạy tiếp, Summary đúng; skipErrors off → dừng ngay (CTS), log dừng;
  đa luồng (MaxDOP>1) vs đơn — cùng kết quả file; progress tới (N,N); retry chỉ chạy lại danh sách lỗi.
- `ExcelExporterTests`: ghi tổng hợp đúng; formula-injection (`= + - @` sau trim khoảng trắng, gồm tab) → prefix `'`; số âm giữ nguyên.

## Implementation Steps
1. Viết `BatchRunnerTests` + `IPdfRenderer` fake (RED).
2. `BatchRunner` (tên tuần tự, Parallel.ForEachAsync, skip/overwrite, skipErrors, retry, IProgress) → GREEN.
3. `ExcelExporter` + tests injection/số âm.
4. `WebView2RendererPool` (App) + `Step3RunControl` UI; nối progress/log/retry/mở thư mục.
5. Chạy thử ~1000 hồ sơ trên Windows; đo thời gian đơn vs đa luồng.

## Success Criteria
- [ ] N hồ sơ → N PDF phân biệt; ghi đè/skip đúng; **mặc định serial chạy đúng** (đa luồng chỉ là opt-in, không phải tiêu chí).
- [ ] Bỏ-qua-lỗi: 1 hồ sơ lỗi → log + tiếp; tắt → dừng cả mẻ + log "■ Đã dừng". Retry chạy lại đúng danh sách lỗi.
- [ ] Tiến trình (x/y) + console log realtime; nút Mở thư mục; Excel tổng hợp đúng (chống injection).
- [ ] Một nút "Tạo Mục lục" (nav bar), đổi nhãn Đang tạo/Tạo lại; UI không treo.

## Risk Assessment
- Pool WebView2 offscreen: mỗi instance nặng → giới hạn size, tái dùng, dispose; đo RAM ~1000 hồ sơ. Nếu quá tải
  → giảm MaxDOP; đây là điểm đo thực tế trên Windows.
- `IProgress<T>` marshal về UI thread (`Progress<T>` tạo trên UI). Nav lock khi chạy — unlock trong `finally`.
- `PrintToPdfAsync` không thread-safe trên **cùng** một WebView2 → mỗi worker một renderer riêng trong pool.
