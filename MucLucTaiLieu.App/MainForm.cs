using System.Drawing;
using MucLucTaiLieu.App.Forms;

namespace MucLucTaiLieu.App;

/// <summary>
/// Wizard shell (mota3 §2.2): step header + swappable content + nav bar
/// (◀ Quay lại / Tiếp theo ▶ / "Tạo Mục lục"). Owns the shared <see cref="AppState"/>.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppState _state = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
    private readonly Label[] _stepLabels = new Label[3];
    private readonly Button _back = new() { Text = "◀ Quay lại", Width = 120, Height = 34 };
    private readonly Button _next = new() { Text = "Tiếp theo ▶", Width = 140, Height = 34 };

    private Step1InputControl _step1 = null!;
    private Step2DesignControl _step2 = null!;
    private Step3RunControl _step3 = null!;
    private int _current;

    private static readonly string[] StepTitles = { "1 · Đầu vào", "2 · Thiết kế", "3 · Chạy" };

    public MainForm()
    {
        Text = "Tạo Mục Lục Hồ Sơ";
        Width = 1240;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Ui;

        BuildHeader();
        BuildNavBar();
        Controls.Add(_content);

        _step1 = new Step1InputControl(_state);
        _step2 = new Step2DesignControl(_state);
        _step3 = new Step3RunControl(_state);
        _step1.ValidityChanged += (_, _) => UpdateNav();

        GoTo(0);
    }

    private void BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.PanelBg };
        for (var i = 0; i < 3; i++)
        {
            var lbl = new Label
            {
                Text = StepTitles[i],
                AutoSize = false,
                Width = 200,
                Height = 40,
                Left = 16 + i * 220,
                Top = 8,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.UiBold,
            };
            _stepLabels[i] = lbl;
            header.Controls.Add(lbl);
        }
        Controls.Add(header);
    }

    private void BuildNavBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 8, 12, 8) };
        Theme.Primary(_next);
        _back.FlatStyle = FlatStyle.Flat;
        _back.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        _next.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _back.Left = 12; _back.Top = 8;
        _next.Left = bar.Width - _next.Width - 12; _next.Top = 8;
        _next.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _back.Click += (_, _) => GoTo(_current - 1);
        _next.Click += async (_, _) => await OnNextAsync();

        bar.Controls.Add(_back);
        bar.Controls.Add(_next);
        bar.Resize += (_, _) => _next.Left = bar.Width - _next.Width - 12;
        Controls.Add(bar);
    }

    private async Task OnNextAsync()
    {
        if (_current == 2)
        {
            await _step3.RunAsync();
            return;
        }
        GoTo(_current + 1);
        if (_current == 1) await _step2.EnterAsync();
    }

    private void GoTo(int step)
    {
        _current = Math.Clamp(step, 0, 2);
        _content.Controls.Clear();
        UserControl active = _current switch
        {
            0 => _step1,
            1 => _step2,
            _ => _step3,
        };
        active.Dock = DockStyle.Fill;
        _content.Controls.Add(active);
        UpdateNav();
    }

    private void UpdateNav()
    {
        _back.Enabled = _current > 0;
        _next.Text = _current == 2 ? "Tạo Mục lục" : "Tiếp theo ▶";
        // Gate leaving Step 1 until data is read and every variable is mapped (mota3 §6.5).
        _next.Enabled = _current != 0 || _step1.IsValid;

        for (var i = 0; i < 3; i++)
        {
            _stepLabels[i].ForeColor = i == _current ? Theme.Accent : Color.Gray;
            var badge = i < _current ? "✓ " : "";
            _stepLabels[i].Text = badge + StepTitles[i];
        }
    }
}
