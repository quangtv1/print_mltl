using System.Drawing;
using MucLucTaiLieu.Core.Excel;
using MucLucTaiLieu.Core.Text;

namespace MucLucTaiLieu.App.Forms;

/// <summary>
/// Step 1 — Đầu vào (mota3 §6): choose Excel + sheet, read data, pick template, map
/// variables↔columns (auto-match, no duplicate-column warning), choose grouping column
/// (shows how many PDFs will be created). Gates advancing until data is read and every
/// variable is mapped.
/// </summary>
public sealed class Step1InputControl : UserControl
{
    private readonly AppState _state;
    private readonly IExcelReader _reader = new ClosedXmlReader();

    private readonly TextBox _file = new() { ReadOnly = true, Width = 360 };
    private readonly ComboBox _sheet = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Button _read = new() { Text = "Đọc dữ liệu", Width = 120, Height = 28 };
    private readonly Label _readStatus = new() { AutoSize = true, Text = "Chưa đọc dữ liệu…", ForeColor = Color.Gray };
    private readonly ComboBox _template = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Label _varCount = new() { AutoSize = true, ForeColor = Color.Gray };
    private readonly Label _banner = new() { AutoSize = false, Height = 30, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly ComboBox _groupCol = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Label _fileCount = new() { AutoSize = true, ForeColor = Theme.Accent, Font = Theme.UiBold };

    private CancellationTokenSource? _readCts;

    public event EventHandler? ValidityChanged;
    public bool IsValid => _state.Data is not null && UnmappedCount() == 0;

    public Step1InputControl(AppState state)
    {
        _state = state;
        Dock = DockStyle.Fill;
        BuildLayout();
        LoadTemplates();
        ApplyConfig();
        UpdateBanner();
    }

    private void BuildLayout()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 96, WrapContents = true, Padding = new Padding(4) };
        var browse = new Button { Text = "Duyệt…", Width = 90, Height = 28 };
        browse.Click += OnBrowse;
        Theme.Primary(_read, Theme.ReadCyan);
        _read.ForeColor = Color.Black;
        _read.Enabled = false;
        _read.Click += async (_, _) => await ReadDataAsync();
        _sheet.SelectedIndexChanged += (_, _) => _read.Enabled = _sheet.SelectedItem is not null;

