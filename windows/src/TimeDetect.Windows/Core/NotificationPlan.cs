using System;
using System.Collections.Generic;

namespace TimeDetect.Core;

/// <summary>一个重复的北京时间通知计划。仅包含纯数据，便于单元测试。</summary>
/// <param name="Identifier">稳定标识，用于系统通知去重。</param>
/// <param name="Weekday">周日为 1，周一为 2，依次到周六为 7。</param>
/// <param name="Hour">北京时间小时。</param>
/// <param name="Minute">北京时间分钟。</param>
/// <param name="Title">通知标题。</param>
/// <param name="Body">通知正文。</param>
public sealed record NotificationPlan(
    string Identifier,
    int Weekday,
    int Hour,
    int Minute,
    string Title,
    string Body);

public static class TimeDetectNotificationPlan
{
    private static readonly int[] OffPeakStartMinutes = { 12 * 60, 18 * 60 };
    private static readonly int[] Weekdays = { 2, 3, 4, 5, 6 };

    public static List<NotificationPlan> OffPeakPlans()
    {
        var plans = new List<NotificationPlan>();
        foreach (var weekday in Weekdays)
        {
            foreach (var minute in OffPeakStartMinutes)
            {
                var end = minute == 12 * 60 ? "14:00" : "次日 09:00";
                plans.Add(new NotificationPlan(
                    IdentifierFor(minute, weekday, "offpeak"),
                    weekday,
                    minute / 60,
                    minute % 60,
                    "梁文谷上线",
                    $"当前已进入谷时 · 0.5x 半价，本轮谷时持续至{end}。"));
            }
        }
        return plans;
    }

    public static List<NotificationPlan> AdvancePlans(int advanceMinutes)
    {
        int safeMinutes = Math.Max(0, advanceMinutes);
        var plans = new List<NotificationPlan>();
        foreach (var weekday in Weekdays)
        {
            foreach (var start in OffPeakStartMinutes)
            {
                int notificationMinute = start - safeMinutes;
                int normalized = ((notificationMinute % 1440) + 1440) % 1440;
                plans.Add(new NotificationPlan(
                    IdentifierFor(start, weekday, "advance"),
                    weekday,
                    normalized / 60,
                    normalized % 60,
                    "DeepSeek 即将进入谷时",
                    $"距离谷时还有 {safeMinutes} 分钟，{ClockText(start)} 起进入 0.5x 半价。"));
            }
        }
        return plans;
    }

    private static string IdentifierFor(int minute, int weekday, string kind) =>
        $"timedetect.{kind}.{weekday}.{minute / 60:D2}{minute % 60:D2}";

    private static string ClockText(int minute) => $"{minute / 60:D2}:{minute % 60:D2}";
}
