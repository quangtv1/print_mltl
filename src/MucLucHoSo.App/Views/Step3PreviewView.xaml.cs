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
    }

    private Step3PreviewViewModel? Vm => DataContext as Step3PreviewViewModel;

    // Ctrl + lăn chuột = phóng to / thu nhỏ (chuẩn trình xem tài liệu).
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || Vm is null) return;
        Exec(e.Delta > 0 ? Vm.ZoomInCommand : Vm.ZoomOutCommand);
        e.Handled = true;
    }

    // Phím tắt: Ctrl ←/→ đổi hồ sơ; Ctrl +/−/0 điều chỉnh zoom.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm is null || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.Left: Exec(Vm.PrevCommand); e.Handled = true; break;
            case Key.Right: Exec(Vm.NextCommand); e.Handled = true; break;
            case Key.Add or Key.OemPlus: Exec(Vm.ZoomInCommand); e.Handled = true; break;
            case Key.Subtract or Key.OemMinus: Exec(Vm.ZoomOutCommand); e.Handled = true; break;
            case Key.D0 or Key.NumPad0: Exec(Vm.ZoomResetCommand); e.Handled = true; break;
        }
    }

    private static void Exec(ICommand c) { if (c.CanExecute(null)) c.Execute(null); }
}
