using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SharkiDesktopGuardian.Services;

/// <summary>
/// Lanza un conjunto fijo y muy reducido de utilidades estándar de Windows (Explorador,
/// Calculadora, Bloc de notas, Administrador de tareas, Configuración, Papelera) y dos
/// acciones de escritorio (mostrar escritorio, bloquear sesión). Cada acción corresponde a
/// exactamente un ejecutable o API conocida, sin argumentos ni rutas derivadas de texto
/// reconocido por voz; nunca invoca cmd.exe, PowerShell ni una línea de comandos arbitraria.
/// </summary>
public static class SystemShortcutLauncher
{
    public static bool OpenFileExplorer() => TryStart("explorer.exe");

    public static bool OpenCalculator() => TryStart("calc.exe");

    public static bool OpenNotepad() => TryStart("notepad.exe");

    public static bool OpenTaskManager() => TryStart("taskmgr.exe");

    public static bool OpenSettings() => TryStart("ms-settings:");

    public static bool OpenRecycleBin() => TryStart("explorer.exe", "shell:RecycleBinFolder");

    public static bool ShowDesktop() => TryStart("explorer.exe", "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}");

    public static bool LockComputer() => LockWorkStation();

    private static bool TryStart(string fileName, string? arguments = null)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();
}
