using System.Windows;
using System.Windows.Media;
using TimeDetect.Core;

namespace TimeDetect.UI;

/// <summary>视觉主题：峰时暖橙（锋芒），谷时青绿（省流）。固定 sRGB 值保证观感一致。</summary>
public sealed class PhaseTheme
{
    public SolidColorBrush AccentStart { get; }
    public SolidColorBrush AccentEnd { get; }
    public SolidColorBrush Glow { get; }
    public SolidColorBrush TrackDim { get; }
    public LinearGradientBrush Gradient { get; }
    public LinearGradientBrush BarGradient { get; }

    private PhaseTheme(Color accentStart, Color accentEnd, Color glow)
    {
        AccentStart = accentStart.Brush();
        AccentEnd = accentEnd.Brush();
        Glow = glow.Brush();
        TrackDim = glow.WithAlpha(0.16).Brush();
        Gradient = new LinearGradientBrush(accentStart, accentEnd, new Point(0, 0), new Point(1, 1));
        BarGradient = new LinearGradientBrush(accentStart, accentEnd, new Point(0, 0.5), new Point(1, 0.5));
        AccentStart.Freeze(); AccentEnd.Freeze(); Glow.Freeze(); TrackDim.Freeze();
        Gradient.Freeze(); BarGradient.Freeze();
    }

    public static PhaseTheme Peak { get; } = new(
        Color.FromRgb(255, 191, 89),
        Color.FromRgb(255, 99, 92),
        Color.FromRgb(255, 130, 84));

    public static PhaseTheme OffPeak { get; } = new(
        Color.FromRgb(92, 235, 181),
        Color.FromRgb(51, 168, 255),
        Color.FromRgb(64, 212, 217));

    public static PhaseTheme For(PricePhase phase) => phase == PricePhase.Peak ? Peak : OffPeak;

    /// <summary>周末全天使用统一的蓝色时间条，不与工作日的峰谷颜色混淆。</summary>
    public static readonly LinearGradientBrush WeekendBarGradient = CreateHorizontal(
        Color.FromRgb(71, 148, 255), Color.FromRgb(31, 92, 235));

    private static LinearGradientBrush CreateHorizontal(Color start, Color end)
    {
        var brush = new LinearGradientBrush(start, end, new Point(0, 0.5), new Point(1, 0.5));
        brush.Freeze();
        return brush;
    }
}

public static class ColorHelpers
{
    public static Color WithAlpha(this Color color, double alpha) =>
        Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B);

    public static SolidColorBrush Brush(this Color color) => new SolidColorBrush(color);

    public static SolidColorBrush BrushWithAlpha(this Color color, double alpha) =>
        new SolidColorBrush(color.WithAlpha(alpha));

    public static SolidColorBrush WithAlpha(this SolidColorBrush brush, double alpha) =>
        new SolidColorBrush(brush.Color.WithAlpha(alpha));
}
