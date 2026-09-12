using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AudioFlow.Core.Logging;
using AudioFlow.UI.Services;
using AudioFlow.UI.ViewModels;
using Application = System.Windows.Application;

namespace AudioFlow.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private TrayIconService? _tray;
    private bool _allowClose;

    public MainWindow()
    {
        Log.Info("MainWindow: InitializeComponent");
        InitializeComponent();
        Log.Info("MainWindow: XAML loaded");

        DataContext = _viewModel;
        _viewModel.Initialize();

        Loaded += OnLoaded;
        Closing += OnClosing;

        if (Application.Current is { } app)
        {
            // Windows shutdown/logoff: always restore before the process ends.
            app.SessionEnding += (_, _) => _viewModel.Dispose();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Log.Info("MainWindow: Loaded");
        _tray = new TrayIconService(this, OpenFromTray, StopRouting, OpenDiagnostics, ExitFromTray);
    }

    private void OpenFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void StopRouting() => _viewModel.StopRouting();

    private void OpenDiagnostics()
    {
        OpenFromTray();
        NavTabs.SelectedIndex = 4; // Diagnostics
    }

    private void ExitFromTray()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose || _viewModel.CloseCompletely)
        {
            _allowClose = true;
            Cleanup();
            return;
        }

        // Minimize to tray; routing stays active until the user exits.
        e.Cancel = true;
        Hide();
        _tray?.ShowBalloon(
            "AudioFlow",
            "AudioFlow is still running in the tray. Windows audio will be restored when you exit.");
    }

    private void Cleanup()
    {
        _tray?.Dispose();
        _tray = null;
        _viewModel.Dispose();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Enable the immersive dark title bar on Windows 10/11.
        try
        {
            var enabled = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref enabled, sizeof(int));
        }
        catch
        {
            // Older Windows versions do not support this attribute.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
