using System.Drawing;
using System.Windows;
using AudioFlow.UI.Localization;
using Forms = System.Windows.Forms;

namespace AudioFlow.UI.Services;

/// <summary>
/// Tray icon built on the built-in WinForms NotifyIcon (no external package).
/// Closing the window minimizes to the tray unless the user opts to close
/// completely; Exit always restores Windows audio.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Window _window;

    public TrayIconService(
        Window window,
        Action onOpen,
        Action onStopRouting,
        Action onDiagnostics,
        Action onExit)
    {
        _window = window;

        Icon icon;
        try
        {
            icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
        }
        catch
        {
            icon = SystemIcons.Application;
        }

        _icon = new Forms.NotifyIcon
        {
            Text = "AudioFlow",
            Visible = true,
            Icon = icon
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Loc.Get("TrayOpen"), null, (_, _) => onOpen());
        menu.Items.Add(Loc.Get("TrayStopRouting"), null, (_, _) => onStopRouting());
        menu.Items.Add(Loc.Get("TrayDiagnostics"), null, (_, _) => onDiagnostics());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Loc.Get("TrayExit"), null, (_, _) => onExit());

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => onOpen();
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            _icon.ShowBalloonTip(3000, title, text, Forms.ToolTipIcon.Info);
        }
        catch
        {
            // Balloons are best effort.
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
