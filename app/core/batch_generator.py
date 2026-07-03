"""Sinh hàng loạt docx **song song** (ProcessPoolExecutor) + Excel tổng hợp.

`render_group_worker` là hàm module-level (picklable) chạy trong tiến trình con.
`BatchController` (QObject) điều phối pool trong 1 QThread, phát progress/finished,
gom dữ liệu Excel ở main process. Lỗi 1 hồ sơ không dừng cả mẻ.

An toàn khi đóng exe Windows: cần `multiprocessing.freeze_support()` trong
`main.py` (spawn re-import module này).
"""

from __future__ import annotations

import os
from concurrent.futures import ProcessPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Tuple

from PyQt5.QtCore import QObject, pyqtSignal

from app.core import docx_renderer as R
from app.models.style import StyleConfig

# Task gửi cho worker: (style_dict, records, out_path, group_value)
WorkerTask = Tuple[Dict[str, Any], List[Dict[str, Any]], str, str]
# Kết quả worker trả: (ok, group_value, path_or_error)
WorkerResult = Tuple[bool, str, str]


def render_group_worker(task: WorkerTask) -> WorkerResult:
    """Render 1 nhóm trong tiến trình con. Trả (ok, group_value, path|error).

    Bắt mọi lỗi để 1 hồ sơ hỏng không làm sập cả pool.
    """
    style_dict, records, out_path, group_value = task
    try:
        path = R.render_group(style_dict, records, out_path)
        return True, group_value, path
    except Exception as e:  # noqa: BLE001 - cô lập lỗi từng hồ sơ
        return False, group_value, str(e)


@dataclass
class BatchSummary:
    """Tổng kết một mẻ generate."""

    total: int = 0
    succeeded: int = 0
    failed: int = 0
    errors: List[Tuple[str, str]] = field(default_factory=list)  # (group, message)
    out_dir: str = ""
    excel_path: str = ""


def build_tasks(
    style: StyleConfig, style_dir, df, out_dir
) -> Tuple[List[WorkerTask], List[Dict[str, Any]]]:
    """Dựng danh sách task cho pool + dữ liệu Excel (theo thứ tự nhóm).

    Tên file xác định trước theo `ho_so_so` (chống trùng bằng `make_unique_path`
    tuần tự ở main process → an toàn khi chạy song song).
    """
    groups = R.group_dataframe(style, df)
    tasks: List[WorkerTask] = []
    excel_items: List[Dict[str, Any]] = []
    used: set = set()

    for group_value, gdf in groups.items():
        records = R.df_to_records(gdf)
        ho_so_so = R._resolve_ho_so_so(style, records)
        filename = R.resolve_output_name(style, ho_so_so)
        out_path = R.make_unique_path(out_dir, filename, used)
        style_dict, recs, out = R.build_task(style, style_dir, records, out_path)
        tasks.append((style_dict, recs, out, str(group_value)))
        excel_items.append({"ho_so_so": ho_so_so, "records": records})

    return tasks, excel_items


class BatchController(QObject):
    """Điều phối generate song song trong 1 QThread (giữ UI mượt)."""

    progress = pyqtSignal(int, int)  # (done, total)
    finished = pyqtSignal(object)  # BatchSummary
    failed = pyqtSignal(str)  # lỗi fatal (không phải lỗi từng hồ sơ)

    def __init__(self, style: StyleConfig, style_dir, df, out_dir, export_excel: bool):
        super().__init__()
        self._style = style
        self._style_dir = style_dir
        self._df = df
        self._out_dir = Path(out_dir)
        self._export_excel = export_excel

    def run(self) -> None:
        """Chạy toàn mẻ. Gọi trong QThread (blocking cho tới khi xong)."""
        try:
            self._out_dir.mkdir(parents=True, exist_ok=True)
            tasks, excel_items = build_tasks(
                self._style, self._style_dir, self._df, self._out_dir
            )
        except Exception as e:  # noqa: BLE001 - lỗi dựng task = fatal
            self.failed.emit(str(e))
            return

        total = len(tasks)
        summary = BatchSummary(total=total, out_dir=str(self._out_dir))
        if total == 0:
            self.failed.emit("Không có hồ sơ nào để xuất.")
            return

        # group_value -> có thành công không (để lọc Excel).
        ok_groups: Dict[str, bool] = {}
        max_workers = max(1, (os.cpu_count() or 2) - 1)
        done = 0
        try:
            with ProcessPoolExecutor(max_workers=max_workers) as ex:
                futures = [ex.submit(render_group_worker, t) for t in tasks]
                for fut in as_completed(futures):
                    ok, group_value, info = fut.result()
                    ok_groups[group_value] = ok
                    if ok:
                        summary.succeeded += 1
                    else:
                        summary.failed += 1
                        summary.errors.append((group_value, info))
                    done += 1
                    self.progress.emit(done, total)
        except Exception as e:  # noqa: BLE001 - lỗi pool = fatal
            self.failed.emit(f"Lỗi khi chạy song song: {e}")
            return

        # Excel: gom nhóm THÀNH CÔNG theo thứ tự gốc, ghi 1 lần ở main process.
        if self._export_excel and summary.succeeded > 0:
            try:
                from app.core.excel_exporter import export_excel

                # Lọc theo thứ tự tasks (build_tasks giữ song song excel_items↔tasks).
                success_items = [
                    item
                    for item, task in zip(excel_items, tasks)
                    if ok_groups.get(task[3], False)
                ]
                xlsx = self._out_dir / "Muc_Luc_Ho_So.xlsx"
                if export_excel(success_items, xlsx, self._style):
                    summary.excel_path = str(xlsx)
            except Exception as e:  # noqa: BLE001 - lỗi Excel không hủy docx đã tạo
                summary.errors.append(("Excel", str(e)))

        self.finished.emit(summary)
