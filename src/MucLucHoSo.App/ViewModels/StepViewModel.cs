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
    /// <summary>Màu nền nút chính (đổi động ở Bước 4: đỏ khi Tạm dừng, xanh khi Chạy tiếp).</summary>
    public virtual System.Windows.Media.Brush PrimaryBackground => Brushes.Accent;

    /// <summary>Bảng màu nút chính (đóng băng để chia sẻ an toàn).</summary>
    protected static class Brushes
    {
        public static readonly System.Windows.Media.Brush Accent = Frozen(0x00, 0x43, 0xA5);
        public static readonly System.Windows.Media.Brush Danger = Frozen(0xD1, 0x3A, 0x3A);
        public static readonly System.Windows.Media.Brush Success = Frozen(0x1F, 0x9D, 0x55);
        private static System.Windows.Media.Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>Gọi mỗi khi bước được hiển thị.</summary>
    public virtual void OnActivated() { }
}
