using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MucLucHoSo.App.Shared;
using MucLucHoSo.Core.Templating;

namespace MucLucHoSo.App.ViewModels;

public partial class Step2MappingViewModel : StepViewModel
{
    public SessionState S => Wizard.Session;

    [ObservableProperty] private string _bindStatusText = "";
    [ObservableProperty] private bool _bindStatusIsOk;
    [ObservableProperty] private string _mappedCountText = "";
    [ObservableProperty] private string _validationText = "";
    [ObservableProperty] private bool _validationIsOk;
    [ObservableProperty] private bool _validated;
    [ObservableProperty] private bool _busy;

    private RuntimeTemplate? _builtFor;

    public Step2MappingViewModel(WizardViewModel w) : base(w, 2, "Ghép biến", "Ghép cột dữ liệu với biến, chọn cột gom nhóm, kiểm tra dữ liệu.")
    {
        S.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SessionState.GroupColumn)) MappingChanged();
        };
    }

    public override void OnActivated()
    {
        if (S.Runtime is null) return;
        if (!ReferenceEquals(_builtFor, S.Runtime)) BuildBindings();
        Recompute();
    }

    private void BuildBindings()
    {
        S.Bindings.Clear();
        var cols = S.Headers;
        int idx = 1;
        void Add(string v, bool isRow, bool isAuto)
        {
            var row = new VariableBindingRowViewModel(idx++, v, isRow, isAuto, cols, MappingChanged);
            if (!isAuto)
            {
                var match = cols.FirstOrDefault(c => TextUtil.Normalize(c) == TextUtil.Normalize(v))
                            ?? cols.FirstOrDefault(c => TextUtil.Normalize(c).Contains(TextUtil.Normalize(v)));
                if (match != null) row.Value = match;   // tự khớp cột; nếu không có, để trống -> người dùng gõ hằng
            }
            S.Bindings.Add(row);
        }
        foreach (var v in S.Runtime!.HeaderFields.OrderBy(x => x)) Add(v, false, false);
        foreach (var v in S.Runtime!.RowFields.OrderBy(x => x)) Add(v, true, false);
        foreach (var v in S.Runtime!.AutoFields.OrderBy(x => x)) Add(v, false, true);

        S.GroupColumn ??= S.Headers.FirstOrDefault(c => TextUtil.Normalize(c).Contains("hoso"))
                          ?? S.Headers.FirstOrDefault();
        _builtFor = S.Runtime;
    }

    /// <summary>Mapping/cột gom nhóm vừa đổi → phải kiểm tra lại trước khi qua bước sau.</summary>
    private void MappingChanged()
    {
        Validated = false;
        ValidationIsOk = false;
        ValidationText = "";
        Recompute();
    }

    public void Recompute()
    {
        int total = S.Bindings.Count;
        int bound = S.Bindings.Count(b => b.IsBound);
        bool groupOk = !string.IsNullOrEmpty(S.GroupColumn);
        bool allBound = bound == total && total > 0 && groupOk;
        BindStatusText = allBound
            ? (Validated && ValidationIsOk ? $"✓ Đã ghép đủ {total} biến — dữ liệu hợp lệ."
                                           : $"✓ Đã ghép đủ {total} biến · hãy bấm Kiểm tra dữ liệu.")
            : $"Đã ghép {bound}/{total} biến" + (groupOk ? "." : " · chưa chọn cột gom nhóm.");
        BindStatusIsOk = allBound && Validated && ValidationIsOk;
        MappedCountText = $"(đã ghép {bound}/{total})";
        // Bắt buộc chạy Validation hợp lệ mới cho qua bước sau.
        CanGoNext = allBound && Validated && ValidationIsOk;
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        if (S.Runtime is null) return;
        Busy = true; ValidationText = "Đang kiểm tra toàn bộ dữ liệu…";
        try
        {
            var res = await Task.Run(() => Wizard.Core.Validate(S));
            ValidationText = res.ToString();
            ValidationIsOk = res.IsValid;
            Validated = true;
            S.ValidatedGroupCount = res.TotalGroups;   // tổng hồ sơ cho thanh tiến trình Bước 4
        }
        catch (Exception ex) { ValidationText = "Lỗi: " + ex.Message; ValidationIsOk = false; Validated = false; }
        finally { Busy = false; Recompute(); }   // cập nhật CanGoNext theo kết quả kiểm tra
    }
}