        top.Controls.Add(new Label { Text = "Tệp:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_file);
        top.Controls.Add(browse);
        top.Controls.Add(new Label { Text = "Sheet:", AutoSize = true, Padding = new Padding(12, 6, 0, 0) });
        top.Controls.Add(_sheet);
        top.Controls.Add(_read);
        top.Controls.Add(_readStatus);
        top.SetFlowBreak(_readStatus, true);
        top.Controls.Add(new Label { Text = "Mẫu:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        top.Controls.Add(_template);
        top.Controls.Add(_varCount);
        _template.SelectedIndexChanged += OnTemplateChanged;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(4) };
        bottom.Controls.Add(new Label { Text = "Cột gom nhóm:", AutoSize = true, Left = 4, Top = 12 });
        _groupCol.Left = 110; _groupCol.Top = 8;
        _fileCount.Left = 384; _fileCount.Top = 12;
        _groupCol.SelectedIndexChanged += (_, _) => { _state.Config.GroupCol = GroupColValue(); UpdateFileCount(); _state.Save(); };
        bottom.Controls.Add(_groupCol);
        bottom.Controls.Add(_fileCount);

        BuildGrid();

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(_banner);
        Controls.Add(top);
    }

    private void BuildGrid()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", FillWeight = 6, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Cột trong Excel", Name = "col", FillWeight = 40, FlatStyle = FlatStyle.Flat });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "→", FillWeight = 6, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Biến trong mẫu", Name = "var", FillWeight = 30, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tự khớp", Name = "auto", FillWeight = 18, ReadOnly = true });
        _grid.CellValueChanged += OnMappingCellChanged;
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
    }

    private void LoadTemplates()
    {
        _template.Items.Clear();
        foreach (var t in _state.Catalog.All) _template.Items.Add(t.Name);
    }

    private void ApplyConfig()
    {
        var idx = _state.Catalog.All.ToList().FindIndex(t => t.Id == _state.Config.TemplateId);
        _template.SelectedIndex = Math.Max(0, idx);
        RebuildMappingRows();
    }

    private void OnBrowse(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _file.Text = dlg.FileName;
        try
        {
            _sheet.Items.Clear();
            foreach (var s in _reader.ListSheets(dlg.FileName)) _sheet.Items.Add(s);
            if (_sheet.Items.Count > 0) _sheet.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi đọc sheet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task ReadDataAsync()
    {
        if (_sheet.SelectedItem is null) return;
        _readCts?.Cancel();
        _readCts = new CancellationTokenSource();
        var path = _file.Text;
        var sheet = _sheet.SelectedItem!.ToString()!;

        _read.Enabled = false;
        _readStatus.Text = "Đang đọc…";
        _readStatus.ForeColor = Color.Gray;
        try
        {
            var data = await Task.Run(() => _reader.Read(path, sheet), _readCts.Token);
            _state.SetData(path, sheet, data);
            _readStatus.Text = $"✓ Đã đọc {data.Rows.Count} dòng · {data.Headers.Count} cột";
            _readStatus.ForeColor = Theme.OkGreen;

            PopulateColumnChoices(data.Headers);
            AutoMatch(data.Headers);
            PopulateGroupColumns(data.Headers);
            UpdateBanner();
        }
        catch (OperationCanceledException) { /* superseded by a newer read */ }
        catch (Exception ex)
        {
            _readStatus.Text = "✗ " + ex.Message;
            _readStatus.ForeColor = Theme.ErrRed;
        }
        finally
        {
            _read.Enabled = true;
        }
    }

    private void OnTemplateChanged(object? sender, EventArgs e)
    {
        if (_template.SelectedIndex < 0) return;
        _state.Config.TemplateId = _state.Catalog.All[_template.SelectedIndex].Id;
        _state.Save();
        RebuildMappingRows();
        if (_state.Data is not null)
        {
            PopulateColumnChoices(_state.Data.Headers);
            AutoMatch(_state.Data.Headers);
        }
        UpdateBanner();
    }

    private void RebuildMappingRows()
    {
        _grid.Rows.Clear();
        var vars = _state.CurrentTemplate.InputVars.ToList();
        _varCount.Text = $"({vars.Count} biến đầu vào)";
        var n = 1;
        foreach (var v in vars)
        {
            var row = new DataGridViewRow();
            row.CreateCells(_grid, n.ToString(), "", "→", v.V, "⚠ chưa gán");
            _grid.Rows.Add(row);
            n++;
        }
    }

    private void PopulateColumnChoices(IReadOnlyList<string> headers)
    {
        var choices = new List<string> { "— (chưa gán) —" };
        choices.AddRange(headers);
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Cells["col"] is DataGridViewComboBoxCell cell)
            {
                cell.Items.Clear();
                foreach (var c in choices) cell.Items.Add(c);
            }
        }
    }

    private void AutoMatch(IReadOnlyList<string> headers)
    {
        var suggestions = HeaderMatch.AutoMatch(_state.CurrentTemplate.InputVars, headers);
        _state.Config.ColMap.Clear();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var token = row.Cells["var"].Value?.ToString() ?? "";
            if (suggestions.TryGetValue(token, out var header))
            {
                row.Cells["col"].Value = header;
                _state.Config.ColMap[token] = header;
                SetAutoCell(row, true);
            }
            else
            {
                row.Cells["col"].Value = "— (chưa gán) —";
                SetAutoCell(row, false);
            }
        }
        _state.Save();
        UpdateBanner();
    }

    private void OnMappingCellChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "col") return;
        var row = _grid.Rows[e.RowIndex];
        var token = row.Cells["var"].Value?.ToString() ?? "";
        var value = row.Cells["col"].Value?.ToString() ?? "";
        var mapped = value.Length > 0 && !value.StartsWith("—");
        if (mapped) _state.Config.ColMap[token] = value;
        else _state.Config.ColMap.Remove(token);
        SetAutoCell(row, mapped);
        _state.Save();
        UpdateBanner();
    }

    private static void SetAutoCell(DataGridViewRow row, bool ok)
    {
        var cell = row.Cells["auto"];
        cell.Value = ok ? "✓ khớp" : "⚠ chưa gán";
        cell.Style.ForeColor = ok ? Theme.OkGreen : Theme.WarnAmber;
    }

    private int UnmappedCount() => _state.CurrentTemplate.InputVars.Count(v => !_state.Config.ColMap.ContainsKey(v.V));

    private void PopulateGroupColumns(IReadOnlyList<string> headers)
    {
        _groupCol.Items.Clear();
        _groupCol.Items.Add("(không gom nhóm)");
        foreach (var h in headers) _groupCol.Items.Add(h);
        // Default to "Số, ký hiệu hồ sơ" when present (mota3 §6.4).
        var def = headers.ToList().FindIndex(h => h.Contains("Số, ký hiệu hồ sơ"));
        _groupCol.SelectedIndex = def >= 0 ? def + 1 : 0;
    }

    private string GroupColValue() => _groupCol.SelectedIndex > 0 ? _groupCol.SelectedItem!.ToString()! : "";

    private void UpdateFileCount()
    {
        if (_state.Data is null) { _fileCount.Text = ""; return; }
        var col = GroupColValue();
        if (col.Length == 0)
        {
            _fileCount.Text = "→ gộp tất cả hồ sơ vào 1 file PDF";
            return;
        }
        var distinct = _state.Data.Rows
            .Select(r => r.TryGetValue(col, out var v) ? v : "")
            .Distinct(StringComparer.Ordinal).Count();
        _fileCount.Text = $"→ sẽ tạo {distinct:N0} file PDF (mỗi giá trị của cột này 1 file)";
    }

    private void UpdateBanner()
    {
        var unmapped = UnmappedCount();
        if (_state.Data is null)
        {
            _banner.Text = "Chưa đọc dữ liệu — chọn tệp Excel và bấm \"Đọc dữ liệu\".";
            _banner.BackColor = Color.FromArgb(255, 250, 205);
        }
        else if (unmapped == 0)
        {
            _banner.Text = $"✓ Đã ghép đủ {_state.CurrentTemplate.InputVars.Count()} biến.";
            _banner.BackColor = Color.FromArgb(220, 245, 220);
        }
        else
        {
            _banner.Text = $"⚠ {unmapped} biến chưa gán — sửa xong mới sang được bước sau.";
            _banner.BackColor = Color.FromArgb(250, 220, 220);
        }
        UpdateFileCount();
        ValidityChanged?.Invoke(this, EventArgs.Empty);
    }
}
