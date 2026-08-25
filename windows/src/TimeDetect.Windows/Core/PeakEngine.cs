using System;
using System.Collections.Generic;

namespace TimeDetect.Core;

/// <summary>
/// DeepSeek 计费时段。
/// 官方规则按 UTC 发布：周一至周五 01:00-04:00、06:00-10:00 UTC 为峰时，
/// 换算为北京时间即工作日 09:00-12:00、14:00-18:00；周末全天为空闲时段。
/// 空闲时段价格为高峰时段的一半。
/// </summary>
public enum PricePhase
{
    Peak,    // 高峰时段 —— 梁文锋
    OffPeak  // 空闲时段 —— 梁文谷
}

public static class PricePhaseExtensions
{
    public static string PersonaName(this PricePhase phase) =>
        phase == PricePhase.Peak ? "梁文锋" : "梁文谷";

    public static string ShortLabel(this PricePhase phase) =>
        phase == PricePhase.Peak ? "峰时" : "谷时";

    public static string PriceLabel(this PricePhase phase) =>
        phase == PricePhase.Peak ? "1.0x 原价" : "0.5x 半价";

    public static string MultiplierLabel(this PricePhase phase) =>
        phase == PricePhase.Peak ? "1.0x" : "0.5x";

    public static string PriceKindLabel(this PricePhase phase) =>
        phase == PricePhase.Peak ? "原价" : "半价";

    public static string LatinLabel(this PricePhase phase) =>
        phase == PricePhase.Peak ? "PEAK" : "OFF-PEAK";

    public static PricePhase Opposite(this PricePhase phase) =>
        phase == PricePhase.Peak ? PricePhase.OffPeak : PricePhase.Peak;
}

/// <summary>一天之内的时段切片（以北京时间的「零点起分钟数」表示）。</summary>
public readonly record struct PhaseWindow(int StartMinute, int EndMinute, PricePhase Phase)
{
    public int LengthInMinutes => EndMinute - StartMinute;
}

/// <summary>视图渲染所需的一次性快照，App 与桌面挂件共用。</summary>
public readonly record struct PhaseSnapshot(
    DateTimeOffset Date,
    PricePhase Phase,
    DateTimeOffset NextBoundary,
    int BeijingMinuteOfDay,
    double DayProgress,
    double SecondsToNextBoundary,
    bool IsLocalBeijing);

/// <summary>峰谷判定与时间轴计算。纯函数、无状态，便于低功耗的一次性求值。</summary>
public static class PeakEngine
{
    public static readonly TimeZoneInfo BeijingTimeZone = ResolveBeijingTimeZone();

    /// <summary>工作日高峰时段区间（北京时间，单位：分钟）。</summary>
    public static readonly (int Start, int End)[] PeakRanges =
    {
        (9 * 60, 12 * 60),
        (14 * 60, 18 * 60)
    };

    /// <summary>一天内所有真正发生峰谷切换的节点。</summary>
    public static readonly int[] BoundaryMinutes = { 9 * 60, 12 * 60, 14 * 60, 18 * 60 };

    /// <summary>北京时间当日 24 小时完整分段，用于绘制时间轴。</summary>
    public static readonly PhaseWindow[] DayWindows =
    {
        new PhaseWindow(0, 9 * 60, PricePhase.OffPeak),
        new PhaseWindow(9 * 60, 12 * 60, PricePhase.Peak),
        new PhaseWindow(12 * 60, 14 * 60, PricePhase.OffPeak),
        new PhaseWindow(14 * 60, 18 * 60, PricePhase.Peak),
        new PhaseWindow(18 * 60, 24 * 60, PricePhase.OffPeak)
    };

