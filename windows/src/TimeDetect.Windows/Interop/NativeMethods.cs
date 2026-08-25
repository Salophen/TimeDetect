using System;
using System.Runtime.InteropServices;

namespace TimeDetect.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>给窗口加上 toolwindow 样式（不出现在 Alt-Tab / 任务栏），可选禁止激活。</summary>
    public static void ApplyToolWindowStyle(IntPtr hwnd, bool noActivate)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW;
        if (noActivate) ex |= WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
    }
}
