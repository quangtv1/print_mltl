---
phase: 6
title: "Batch Generator (QThread)"
status: done
effort: ""
---

# Phase 6: Batch Generator (QThread)

<!-- Updated: Validation Session 1 - generate SONG SONG (ProcessPoolExecutor) ngay từ đầu, quy mô hàng nghìn hồ sơ -->

## Overview
Sinh hàng loạt **song song** (`ProcessPoolExecutor`) qua tất cả nhóm → docx (+ Excel tùy chọn) vào thư mục chọn. Một QThread điều phối pool + gom kết quả + progress, không treo UI. Quy mô mục tiêu ~hàng nghìn hồ sơ.

## Requirements
- Functional: chọn thư mục xuất; checkbox xuất Excel; nút Generate; progress + summary (thành công/lỗi).
- Non-functional: **song song đa tiến trình**; báo lỗi từng hồ sơ không dừng cả mẻ; an toàn khi đóng exe (`freeze_support`).

## Architecture
```
app/core/batch_generator.py
  def render_group_worker(args) -> (ok, group_value, out_path_or_error)
      # HÀM MODULE-LEVEL (picklable) — mở DocxTemplate riêng trong tiến trình con
      # args = (style_dict, group_records, out_dir)  → dựng context + render_group
  class BatchController(QObject)   # signals: progress(done,total), finished(summary), failed(msg)
      run(): 
        tasks = group_dataframe(...) -> [(style_dict, records, out_dir), ...]
        with ProcessPoolExecutor(max_workers=cpu-1) as ex:
            for res in ex.map/as_completed(render_group_worker, tasks): emit progress
        gom rows cho Excel (main process) -> export_excel nếu bật
app/ui: nút Generate + QProgressBar + chọn thư mục + checkbox Excel (gắn vào main_window/tab cuối)
```
QThread chỉ để giữ UI mượt trong khi chờ pool; công việc nặng ở các process con. `DocxTemplate` **không pickle được** → worker nhận `style_dict` + records và tự mở template (xem P3).

## Related Code Files
- Create: `app/core/batch_generator.py`; bổ sung UI generate vào `app/ui/main_window.py` (hoặc tab riêng)
- Depends: **P3** (`render_group_worker`/`render_group` process-safe, excel_exporter), P5 (UI)
- `main.py` gọi `multiprocessing.freeze_support()` đầu tiên (bắt buộc cho exe Windows).

## Implementation Steps
1. `render_group_worker` (module-level): nhận `(style_dict, records, out_dir)`, dựng StyleConfig từ dict, gọi `render_group` (P3), trả `(ok, group, path/err)`.
2. `BatchController.run`: dựng task list từ `group_dataframe`; chạy `ProcessPoolExecutor(max_workers=cpu_count()-1)` với `as_completed`; emit `progress` mỗi hồ sơ.
3. Gom dữ liệu Excel ở main process (worker chỉ trả path + records tối thiểu) → `export_excel` 1 lần nếu bật.
4. Move controller vào QThread; nối signal → QProgressBar + log; hộp thoại summary + nút mở thư mục.
5. `freeze_support()` trong `main.py`.

## Success Criteria
- [ ] Generate mẻ lớn (vài trăm–nghìn hồ sơ) chạy song song, nhanh hơn tuần tự rõ rệt; UI mượt, progress chạy.
- [ ] 1 hồ sơ lỗi → ghi summary, các hồ sơ khác vẫn xong.
- [ ] Output docx khớp output script cũ.
- [ ] Chạy đúng cả khi đóng exe (không lỗi spawn/pickle).

## Risk Assessment
- Pickling: `DocxTemplate`/`InlineImage` không pickle → worker tự mở template; chỉ truyền dict/records (giải quyết ở P3).
- `ProcessPoolExecutor` trong exe PyInstaller cần `freeze_support()` + spawn start method (Windows mặc định spawn) → test kỹ ở P7.
- Ghi đè file trùng tên → `resolve_output_name` chống trùng (P3); lưu ý race khi song song → tên xác định trước theo `ho_so_so`, không phụ thuộc thứ tự.

## Related Code Files
- Create: `app/core/batch_generator.py`; bổ sung UI generate vào `app/ui/main_window.py` (hoặc tab riêng)
- Depends: P3 (renderer, excel_exporter), P5 (UI)

## Implementation Steps
1. `BatchWorker`: nhận StyleConfig + df + out_dir + flag Excel; lặp nhóm, emit `progress`; thu lỗi per-nhóm vào summary.
2. Move worker sang QThread; nối signal → QProgressBar + log.
3. Excel: gom `all_groups` trong RAM → `export_excel` 1 lần (như bản cũ).
4. Kết thúc: hộp thoại summary (N thành công / M lỗi + danh sách lỗi), nút mở thư mục xuất.

## Success Criteria
- [ ] Generate mẻ nhiều hồ sơ → đủ N docx + (Excel nếu bật) đúng thư mục; UI mượt, progress chạy.
- [ ] 1 hồ sơ lỗi (vd thiếu field) → ghi vào summary, các hồ sơ khác vẫn xong.
- [ ] Output docx khớp output script cũ.

## Risk Assessment
- Số hồ sơ rất lớn → tuần tự có thể chậm; ghi chú để cân nhắc `ProcessPoolExecutor` sau (kèm `freeze_support` khi đóng exe).
- Ghi đè file trùng tên → dùng `resolve_output_name` chống trùng từ P3.
