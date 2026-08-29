using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SharkiDesktopGuardian.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Drawing.Icon _icon;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayIconService(string petName)
    {
        _menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Abrir panel");
        var hideItem = new Forms.ToolStripMenuItem("Ocultar panel");
        var exitItem = new Forms.ToolStripMenuItem("Salir");

        openItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        hideItem.Click += (_, _) => HideRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu.Items.Add(openItem);
        _menu.Items.Add(hideItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(exitItem);

        _icon = LoadIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _icon,
            Text = BuildToolTip(petName),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;

    public void UpdateName(string petName) => _notifyIcon.Text = BuildToolTip(petName);

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/sharki.ico", UriKind.Absolute));
        if (resource?.Stream is not null)
        {
            using var icon = new Drawing.Icon(resource.Stream);
            return (Drawing.Icon)icon.Clone();
        }

        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            var extracted = Drawing.Icon.ExtractAssociatedIcon(executable);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    private static string BuildToolTip(string petName)
    {
        var text = $"{petName} · Monitor activo";
        return text.Length <= 63 ? text : text[..63];
    }
}
