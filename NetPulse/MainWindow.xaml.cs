using System.Windows;
using NetPulse.ViewModels;

namespace NetPulse;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Auto run initial diagnostics on start
            _ = vm.StartDiagnosticsAsync();
        }
    }
}