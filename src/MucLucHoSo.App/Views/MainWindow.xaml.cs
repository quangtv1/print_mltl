using System.Windows;
using MucLucHoSo.App.ViewModels;

namespace MucLucHoSo.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new WizardViewModel();
    }
}
