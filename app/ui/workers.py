"""Worker chạy tác vụ nặng trên QThread để UI không treo.

`run_async` gắn 1 callable vào QThread mới, phát `finished(result)` hoặc
`failed(message)` về main thread. Caller giữ tham chiếu thread+worker để không
bị GC giữa chừng.
"""

from __future__ import annotations

from typing import Callable

from PyQt5.QtCore import QObject, QThread, pyqtSignal


class _Worker(QObject):
    finished = pyqtSignal(object)
    failed = pyqtSignal(str)

    def __init__(self, fn: Callable):
        super().__init__()
        self._fn = fn

    def run(self) -> None:
        try:
            result = self._fn()
        except Exception as e:  # noqa: BLE001 - chuyển mọi lỗi thành tín hiệu UI
            self.failed.emit(str(e))
            return
        self.finished.emit(result)


def run_async(owner, fn: Callable, on_done: Callable, on_error: Callable) -> None:
    """Chạy `fn()` trên QThread; gọi `on_done(result)` hoặc `on_error(msg)`.

    `owner` phải là QObject để giữ tham chiếu thread/worker (tránh GC). Thread tự
    dọn khi xong.
    """
    thread = QThread()
    worker = _Worker(fn)
    worker.moveToThread(thread)

    thread.started.connect(worker.run)
    worker.finished.connect(on_done)
    worker.failed.connect(on_error)

    # Dọn dẹp: dừng thread rồi xóa đối tượng.
    worker.finished.connect(thread.quit)
    worker.failed.connect(thread.quit)
    thread.finished.connect(worker.deleteLater)
    thread.finished.connect(thread.deleteLater)

    # Giữ tham chiếu trên owner để không bị thu gom.
    if not hasattr(owner, "_active_threads"):
        owner._active_threads = []
    owner._active_threads.append((thread, worker))

    def _cleanup():
        try:
            owner._active_threads.remove((thread, worker))
        except (ValueError, AttributeError):
            pass

    thread.finished.connect(_cleanup)
    thread.start()
