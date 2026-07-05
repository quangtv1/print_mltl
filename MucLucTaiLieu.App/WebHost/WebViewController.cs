using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MucLucTaiLieu.App.WebHost;

/// <summary>
/// Thin wrapper around a <see cref="WebView2"/> control that loads the prototype A4
/// page (App/Web/index.html) and brokers calls to the page's <c>window.MLTL</c> bridge
/// (mota3 §8). Shared by the Step 2 editor/preview (P5) and the offscreen PDF renderer.
///
/// NOTE: WebView2 runs only on Windows; this compiles cross-platform (EnableWindowsTargeting)
/// but its behavior must be verified on Windows/CI.
/// </summary>
public sealed class WebViewController : IAsyncDisposable
{
    private readonly WebView2 _webView;
    private readonly string _userDataFolder;

    // Same snake_case policy the seed JSON uses, so records reach the prototype in its shape.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WebView2 View => _webView;

    public WebViewController(WebView2 webView)
    {
        _webView = webView;
        _userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MLTL", "WebView2");
    }

    /// <summary>Initialize CoreWebView2 (with a writable user-data folder) and load index.html.</summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_userDataFolder);
        var env = await CoreWebView2Environment.CreateAsync(null, _userDataFolder);
        await _webView.EnsureCoreWebView2Async(env);

        var indexPath = Path.Combine(AppContext.BaseDirectory, "Web", "index.html");
        _webView.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
    }

    /// <summary>Select the active template (mau01..mau04).</summary>
    public Task SetTemplateAsync(string templateId) =>
        _webView.CoreWebView2.ExecuteScriptAsync($"window.MLTL.setTemplate({JsonSerializer.Serialize(templateId)})");

    /// <summary>Push one record (hồ sơ) into the page for rendering/preview.</summary>
    public Task SetRecordAsync(object hoSo) =>
        _webView.CoreWebView2.ExecuteScriptAsync(
            $"window.MLTL.setRecord({JsonSerializer.Serialize(JsonSerializer.Serialize(hoSo, JsonOpts))})");

    /// <summary>Push the variable→column mapping.</summary>
    public Task SetMappingAsync(IReadOnlyDictionary<string, string> mapping) =>
        _webView.CoreWebView2.ExecuteScriptAsync(
            $"window.MLTL.setMapping({JsonSerializer.Serialize(JsonSerializer.Serialize(mapping))})");

    /// <summary>Run an arbitrary bridge command (e.g. execFormat, insertVar, setZoom).</summary>
    public Task<string> InvokeAsync(string script) => _webView.CoreWebView2.ExecuteScriptAsync(script);

    public async ValueTask DisposeAsync()
    {
        _webView.Dispose();
        await Task.CompletedTask;
    }
}
