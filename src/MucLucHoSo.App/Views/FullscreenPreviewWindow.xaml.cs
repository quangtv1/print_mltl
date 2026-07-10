using System.Windows;
using System.Windows.Input;
using MucLucHoSo.App.ViewModels;

namespace MucLucHoSo.App.Views;

/// <summary>
/// Cửa sổ toàn màn hình xem ảnh render. Dùng chung Step3PreviewViewModel với màn Xem trước:
/// điều hướng hồ sơ (←/→) tự cập nhật PreviewPages → ảnh đổi theo. Esc để thoát.
/// </summary>
public partial class FullscreenPreviewWindow : Window
{
    public FullscreenPreviewWindow() => InitializeComponent();

    private Step3PreviewViewModel? Vm => DataContext as Step3PreviewViewModel;

    // ← / Ctrl+← : hồ sơ trước · → / Ctrl+→ : hồ sơ sau · Esc: thoát (bỏ qua phím tu chỉnh).
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Vm is null) return;
        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.Left: Exec(Vm.PrevCommand); e.Handled = true; break;
            case Key.Right: Exec(Vm.NextCommand); e.Handled = true; break;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static void Exec(ICommand c) { if (c.CanExecute(null)) c.Execute(null); }
}
