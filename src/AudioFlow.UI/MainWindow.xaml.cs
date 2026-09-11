using System.Windows;
using AudioFlow.UI.ViewModels;

namespace AudioFlow.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Initialize();
        Closing += (_, _) => _viewModel.Dispose();
    }
}
