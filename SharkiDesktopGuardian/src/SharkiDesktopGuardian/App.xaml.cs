using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using SharkiDesktopGuardian.Models;
using SharkiDesktopGuardian.Services;

namespace SharkiDesktopGuardian;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private HardwareMonitorService? _hardwareMonitor;
    private LocalVoiceService? _voiceService;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);

        _settingsService = new SettingsService();
        var settings = await _settingsService.LoadAsync();

        if (settings.AlwaysRunElevated && !IsRunningElevated() && TryRestartElevated())
        {
            Shutdown();
            return;
        }

        var router = new SafeCommandRouter();
        _voiceService = new LocalVoiceService();
        _hardwareMonitor = new HardwareMonitorService(settings);

        _mainWindow = new MainWindow(
            settings,
            _settingsService,
            _hardwareMonitor,
            new AlertEvaluator(),
            router,
            _voiceService);
        MainWindow = _mainWindow;
        _mainWindow.Show();
        _hardwareMonitor.Start();
    }

    public async Task ShutdownSafelyAsync()
    {
        if (_hardwareMonitor is not null)
        {
            await _hardwareMonitor.DisposeAsync();
            _hardwareMonitor = null;
        }

        _voiceService?.Dispose();
        _voiceService = null;
        Shutdown();
    }

    private static bool IsRunningElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Intenta relanzar el propio ejecutable elevado. Windows siempre pide confirmación
    /// mediante UAC; si el usuario la cancela, devuelve false y la aplicación sigue
    /// arrancando normalmente sin permisos elevados.
    /// </summary>
    private static bool TryRestartElevated()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
