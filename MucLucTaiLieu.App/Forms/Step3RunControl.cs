using System.Drawing;
using MucLucTaiLieu.App.Pdf;
using MucLucTaiLieu.Core.Batch;
using MucLucTaiLieu.Core.Excel;
using MucLucTaiLieu.Core.Models;

namespace MucLucTaiLieu.App.Forms;

/// <summary>
/// Step 3 — Chạy (mota3 §9, §10): output folder + PDF name pattern + options, one
/// "Tạo Mục lục" action (driven from the nav bar), progress (x/y), dark console log,
/// open-folder and retry-failed actions. Batch runs via <see cref="BatchRunner"/>.
/// </summary>
public sealed class Step3RunControl : UserControl
{
    private readonly AppState _state;

    private readonly TextBox _folder = new() { Width = 360, ReadOnly = true };
    private readonly TextBox _pattern = new() { Width = 300, Font = Theme.Mono, Text = "{stt_file}_{so_ho_so}" };
    private readonly Label _example = new() { AutoSize = true, ForeColor = Color.Gray };
    private readonly CheckBox _multi = new() { Text = "Đa luồng (nhanh hơn)", AutoSize = true };
    private readonly CheckBox _overwrite = new() { Text = "Ghi đè tệp đã có", AutoSize = true };
    private readonly CheckBox _excel = new() { Text = "Xuất Excel tổng hợp", AutoSize = true };
    private readonly CheckBox _skipErrors = new() { Text = "Bỏ qua hồ sơ lỗi", AutoSize = true, Checked = true };
    private readonly ProgressBar _progress = new() { Width = 500, Height = 16, Visible = false };
    private readonly Label _progressText = new() { AutoSize = true, Visible = false };
    private readonly TextBox _console = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Theme.ConsoleBg, ForeColor = Theme.ConsoleFg, Font = Theme.Mono, Dock = DockStyle.Fill };
    private readonly Button _openFolder = new() { Text = "📁 Mở thư mục", Width = 130, Height = 28, Enabled = false };
    private readonly Button _retry = new() { Text = "↻ Thử lại hồ sơ lỗi", Width = 150, Height = 28, Enabled = false };

    private IReadOnlyList<HoSo> _lastFailed = Array.Empty<HoSo>();
    private bool _running;

    public Step3RunControl(AppState state)
    {
        _state = state;
        Dock = DockStyle.Fill;
        BuildLayout();
        _pattern.TextChanged += (_, _) => UpdateExample();
        UpdateExample();
    }

    private void BuildLayout()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 170, WrapContents = true, Padding = new Padding(4) };
        var browse = new Button { Text = "Chọn…", Width = 90, Height = 26 };
        browse.Click += OnChooseFolder;

        top.Controls.Add(new Label { Text = "Thư mục lưu:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_folder);
        top.Controls.Add(browse);
        top.SetFlowBreak(browse, true);
        top.Controls.Add(new Label { Text = "Mẫu tên tệp:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_pattern);
        top.Controls.Add(_example);
        top.SetFlowBreak(_example, true);
        top.Controls.Add(_multi);
        top.Controls.Add(_overwrite);
        top.Controls.Add(_excel);
        top.Controls.Add(_skipErrors);
        top.SetFlowBreak(_skipErrors, true);
        top.Controls.Add(_progress);
        top.Controls.Add(_progressText);

        var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };
        _openFolder.Left = 4; _openFolder.Top = 6;
        _retry.Left = 144; _retry.Top = 6;
        _openFolder.Click += (_, _) => OpenFolder();
        _retry.Click += async (_, _) => await RunAsync(_lastFailed);
        bottomBar.Controls.Add(_openFolder);
        bottomBar.Controls.Add(_retry);

        Controls.Add(_console);
        Controls.Add(bottomBar);
        Controls.Add(top);
    }

    private void OnChooseFolder(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        if (dlg.ShowDialog() == DialogResult.OK) _folder.Text = dlg.SelectedPath;
    }

    private void UpdateExample()
    {
        var sample = _state.RecordsForPreview().FirstOrDefault() ?? new HoSo { SoHoSo = "42359" };
        _example.Text = "Ví dụ: " + Core.Templating.NameResolver.Build(_pattern.Text, sample, 0, DateTime.Now);
    }

    /// <summary>Run the batch for the current dataset (invoked by the nav "Tạo Mục lục" button).</summary>
    public Task RunAsync() => RunAsync(_state.RecordsForPreview());

    private async Task RunAsync(IReadOnlyList<HoSo> hoSoList)
    {
        if (_running) return;
        if (string.IsNullOrEmpty(_folder.Text)) { MessageBox.Show("Chọn thư mục lưu trước.", "Thiếu thư mục"); return; }
        if (hoSoList.Count == 0) { MessageBox.Show("Không có hồ sơ để tạo.", "Rỗng"); return; }

        _running = true;
        _openFolder.Enabled = _retry.Enabled = false;
        _console.Clear();
        _progress.Visible = _progressText.Visible = true;
        _progress.Value = 0;
        _progress.Maximum = hoSoList.Count;

        var progress = new Progress<BatchProgress>(p =>
        {
            _progress.Value = Math.Min(p.Done, _progress.Maximum);
            _progressText.Text = $"Tiến trình chạy ({p.Done}/{p.Total})";
            _console.AppendText(p.Log + Environment.NewLine);
        });

        var request = new BatchRequest
        {
            HoSoList = hoSoList,
            TemplateId = _state.Config.TemplateId,
            Mapping = _state.Config.ColMap,
            OutDir = _folder.Text,
            Pattern = _pattern.Text,
            Options = new RunOptions
            {
                MultiThread = _multi.Checked,
                Overwrite = _overwrite.Checked,
                ExportExcel = _excel.Checked,
                SkipErrors = _skipErrors.Checked,
            },
        };

        try
        {
            await using var renderer = new WebView2PrintRenderer();
            var summary = await new BatchRunner(renderer).RunAsync(request, progress);
            _lastFailed = summary.FailedHoSo;

            _console.AppendText($"— Xong: {summary.Succeeded} thành công · {summary.Skipped} bỏ qua · {summary.Failed} lỗi —{Environment.NewLine}");
            if (request.Options.ExportExcel) ExportSummary(hoSoList, summary);

            _openFolder.Enabled = true;
            _retry.Enabled = summary.Failed > 0;
        }
        catch (Exception ex)
        {
            _console.AppendText("✗ Lỗi nghiêm trọng: " + ex.Message + Environment.NewLine);
        }
        finally
        {
            _running = false;
        }
    }

    private void ExportSummary(IReadOnlyList<HoSo> hoSoList, BatchSummary summary)
    {
        try
        {
            var headers = new[] { "STT", "Số hồ sơ", "Tên tệp", "Trạng thái" };
            var rows = summary.Items.Select((it, i) => (IReadOnlyList<string>)new[]
            {
                (i + 1).ToString(),
                it.HoSo.SoHoSo,
                it.FileName,
                it.Status.ToString(),
            }).ToList();
            var path = Path.Combine(_folder.Text, "TongHop_MucLuc.xlsx");
            new ExcelExporter().Export(path, headers, rows);
            _console.AppendText("✓ Đã xuất Excel tổng hợp: " + Path.GetFileName(path) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _console.AppendText("⚠ Không xuất được Excel: " + ex.Message + Environment.NewLine);
        }
    }

    private void OpenFolder()
    {
        if (Directory.Exists(_folder.Text))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_folder.Text) { UseShellExecute = true });
    }
}
