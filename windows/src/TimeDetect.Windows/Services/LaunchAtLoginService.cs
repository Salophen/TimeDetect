using System;
using Microsoft.Win32;

namespace TimeDetect.Services;

/// <summary>登录 Windows 时自动启动（注册表 Run 键），等价 macOS 版 SMAppService。</summary>
public static class LaunchAtLoginService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TimeDetect";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Environment.ProcessPath ?? "");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
