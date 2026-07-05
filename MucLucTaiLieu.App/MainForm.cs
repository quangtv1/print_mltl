using Microsoft.Web.WebView2.WinForms;
using MucLucTaiLieu.App.WebHost;

namespace MucLucTaiLieu.App;

/// <summary>
/// Application shell (direction B): a single full-screen WebView2 hosting the whole
/// design_v3 prototype UI (all 3 wizard steps). C# supplies real data/actions through
/// the bridge (Excel read, file/folder dialogs, PDF batch) — wired incrementally.
///
/// Stage 1 renders the prototype so the WebView2 + React bootstrap can be verified on
/// Windows before the data/generate wiring is built on top.
/// </summary>
public sealed class MainForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly WebViewController _controller;

    public MainForm()
    {
        Text = "Tạo Mục Lục Hồ Sơ";
        Width = 1240;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;

        _controller = new WebViewController(_webView);
        Controls.Add(_webView);

        Load += async (_, _) =>
        {
            try
            {
                await _controller.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không khởi tạo được WebView2. Cần cài WebView2 Runtime.\n\n" + ex.Message,
                    "Lỗi WebView2", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }
}
