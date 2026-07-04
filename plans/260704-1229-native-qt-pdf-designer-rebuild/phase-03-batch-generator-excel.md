---
phase: 3
title: "Batch Generator & Excel"
status: completed
priority: P1
dependencies: [2]
---

# Phase 3: Batch Generator & Excel

## Overview
Viết lại `batch_generator` để sinh **N PDF song song** bằng `qt_pdf_renderer` (P2). Vì render Qt cần
`QApplication`/không process-safe → đổi **ProcessPoolExecutor → QThreadPool**. Giữ Excel tổng hợp
(reuse `excel_exporter`) + progress/log realtime + tùy chọn ghi đè.

## Requirements
- Functional: đi qua từng group → PDF; song song; đếm done/total; báo lỗi từng hồ sơ không dừng cả batch;
  Excel tổng hợp tùy chọn; ghi đè hoặc bỏ qua file tồn tại.
- Non-functional: nghìn group vẫn chạy vài chục giây; UI không treo (chạy ngoài main thread, signal về).

## Architecture
`app/core/batch_generator.py` (rewrite):
- Giữ `BatchController(QObject)` + signals `progress(done,total)`, `finished(BatchSummary)`,
  `failed(str)`; **thêm** `log = pyqtSignal(str)` (realtime từng hồ sơ) cho step_run.
- **Song song bằng `QThreadPool` + `QRunnable`** (thay `ProcessPoolExecutor`):
  - Mỗi `QRunnable` = render 1 group → PDF qua `qt_pdf_renderer.render_group_pdf`.
  - `QThreadPool.globalInstance().setMaxThreadCount(...)`; option "đa luồng" bật/tắt → max=1 khi tắt.
  - Thu kết quả qua signal (mỗi runnable phát done/err) — cộng dồn trong controller ở main thread.
- `build_tasks(style, df, out_dir)` giữ ý tưởng cũ nhưng task chứa: group records, doc_fields,
  out_path (`format_output_name`), stt_file (index). Bỏ WorkerTask docx.
- Ghi đè: nếu file tồn tại và không ghi đè → skip + log "bỏ qua".
- Excel: sau khi xong, nếu `export_excel` bật → gọi `excel_exporter.export_excel(all_groups, path, style)`.

## Red Team Fixes (áp dụng 2026-07-04)
- **#2 (Critical) — GIỮ dedup tuần tự:** `build_tasks` chạy `make_unique_path(out_dir, name, used)` với
  set `used` **ở main thread** trước khi dispatch (như `batch_generator.py:66-72`); mỗi task nhận
  `out_path` **đã chốt duy nhất**. Runnable **chỉ render**, KHÔNG tự quyết skip/tên (tránh race
  TOCTOU + ghi đè im lặng). Quyết định ghi-đè/skip cũng làm tuần tự trong controller.
  **Success criteria:** N group (kể cả trùng `ho_so_so`) → **N file phân biệt**.
- **#4 (Critical) — SERIALIZE render cho MVP** <!-- Updated: Validation Session 1 - serialize-first, đo rồi mới tính -->:
  vì QTextDocument/QPdfWriter dùng font/glyph cache chung → render tuần tự (`setMaxThreadCount(1)`).
  **KHÔNG đầu tư spike đa luồng upfront** — **đo thời gian trên dữ liệu thật trước** (layout C++ nhanh,
  nghìn hồ sơ có thể đủ nhanh khi serial). **Chỉ** làm đa luồng (QThreadPool + soak-test ≥1000 group, hoặc
  ProcessPool + `QGuiApplication` offscreen) nếu đo thấy **quá chậm**. Checkbox "đa luồng" trong UI để
  **disabled/ẩn** ở MVP tới khi có bản đa luồng đã kiểm chứng.
- **#5 (High) — QRunnable không phát signal:** dùng 1 `QObject` signaller
  (`class _RunnableSignals(QObject): done = pyqtSignal(bool,str,str)`) khởi tạo trên thread controller,
  truyền vào mỗi `_GroupRunnable`; emit qua `Qt.QueuedConnection`. Ghi rõ controller sống ở thread nào
  (hiện `generate_tab.py:91-92` chuyển controller vào worker QThread) — theo mẫu `workers.py:15-29`.
- **Contract drift (#8-batch):** P3 chốt **chữ ký đầy đủ** `BatchController` (thêm `overwrite`,
  `max_workers`/đa-luồng, `filename_pattern`, signal `log`); bỏ `style_dir` khỏi `build_tasks` nếu không
  cần. P6 wiring UI theo đúng chữ ký này.
- **Formula/CSV injection (minor):** khi ghi Excel tổng hợp, ô bắt đầu bằng `= + - @` → prefix `'`
  (hoặc từ chối) trước khi ghi (`excel_exporter` nhận giá trị đã làm sạch).

## Related Code Files
- Modify (rewrite): `app/core/batch_generator.py`
- Reuse: `app/core/qt_pdf_renderer.py` (P2), `app/core/excel_exporter.py` (API `export_excel` giữ nguyên)
- Reference: cấu trúc `BatchController`/`build_tasks`/`BatchSummary` cũ

## Implementation Steps
1. Đổi import ProcessPool→`QThreadPool`/`QRunnable`/`QObject` signal; định nghĩa `_GroupRunnable`.
2. `build_tasks`: dựng list group + doc_fields + out_path + stt_file từ `group_dataframe`/`df_to_records`.
3. `BatchController.run`: submit runnables, nối signal đếm tiến độ + `log` từng hồ sơ; xử lý ghi đè/skip.
4. Sau batch: BatchSummary (tổng, lỗi, thời gian) + gọi Excel nếu bật.
5. Đo hiệu năng với ~500–1000 group; nếu QThreadPool không đủ nhanh, ghi chú phương án ProcessPool +
   QGuiApplication offscreen (mỗi child tạo app) làm dự phòng — **không** đổi mặc định nếu QThreadPool đạt.

## Success Criteria
- [ ] N group → N PDF đúng, song song; progress chạy tới total.
- [ ] Lỗi 1 hồ sơ → log lỗi, batch tiếp tục; BatchSummary đếm đúng số lỗi.
- [ ] Ghi đè tắt → file tồn tại bị bỏ qua + log; bật → ghi đè.
- [ ] Excel tổng hợp đúng khi bật (reuse `export_excel`).
- [ ] `log` signal phát realtime từng hồ sơ (cho step_run P6).
- [ ] UI không treo trong lúc chạy.

## Risk Assessment
- QThreadPool + Qt render: mỗi runnable phải tạo `QTextDocument`/`QPdfWriter` **riêng** (không share); không chạm widget. Verify không crash đa luồng.
- GIL: phần Python nhẹ; layout/paint là C++ (nhả GIL phần lớn) → song song thực tế đủ tốt. Đo ở bước 5.
- Nếu QPdfWriter đa luồng không ổn định trên Windows → fallback max=1 hoặc ProcessPool offscreen (ghi chú).
