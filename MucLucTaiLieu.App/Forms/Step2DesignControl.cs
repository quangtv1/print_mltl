using System.Drawing;
using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using MucLucTaiLieu.App.WebHost;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.App.Forms;

/// <summary>
/// Step 2 — Thiết kế &amp; Preview (mota3 §7): left = the A4 sheet in WebView2 (the
/// prototype's contentEditable + pagination engine), right = variable panel. The
/// WinForms toolbar drives the editor through <c>window.MLTL</c> (bridge.js). Behavior
/// requires Windows/WebView2 to verify.
/// </summary>
public sealed class Step2DesignControl : UserControl
{
    private readonly AppState _state;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly WebViewController _controller;
    private readonly FlowLayoutPanel _varPanel = new() { Dock = DockStyle.Right, Width = 240, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.PanelBg, Padding = new Padding(8) };
    private readonly Label _recordLabel = new() { AutoSize = true, Padding = new Padding(8, 6, 0, 0) };

    private List<HoSo> _records = new();
    private int _recordIndex;
    private bool _preview;

    public Step2DesignControl(AppState state)
    {
        _state = state;
        _controller = new WebViewController(_webView);
        Dock = DockStyle.Fill;
        BuildLayout();
    }

    private void BuildLayout()
    {
        Controls.Add(_webView);
        Controls.Add(_varPanel);
        Controls.Add(BuildToolbar());
    }

    private ToolStrip BuildToolbar()
    {
        var ts = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };

        AddFontControls(ts);
        ts.Items.Add(new ToolStripSeparator());
        AddButton(ts, "B", () => Exec("execFormat('bold')"), bold: true);
        AddButton(ts, "I", () => Exec("execFormat('italic')"), italic: true);
        AddButton(ts, "U", () => Exec("execFormat('underline')"), underline: true);
        ts.Items.Add(new ToolStripSeparator());
        AddButton(ts, "A−", () => Exec("execFormat('decreaseFont')"));
        AddButton(ts, "A+", () => Exec("execFormat('increaseFont')"));
        ts.Items.Add(new ToolStripSeparator());
        AddButton(ts, "◧", () => Exec("execFormat('justifyLeft')"));
        AddButton(ts, "▤", () => Exec("execFormat('justifyCenter')"));
        AddButton(ts, "◨", () => Exec("execFormat('justifyRight')"));
        ts.Items.Add(new ToolStripSeparator());
        AddZoom(ts);
        AddButton(ts, "Dọc/Ngang", () => Exec("setOrient(window.__mltlComponent && window.__mltlComponent.state.orient==='landscape'?'portrait':'landscape')"));
        ts.Items.Add(new ToolStripSeparator());
        AddButton(ts, "Chèn cột", () => Exec("execFormat('insertCol')"));
        AddButton(ts, "Xóa cột", () => Exec("execFormat('delCol')"));
        AddButton(ts, "↶ Undo", () => Exec("execFormat('undo')"));
        ts.Items.Add(new ToolStripSeparator());
        var toggle = new ToolStripButton("Xem trước") { CheckOnClick = true };
        toggle.CheckedChanged += (_, _) => { _preview = toggle.Checked; toggle.Text = _preview ? "Chỉnh sửa" : "Xem trước"; _ = Exec($"setMode('{(_preview ? "preview" : "edit")}')"); };
        ts.Items.Add(toggle);

