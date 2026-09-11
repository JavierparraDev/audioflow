using System.Windows;
using System.Windows.Threading;
using AudioFlow.Core.Logging;

namespace AudioFlow.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error($"Unhandled domain exception: {args.ExceptionObject}");

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "UI unhandled exception");
        e.Handled = true;
    }
}
