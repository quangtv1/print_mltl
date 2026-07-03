"""Entry point app desktop "Tạo Mục Lục Hồ Sơ".

`multiprocessing.freeze_support()` PHẢI gọi đầu tiên trong `__main__` — cần cho
generate song song (ProcessPoolExecutor, P6) khi đóng gói exe Windows (spawn, P7).
"""

import multiprocessing
import sys


def main() -> int:
    from PyQt5.QtWidgets import QApplication

    from app.ui.main_window import MainWindow

    app = QApplication(sys.argv)
    app.setApplicationName("Tạo Mục Lục Hồ Sơ")

    window = MainWindow()
    window.show()
    return app.exec_()


if __name__ == "__main__":
    multiprocessing.freeze_support()
    sys.exit(main())
