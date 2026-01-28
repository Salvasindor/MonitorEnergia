using System.Windows;
using TFG_MonitorEnergia.WpfApp.ViewModels;

namespace TFG_MonitorEnergia.WpfApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
