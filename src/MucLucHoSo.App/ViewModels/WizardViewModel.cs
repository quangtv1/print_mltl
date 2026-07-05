using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MucLucHoSo.App.Services;
using MucLucHoSo.App.Shared;

namespace MucLucHoSo.App.ViewModels;

public partial class WizardViewModel : ObservableObject
{
    public SessionState Session { get; } = new();
    public CoreService Core { get; } = new();
    public PreviewService Preview { get; } = new();
    public PdfRenderService Pdf { get; } = new();

    public ObservableCollection<StepViewModel> Steps { get; }

    [ObservableProperty] private int _currentStepIndex;

    public StepViewModel CurrentStep => Steps[CurrentStepIndex];
    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;

    public WizardViewModel()
    {
        Steps = new ObservableCollection<StepViewModel>
        {
            new Step1SourceViewModel(this),
            new Step2MappingViewModel(this),
            new Step3PreviewViewModel(this),
            new Step4GenerateViewModel(this),
        };
        foreach (var s in Steps) s.PropertyChanged += OnStepPropertyChanged;
        UpdateActive();
        Preview.Warmup();   // làm nóng Word ở nền để xem trước nhanh
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsLastStep));
        UpdateActive();
        CurrentStep.OnActivated();
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }

    private void UpdateActive()
    {
        for (int i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsActive = i == CurrentStepIndex;
            Steps[i].IsCompleted = i < CurrentStepIndex;
        }
    }

    private void OnStepPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StepViewModel.CanGoNext) && ReferenceEquals(sender, CurrentStep))
            NextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanNext))]
    private void Next() { if (CurrentStepIndex < Steps.Count - 1) CurrentStepIndex++; }
    private bool CanNext() => CurrentStepIndex < Steps.Count - 1 && CurrentStep.CanGoNext;

    [RelayCommand(CanExecute = nameof(CanBack))]
    private void Back() { if (CurrentStepIndex > 0) CurrentStepIndex--; }
    private bool CanBack() => CurrentStepIndex > 0;
}
