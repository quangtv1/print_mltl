using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MucLucHoSo.App;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobserved;
    }

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "MLHS_crash.log");

    private static void Log(Exception ex, string src)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:u}] {src}\n{ex}\n\n"); } catch { }
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception, "Dispatcher");
        MessageBox.Show(e.Exception.Message + $"\n\nChi tiết đã ghi vào:\n{LogPath}",
            "Đã xảy ra lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;   // không cho ứng dụng tự thoát
    }

    private void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) Log(ex, "AppDomain");
    }

    private void OnUnobserved(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        Log(e.Exception, "UnobservedTask");
        e.SetObserved();
    }
}