        return ts;
    }

    private void AddFontControls(ToolStrip ts)
    {
        var font = new ToolStripComboBox { Width = 130 };
        font.Items.AddRange(new object[] { "Times New Roman", "Arial", "Segoe UI", "Calibri" });
        font.SelectedIndex = 0;
        font.SelectedIndexChanged += (_, _) => Exec($"execFormat('fontName', {JsonSerializer.Serialize(font.SelectedItem)})");
        ts.Items.Add(font);

        var size = new ToolStripComboBox { Width = 60 };
        size.Items.AddRange(new object[] { "10", "11", "12", "13", "14", "16", "18" });
        size.SelectedIndex = 1;
        size.SelectedIndexChanged += (_, _) => Exec($"execFormat('fontSizePx', {JsonSerializer.Serialize(size.SelectedItem)})");
        ts.Items.Add(size);
    }

    private void AddZoom(ToolStrip ts)
    {
        var zoom = new ToolStripComboBox { Width = 70 };
        zoom.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%", "200%" });
        zoom.SelectedIndex = 2;
        zoom.SelectedIndexChanged += (_, _) =>
        {
            var pct = int.Parse(zoom.SelectedItem!.ToString()!.TrimEnd('%'));
            Exec($"setZoom({pct / 100.0})");
        };
        ts.Items.Add(zoom);
    }

    private void AddButton(ToolStrip ts, string text, Action onClick, bool bold = false, bool italic = false, bool underline = false)
    {
        var b = new ToolStripButton(text);
        var style = FontStyle.Regular;
        if (bold) style |= FontStyle.Bold;
        if (italic) style |= FontStyle.Italic;
        if (underline) style |= FontStyle.Underline;
        b.Font = new Font("Segoe UI", 9f, style);
        b.Click += (_, _) => onClick();
        ts.Items.Add(b);
    }

    /// <summary>Initialize the WebView (once) and load the current template + record.</summary>
    public async Task EnterAsync()
    {
        if (_webView.CoreWebView2 is null)
            await _controller.InitializeAsync();

        _records = _state.RecordsForPreview();
        _recordIndex = 0;
        BuildVarPanel();
        await _controller.SetTemplateAsync(_state.Config.TemplateId);
        await _controller.SetMappingAsync(_state.Config.ColMap);
        await ShowRecordAsync();
    }

    private void BuildVarPanel()
    {
        _varPanel.Controls.Clear();
        _varPanel.Controls.Add(new Label { Text = "Biến trong mẫu", Font = Theme.UiBold, AutoSize = true });
        foreach (var group in _state.CurrentTemplate.Vars.GroupBy(v => v.G))
        {
            _varPanel.Controls.Add(new Label { Text = group.Key, Font = Theme.UiBold, ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
            foreach (var v in group)
            {
                var chip = new Button { Text = v.V, Width = 210, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
                chip.FlatStyle = FlatStyle.Flat;
                if (v.Auto) chip.BackColor = Color.FromArgb(238, 250, 240);
                var token = v.V.Trim('{', '}');
                chip.Click += (_, _) => Exec(_preview ? $"highlightVar({JsonSerializer.Serialize(token)})" : $"insertVar({JsonSerializer.Serialize(token)})");
                _varPanel.Controls.Add(chip);
            }
        }

        var nav = new FlowLayoutPanel { Width = 220, Height = 30, Margin = new Padding(0, 10, 0, 0) };
        var prev = new Button { Text = "◀", Width = 40 };
        var next = new Button { Text = "▶", Width = 40 };
        prev.Click += async (_, _) => await MoveRecordAsync(-1);
        next.Click += async (_, _) => await MoveRecordAsync(1);
        nav.Controls.Add(prev);
        nav.Controls.Add(_recordLabel);
        nav.Controls.Add(next);
        _varPanel.Controls.Add(nav);
    }

    private async Task MoveRecordAsync(int delta)
    {
        if (_records.Count == 0) return;
        _recordIndex = (_recordIndex + delta + _records.Count) % _records.Count;
        await ShowRecordAsync();
    }

    private async Task ShowRecordAsync()
    {
        if (_records.Count == 0) { _recordLabel.Text = "(không có hồ sơ)"; return; }
        var rec = _records[_recordIndex];
        _recordLabel.Text = $"Hồ sơ {rec.SoHoSo} ({_recordIndex + 1}/{_records.Count})";
        await _controller.SetRecordAsync(rec);
    }

    private Task Exec(string method) => _controller.InvokeAsync($"window.MLTL.{method}");
}
