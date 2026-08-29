using System.Runtime.InteropServices;

namespace SharkiDesktopGuardian.Services;

/// <summary>
/// Detecta clics del ratón en cualquier punto del escritorio mediante un hook de bajo nivel
/// (WH_MOUSE_LL), sin robar el foco de otras ventanas. Se usa únicamente para saber si un
/// clic cayó fuera de la mascota o del panel, no para leer ni reenviar pulsaciones.
/// </summary>
public sealed class GlobalClickWatcher : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public GlobalClickWatcher()
    {
        _proc = HookCallback;
    }

    public event Action<IntPtr>? WindowClicked;

    public void Start()
    {
        if (_hookId != IntPtr.Zero || _disposed)
        {
            return;
        }

        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);
        _hookId = SetWindowsHookEx(WhMouseLl, _proc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code >= 0 && (wParam == (IntPtr)WmLButtonDown || wParam == (IntPtr)WmRButtonDown))
            {
                var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                var hwndAtPoint = WindowFromPoint(data.Point);
                var rootHwnd = hwndAtPoint == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwndAtPoint, GaRoot);
                WindowClicked?.Invoke(rootHwnd);
            }
        }
        catch
        {
            // Un hook global nunca debe propagar excepciones: se ignora y se continúa la cadena.
        }

        return CallNextHookEx(_hookId, code, wParam, lParam);
    }

    private const uint GaRoot = 2;

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public Point Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
}
