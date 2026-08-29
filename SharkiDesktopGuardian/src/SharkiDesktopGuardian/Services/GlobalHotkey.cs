using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SharkiDesktopGuardian.Services;

/// <summary>
/// Registra un atajo de teclado global (funciona aunque Sharki no tenga el foco) usando
/// RegisterHotKey/WM_HOTKEY sobre el HWND de una ventana ya cargada. No instala ningún
/// gancho de teclado de bajo nivel ni lee otras pulsaciones: solo recibe el aviso cuando
/// se pulsa exactamente la combinación registrada.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModControl = 0x0002;
    private const int ModAlt = 0x0001;
    private const int ModNoRepeat = 0x4000;
    private const int VkS = 0x53;
    private const int HotkeyId = 0xA1BE; // identificador arbitrario propio de Sharki

    private HwndSource? _source;
    private IntPtr _handle;
    private bool _registered;

    public event Action? Pressed;

    public void Register(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || _registered)
        {
            return;
        }

        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
        _registered = RegisterHotKey(handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkS);
        if (_registered)
        {
            _handle = handle;
        }
    }

    public void Unregister(Window window) => Unregister();

    private void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        if (_handle != IntPtr.Zero)
        {
            UnregisterHotKey(_handle, HotkeyId);
        }

        _source?.RemoveHook(WndProc);
        _source = null;
        _handle = IntPtr.Zero;
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        // Libera también el registro en Windows: sin esto, Ctrl+Alt+S seguiría
        // ocupado por este proceso aunque Sharki ya no lo escuche.
        Unregister();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
