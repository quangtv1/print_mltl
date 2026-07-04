"""Sinh hàng loạt **PDF** từ nhiều nhóm hồ sơ bằng `qt_pdf_renderer` + Excel tổng hợp.

Render **tuần tự** (MVP): `QTextDocument`/`QPdfWriter` dùng chung cache font/glyph
không an toàn đa luồng (Red Team #4) — đo hiệu năng thật trước khi cân nhắc đa luồng.
Tên file chốt **tuần tự ở main thread** (`make_unique_path`) nên không ghi đè im lặng
dù trùng `ho_so_so` (Red Team #2). `BatchController` (QObject) chạy trong 1 QThread,
phát `progress`/`log`/`finished`/`failed`; lỗi 1 hồ sơ không dừng cả mẻ.
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

from PyQt5.QtCore import QObject, pyqtSignal

from app.core import qt_pdf_renderer as R
from app.models.style import StyleConfig


@dataclass
class _Task:
    """1 hồ sơ cần render (tên file đã chốt duy nhất ở main thread)."""

    group_value: str
    ho_so_so: str
    records: List[Dict[str, Any]]
    out_path: Path
    stt_file: int


@dataclass
class BatchSummary:
    """Tổng kết một mẻ generate."""

    total: int = 0
    succeeded: int = 0
    failed: int = 0
    skipped: int = 0
    errors: List[Tuple[str, str]] = field(default_factory=list)  # (hồ sơ, lỗi)
    out_dir: str = ""
    excel_path: str = ""
    elapsed_sec: float = 0.0


def build_tasks(
    style: StyleConfig,
    df,
    out_dir,
    filename_pattern: Optional[str] = None,
) -> Tuple[List[_Task], List[Dict[str, Any]]]:
    """Dựng danh sách task + dữ liệu Excel (theo thứ tự nhóm).

    Tên file expand đầy đủ token (`format_output_name`) rồi chống trùng bằng
    `make_unique_path` **tuần tự** → mỗi nhóm 1 file phân biệt (Red Team #1/#2).
    """
    pattern = filename_pattern or style.output_filename_pattern
    groups = R.group_dataframe(style, df)
    tasks: List[_Task] = []
    excel_items: List[Dict[str, Any]] = []
    used: set = set()

    for idx, (group_value, gdf) in enumerate(groups.items(), start=1):
        records = R.df_to_records(gdf)
        ho_so_so = R.resolve_ho_so_so(style, records)
        ctx = R.build_context(style, records, idx)
        filename = R.format_output_name(pattern, ctx)
        out_path = R.make_unique_path(out_dir, filename, used)
        tasks.append(
            _Task(
                group_value=str(group_value),
                ho_so_so=ho_so_so,
                records=records,
                out_path=out_path,
                stt_file=idx,
            )
        )
        excel_items.append({"ho_so_so": ho_so_so, "records": records})

    return tasks, excel_items


class BatchController(QObject):
    """Điều phối generate PDF tuần tự trong 1 QThread (giữ UI mượt)."""

    progress = pyqtSignal(int, int)  # (done, total)
    log = pyqtSignal(str)  # dòng nhật ký realtime từng hồ sơ
    finished = pyqtSignal(object)  # BatchSummary
    failed = pyqtSignal(str)  # lỗi fatal (không phải lỗi từng hồ sơ)

    def __init__(
        self,
        style: StyleConfig,
        df,
        out_dir,
        *,
        export_excel: bool = False,
        overwrite: bool = False,
        filename_pattern: Optional[str] = None,
        max_workers: int = 1,  # MVP: luôn 1 (serialize). Giữ tham số cho tương lai.
    ):
        super().__init__()
        self._style = style
        self._df = df
        self._out_dir = Path(out_dir)
        self._export_excel = export_excel
        self._overwrite = overwrite
        self._filename_pattern = filename_pattern
        self._max_workers = max_workers  # chưa dùng ở MVP (render tuần tự)

    def run(self) -> None:
        """Chạy toàn mẻ (blocking). Gọi trong QThread; phát tín hiệu về main."""
        start = time.monotonic()
        try:
            self._out_dir.mkdir(parents=True, exist_ok=True)
            tasks, excel_items = build_tasks(
                self._style, self._df, self._out_dir, self._filename_pattern
            )
        except Exception as e:  # noqa: BLE001 - lỗi dựng task = fatal
            self.failed.emit(str(e))
            return

        total = len(tasks)
        if total == 0:
            self.failed.emit("Không có hồ sơ nào để xuất.")
            return

        summary = BatchSummary(total=total, out_dir=str(self._out_dir))
        # group_value → thành công (để lọc Excel chỉ gồm hồ sơ đã tạo).
        ok_groups: Dict[str, bool] = {}

        for done, task in enumerate(tasks, start=1):
            label = task.ho_so_so or task.group_value
            if task.out_path.exists() and not self._overwrite:
                summary.skipped += 1
                ok_groups[task.group_value] = False
                self.log.emit(f"⏭️  Bỏ qua (đã tồn tại): {task.out_path.name}")
            else:
                try:
                    R.render_group_pdf(
                        self._style, task.records, task.out_path, task.stt_file
                    )
                    summary.succeeded += 1
                    ok_groups[task.group_value] = True
                    self.log.emit(
                        f"✅  [{done}/{total}] {label} → {task.out_path.name}"
                    )
                except Exception as e:  # noqa: BLE001 - cô lập lỗi từng hồ sơ
                    summary.failed += 1
                    ok_groups[task.group_value] = False
                    summary.errors.append((label, str(e)))
                    self.log.emit(f"❌  [{done}/{total}] {label}: {e}")
            self.progress.emit(done, total)

        # Excel tổng hợp: chỉ gồm hồ sơ đã tạo thành công, theo thứ tự gốc.
        if self._export_excel and summary.succeeded > 0:
            try:
                from app.core.excel_exporter import export_excel

                success_items = [
                    item
                    for item, task in zip(excel_items, tasks)
                    if ok_groups.get(task.group_value, False)
                ]
                xlsx = self._out_dir / "Muc_Luc_Ho_So.xlsx"
                if export_excel(success_items, xlsx, self._style):
                    summary.excel_path = str(xlsx)
                    self.log.emit(f"📊  Đã ghi Excel tổng hợp: {xlsx.name}")
            except Exception as e:  # noqa: BLE001 - lỗi Excel không hủy PDF đã tạo
                summary.errors.append(("Excel", str(e)))
                self.log.emit(f"⚠️  Lỗi ghi Excel tổng hợp: {e}")

        summary.elapsed_sec = round(time.monotonic() - start, 2)
        self.finished.emit(summary)