    private static TimeZoneInfo ResolveBeijingTimeZone()
    {
        foreach (var id in new[] { "China Standard Time", "Asia/Shanghai", "Asia/Chongqing" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Shanghai", TimeSpan.FromHours(8), "Asia/Shanghai", "Asia/Shanghai");
    }

    /// <summary>把任意时刻换算为北京时间。</summary>
    public static DateTimeOffset ToBeijing(DateTimeOffset date) =>
        TimeZoneInfo.ConvertTime(date, BeijingTimeZone);

    /// <summary>指定时刻所属的「北京时间当日零点」。</summary>
    public static DateTimeOffset StartOfBeijingDay(DateTimeOffset date)
    {
        var beijing = ToBeijing(date);
        return beijing.AddTicks(-beijing.TimeOfDay.Ticks);
    }

    /// <summary>北京时间当日已过的分钟数（含小数）。</summary>
    public static double BeijingMinutes(DateTimeOffset date) =>
        (ToBeijing(date) - StartOfBeijingDay(date)).TotalMinutes;

    public static PricePhase PhaseAt(DateTimeOffset date)
    {
        if (!IsWeekday(date)) return PricePhase.OffPeak;
        double minutes = BeijingMinutes(date);
        foreach (var range in PeakRanges)
        {
            if (minutes >= range.Start && minutes < range.End) return PricePhase.Peak;
        }
        return PricePhase.OffPeak;
    }
    /// <summary>下一次峰谷切换时刻。</summary>
    public static DateTimeOffset NextBoundaryAfter(DateTimeOffset date)
    {
        var dayStart = StartOfBeijingDay(date);
        double minutes = BeijingMinutes(date);
        if (IsWeekday(date))
        {
            foreach (var boundary in BoundaryMinutes)
            {
                if (boundary > minutes) return dayStart.AddMinutes(boundary);
            }
        }
        // 当前日剩余时间，以及周末，都跳到下一个工作日 09:00。
        for (int dayOffset = 1; dayOffset <= 7; dayOffset++)
        {
            var candidateDay = dayStart.AddDays(dayOffset);
            if (IsWeekday(candidateDay)) return candidateDay.AddMinutes(9 * 60);
        }
        return dayStart.AddDays(7).AddMinutes(9 * 60);
    }

    /// <summary>从指定时刻起、未来 days 天内的全部切换时刻。</summary>
    public static List<DateTimeOffset> BoundariesCovering(DateTimeOffset date, int days = 2)
    {
        var dayStart = StartOfBeijingDay(date);
        var result = new List<DateTimeOffset>();
        if (days <= 0) return result;
        for (int dayOffset = 0; dayOffset < days; dayOffset++)
        {
            var baseDay = dayStart.AddDays(dayOffset);
            if (!IsWeekday(baseDay)) continue;
            foreach (var boundary in BoundaryMinutes)
            {
                var candidate = baseDay.AddMinutes(boundary);
                if (candidate > date) result.Add(candidate);
            }
        }
        result.Sort();
        return result;
    }

    /// <summary>DeepSeek 的峰时只在北京时间周一至周五生效。</summary>
    public static bool IsWeekday(DateTimeOffset date)
    {
        var dayOfWeek = ToBeijing(date).DayOfWeek;
        return dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Friday;
    }

    public static PhaseSnapshot SnapshotAt(DateTimeOffset date)
    {
        double minutes = BeijingMinutes(date);
        var boundary = NextBoundaryAfter(date);
        return new PhaseSnapshot(
            date,
            PhaseAt(date),
            boundary,
            (int)minutes,
            Math.Min(Math.Max(minutes / 1440.0, 0.0), 1.0),
            Math.Max((boundary - date).TotalSeconds, 0.0),
            IsLocalBeijing(date));
    }

    private static bool IsLocalBeijing(DateTimeOffset date) =>
        TimeZoneInfo.Local.GetUtcOffset(date) == BeijingTimeZone.GetUtcOffset(date);

    /// <summary>把秒数格式化为 `H:mm:ss` 倒计时。</summary>
    public static string CountdownText(double seconds)
    {
        int total = (int)Math.Floor(seconds);
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int secs = total % 60;
        return hours > 0
            ? $"{hours}:{minutes:D2}:{secs:D2}"
            : $"{minutes:D2}:{secs:D2}";
    }

    /// <summary>把北京时间分钟数格式化为 `HH:mm`。</summary>
    public static string ClockText(int beijingMinute) =>
        $"{((beijingMinute / 60) % 24):D2}:{beijingMinute % 60:D2}";
}
