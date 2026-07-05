using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using MucLucTaiLieu.Core.Models;
using MucLucTaiLieu.Core.Pdf;

namespace MucLucTaiLieu.App.Pdf;

/// <summary>
/// Renders one hồ sơ to PDF with <see cref="CoreWebView2.PrintToPdfAsync"/>, printing the
/// exact resolved HTML the preview shows (mota3 §3.3, §8) — so preview == PDF. Runs an
/// offscreen (hidden) WebView2 so it can be reused for batch generation (P6).
///
/// NOTE: Windows-only; compiles cross-platform but requires Windows/CI to verify. The
/// PrintToPdf spike gate (phase 03 step 0) must confirm output matches preview before
/// this is relied upon; if margins/scale can't be tuned via CoreWebView2PrintSettings,
/// the plan's Puppeteer fallback applies.
/// </summary>
public sealed class WebView2PrintRenderer : IPdfRenderer, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly Form _host;
    private readonly WebView2 _webView;
    private bool _initialized;
    private TaskCompletionSource<bool>? _renderReady;

    public WebView2PrintRenderer()
    {
        // Hidden host window: gives WebView2 an HWND + message pump without showing UI.
        _host = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new System.Drawing.Point(-5000, -5000),
            Size = new System.Drawing.Size(1000, 1400),
        };
        _webView = new WebView2 { Dock = DockStyle.Fill };
        _host.Controls.Add(_webView);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        _host.CreateControl();          // force handle creation for the offscreen host
        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MLTL", "WebView2");
        Directory.CreateDirectory(userData);

        var env = await CoreWebView2Environment.CreateAsync(null, userData);
        await _webView.EnsureCoreWebView2Async(env);
        _webView.CoreWebView2.WebMessageReceived += OnWebMessage;

        var indexPath = Path.Combine(AppContext.BaseDirectory, "Web", "index.html");
        var navDone = new TaskCompletionSource<bool>();
        void OnNav(object? s, CoreWebView2NavigationCompletedEventArgs e) => navDone.TrySetResult(e.IsSuccess);
        _webView.CoreWebView2.NavigationCompleted += OnNav;
        _webView.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
        await navDone.Task;
        _webView.CoreWebView2.NavigationCompleted -= OnNav;

        _initialized = true;
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // The bridge posts {"type":"rendered"} once pagination is complete.
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "rendered")
                _renderReady?.TrySetResult(true);
        }
        catch (JsonException) { /* ignore unrelated messages */ }
    }

    public async Task RenderAsync(
        HoSo hoSo,
        string templateId,
        IReadOnlyDictionary<string, string> mapping,
        string outPath,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        _renderReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = ct.Register(() => _renderReady.TrySetCanceled(ct));

        var core = _webView.CoreWebView2;
        await core.ExecuteScriptAsync($"window.MLTL.setTemplate({JsonSerializer.Serialize(templateId)})");
        await core.ExecuteScriptAsync($"window.MLTL.setMapping({JsonSerializer.Serialize(JsonSerializer.Serialize(mapping))})");
        await core.ExecuteScriptAsync($"window.MLTL.setRecord({JsonSerializer.Serialize(JsonSerializer.Serialize(hoSo, JsonOpts))})");

        await _renderReady.Task; // wait for pagination-complete signal (no hard sleep)

        var settings = core.Environment.CreatePrintSettings();
        settings.Orientation = CoreWebView2PrintOrientation.Portrait;
        settings.ShouldPrintBackgrounds = true;
        settings.PageWidth = 8.27;   // A4 width in inches
        settings.PageHeight = 11.69; // A4 height in inches
        settings.MarginTop = settings.MarginBottom = 0.4;
        settings.MarginLeft = settings.MarginRight = 0.4;

        var ok = await core.PrintToPdfAsync(outPath, settings);
        if (!ok) throw new InvalidOperationException($"PrintToPdf thất bại: {Path.GetFileName(outPath)}");
    }

    public async ValueTask DisposeAsync()
    {
        _webView.Dispose();
        _host.Dispose();
        await Task.CompletedTask;
    }
}
