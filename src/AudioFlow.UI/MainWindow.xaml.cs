using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AudioFlow.Core.Logging;
using AudioFlow.UI.ViewModels;

namespace AudioFlow.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        Log.Info("MainWindow: InitializeComponent");
        InitializeComponent();
        Log.Info("MainWindow: XAML loaded");

        DataContext = _viewModel;
        _viewModel.Initialize();

        Loaded += (_, _) => Log.Info("MainWindow: Loaded");
        Closing += (_, _) => _viewModel.Dispose();
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
