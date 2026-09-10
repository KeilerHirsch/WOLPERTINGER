using Avalonia;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Wolpertinger.Presentation.Windows.Interop;

internal interface IWindowsNativeApi
{
    nint ReadExtendedStyle(nint handle);
    void WriteExtendedStyle(nint handle, nint style);
    void Position(nint handle, nint after, PixelRect bounds, uint flags);
}

internal sealed class WindowsNativeMethods : IWindowsNativeApi
{
    private const int ExtendedStyle = -20;
    internal static readonly nint Topmost = new(-1);
    internal const uint NoSize = 0x0001;
    internal const uint NoMove = 0x0002;
    internal const uint NoZOrder = 0x0004;
    internal const uint NoActivate = 0x0010;
    internal const uint FrameChanged = 0x0020;

    public nint ReadExtendedStyle(nint handle)
    {
        Marshal.SetLastPInvokeError(0);
        var result = IntPtr.Size == 8 ? GetWindowLongPtr(handle, ExtendedStyle) : GetWindowLong(handle, ExtendedStyle);
        ThrowIfZeroFailed(result);
        return result;
    }

    public void WriteExtendedStyle(nint handle, nint style)
    {
        Marshal.SetLastPInvokeError(0);
        var result = IntPtr.Size == 8
            ? SetWindowLongPtr(handle, ExtendedStyle, style)
            : SetWindowLong(handle, ExtendedStyle, unchecked((int)style));
        ThrowIfZeroFailed(result);
    }

    public void Position(nint handle, nint after, PixelRect bounds, uint flags)
    {
        if (!SetWindowPos(handle, after, bounds.X, bounds.Y, bounds.Width, bounds.Height, flags))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    private static void ThrowIfZeroFailed(nint result)
    {
        var error = Marshal.GetLastPInvokeError();
        if (result == 0 && error != 0)
            throw new Win32Exception(error);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    // The Ptr exports do not exist on 32-bit Windows.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
