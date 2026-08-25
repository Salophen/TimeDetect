namespace TimeDetect.Core;

/// <summary>桌面悬浮挂件的显示层级。</summary>
public enum FloatingWindowMode
{
    Desktop,
    AlwaysOnTop
}

public static class FloatingWindowModeExtensions
{
    public static FloatingWindowMode DefaultMode => FloatingWindowMode.Desktop;

    /// <summary>从持久化的原始字符串恢复模式；缺失或非法时回退到默认桌面模式。</summary>
    public static FloatingWindowMode FromStored(string? rawValue) => rawValue switch
    {
        "desktop" => FloatingWindowMode.Desktop,
        "alwaysOnTop" => FloatingWindowMode.AlwaysOnTop,
        _ => FloatingWindowMode.Desktop
    };

    public static string ToRawValue(this FloatingWindowMode mode) => mode switch
    {
        FloatingWindowMode.AlwaysOnTop => "alwaysOnTop",
        _ => "desktop"
    };

    public static string Title(this FloatingWindowMode mode) => mode switch
    {
        FloatingWindowMode.AlwaysOnTop => "始终置顶",
        _ => "桌面模式"
    };
}
