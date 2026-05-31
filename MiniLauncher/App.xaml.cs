using System.Windows;

namespace MiniLauncher;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(args.Exception.ToString(), "Mini Launcher error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _mainWindow = new MainWindow();
        _mainWindow.ShowLauncher();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _mainWindow?.Dispose();
    }
}
