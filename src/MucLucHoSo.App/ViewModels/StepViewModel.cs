using CommunityToolkit.Mvvm.ComponentModel;

namespace MucLucHoSo.App.ViewModels;

public abstract partial class StepViewModel : ObservableObject
{
    public int Number { get; }
    public string Title { get; }
    public string Description { get; }
    protected WizardViewModel Wizard { get; }

    protected StepViewModel(WizardViewModel wizard, int number, string title, string description = "")
    { Wizard = wizard; Number = number; Title = title; Description = description; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _canGoNext = true;

    /// <summary>Nút hành động chính ở thanh dưới (chỉ bước cuối dùng). Mặc định null.</summary>
    public virtual System.Windows.Input.ICommand? PrimaryCommand => null;
    public virtual string PrimaryLabel => "";

    /// <summary>Gọi mỗi khi bước được hiển thị.</summary>
    public virtual void OnActivated() { }
}
