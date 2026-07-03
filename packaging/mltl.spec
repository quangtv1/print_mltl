# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec — build .exe cho app "Tạo Mục Lục Hồ Sơ".

--onedir (nhẹ khởi động, dễ kèm styles/). `styles/` KHÔNG bundle vào gói mà được
workflow copy CẠNH exe (đọc/ghi được, copy sang máy khác dùng lại — xem
resource_base_dir() trong app/core/platform_utils.py). Khóa Google KHÔNG bundle.

Build: pyinstaller packaging/mltl.spec  (chạy trên Windows / CI windows-latest)
"""

block_cipher = None


a = Analysis(
    ["../main.py"],
    pathex=["."],
    binaries=[],
    datas=[],  # styles/ copy cạnh exe ở workflow, không nhồi vào bundle
    hiddenimports=[
        "google.oauth2.service_account",
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    # Loại tài nguyên nhạy cảm / không cần: KHÔNG bao giờ gói khóa Google.
    excludes=["tkinter", "get_link_pdf_"],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="MucLucHoSo",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,  # app GUI, không mở cửa sổ console
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="MucLucHoSo",
)
