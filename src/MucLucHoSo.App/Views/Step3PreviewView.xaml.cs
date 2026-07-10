using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MucLucHoSo.App.ViewModels;

namespace MucLucHoSo.App.Views;

public partial class Step3PreviewView : UserControl
{
    public Step3PreviewView()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
        PreviewKeyDown += OnPreviewKeyDown;
        // Lấy focus khi màn hiện ra để phím tắt (Ctrl ←/→, Ctrl/Shift+Enter) chạy ngay, không cần click trước.
        // Hoãn qua Dispatcher (Input) để né đua thời điểm khi cây trực quan chưa sẵn sàng nhận focus.
        Loaded += (_, _) => FocusSelfDeferred();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) FocusSelfDeferred(); };
    }

    private Step3PreviewViewModel? Vm => DataContext as Step3PreviewViewModel;

    private void FocusSelfDeferred() =>
        Dispatcher.BeginInvoke(new Action(() => Focus()), System.Windows.Threading.DispatcherPriority.Input);

    // Ctrl + lăn chuột = phóng to / thu nhỏ (chuẩn trình xem tài liệu).
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || Vm is null) return;
        Exec(e.Delta > 0 ? Vm.ZoomInCommand : Vm.ZoomOutCommand);
        e.Handled = true;
    }

    // Phím tắt màn Xem trước: Ctrl+F toàn màn hình; Ctrl ←/→ đổi hồ sơ; Ctrl +/−/0 zoom.
    // (Ctrl+Enter/Shift+Enter điều hướng wizard xử lý toàn cục ở MainWindow.)
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.F: OpenFullscreen(); e.Handled = true; break;
            case Key.Left: Exec(Vm.PrevCommand); e.Handled = true; break;
            case Key.Right: Exec(Vm.NextCommand); e.Handled = true; break;
            case Key.Add or Key.OemPlus: Exec(Vm.ZoomInCommand); e.Handled = true; break;
            case Key.Subtract or Key.OemMinus: Exec(Vm.ZoomOutCommand); e.Handled = true; break;
            case Key.D0 or Key.NumPad0: Exec(Vm.ZoomResetCommand); e.Handled = true; break;
        }
    }

    private static void Exec(ICommand c) { if (c.CanExecute(null)) c.Execute(null); }

    // Mở cửa sổ toàn màn hình xem ảnh render (dùng chung VM → điều hướng hồ sơ tự đồng bộ preview).
    private void OnFullscreenClick(object sender, RoutedEventArgs e) => OpenFullscreen();

    private void OpenFullscreen()
    {
        if (Vm is null || !Vm.HasPreview) return;
        new FullscreenPreviewWindow { DataContext = Vm, Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
