using System;
using System.Windows;
using System.Windows.Media;
using TimeDetect.Core;

namespace TimeDetect.UI;

/// <summary>24 小时峰谷时间轴：细线条 + 当前时刻指针。纯几何绘制，重绘成本极低。</summary>
public sealed class DayTimelineBar : FrameworkElement
{
    private PhaseSnapshot _snapshot;
    private double _barHeight = 6;
    private bool _showsHourTicks = true;

    public PhaseSnapshot Snapshot { get => _snapshot; set { _snapshot = value; InvalidateVisual(); } }
    public double BarHeight { get => _barHeight; set { _barHeight = value; InvalidateVisual(); } }
    public bool ShowsHourTicks { get => _showsHourTicks; set { _showsHourTicks = value; InvalidateVisual(); } }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth;
        double h = _barHeight;
        double top = (RenderSize.Height - h) / 2;
        bool isWeekend = !PeakEngine.IsWeekday(_snapshot.Date);
        var windows = isWeekend
            ? new[] { new PhaseWindow(0, 1440, PricePhase.OffPeak) }
            : PeakEngine.DayWindows;

        // 底轨
        dc.PushOpacity(0.08);
        DrawCapsule(dc, new Rect(0, top, width, h), Brushes.White);
        dc.Pop();

        // 完整绘制峰谷时段；暖橙代表峰时，青蓝代表谷时。
        foreach (var window in windows)
        {
            double x = width * window.StartMinute / 1440;
            double w = Math.Max(width * window.LengthInMinutes / 1440, 2);
            Brush fill = isWeekend ? PhaseTheme.WeekendBarGradient : PhaseTheme.For(window.Phase).BarGradient;
            double opacity = isWeekend || _snapshot.Phase == window.Phase ? 0.96 : 0.58;
            dc.PushOpacity(opacity);
            DrawCapsule(dc, new Rect(x, top, w, h), fill);
            dc.Pop();
        }

        // 整点刻度（06/12/18）
        if (_showsHourTicks)
        {
            dc.PushOpacity(0.18);
            foreach (int hour in new[] { 6, 12, 18 })
            {
                double x = width * hour / 24;
                dc.DrawRectangle(Brushes.White, null, new Rect(x - 0.5, top - 2, 1, h + 4));
            }
            dc.Pop();
        }

        // 当前时刻指针 + 微光
        double pointerX = Math.Max(0, Math.Min(width - 2.5, width * _snapshot.DayProgress));
        var glow = PhaseTheme.For(_snapshot.Phase).Glow;
        dc.PushOpacity(0.9);
        DrawCapsule(dc, new Rect(pointerX - 1.5, top - 4, 5.5, h + 8), glow);
        dc.Pop();
        DrawCapsule(dc, new Rect(pointerX, top - 4, 2.5, h + 8), Brushes.White);
    }

    private static void DrawCapsule(DrawingContext dc, Rect rect, Brush fill)
    {
        double radius = rect.Height / 2;
        dc.DrawRoundedRectangle(fill, null, rect, radius, radius);
    }
}
