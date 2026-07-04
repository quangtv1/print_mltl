"""Entry point app desktop "Tạo Mục Lục Hồ Sơ".

Engine Native Qt (QTextDocument → PDF), render tuần tự trong QThread — không còn
ProcessPool nên không cần `multiprocessing.freeze_support()`.
"""

import sys


def main() -> int:
    from PyQt5.QtWidgets import QApplication

    from app.ui.main_window import MainWindow
    from app.ui.theme import apply_theme

    app = QApplication(sys.argv)
    app.setApplicationName("Tạo Mục Lục Hồ Sơ")
    apply_theme(app)

    window = MainWindow()
    window.show()
    return app.exec_()


if __name__ == "__main__":
    sys.exit(main())
